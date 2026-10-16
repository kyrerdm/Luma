using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Luma.Models;
using Windows.Media.Control;

namespace Luma.Services;

internal interface IMediaSessionProvider : IDisposable
{
    event Action? Changed;
    Task InitializeAsync();
    IReadOnlyList<IMediaSession> GetSessions();
    string? CurrentSessionId { get; }
}

internal interface IMediaSession : IDisposable
{
    string Id { get; }
    event Action? Changed;
    Task<MediaState> ReadAsync(bool includeArtwork);
    Task<bool> ExecuteAsync(MediaCommand command);
    Task<bool> SeekAsync(TimeSpan position);
}

// All native objects are created and used on MediaService's worker STA.
internal sealed class WindowsMediaSessionProvider : IMediaSessionProvider
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private readonly Dictionary<IntPtr, WindowsMediaSession> sessions = new();
    public event Action? Changed;

    public async Task InitializeAsync()
    {
        manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        manager.SessionsChanged += OnSessionsChanged;
        manager.CurrentSessionChanged += OnCurrentChanged;
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs e) => Changed?.Invoke();
    private void OnCurrentChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs e) => Changed?.Invoke();

    public IReadOnlyList<IMediaSession> GetSessions()
    {
        if (manager is null) return Array.Empty<IMediaSession>();
        var live = new HashSet<IntPtr>();
        var result = new List<IMediaSession>();
        foreach (var native in manager.GetSessions())
        {
            var identity = Identity(native);
            if (!live.Add(identity)) continue;
            if (!sessions.TryGetValue(identity, out var entry))
                sessions.Add(identity, entry = new WindowsMediaSession(native));
            result.Add(entry);
        }
        foreach (var stale in sessions.Keys.Where(key => !live.Contains(key)).ToArray())
        {
            sessions[stale].Dispose();
            sessions.Remove(stale);
        }
        return result;
    }

    public string? CurrentSessionId
    {
        get
        {
            var current = manager?.GetCurrentSession();
            return current is not null && sessions.TryGetValue(Identity(current), out var entry) ? entry.Id : null;
        }
    }

    private static IntPtr Identity(GlobalSystemMediaTransportControlsSession session)
    {
        // AppUserModelId is an app label, not a unique media-session identity.
        var iid = new Guid("00000000-0000-0000-C000-000000000046");
        var pointer = ((WinRT.IWinRTObject)session).NativeObject.ThisPtr;
        Marshal.ThrowExceptionForHR(Marshal.QueryInterface(pointer, in iid, out var identity));
        Marshal.Release(identity);
        GC.KeepAlive(session);
        return identity;
    }

    public void Dispose()
    {
        foreach (var entry in sessions.Values) entry.Dispose();
        sessions.Clear();
        if (manager is not null)
        {
            manager.SessionsChanged -= OnSessionsChanged;
            manager.CurrentSessionChanged -= OnCurrentChanged;
            manager = null;
        }
    }
}

internal sealed class WindowsMediaSession : IMediaSession
{
    private readonly GlobalSystemMediaTransportControlsSession native;
    private volatile bool propertiesDirty = true;
    private string title = "";
    private string artist = "";
    private Windows.Storage.Streams.IRandomAccessStreamReference? thumbnail;
    private ImageSource? artwork;
    private bool artworkLoaded;
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public event Action? Changed;

    public WindowsMediaSession(GlobalSystemMediaTransportControlsSession native)
    {
        this.native = native;
        native.MediaPropertiesChanged += OnPropertiesChanged;
        native.PlaybackInfoChanged += OnPlaybackChanged;
        native.TimelinePropertiesChanged += OnTimelineChanged;
    }

    private void OnPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs e) { propertiesDirty = true; Changed?.Invoke(); }
    private void OnPlaybackChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs e) => Changed?.Invoke();
    private void OnTimelineChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs e) => Changed?.Invoke();

    public async Task<MediaState> ReadAsync(bool includeArtwork)
    {
        if (propertiesDirty)
        {
            propertiesDirty = false;
            try
            {
                var properties = await native.TryGetMediaPropertiesAsync();
                title = properties.Title;
                artist = string.IsNullOrWhiteSpace(properties.Artist) ? properties.AlbumArtist : properties.Artist;
                thumbnail = properties.Thumbnail;
                artwork = null; artworkLoaded = false;
            }
            catch { propertiesDirty = true; throw; }
        }
        if (includeArtwork && !artworkLoaded)
        {
            artworkLoaded = true;
            artwork = await ReadArtworkAsync(thumbnail);
        }
        var playback = native.GetPlaybackInfo();
        var timeline = native.GetTimelineProperties();
        var controls = playback.Controls;
        var start = timeline.StartTime;
        var end = timeline.EndTime < start ? start : timeline.EndTime;
        var playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        var now = DateTimeOffset.UtcNow;
        var position = timeline.Position;
        if (playing && timeline.LastUpdatedTime <= now)
            position += TimeSpan.FromSeconds(Math.Clamp((now - timeline.LastUpdatedTime).TotalSeconds, 0, 86400) * (playback.PlaybackRate ?? 1));
        position = TimeSpan.FromTicks(Math.Clamp(position.Ticks, start.Ticks, end.Ticks));
        return new MediaState
        {
            Available = playback.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed && (!string.IsNullOrWhiteSpace(title) || playing),
            SessionId = Id, AppName = AppDisplayName(native.SourceAppUserModelId),
            Title = string.IsNullOrWhiteSpace(title) ? "Medya" : title, Artist = artist,
            Artwork = artwork, IsPlaying = playing,
            CanToggle = controls.IsPlayPauseToggleEnabled || (playing ? controls.IsPauseEnabled : controls.IsPlayEnabled),
            CanPrevious = controls.IsPreviousEnabled, CanNext = controls.IsNextEnabled,
            CanSeek = controls.IsPlaybackPositionEnabled && end > start,
            Start = start, End = end, Position = position, SampledAt = now, PlaybackRate = playback.PlaybackRate ?? 1
        };
    }

    public async Task<bool> ExecuteAsync(MediaCommand command)
    {
        var playback = native.GetPlaybackInfo();
        var controls = playback.Controls;
        return command switch
        {
            MediaCommand.Previous => controls.IsPreviousEnabled && await native.TrySkipPreviousAsync(),
            MediaCommand.Next => controls.IsNextEnabled && await native.TrySkipNextAsync(),
            _ when controls.IsPlayPauseToggleEnabled => await native.TryTogglePlayPauseAsync(),
            _ when playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => controls.IsPauseEnabled && await native.TryPauseAsync(),
            _ => controls.IsPlayEnabled && await native.TryPlayAsync()
        };
    }

    public async Task<bool> SeekAsync(TimeSpan position)
    {
        if (!native.GetPlaybackInfo().Controls.IsPlaybackPositionEnabled) return false;
        var timeline = native.GetTimelineProperties();
        return timeline.EndTime > timeline.StartTime && await native.TryChangePlaybackPositionAsync(Math.Clamp(position.Ticks, timeline.StartTime.Ticks, timeline.EndTime.Ticks));
    }

    private static async Task<ImageSource?> ReadArtworkAsync(Windows.Storage.Streams.IRandomAccessStreamReference? reference)
    {
        if (reference is null) return null;
        try
        {
            using var random = await reference.OpenReadAsync();
            if (random.Size == 0 || random.Size > 8 * 1024 * 1024) return null;
            using var stream = random.AsStreamForRead();
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 320;
            image.StreamSource = stream; image.EndInit(); image.Freeze();
            return image;
        }
        catch (Exception ex) when (MediaService.IsMediaError(ex) || ex is NotSupportedException or IOException) { return null; }
    }

    private static string AppDisplayName(string id)
    {
        var lower = id.ToLowerInvariant();
        if (lower.Contains("spotify")) return "Spotify";
        if (lower.Contains("chrome")) return "Chrome";
        if (lower.Contains("msedge") || lower.Contains("microsoftedge")) return "Edge";
        if (lower.Contains("firefox")) return "Firefox";
        if (lower.Contains("vlc")) return "VLC";
        if (lower.Contains("zunemusic") || lower.Contains("mediaplayer")) return "Media Player";
        return id.Split('!')[^1].Split('.')[0] is { Length: > 0 } name ? name : "Medya";
    }

    public void Dispose()
    {
        native.MediaPropertiesChanged -= OnPropertiesChanged;
        native.PlaybackInfoChanged -= OnPlaybackChanged;
        native.TimelinePropertiesChanged -= OnTimelineChanged;
    }
}
