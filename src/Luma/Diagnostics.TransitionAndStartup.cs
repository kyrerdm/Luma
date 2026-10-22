using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
    private static async Task VerifyTransitionAndStartupAsync(string directory, Action<string, bool, string> check)
    {
        var pointer = System.Windows.Forms.Cursor.Position;
        var bounds = System.Windows.Forms.Screen.FromPoint(pointer).WorkingArea;
        var prefs = new AppSettings { Animations = false, DurationSeconds = 1,
            Position = (pointer.Y < bounds.Top + bounds.Height / 2 ? "Bottom" : "Top") + (pointer.X < bounds.Left + bounds.Width / 2 ? "Right" : "Left") };
        var flyout = new FlyoutWindow(prefs);
        var sample = PreviewData.Media();
        try
        {
            flyout.ShowVolume(new(true, 40, false, "Fixture"));
            flyout.UpdateMedia(sample);
            var commands = new List<MediaCommand>();
            flyout.MediaRequested += (_, command) => commands.Add(command);
            ((Button)flyout.FindName("NextButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            flyout.UpdateMedia(MediaState.Empty);
            await Task.Delay(1300);
            check("Skipping a track holds the media card beyond the normal hide timeout during an empty session", flyout.IsVisible &&
                flyout.MediaTransitionActive && ((FrameworkElement)flyout.FindName("MediaCard")).Visibility == Visibility.Visible &&
                ((TextBlock)flyout.FindName("TrackTitleText")).Text == sample.Title, "");
            flyout.UpdateMedia(sample with { Title = "Media", HasTrackMetadata = false });
            check("Placeholder track metadata does not end a pending transition or blank the old artwork", flyout.MediaTransitionActive &&
                ((TextBlock)flyout.FindName("TrackTitleText")).Text == sample.Title && ((System.Windows.Controls.Image)flyout.FindName("ArtworkBackdrop")).Source == sample.Artwork, "");
            flyout.UpdateMedia(sample with { Title = "Afterglow", Position = TimeSpan.Zero });
            await Task.Delay(350);
            check("A new track replaces the held card and gets its full visibility duration", flyout.IsVisible && !flyout.MediaTransitionActive &&
                ((TextBlock)flyout.FindName("TrackTitleText")).Text == "Afterglow" && commands.SequenceEqual(new[] { MediaCommand.Next }), "");
            await Task.Delay(850);
            check("A completed track transition resumes normal auto-hide", !flyout.IsVisible, "");

            flyout.ShowVolume(new(true, 40, false, "Fixture"));
            flyout.UpdateMedia(sample);
            ((Button)flyout.FindName("PreviousButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            flyout.UpdateMedia(MediaState.Empty);
            await Task.Delay(FlyoutWindow.MediaTransitionLimit + TimeSpan.FromMilliseconds(100));
            check("A closed player cannot leave the transition permanently pinned", !flyout.MediaTransitionActive &&
                ((FrameworkElement)flyout.FindName("MediaCard")).Visibility == Visibility.Collapsed, "The real eight-second transition timer expired.");
            await Task.Delay(1150);
            check("The bounded failed transition also dismisses the remaining volume strip", !flyout.IsVisible, "");
            flyout.ShowVolume(new(true, 40, false, "Fixture")); flyout.UpdateMedia(sample);
            flyout.BeginMediaTransition(); flyout.Dismiss();
            flyout.UpdateMedia(sample with { Title = "Late track" });
            check("Explicit dismissal cancels a transition and late metadata cannot reopen the card", !flyout.MediaTransitionActive && !flyout.IsVisible, "");
            flyout.ShowVolume(new(true, 40, false, "Fixture")); flyout.UpdateMedia(sample); flyout.BeginMediaTransition();
            flyout.ReportMediaResult(false);
            check("A rejected playback command releases its transition hold", !flyout.MediaTransitionActive, "");
            flyout.BeginMediaTransition(); flyout.ShowLock("Caps Lock", true);
            check("Lock notifications cancel the media hold instead of inheriting it", !flyout.MediaTransitionActive, "");
        }
        finally { flyout.Close(); }

        var artworkGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slowCover = new FakeMediaSession(sample with { Title = "New song before cover" });
        slowCover.BlockNextArtworkRead(artworkGate);
        using (var media = new MediaService(Application.Current.Dispatcher, () => new FakeMediaProvider(slowCover)))
        {
            var start = media.StartAsync();
            await slowCover.ArtworkReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            try { check("Track metadata is published before delayed artwork finishes loading", media.State.Available && media.State.Title == "New song before cover" && !start.IsCompleted, ""); }
            finally { artworkGate.TrySetResult(); }
            await start.WaitAsync(TimeSpan.FromSeconds(3));
        }

        using (var keyboard = new KeyboardService(Application.Current.Dispatcher))
        {
            var observed = new List<MediaKeyCommand>();
            keyboard.MediaKeyPressed += input => observed.Add(input.Command);
            foreach (var key in new[] { 0xB0, 0xB1, 0xB2, 0xB3 }) { keyboard.ProcessKey(key, true, false); keyboard.ProcessKey(key, false, true); }
            await Task.Delay(40);
            check("Hardware media inputs distinguish track skips from stop and play-pause", observed.SequenceEqual(new[] { MediaKeyCommand.Next, MediaKeyCommand.Previous, MediaKeyCommand.Stop, MediaKeyCommand.TogglePlayback }), "No global playback input is injected.");
        }

        var name = "Local\\Luma.Test.Startup." + Guid.NewGuid().ToString("N");
        using (var keeper = new Mutex(false, name))
        {
            using (var reopened = new Mutex(true, name, out var created))
            {
                var acquired = StartupRecovery.TryAcquire(reopened);
                check("Startup acquires an existing unowned instance mutex instead of silently exiting", !created && acquired, "An unrelated test mutex is used.");
                if (acquired) reopened.ReleaseMutex();
            }
            var owner = new Thread(() => { using var held = new Mutex(false, name); held.WaitOne(); });
            owner.Start(); owner.Join();
            var recovered = StartupRecovery.TryAcquire(keeper);
            check("Startup recovers an abandoned instance mutex after a crashed owner", recovered, "");
            if (recovered) keeper.ReleaseMutex();
        }

        var exe = Environment.ProcessPath!;
        var error = new COMException("Do not record this private path: C:\\Users\\private\\track.txt");
        var args = new[] { "--background", "--data-dir", "C:\\folder with spaces\\Luma" };
        var first = StartupRecovery.Retry(exe, args, error)!;
        var second = StartupRecovery.Retry(exe, first.Start.ArgumentList.ToArray(), error)!;
        var third = StartupRecovery.Retry(exe, second.Start.ArgumentList.ToArray(), error)!;
        check("Transient background startup errors get a bounded 2/4/8-second retry plan", first.Delay.TotalSeconds == 2 && second.Delay.TotalSeconds == 4 && third.Delay.TotalSeconds == 8 &&
            StartupRecovery.Retry(exe, third.Start.ArgumentList.ToArray(), error) is null, "The retry fixture never starts a real process.");
        check("Startup retries preserve quoted arguments and wait for the previous instance to exit", first.Start.ArgumentList.Contains(args[2]) &&
            first.Start.ArgumentList.Contains("--wait-for-instance") && first.Start.UseShellExecute == false && first.Start.WindowStyle == System.Diagnostics.ProcessWindowStyle.Hidden, "");
        check("Manual launches and updater handshakes keep their existing failure behavior", StartupRecovery.Retry(exe, Array.Empty<string>(), error) is null &&
            StartupRecovery.Retry(exe, new[] { "--background", "--ready-event", "fixture" }, error) is null && StartupRecovery.Retry(exe, args, error, packaged: true) is null, "Store activation is managed by Windows.");
        check("Invalid retry flags and permission errors cannot create an endless startup loop", StartupRecovery.Retry(exe, new[] { "--background", "--startup-retry", "bad" }, error) is null &&
            StartupRecovery.Retry(exe, args, new UnauthorizedAccessException()) is null, "");

        var root = Path.Combine(directory, "startup-paths-" + Guid.NewGuid().ToString("N"));
        var installed = Path.Combine(root, AppVersion.Current.ToString(3), "Luma.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(installed)!); File.Copy(exe, installed);
        var invalid = Path.Combine(root, "9.0.0", "Luma.exe"); Directory.CreateDirectory(Path.GetDirectoryName(invalid)!); File.Copy(exe, invalid);
        var temporary = Path.Combine(Path.GetTempPath(), "Luma-in-archive", "Luma.exe");
        check("A matching installed copy replaces a temporary startup path and rejects mislabeled versions", StartupService.ResolveExecutable(temporary, root) == installed, "Only an isolated install tree is inspected.");
        var refused = false;
        try { StartupService.ResolveExecutable(temporary, Path.Combine(root, "missing")); } catch (InvalidOperationException) { refused = true; }
        check("A temporary archive extraction cannot be registered as a permanent startup location", refused, "No Windows startup registry value is changed.");

        var health = Path.Combine(root, "health");
        StartupRecovery.Record(health, StartupOutcome.Retrying, 1, error);
        var json = File.ReadAllText(Path.Combine(health, "startup-health.json"));
        var summary = StartupRecovery.Summary(health);
        check("Startup health records contain only bounded technical fields, not exception messages or paths", summary.Contains("Retrying") && summary.Contains("retries: 1") &&
            !json.Contains("private") && !json.Contains("track.txt") && !summary.Contains("C:\\"), "");
        StartupRecovery.Record(health, StartupOutcome.Ready, 2);
        check("Successful background startup replaces the last failure record", StartupRecovery.Summary(health).Contains("Ready; retries: 2") &&
            !Directory.EnumerateFiles(health, "*.tmp").Any(), "");
        File.WriteAllText(Path.Combine(health, "startup-health.json"), "{broken");
        check("Damaged startup diagnostics cannot block the support report", StartupRecovery.Summary(health).Contains("unavailable"), "");
    }
}
