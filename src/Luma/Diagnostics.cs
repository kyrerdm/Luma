using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Luma.Models;
using Luma.Services;
using Luma.Views;
using Luma.Controls;
using Luma.Themes;

namespace Luma;

internal static class Diagnostics
{
    public static async Task<int> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var results = new List<object>();
        var failures = 0;
        void Check(string name, bool passed, string detail = "")
        {
            results.Add(new { name, passed, detail });
            if (!passed) failures++;
            File.AppendAllText(Path.Combine(directory, "progress.log"), $"{DateTime.UtcNow:O} {name}: {passed}{Environment.NewLine}");
        }

        var normalized = new AppSettings { DurationSeconds = double.NaN, VolumeStep = 100, Position = "invalid", Accent = "invalid", FlyoutScale = double.NaN };
        normalized.Normalize();
        Check("Invalid settings recover", normalized.DurationSeconds == 2.5 && normalized.VolumeStep == 10 && normalized.Position == "BottomCenter" && normalized.Accent == "Ice" && normalized.FlyoutScale == 1);
        var store = new SettingsStore(Path.Combine(directory, "test-data"));
        Directory.CreateDirectory(store.DirectoryPath);
        File.WriteAllText(store.FilePath, "{broken-json");
        Check("Corrupt settings recover", store.Load().VolumeKeys);
        var settings = new AppSettings { VolumeStep = 4, Position = "TopLeft", DurationSeconds = 3.5, Animations = false, ShowMedia = false, ArtworkBackground = false, Accent = "Lilac", FlyoutScale = 1.25, TargetDisplay = "panel-b" };
        store.Save(settings);
        var loaded = store.Load();
        Check("Settings survive restart", loaded.VolumeStep == 4 && loaded.Position == "TopLeft" && loaded.DurationSeconds == 3.5 && !loaded.ShowMedia && !loaded.ArtworkBackground && loaded.Accent == "Lilac" && loaded.FlyoutScale == 1.25 && loaded.TargetDisplay == "panel-b");
        var displays = new[] { new DisplayInfo("panel-a", "display-a", "A", "A", new System.Drawing.Rectangle(0, 0, 1920, 1080), true), new DisplayInfo("panel-b", "display-b", "B", "B", new System.Drawing.Rectangle(-1920, 0, 1920, 1080), false) };
        Check("Monitor selection and disconnect fallback", MonitorCatalog.SelectTarget("panel-b", displays)?.Id == "panel-b" && MonitorCatalog.SelectTarget("Primary", displays)?.Id == "panel-a" && MonitorCatalog.SelectTarget("missing-display", displays)?.Id == "panel-a" && MonitorCatalog.SelectTarget("Cursor", displays) is null);
        Check("Connected Windows monitors enumerate", MonitorCatalog.Current.Length > 0 && MonitorCatalog.Current.Any(d => d.Primary));
        var work = new Rect(-1920, -200, 1920, 1080);
        var size = new Size(396, 310);
        Check("All four corners support negative monitor coordinates", FlyoutWindow.CalculateLocation(work, size, 12, "TopLeft") == new Point(-1908, -188) && FlyoutWindow.CalculateLocation(work, size, 12, "TopRight") == new Point(-408, -188) && FlyoutWindow.CalculateLocation(work, size, 12, "BottomLeft") == new Point(-1908, 558) && FlyoutWindow.CalculateLocation(work, size, 12, "BottomRight") == new Point(-408, 558));

        using var audio = new AudioService();
        var state = audio.Read();
        Check("CoreAudio read is safe", state.Volume is >= 0 and <= 100 && !string.IsNullOrWhiteSpace(state.DeviceName), JsonSerializer.Serialize(state));
        using (var keyboard = new KeyboardService(Application.Current.Dispatcher) { AudioAvailable = true })
        {
            Check("Global keyboard hook installs", true);
            var commands = new List<VolumeCommand>();
            keyboard.VolumePressed += commands.Add;
            var keys = state.Volume >= 98 ? new ushort[] { 0xAE, 0xAF } : new ushort[] { 0xAF, 0xAE };
            var inputs = keys.SelectMany(key => new[] { MakeKey(key, false), MakeKey(key, true) }).ToArray();
            var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput>());
            var inputError = Marshal.GetLastWin32Error();
            await Task.Delay(150);
            Check("Volume key presses reach Luma once per key", sent == inputs.Length && commands.Count == 2 &&
                commands[0] == (keys[0] == 0xAF ? VolumeCommand.Up : VolumeCommand.Down) &&
                commands[1] == (keys[1] == 0xAF ? VolumeCommand.Up : VolumeCommand.Down),
                $"Sent={sent}; error={inputError}; size={Marshal.SizeOf<NativeInput>()}; commands={string.Join(',', commands)}");
            var after = audio.Read();
            var unchanged = !state.Available || (after.Available && after.Volume == state.Volume && after.Muted == state.Muted);
            Check("Intercepted keys do not also change Windows volume", unchanged);
            if (!unchanged && state.Available)
            {
                audio.SetVolume(state.Volume);
                if (audio.Read().Muted != state.Muted) audio.ToggleMute();
            }
        }

        using (var mediaService = new MediaService(Application.Current.Dispatcher))
        {
            await mediaService.StartAsync().WaitAsync(TimeSpan.FromSeconds(6));
            Check("Windows media session API initializes", mediaService.Error is null, mediaService.Error ?? $"Active session: {mediaService.State.Available}");
        }
        var sample = PreviewData.Media();
        var now = DateTimeOffset.UtcNow;
        var timed = sample with { SampledAt = now };
        Check("Playback clock advances and clamps", timed.CurrentPosition(now.AddSeconds(2)) == TimeSpan.FromSeconds(74) && timed.CurrentPosition(now.AddHours(1)) == timed.End && (timed with { IsPlaying = false }).CurrentPosition(now.AddSeconds(2)) == timed.Position);

        var previewSettings = new AppSettings { DurationSeconds = 2.5 };
        var flyout = new FlyoutWindow(previewSettings);
        flyout.Prepare();
        var saved = 0;
        var window = new SettingsWindow(previewSettings, () => saved++, () => flyout.ShowPreview(sample)) { AllowClose = true };
        window.Show();
        flyout.ShowVolume(new(true, 40, false, "Preview"), true);
        await Task.Delay(400);
        window.UpdateLayout(); flyout.UpdateLayout();
        Check("Settings window loads", window.ActualWidth >= 900 && window.ActualHeight >= 660);
        Check("Overlay is topmost without focus", flyout.Topmost && !flyout.ShowActivated && !flyout.IsActive && !flyout.ShowInTaskbar);
        Check("Volume strip stays compact", Math.Abs(flyout.ActualHeight - 80) < 1 && ((FrameworkElement)flyout.FindName("VolumeCard")).ActualHeight == 56);
        Render(window, Path.Combine(directory, "luma-settings.png"));
        Render(flyout, Path.Combine(directory, "luma-flyout.png"));
        flyout.ShowPreview(sample);
        await Task.Delay(400);
        Render(flyout, Path.Combine(directory, "luma-media.png"));
        Check("Media card expands separately", Math.Abs(flyout.ActualHeight - 310) < 1 && ((System.Windows.Controls.TextBlock)flyout.FindName("TrackTitleText")).Text == sample.Title);
        var cover = (FrameworkElement)flyout.FindName("ArtworkCover");
        var mediaCard = (FrameworkElement)flyout.FindName("MediaCard");
        var seekControl = (FrameworkElement)flyout.FindName("TimelineSlider");
        Check("Larger cover and lower timeline fit", cover.ActualWidth == 80 && cover.ActualHeight == 80 && seekControl.TransformToAncestor(mediaCard).Transform(new Point()).Y >= 110);
        var backdrop = (System.Windows.Controls.Image)flyout.FindName("ArtworkBackdrop");
        Check("Artwork is softly blurred behind media", backdrop.Source == sample.Artwork && backdrop.Effect is System.Windows.Media.Effects.BlurEffect { Radius: 18 } && backdrop.Opacity == 0.55);
        var liveTimeline = (SmoothSlider)flyout.FindName("TimelineSlider");
        var sampledPosition = liveTimeline.Value;
        await Task.Delay(240);
        Check("Timeline advances on first card opening", liveTimeline.Value > sampledPosition + 0.05);
        var volumeRequests = 0;
        var mediaRequests = new List<MediaCommand>();
        var seekRequests = new List<TimeSpan>();
        flyout.VolumeRequested += _ => volumeRequests++;
        flyout.MediaRequested += mediaRequests.Add;
        flyout.SeekRequested += seekRequests.Add;
        var volumeSlider = (SmoothSlider)flyout.FindName("VolumeSlider");
        var play = (System.Windows.Controls.Button)flyout.FindName("PlayButton");
        var timeline = (SmoothSlider)flyout.FindName("TimelineSlider");
        volumeSlider.Value = 65;
        play.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        timeline.Value = 90;
        Check("Preview controls stay local", volumeRequests == 0 && mediaRequests.Count == 0 && seekRequests.Count == 0 && ((System.Windows.Controls.TextBlock)flyout.FindName("PlayIcon")).Text == "\uE768");

        flyout.ShowVolume(new(true, 40, false, "Test endpoint"));
        flyout.UpdateMedia(sample);
        volumeSlider.Value = 70;
        await Task.Delay(250);
        Check("Volume animation sends one request", volumeRequests == 1 && Math.Abs(volumeSlider.DisplayValue - 70) < 0.01);
        ((System.Windows.Controls.Button)flyout.FindName("NextButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        timeline.Value = 105;
        Check("Media and seek commands route to service", mediaRequests.SequenceEqual(new[] { MediaCommand.Next }) && seekRequests.Count == 1 && Math.Abs(seekRequests[0].TotalSeconds - 105) < 0.1);
        flyout.UpdateMedia(sample with { CanNext = false, CanSeek = false });
        Check("Unsupported player controls are disabled", !((System.Windows.Controls.Button)flyout.FindName("NextButton")).IsEnabled && !timeline.IsEnabled);
        flyout.UpdateMedia(Luma.Models.MediaState.Empty);
        await Task.Delay(280);
        Check("No empty media card remains", ((FrameworkElement)flyout.FindName("MediaCard")).Visibility == Visibility.Collapsed && Math.Abs(flyout.ActualHeight - 80) < 1);
        var preferencesScroll = (System.Windows.Controls.ScrollViewer)window.FindName("PreferencesScroll");
        preferencesScroll.ScrollToEnd();
        await Task.Delay(100);
        Render(window, Path.Combine(directory, "luma-settings-more.png"));

        var duration = (System.Windows.Controls.Slider)window.FindName("DurationSlider");
        duration.Value = 4;
        Check("Duration preference updates", previewSettings.DurationSeconds == 4 && saved > 0);
        var position = (System.Windows.Controls.ComboBox)window.FindName("PositionCombo");
        position.SelectedIndex = 2;
        Check("Placement preference updates", previewSettings.Position == "TopRight");
        ((System.Windows.Controls.ComboBox)window.FindName("MonitorCombo")).SelectedIndex = 1;
        Check("Monitor preference updates", previewSettings.TargetDisplay == "Primary");
        var toggle = (System.Windows.Controls.CheckBox)window.FindName("VolumeToggle");
        toggle.IsChecked = false;
        Check("Volume interception can be disabled", !previewSettings.VolumeKeys);
        ((System.Windows.Controls.ComboBox)window.FindName("AccentCombo")).SelectedIndex = 1;
        Check("Accent preference updates preview", previewSettings.Accent == "Lilac" && ((SolidColorBrush)Application.Current.Resources["AccentBrush"]).Color == (Color)ColorConverter.ConvertFromString("#B398EE"));
        ((System.Windows.Controls.CheckBox)window.FindName("MediaToggle")).IsChecked = false;
        Check("Media visibility preference updates", !previewSettings.ShowMedia && ((FrameworkElement)window.FindName("MediaPreviewCard")).Visibility == Visibility.Collapsed);
        ((System.Windows.Controls.CheckBox)window.FindName("MediaToggle")).IsChecked = true;
        var scaleControl = (System.Windows.Controls.Slider)window.FindName("ScaleSlider");
        foreach (var scale in new[] { 0.75, 1.0, 1.4 })
        {
            scaleControl.Value = scale;
            flyout.ApplyPreferences(); flyout.ShowPreview(PreviewData.Media());
            await Task.Delay(320);
            Check($"Overlay scale {scale:0.##} preserves layout", Math.Abs(flyout.ActualWidth - 396 * scale) < 1 && Math.Abs(flyout.ActualHeight - 310 * scale) < 1 && previewSettings.FlyoutScale == scale);
            Render(flyout, Path.Combine(directory, $"luma-scale-{scale * 100:0}.png"));
        }
        scaleControl.Value = 1; flyout.ApplyPreferences();
        flyout.ShowLock("Caps Lock", true);
        await Task.Delay(100);
        Render(flyout, Path.Combine(directory, "luma-lock.png"));
        Check("Lock notification renders expected label", ((System.Windows.Controls.TextBlock)flyout.FindName("StatusText")).Text == "Caps Lock");
        window.Close(); flyout.Close();
        Palette.Apply("Ice");
        var motion = new FlyoutWindow(new AppSettings { DurationSeconds = 8, Animations = true });
        var frameDirectory = Path.Combine(directory, "motion"); Directory.CreateDirectory(frameDirectory);
        motion.ShowPreview(PreviewData.Media());
        for (var frame = 0; frame < 34; frame++)
        {
            if (frame == 14) motion.ShowVolume(new(true, 72, false, "Preview"), true);
            await Task.Delay(33);
            Render(motion, Path.Combine(frameDirectory, $"frame-{frame:000}.png"), 396, 310);
        }
        motion.Dismiss();
        for (var frame = 34; frame < 41; frame++)
        {
            await Task.Delay(33);
            Render(motion, Path.Combine(frameDirectory, $"frame-{frame:000}.png"), 396, 310);
        }
        Check("Animated dismissal completes", !motion.IsVisible);
        motion.Close();
        using (var runtime = new AppController(Path.Combine(directory, "runtime-data")))
        {
            runtime.ShowSettings();
            await runtime.AudioReady.WaitAsync(TimeSpan.FromSeconds(5));
            Check("Normal startup creates settings and system tray", true);
        }
        var slowDevice = new DelayedAudioDevice();
        using (var responsive = new AppController(Path.Combine(directory, "latency-data"), () => slowDevice))
        {
            await responsive.AudioReady.WaitAsync(TimeSpan.FromSeconds(4));
            slowDevice.DelayReads = true;
            Check("Slow device fixture entered background query", await Task.Run(() => slowDevice.Entered.Wait(2500)));
            var frame = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
            responsive.DiagnosticFlyout.ResponseMeasured += elapsed => frame.TrySetResult(elapsed);
            var inputs = new[] { MakeKey(0xAF, false), MakeKey(0xAF, true) };
            var sent = SendInput(2, inputs, Marshal.SizeOf<NativeInput>());
            var elapsed = await frame.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Check("First volume frame does not wait for a slow driver", sent == 2 && elapsed < 250, $"Input-to-render preparation: {elapsed:0.0} ms; simulated device query: 1200 ms");
            slowDevice.DelayReads = false;
            await responsive.FlushAudioAsync().WaitAsync(TimeSpan.FromSeconds(4));
            Check("Background audio command reaches device", slowDevice.Volume == 42 && slowDevice.Writes == 1);
        }
        File.WriteAllText(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(new { failures, results }, new JsonSerializerOptions { WriteIndented = true }));
        return failures == 0 ? 0 : 1;
    }

    private sealed class DelayedAudioDevice : IAudioDevice
    {
        public readonly ManualResetEventSlim Entered = new();
        public volatile bool DelayReads;
        public int Volume = 40;
        public int Writes;
        private bool muted;
        public AudioState Read()
        {
            if (DelayReads) { Entered.Set(); Thread.Sleep(1200); }
            return new(true, Volume, muted, "Diagnostic audio device");
        }
        public bool SetVolume(int volume) { Volume = Math.Clamp(volume, 0, 100); muted = false; Writes++; return true; }
        public bool ToggleMute() { muted = !muted; Writes++; return true; }
        public void Dispose() { Entered.Dispose(); }
    }

    private static void Render(Window window, string path, int? fixedWidth = null, int? fixedHeight = null)
    {
        var content = (FrameworkElement)window.Content;
        content.UpdateLayout();
        var width = fixedWidth ?? (int)Math.Ceiling(content.ActualWidth);
        var height = fixedHeight ?? (int)Math.Ceiling(content.ActualHeight);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var drawing = background.RenderOpen()) drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(18, 18, 20)), null, new Rect(0, 0, width, height));
        bitmap.Render(background);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static NativeInput MakeKey(ushort key, bool up) => new() { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = key, Flags = up ? 2u : 0u } } };
    [StructLayout(LayoutKind.Sequential)] private struct NativeInput { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KeyboardInput Keyboard; [FieldOffset(0)] public MouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey; public ushort ScanCode; public uint Flags; public uint Time; public UIntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X; public int Y; public uint MouseData; public uint Flags; public uint Time; public UIntPtr ExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, NativeInput[] input, int size);
}
