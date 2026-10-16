using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Luma.Models;
using Luma.Services;
using Luma.Views;
using MediaState = Luma.Models.MediaState;

namespace Luma;

internal static partial class Diagnostics
{
    private static async Task VerifyMediaSourcesAsync(string directory, Action<string, bool> check)
    {
        var sample = PreviewData.Media();
        var chrome = new FakeMediaSession(sample with { SessionId = "chrome-a", AppName = "Chrome", Title = "Morning radio" });
        var spotify = new FakeMediaSession(sample with { SessionId = "spotify-b", AppName = "Spotify", Title = "Afterglow", IsPlaying = false });
        var otherChrome = new FakeMediaSession(sample with { SessionId = "chrome-c", AppName = "Chrome", Title = "Night drive", IsPlaying = false });
        var provider = new FakeMediaProvider(chrome, spotify, otherChrome) { CurrentSessionId = chrome.Id };
        using var service = new MediaService(Application.Current.Dispatcher, () => provider);
        await service.StartAsync().WaitAsync(TimeSpan.FromSeconds(3));
        check("Multiple media sessions enumerate and auto-select playing source", service.State.Sources.Count == 3 && service.State.SessionId == chrome.Id && service.State.AutomaticSource);
        var selected = await service.SelectSourceAsync(spotify.Id);
        await service.RefreshForDiagnosticsAsync();
        check("Manual source stays selected while paused and another source plays", selected && service.State.SessionId == spotify.Id && !service.State.IsPlaying && !service.State.AutomaticSource);
        var toggled = await service.ExecuteAsync(MediaCommand.TogglePlayback, spotify.Id);
        await service.RefreshForDiagnosticsAsync();
        check("Play pause targets only the selected session", toggled && spotify.Commands.SequenceEqual(new[] { MediaCommand.TogglePlayback }) && chrome.Commands.Count == 0 && otherChrome.Commands.Count == 0 && chrome.Snapshot.IsPlaying);
        var sought = await service.SeekAsync(TimeSpan.FromSeconds(42), spotify.Id);
        check("Seeking targets only the selected session", sought && spotify.Seeks.SequenceEqual(new[] { TimeSpan.FromSeconds(42) }) && chrome.Seeks.Count == 0 && otherChrome.Seeks.Count == 0);
        selected = await service.SelectSourceAsync(otherChrome.Id);
        await service.ExecuteAsync(MediaCommand.Next, otherChrome.Id);
        check("Sessions from the same application keep separate identities", selected && service.State.SessionId == otherChrome.Id && otherChrome.Commands.SequenceEqual(new[] { MediaCommand.Next }) && chrome.Commands.Count == 0);
        var stale = await service.ExecuteAsync(MediaCommand.TogglePlayback, chrome.Id);
        check("Stale source commands cannot control the new selection", !stale && chrome.Commands.Count == 0 && otherChrome.Commands.Count == 1);
        var invalid = await service.SelectSourceAsync("closed-source");
        check("Unavailable source selection preserves the current source", !invalid && service.State.SessionId == otherChrome.Id);
        await service.SelectSourceAsync(null);
        check("Automatic mode resumes following the active playing source", service.State.AutomaticSource && service.State.SessionId == chrome.Id);

        var blockedRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        spotify.BlockNextArtworkRead(blockedRead);
        var first = service.SelectSourceAsync(spotify.Id);
        await spotify.ArtworkReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var second = service.SelectSourceAsync(chrome.Id);
        blockedRead.SetResult();
        var changes = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(3));
        check("Latest selection wins while metadata is loading", !changes[0] && changes[1] && service.State.SessionId == chrome.Id);

        // A separate window and fake provider keep system players untouched.
        provider.Replace(chrome, spotify);
        await service.RefreshForDiagnosticsAsync();
        var pointer = System.Windows.Forms.Cursor.Position;
        var bounds = System.Windows.Forms.Screen.FromPoint(pointer).WorkingArea;
        var settings = new AppSettings
        {
            DurationSeconds = 1, Animations = false,
            Position = (pointer.Y < bounds.Top + bounds.Height / 2 ? "Bottom" : "Top") + (pointer.X < bounds.Left + bounds.Width / 2 ? "Right" : "Left")
        };
        var flyout = new FlyoutWindow(settings);
        flyout.ShowVolume(new(true, 40, false, "Test endpoint"));
        flyout.UpdateMedia(service.State);
        service.Changed += flyout.UpdateMedia;
        var chosen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        flyout.SourceRequested += async id =>
        {
            var success = await service.SelectSourceAsync(id);
            flyout.UpdateMedia(service.State);
            flyout.ReportSourceSelection(id, success);
            chosen.TrySetResult(success);
        };
        var button = (Button)flyout.FindName("SourceButton");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var menu = (ContextMenu)flyout.FindName("SourceMenu");
        await Task.Delay(150);
        check("Subtle source arrow opens the source list", button.Visibility == Visibility.Visible && menu.IsOpen && menu.Items.OfType<MenuItem>().Count() == 3);
        Render(flyout, Path.Combine(directory, "luma-source-card.png"));
        RenderElement(menu, Path.Combine(directory, "luma-source-menu.png"));
        await Task.Delay(1150);
        check("Source menu pauses automatic dismissal", flyout.IsVisible && menu.IsOpen);
        menu.Items.OfType<MenuItem>().Single(item => item.Tag as string == spotify.Id).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        var didChoose = await chosen.Task.WaitAsync(TimeSpan.FromSeconds(3));
        check("Source menu selection updates the displayed track", didChoose && ((TextBlock)flyout.FindName("TrackTitleText")).Text == "Afterglow" && ((TextBlock)flyout.FindName("AppNameText")).Text == "Spotify" && ((Button)flyout.FindName("PlayButton")).IsEnabled);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        flyout.ShowLock("Caps Lock", true);
        await Task.Delay(1150);
        check("Lock notice closes the source menu and expires normally", !menu.IsOpen && !flyout.IsVisible);
        await service.SelectSourceAsync(spotify.Id);
        provider.Replace(chrome);
        await service.RefreshForDiagnosticsAsync();
        check("Closing a selected source falls back to the remaining session", service.State.SessionId == chrome.Id && service.State.AutomaticSource && service.State.Sources.Count == 1);
        service.Changed -= flyout.UpdateMedia;
        provider.Replace();
        await service.RefreshForDiagnosticsAsync();
        check("Closing every media source clears the card", !service.State.Available && service.State.Sources.Count == 0);
        flyout.Close();

        var preview = new FlyoutWindow(new AppSettings { DurationSeconds = 8, Animations = false });
        var realRequests = 0;
        preview.SourceRequested += _ => realRequests++;
        preview.MediaRequested += (_, _) => realRequests++;
        preview.SeekRequested += (_, _) => realRequests++;
        preview.ShowPreview(PreviewData.Media());
        ((Button)preview.FindName("SourceButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var previewMenu = (ContextMenu)preview.FindName("SourceMenu");
        previewMenu.Items.OfType<MenuItem>().Single(item => item.Tag as string == sample.SessionId + "-alt").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        ((Button)preview.FindName("PlayButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check("Preview source switching and playback remain local", realRequests == 0 && ((TextBlock)preview.FindName("TrackTitleText")).Text == "Afterglow");
        preview.Close();
    }

    private sealed class FakeMediaProvider(params IMediaSession[] initial) : IMediaSessionProvider
    {
        private IMediaSession[] sessions = initial;
        public event Action? Changed;
        public string? CurrentSessionId { get; set; }
        public Task InitializeAsync() => Task.CompletedTask;
        public IReadOnlyList<IMediaSession> GetSessions() => Volatile.Read(ref sessions);
        public void Replace(params IMediaSession[] next) { Volatile.Write(ref sessions, next); Changed?.Invoke(); }
        public void Dispose() { }
    }

    private sealed class FakeMediaSession(MediaState initial) : IMediaSession
    {
        private MediaState snapshot = initial;
        private TaskCompletionSource? artworkGate;
        public TaskCompletionSource ArtworkReadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Id { get; } = initial.SessionId;
        public MediaState Snapshot => Volatile.Read(ref snapshot);
        public readonly List<MediaCommand> Commands = new();
        public readonly List<TimeSpan> Seeks = new();
        public event Action? Changed;
        public void BlockNextArtworkRead(TaskCompletionSource gate) => Volatile.Write(ref artworkGate, gate);
        public async Task<MediaState> ReadAsync(bool includeArtwork)
        {
            if (includeArtwork && Interlocked.Exchange(ref artworkGate, null) is { } gate)
            {
                ArtworkReadEntered.TrySetResult();
                await gate.Task;
            }
            return Snapshot;
        }
        public Task<bool> ExecuteAsync(MediaCommand command)
        {
            Commands.Add(command);
            if (command == MediaCommand.TogglePlayback) Volatile.Write(ref snapshot, Snapshot with { IsPlaying = !Snapshot.IsPlaying });
            Changed?.Invoke();
            return Task.FromResult(true);
        }
        public Task<bool> SeekAsync(TimeSpan position) { Seeks.Add(position); Volatile.Write(ref snapshot, Snapshot with { Position = position }); Changed?.Invoke(); return Task.FromResult(true); }
        public void Dispose() { }
    }
}
