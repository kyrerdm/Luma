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
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Luma.Models;
using Luma.Services;
using Luma.Views;
using Luma.Controls;
using Luma.Themes;

namespace Luma;

internal static partial class Diagnostics
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
        var settings = new AppSettings { VolumeStep = 4, Position = "TopLeft", DurationSeconds = 3.5, Animations = false, ShowMedia = false, ArtworkBackground = false, ObserveBrightness = false, Accent = "Lilac", FlyoutScale = 1.25, TargetDisplay = "panel-b" };
        store.Save(settings);
        var loaded = store.Load();
        Check("Settings survive restart", loaded.VolumeStep == 4 && loaded.Position == "TopLeft" && loaded.DurationSeconds == 3.5 && !loaded.ShowMedia && !loaded.ArtworkBackground && !loaded.ObserveBrightness && loaded.Accent == "Lilac" && loaded.FlyoutScale == 1.25 && loaded.TargetDisplay == "panel-b");
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
            var before = mediaService.State.Sources;
            await mediaService.RefreshForDiagnosticsAsync().WaitAsync(TimeSpan.FromSeconds(6));
            var after = mediaService.State.Sources;
            var sameApps = before.Select(s => s.AppName).Order().SequenceEqual(after.Select(s => s.AppName).Order());
            Check("Windows session identities survive a refresh", !sameApps || before.Select(s => s.Id).Order().SequenceEqual(after.Select(s => s.Id).Order()), $"Available sessions: {after.Count}; unchanged app list: {sameApps}");
        }
        await VerifyMediaSourcesAsync(directory, (name, passed) => Check(name, passed));
        await VerifyBrightnessAsync(directory, Check);
        await VerifyNativeOsdAsync((name, passed) => Check(name, passed));
        await VerifyReleaseFeaturesAsync(directory, Check);
        await VerifyDailyUseAsync(directory, Check);
        await VerifyLivePreviewAsync(directory, Check);
        await VerifyAudioPopupAsync(Check);
        await VerifyAboutAsync(directory, Check);
        await VerifyStabilityAsync(directory, Check);
        var sample = PreviewData.Media();
        var now = DateTimeOffset.UtcNow;
        var timed = sample with { SampledAt = now };
        Check("Playback clock advances and clamps", timed.CurrentPosition(now.AddSeconds(2)) == TimeSpan.FromSeconds(74) && timed.CurrentPosition(now.AddHours(1)) == timed.End && (timed with { IsPlaying = false }).CurrentPosition(now.AddSeconds(2)) == timed.Position);

        var previewSettings = new AppSettings { DurationSeconds = 2.5 };
        var flyout = new FlyoutWindow(previewSettings);
        flyout.Prepare();
        var saved = 0;
        var window = new SettingsWindow(previewSettings, () => saved++) { AllowClose = true };
        SeedSettingsPreview(window);
        window.SetBrightness(new(true, 65, "sample", Array.Empty<int>(), "Dahili ekran · örnek görünüm"));
        window.Show();
        flyout.ShowVolume(new(true, 40, false, "Preview"), true);
        await Task.Delay(400);
        window.UpdateLayout(); flyout.UpdateLayout();
        Check("Settings window loads", window.ActualWidth >= 900 && window.ActualHeight >= 660);
        var titleBar = (FrameworkElement)window.FindName("TitleBar");
        Check("Empty title bar and app label are native drag regions", NativeHitTest(titleBar, new Point(28, 23)) == 2 && NativeHitTest(titleBar, new Point(470, 23)) == 2 && NativeHitTest(titleBar, new Point(810, 23)) == 2);
        var minimizeButton = (FrameworkElement)window.FindName("MinimizeButton");
        var closeButton = (FrameworkElement)window.FindName("CloseButton");
        Check("Title bar buttons remain clickable", NativeHitTest(minimizeButton, new Point(18, 18)) == 1 && NativeHitTest(closeButton, new Point(18, 18)) == 1);
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
        flyout.MediaRequested += (_, command) => mediaRequests.Add(command);
        flyout.SeekRequested += (_, position) => seekRequests.Add(position);
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

        var darkMenus = true;
        foreach (var name in new[] { "PositionCombo", "MonitorCombo", "AccentCombo" })
        {
            window.Navigate(name == "MonitorCombo" ? "Display" : "Appearance");
            await Task.Delay(30);
            var combo = (ComboBox)window.FindName(name);
            combo.IsDropDownOpen = true;
            await Task.Delay(180);
            var popup = (Popup)combo.Template.FindName("PART_Popup", combo);
            var surface = (Border)combo.Template.FindName("DropDownSurface", combo);
            darkMenus &= popup.IsOpen && surface.Background is SolidColorBrush { Color.R: < 70, Color.G: < 70, Color.B: < 70 } && surface.ActualWidth >= combo.ActualWidth;
            foreach (ComboBoxItem item in combo.Items)
            {
                darkMenus &= item.Foreground is SolidColorBrush { Color.R: > 220, Color.G: > 220, Color.B: > 220 } && item.FontFamily.Source == window.FontFamily.Source && item.FontSize == 12;
                if (item.IsSelected) darkMenus &= ((FrameworkElement)item.Template.FindName("SelectionMark", item)).Visibility == Visibility.Visible;
            }
            RenderElement((FrameworkElement)popup.Child, Path.Combine(directory, $"luma-{name}-dropdown.png"));
            combo.IsDropDownOpen = false;
        }
        Check("All preference dropdowns use dark surfaces and consistent text", darkMenus);
        var accents = (ComboBox)window.FindName("AccentCombo");
        var accentPresets = Palette.Options.Count == 12 && Palette.Options.Select(option => option.Hex).Distinct().Count() == 12;
        foreach (ComboBoxItem item in accents.Items)
        {
            if (item.Content is not AccentOption option) continue;
            accents.SelectedItem = item;
            store.Save(previewSettings);
            accentPresets &= store.Load().Accent == option.Id && ((SolidColorBrush)Application.Current.Resources["AccentBrush"]).Color == option.Color;
        }
        Check("All twelve accent presets apply and survive restart", accentPresets);
        accents.SelectedIndex = 0;

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
        var pointer = System.Windows.Forms.Cursor.Position;
        var pointerScreen = System.Windows.Forms.Screen.FromPoint(pointer).WorkingArea;
        var lockSettings = new AppSettings
        {
            DurationSeconds = 1, TargetDisplay = "Cursor",
            Position = (pointer.Y < pointerScreen.Top + pointerScreen.Height / 2 ? "Bottom" : "Top") + (pointer.X < pointerScreen.Left + pointerScreen.Width / 2 ? "Right" : "Left")
        };
        var pinnedFlyout = new FlyoutWindow(lockSettings);
        pinnedFlyout.ShowPreview(PreviewData.Media());
        await Task.Delay(300);
        var pin = (Button)pinnedFlyout.FindName("PinButton");
        pin.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        pinnedFlyout.ShowLock("Caps Lock", true);
        Render(pinnedFlyout, Path.Combine(directory, "luma-pinned-lock.png"));
        await Task.Delay(1500);
        var lockCard = (FrameworkElement)pinnedFlyout.FindName("LockCard");
        var restoredMedia = (FrameworkElement)pinnedFlyout.FindName("MediaCard");
        Check("Pinned media returns after Caps Lock expires", pinnedFlyout.IsVisible && lockCard.Visibility == Visibility.Collapsed && restoredMedia.Visibility == Visibility.Visible);
        Render(pinnedFlyout, Path.Combine(directory, "luma-pinned-restored.png"));
        pinnedFlyout.ShowLock("Scroll Lock", true);
        await Task.Delay(1500);
        Check("Repeated lock notices do not replace pinned media permanently", pinnedFlyout.IsVisible && lockCard.Visibility == Visibility.Collapsed && restoredMedia.Visibility == Visibility.Visible && ((TextBlock)pinnedFlyout.FindName("TrackTitleText")).Text == sample.Title);
        pinnedFlyout.ShowLock("Caps Lock", false);
        pinnedFlyout.ShowPreview(PreviewData.Media());
        await Task.Delay(1200);
        Check("New volume display cancels pending lock dismissal", pinnedFlyout.IsVisible && lockCard.Visibility == Visibility.Collapsed);
        pin.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        pinnedFlyout.ShowLock("Num Lock", true);
        await Task.Delay(1500);
        Check("Unpinned lock notice still dismisses normally", !pinnedFlyout.IsVisible);
        pinnedFlyout.Close();
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
            var actualAudio = await runtime.AudioReady.WaitAsync(TimeSpan.FromSeconds(5));
            Check("Normal startup creates settings and system tray", true);
            Check("The running controller sends cached system audio to settings", ((TextBlock)runtime.DiagnosticSettings.FindName("PreviewVolumeValue")).Text == (actualAudio.Available ? actualAudio.Volume.ToString() : "—"));
            var runtimeWindow = runtime.DiagnosticSettings;
            runtimeWindow.Hide();
            runtime.OnTrayMouseClick(null, new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Right, 1, 0, 0, 0));
            var rightClickStaysHidden = !runtimeWindow.IsVisible;
            runtimeWindow.WindowState = WindowState.Minimized;
            runtime.OnTrayMouseClick(null, new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left, 1, 0, 0, 0));
            Check("One left tray click opens settings and right click stays separate", rightClickStaysHidden && runtimeWindow.IsVisible && runtimeWindow.WindowState == WindowState.Normal);
            ((ComboBox)runtimeWindow.FindName("AccentCombo")).SelectedIndex = 3;
            runtimeWindow.Hide();
            var trayMenu = runtime.DiagnosticTrayMenu;
            trayMenu.Show(new System.Drawing.Point(140, 140));
            await Task.Delay(150);
            trayMenu.Items[0].Select();
            using (var bitmap = new System.Drawing.Bitmap(trayMenu.Width, trayMenu.Height))
            {
                trayMenu.DrawToBitmap(bitmap, new System.Drawing.Rectangle(System.Drawing.Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(directory, "luma-tray-menu.png"), System.Drawing.Imaging.ImageFormat.Png);
                var darkTray = trayMenu.Items.Cast<System.Windows.Forms.ToolStripItem>().All(item => bitmap.GetPixel(bitmap.Width - 8, item.Bounds.Top + item.Height / 2).GetBrightness() < 0.35f);
                Check("Tray menu renders dark backgrounds and separator", darkTray && trayMenu.ForeColor.R > 220 && trayMenu.Font.FontFamily.Name == "Segoe UI");
                var iconArea = ((DarkTrayMenu)trayMenu).GlyphArea(trayMenu.Items[0]);
                var expected = Palette.Resolve("Rose").Color;
                var accentPixels = 0;
                for (var y = iconArea.Top; y < iconArea.Bottom; y++)
                    for (var x = iconArea.Left; x < iconArea.Right; x++)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        if (Math.Abs(pixel.R - expected.R) < 45 && Math.Abs(pixel.G - expected.G) < 45 && Math.Abs(pixel.B - expected.B) < 45) accentPixels++;
                    }
                Check("Tray highlight follows the selected accent", accentPixels >= 3);
            }
            foreach (var index in new[] { 1 })
            {
                trayMenu.Items[index].Select();
                using var bitmap = new System.Drawing.Bitmap(trayMenu.Width, trayMenu.Height);
                trayMenu.DrawToBitmap(bitmap, new System.Drawing.Rectangle(System.Drawing.Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(directory, $"luma-tray-{index}.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            trayMenu.Close();
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
            double elapsed;
            try { elapsed = await frame.Task.WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (TimeoutException)
            {
                File.AppendAllText(Path.Combine(directory, "progress.log"), $"Response timeout: sent={sent}; overlay visible={responsive.DiagnosticFlyout.IsVisible}; size={responsive.DiagnosticFlyout.ActualWidth}x{responsive.DiagnosticFlyout.ActualHeight}{Environment.NewLine}");
                throw;
            }
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
        => RenderElement((FrameworkElement)window.Content, path, fixedWidth, fixedHeight);

    private static void RenderElement(FrameworkElement content, string path, int? fixedWidth = null, int? fixedHeight = null)
    {
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
    private static int NativeHitTest(FrameworkElement element, Point local)
    {
        var point = element.PointToScreen(local);
        var packed = (((int)point.Y & 0xFFFF) << 16) | ((int)point.X & 0xFFFF);
        return SendMessage(new WindowInteropHelper(Window.GetWindow(element)).Handle, 0x84, IntPtr.Zero, (IntPtr)packed).ToInt32();
    }
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] private struct NativeInput { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KeyboardInput Keyboard; [FieldOffset(0)] public MouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey; public ushort ScanCode; public uint Flags; public uint Time; public UIntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X; public int Y; public uint MouseData; public uint Flags; public uint Time; public UIntPtr ExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, NativeInput[] input, int size);
}
