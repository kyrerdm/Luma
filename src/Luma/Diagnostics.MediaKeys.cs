using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Luma.Models;
using Luma.Services;

namespace Luma;

internal static partial class Diagnostics
{
    private static async Task VerifyMediaKeysAsync(string directory, Action<string, bool, string> check)
    {
        // Call the real hook handler without injecting global playback keys into the user's player.
        using (var keyboard = new KeyboardService(Application.Current.Dispatcher) { HandleVolume = false, AudioAvailable = false })
        {
            var notices = 0;
            keyboard.MediaKeyPressed += _ => notices++;
            var consumed = false;
            foreach (var key in new[] { 0xB0, 0xB1, 0xB2, 0xB3 })
            {
                consumed |= keyboard.ProcessKey(key, true, false);
                consumed |= keyboard.ProcessKey(key, true, false); // Held-key repeat.
                consumed |= keyboard.ProcessKey(key, false, true);
            }
            await Task.Delay(40);
            check("All four media keys pass through and notify once per press without volume keys", !consumed && notices == 4, $"Notices={notices}; consumed={consumed}");
            keyboard.ProcessKey(0xB3, true, false);
            keyboard.ProcessKey(0xB3, false, true);
            await Task.Delay(40);
            check("Releasing and pressing a media key triggers a fresh notice", notices == 5, "");
            keyboard.ObserveMedia = false;
            keyboard.ProcessKey(0xB0, true, false);
            keyboard.ProcessKey(0xB0, false, true);
            keyboard.ProcessKey(0x41, true, false);
            await Task.Delay(40);
            check("Disabled media observation and ordinary keys do not trigger notices", notices == 5, "");
        }

        var data = Path.Combine(directory, "media-key-runtime-" + Guid.NewGuid().ToString("N"));
        var pointer = System.Windows.Forms.Cursor.Position;
        var bounds = System.Windows.Forms.Screen.FromPoint(pointer).WorkingArea;
        new SettingsStore(data).Save(new AppSettings
        {
            WelcomeCompleted = true, VolumeKeys = false, ObserveVolume = false, ObserveBrightness = false,
            Animations = false, DurationSeconds = 1, Language = "en",
            Position = (pointer.Y < bounds.Top + bounds.Height / 2 ? "Bottom" : "Top") +
                (pointer.X < bounds.Left + bounds.Width / 2 ? "Right" : "Left")
        });
        var audio = new DelayedAudioDevice();
        var brightness = new DiagnosticBrightnessDevice();
        var provider = new FakeMediaProvider();
        using var runtime = new AppController(data, () => audio, () => brightness, () => provider);
        await runtime.AudioReady.WaitAsync(TimeSpan.FromSeconds(3));
        await runtime.BrightnessReady.WaitAsync(TimeSpan.FromSeconds(3));
        var input = runtime.DiagnosticKeyboard!;
        var flyout = runtime.DiagnosticFlyout;
        void Press(int key) { input.ProcessKey(key, true, false); input.ProcessKey(key, false, true); }
        async Task WaitFor(Func<bool> predicate)
        {
            var watch = Stopwatch.StartNew();
            while (!predicate() && watch.Elapsed < TimeSpan.FromSeconds(3)) await Task.Delay(20);
        }

        Press(0xB3);
        await Task.Delay(60);
        check("A media key without an exposed session does not display an empty card", !flyout.IsVisible, "");
        var sample = PreviewData.Media();
        var player = new FakeMediaSession(sample);
        provider.Replace(player);
        await WaitFor(() => flyout.IsVisible);
        check("Playback creating a session after the key opens its media card", flyout.IsVisible &&
            ((FrameworkElement)flyout.FindName("MediaCard")).Visibility == Visibility.Visible &&
            ((TextBlock)flyout.FindName("TrackTitleText")).Text == sample.Title, "");
        await WaitFor(() => !flyout.IsVisible);
        check("A media key popup auto-hides and periodic metadata refreshes do not reopen it", !flyout.IsVisible, "");

        var allOpened = true;
        foreach (var key in new[] { 0xB0, 0xB1, 0xB2, 0xB3 })
        {
            Press(key);
            await WaitFor(() => flyout.IsVisible);
            allOpened &= flyout.IsVisible;
            flyout.Dismiss();
        }
        check("Next, previous, stop and play-pause keys each reopen the real controller flyout", allOpened, "");
        Press(0xB3);
        await WaitFor(() => flyout.IsVisible);
        var paused = new FakeMediaSession(sample with { Title = "Afterglow", IsPlaying = false });
        provider.Replace(paused);
        await WaitFor(() => ((TextBlock)flyout.FindName("TrackTitleText")).Text == "Afterglow");
        check("Media key popups catch up with fresh track and paused playback metadata", ((TextBlock)flyout.FindName("TrackTitleText")).Text == "Afterglow" &&
            ((TextBlock)flyout.FindName("PlayIcon")).Text == "\uE768", "");
        flyout.Dismiss();
        ((CheckBox)runtime.DiagnosticSettings.FindName("MediaToggle")).IsChecked = false;
        Press(0xB0);
        provider.Replace(player);
        await Task.Delay(100);
        check("Disabling the media card also disables media-key popups", !input.ObserveMedia && !flyout.IsVisible, "");
        check("Media key observation sends no duplicate player, volume or brightness commands", player.Commands.Count == 0 && paused.Commands.Count == 0 && audio.Writes == 0 && brightness.Writes == 0, "");
        ((CheckBox)runtime.DiagnosticSettings.FindName("MediaToggle")).IsChecked = true;
        provider.Replace();
        await WaitFor(() => !runtime.DiagnosticMediaState.Available);
        Press(0xB3);
        await Task.Delay(2150);
        provider.Replace(player);
        await WaitFor(() => runtime.DiagnosticMediaState.Available);
        check("An expired media key does not open an unrelated session appearing later", runtime.DiagnosticMediaState.Available && !flyout.IsVisible, "");
    }
}
