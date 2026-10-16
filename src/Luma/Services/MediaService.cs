using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Luma.Models;
using Windows.Media.Control;

namespace Luma.Services;

public enum MediaCommand { TogglePlayback, Previous, Next }

public sealed class MediaService : IDisposable
{
    private readonly Dispatcher dispatcher;
    private readonly StaWorker worker = new("Luma media");
    private DispatcherTimer? timer;
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? session;
    private volatile bool disposed;
    private bool refreshing;
    private bool pending;
    private bool propertiesDirty = true;
    private string title = "";
    private string artist = "";
    private ImageSource? artwork;
    private MediaState state = MediaState.Empty;
    public MediaState State => Volatile.Read(ref state);
    public string? Error { get; private set; }
    public event Action<MediaState>? Changed;

    public MediaService(Dispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
    }

    public Task StartAsync() => worker.RunAsync(StartCoreAsync);

    private async Task StartCoreAsync()
    {
        try
        {
            manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (disposed) { manager = null; return; }
            manager.CurrentSessionChanged += OnCurrentSessionChanged;
            manager.SessionsChanged += OnSessionsChanged;
            await RefreshAsync();
            if (!disposed)
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                timer.Tick += (_, _) => QueueRefresh(false);
                timer.Start();
            }
        }
        catch (Exception ex) when (IsMediaError(ex)) { Error = ex.Message; Publish(MediaState.Empty); }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) => QueueRefresh(true);
    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args) => QueueRefresh(true);
    private void OnPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) => QueueRefresh(true);
    private void OnPlaybackChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => QueueRefresh(false);
    private void OnTimelineChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args) => QueueRefresh(false);

    private void QueueRefresh(bool reloadProperties)
    {
        if (disposed || dispatcher.HasShutdownStarted) return;
        worker.Post(async () =>
        {
            if (disposed) return;
            propertiesDirty |= reloadProperties;
            await RefreshAsync();
        });
    }

    private async Task RefreshAsync()
    {
        if (disposed || manager is null) return;
        if (refreshing) { pending = true; return; }
        refreshing = true;
        try
        {
            do
            {
                pending = false;
                var selected = manager.GetCurrentSession();
                if (selected is null || selected.GetPlaybackInfo().PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    selected = manager.GetSessions().FirstOrDefault(s => s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) ?? selected ?? manager.GetSessions().FirstOrDefault();
                if (session != selected)
                {
                    DetachSession();
                    session = selected;
                    title = artist = ""; artwork = null;
                    propertiesDirty = true;
                    if (session is not null)
                    {
                        session.MediaPropertiesChanged += OnPropertiesChanged;
                        session.PlaybackInfoChanged += OnPlaybackChanged;
                        session.TimelinePropertiesChanged += OnTimelineChanged;
                    }
                }
                var current = session;
                if (current is null) { Publish(MediaState.Empty); continue; }
                if (propertiesDirty)
                {
                    propertiesDirty = false;
                    var properties = await current.TryGetMediaPropertiesAsync();
                    if (disposed) return;
                    title = properties.Title;
                    artist = string.IsNullOrWhiteSpace(properties.Artist) ? properties.AlbumArtist : properties.Artist;
                    artwork = await ReadArtworkAsync(properties.Thumbnail);
                    if (disposed) return;
                }
                var playback = current.GetPlaybackInfo();
                var timeline = current.GetTimelineProperties();
                var controls = playback.Controls;
                var start = timeline.StartTime;
                var end = timeline.EndTime < start ? start : timeline.EndTime;
                var playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                var position = timeline.Position;
                if (playing && timeline.LastUpdatedTime <= DateTimeOffset.UtcNow)
                    position += TimeSpan.FromSeconds(Math.Clamp((DateTimeOffset.UtcNow - timeline.LastUpdatedTime).TotalSeconds, 0, 86400) * (playback.PlaybackRate ?? 1));
                position = TimeSpan.FromTicks(Math.Clamp(position.Ticks, start.Ticks, end.Ticks));
                Publish(new MediaState
                {
                    Available = !string.IsNullOrWhiteSpace(title), SessionId = current.SourceAppUserModelId,
                    AppName = AppDisplayName(current.SourceAppUserModelId), Title = title, Artist = artist,
                    Artwork = artwork, IsPlaying = playing, CanToggle = controls.IsPlayPauseToggleEnabled || (playing ? controls.IsPauseEnabled : controls.IsPlayEnabled),
                    CanPrevious = controls.IsPreviousEnabled, CanNext = controls.IsNextEnabled,
                    CanSeek = controls.IsPlaybackPositionEnabled && end > start,
                    Start = start, End = end, Position = position, SampledAt = DateTimeOffset.UtcNow, PlaybackRate = playback.PlaybackRate ?? 1
                });
                Error = null;
            } while (pending && !disposed);
        }
        catch (Exception ex) when (IsMediaError(ex))
        {
            Error = ex.Message;
            propertiesDirty = true;
            DetachSession();
            Publish(MediaState.Empty);
        }
        finally { refreshing = false; }
    }

    private static async Task<ImageSource?> ReadArtworkAsync(Windows.Storage.Streams.IRandomAccessStreamReference? thumbnail)
    {
        if (thumbnail is null) return null;
        try
        {
            using var random = await thumbnail.OpenReadAsync();
            if (random.Size == 0 || random.Size > 8 * 1024 * 1024) return null;
            using var stream = random.AsStreamForRead();
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 320;
            image.StreamSource = stream; image.EndInit(); image.Freeze();
            return image;
        }
        catch (Exception ex) when (IsMediaError(ex) || ex is NotSupportedException or System.IO.IOException) { return null; }
    }

    public async Task<bool> ExecuteAsync(MediaCommand command)
    {
        if (disposed) return false;
        try { return await worker.RunAsync(() => ExecuteCoreAsync(command)); }
        catch (OperationCanceledException) { return false; }
    }

    private async Task<bool> ExecuteCoreAsync(MediaCommand command)
    {
        var target = session;
        if (target is null || disposed) return false;
        try
        {
            var playback = target.GetPlaybackInfo();
            var result = command switch
            {
                MediaCommand.Previous => await target.TrySkipPreviousAsync(),
                MediaCommand.Next => await target.TrySkipNextAsync(),
                _ when playback.Controls.IsPlayPauseToggleEnabled => await target.TryTogglePlayPauseAsync(),
                _ when playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => await target.TryPauseAsync(),
                _ => await target.TryPlayAsync()
            };
            QueueRefresh(command != MediaCommand.TogglePlayback);
            return result;
        }
        catch (Exception ex) when (IsMediaError(ex)) { QueueRefresh(true); return false; }
    }

    public async Task<bool> SeekAsync(TimeSpan position)
    {
        if (disposed) return false;
        try { return await worker.RunAsync(() => SeekCoreAsync(position)); }
        catch (OperationCanceledException) { return false; }
    }

    private async Task<bool> SeekCoreAsync(TimeSpan position)
    {
        var target = session;
        if (target is null || disposed || !State.CanSeek) return false;
        try
        {
            var ticks = Math.Clamp(position.Ticks, State.Start.Ticks, State.End.Ticks);
            var result = await target.TryChangePlaybackPositionAsync(ticks);
            QueueRefresh(false);
            return result;
        }
        catch (Exception ex) when (IsMediaError(ex)) { QueueRefresh(true); return false; }
    }

    private void Publish(MediaState next)
    {
        if (disposed) return;
        Volatile.Write(ref state, next);
        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { if (!disposed) Changed?.Invoke(next); }));
    }
    private static bool IsMediaError(Exception ex) => ex is COMException or UnauthorizedAccessException or InvalidOperationException or ArgumentException;
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
    private void DetachSession()
    {
        if (session is null) return;
        session.MediaPropertiesChanged -= OnPropertiesChanged;
        session.PlaybackInfoChanged -= OnPlaybackChanged;
        session.TimelinePropertiesChanged -= OnTimelineChanged;
        session = null;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        worker.Post(() =>
        {
            timer?.Stop(); DetachSession();
            if (manager is not null)
            {
                manager.CurrentSessionChanged -= OnCurrentSessionChanged;
                manager.SessionsChanged -= OnSessionsChanged;
                manager = null;
            }
        });
        worker.Dispose();
    }
}
