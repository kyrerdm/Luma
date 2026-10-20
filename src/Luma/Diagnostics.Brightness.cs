using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Luma.Models;
using Luma.Services;
using Luma.Views;

namespace Luma;

internal static partial class Diagnostics
{
    private static async Task VerifyBrightnessAsync(string directory, Action<string, bool, string> check)
    {
        var ui = Application.Current.Dispatcher;
        using (var native = new BackgroundBrightnessService(ui))
        {
            var state = await native.Ready.WaitAsync(TimeSpan.FromSeconds(8));
            check("Native brightness read is supported or safely unavailable", !state.Available || (state.Value is >= 0 and <= 100 && !string.IsNullOrWhiteSpace(state.InstanceId)), $"Supported internal display: {state.Available}");
            if (state.Available && Environment.GetEnvironmentVariable("LUMA_VERIFY_BRIGHTNESS_WRITE") == "1")
            {
                var levels = state.Levels.Count > 1 ? state.Levels : Enumerable.Range(0, 101).ToArray();
                var target = levels.Where(level => level != state.Value).OrderBy(level => Math.Abs(level - state.Value)).First();
                try
                {
                    native.SetBrightness(target);
                    await native.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    await Task.Delay(150);
                    await native.RefreshForDiagnosticsAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    check("Real internal panel accepts a one-level brightness change", native.State.Available && native.State.Value == target, $"Original={state.Value}; test={target}; actual={native.State.Value}");
                }
                finally
                {
                    native.SetBrightness(state.Value);
                    await native.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    await Task.Delay(150);
                    await native.RefreshForDiagnosticsAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    check("Real internal panel brightness is restored after the smoke test", native.State.Available && native.State.Value == state.Value, $"Original={state.Value}; restored={native.State.Value}");
                }
            }
        }
        var fake = new DiagnosticBrightnessDevice();
        using (var service = new BackgroundBrightnessService(ui, () => fake))
        {
            await service.Ready.WaitAsync(TimeSpan.FromSeconds(3));
            check("Brightness initialization never writes to the panel", fake.Writes == 0 && service.State.Value == 40, "");
            fake.Emit("internal-a", 100);
            var shellWindow = new NativeOsdWindow((IntPtr)7, "XamlExplorerHostIslandWindow", "", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), 0x84000000, 0x08200088, new Rect(854, 944, 211, 76), 120, true, true);
            var shellPlatform = new DiagnosticOsdPlatform(shellWindow);
            using (var nativeOsd = new NativeBrightnessOsdSuppressor(() => shellPlatform))
            {
                await nativeOsd.Ready.WaitAsync(TimeSpan.FromSeconds(3));
                nativeOsd.SetEnabled(true);
                service.Activity += nativeOsd.NotifyBrightnessActivity;
                try
                {
                    var signal = Task.Run(async () =>
                    {
                        // The value stays at its limit, but this is still real hardware activity.
                        fake.Emit("internal-a", 100);
                        await nativeOsd.FlushAsync();
                        return !shellPlatform.Current.Visible;
                    });
                    var watch = Stopwatch.StartNew();
                    // Deliberately keep the UI occupied; dismissal must run independently.
                    var completedWithoutUi = signal.Wait(TimeSpan.FromSeconds(2));
                    var hiddenWithoutUi = completedWithoutUi && signal.Result;
                    await signal;
                    check("Native brightness dismissal bypasses a busy UI even at an unchanged limit", hiddenWithoutUi && fake.Writes == 0, $"UI-independent dismissal: {watch.Elapsed.TotalMilliseconds:F1} ms");
                }
                finally { service.Activity -= nativeOsd.NotifyBrightnessActivity; }
            }
            fake.Emit("internal-a", 40);
            var activityCount = 0;
            void CountActivity() => Interlocked.Increment(ref activityCount);
            service.Activity += CountActivity;
            await service.RefreshForDiagnosticsAsync().WaitAsync(TimeSpan.FromSeconds(3));
            fake.Emit("different-panel", 20);
            check("Unchanged polling and other panels do not arm native OSD dismissal", activityCount == 0, "");
            fake.SetSilently(60);
            await service.RefreshForDiagnosticsAsync().WaitAsync(TimeSpan.FromSeconds(3));
            check("Polling brightness changes trigger native dismissal before the UI update", activityCount == 1 && service.State.Value == 60, "");
            service.Activity -= CountActivity;
            fake.Emit("internal-a", 40);
            await Task.Delay(50);
            var limitActivity = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            service.Changed += update => { if (update.HardwareActivity && update.State.Value == 40) limitActivity.TrySetResult(); };
            fake.Emit("internal-a", 40);
            await limitActivity.Task.WaitAsync(TimeSpan.FromSeconds(2));
            check("Brightness hardware activity is reported even at an unchanged limit", service.State.Value == 40 && fake.Writes == 0, "");
            check("Brightness requests respect the panel's supported levels", service.State.Snap(-20) == 0 && service.State.Snap(83) == 80 && service.State.Snap(120) == 100, "");
            var noticed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            service.Changed += update => { if (update.State.Value == 60) noticed.TrySetResult(); };
            fake.BlockReads = true;
            var refresh = service.RefreshForDiagnosticsAsync();
            try
            {
                var entered = await Task.Run(() => fake.ReadEntered.Wait(2000));
                var watch = Stopwatch.StartNew();
                fake.Emit("internal-a", 60);
                await noticed.Task.WaitAsync(TimeSpan.FromSeconds(2));
                check("Brightness hardware events bypass a blocked driver query", entered && !refresh.IsCompleted && service.State.Value == 60 && watch.ElapsedMilliseconds < 500, "");
            }
            finally { fake.BlockReads = false; fake.ReadReleased.Set(); }
            await refresh.WaitAsync(TimeSpan.FromSeconds(3));
            check("A stale brightness read cannot undo a newer hardware event", service.State.Value == 60, "");
            fake.Emit("different-panel", 20);
            await Task.Delay(70);
            check("Brightness events from other panels are ignored", service.State.Value == 60, "");
            service.SetBrightness(83);
            await service.FlushAsync().WaitAsync(TimeSpan.FromSeconds(3));
            await Task.Delay(70);
            check("Brightness slider commands reach the selected internal panel", fake.Value == 80 && service.State.Value == 80 && fake.Writes == 1, "");
            fake.FailWrites = true;
            var failed = new TaskCompletionSource<BrightnessUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
            service.Changed += update => { if (update.WriteFailed) failed.TrySetResult(update); };
            service.SetBrightness(20);
            var failure = await failed.Task.WaitAsync(TimeSpan.FromSeconds(3));
            check("Failed brightness writes restore the actual panel value", failure.State.Value == 80 && failure.WriteFailed, "");
            fake.Available = false;
            await service.RefreshForDiagnosticsAsync().WaitAsync(TimeSpan.FromSeconds(3));
            check("A disconnected internal display becomes unavailable", !service.State.Available, "");
        }

        var pointer = System.Windows.Forms.Cursor.Position;
        var screen = System.Windows.Forms.Screen.FromPoint(pointer).WorkingArea;
        var settings = new AppSettings
        {
            Animations = false, DurationSeconds = 1, TargetDisplay = "Cursor",
            Position = (pointer.Y < screen.Top + screen.Height / 2 ? "Bottom" : "Top") + (pointer.X < screen.Left + screen.Width / 2 ? "Right" : "Left")
        };
        var sample = new BrightnessState(true, 65, "sample", Array.Empty<int>(), "Dahili ekran");
        var flyout = new FlyoutWindow(settings);
        flyout.Prepare();
        flyout.ShowBrightness(sample, true);
        await Task.Delay(100);
        var card = (FrameworkElement)flyout.FindName("BrightnessCard");
        var volume = (FrameworkElement)flyout.FindName("VolumeCard");
        var media = (FrameworkElement)flyout.FindName("MediaCard");
        check("Brightness flyout is compact and separates volume and media", Math.Abs(flyout.ActualHeight - 80) < 1 && card.Visibility == Visibility.Visible && volume.Visibility == Visibility.Collapsed && media.Visibility == Visibility.Collapsed, "");
        Render(flyout, Path.Combine(directory, "luma-brightness.png"));
        var commands = 0;
        flyout.BrightnessRequested += _ => commands++;
        ((Slider)flyout.FindName("BrightnessSlider")).Value = 55;
        check("Diagnostic brightness samples never send real commands", commands == 0 && ((TextBlock)flyout.FindName("BrightnessValueText")).Text == "55", "");
        await Task.Delay(1400);
        check("Brightness flyout dismisses normally", !flyout.IsVisible, "");
        flyout.ShowVolume(new(true, 40, false, "Diagnostic audio"));
        flyout.UpdateMedia(PreviewData.Media());
        ((Button)flyout.FindName("PinButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        flyout.ShowBrightness(sample, true);
        flyout.RefreshVolume(new(true, 72, false, "Diagnostic audio"));
        await Task.Delay(1400);
        check("Pinned volume and media return with the latest value after brightness expires", flyout.IsVisible && volume.Visibility == Visibility.Visible && media.Visibility == Visibility.Visible && card.Visibility == Visibility.Collapsed && ((TextBlock)flyout.FindName("ValueText")).Text == "72", "");
        flyout.Close();

        var controlDevice = new DiagnosticBrightnessDevice();
        // Use a fresh directory on every run: pre-1.0 Turkish preferences must
        // not make fresh-install checks pass only on the developer's computer.
        using (var runtime = new AppController(Path.Combine(directory, "brightness-runtime-" + Guid.NewGuid().ToString("N")), () => new DelayedAudioDevice(), () => controlDevice))
        {
            await runtime.BrightnessReady.WaitAsync(TimeSpan.FromSeconds(3));
            var window = runtime.DiagnosticSettings;
            var slider = (Slider)window.FindName("DisplayBrightnessSlider");
            slider.Value = 83;
            await runtime.FlushBrightnessAsync().WaitAsync(TimeSpan.FromSeconds(3));
            await Task.Delay(100);
            check("Settings brightness slider controls the service and shows the applied level", controlDevice.Writes == 1 && controlDevice.Value == 80 && slider.Value == 80 && ((TextBlock)window.FindName("DisplayBrightnessText")).Text == "80 %", $"Writes={controlDevice.Writes}; device={controlDevice.Value}; slider={slider.Value}; language={Strings.Language}; text={((TextBlock)window.FindName("DisplayBrightnessText")).Text}");
            ((CheckBox)window.FindName("BrightnessToggle")).IsChecked = false;
            runtime.DiagnosticFlyout.Dismiss();
            controlDevice.Emit("internal-a", 60);
            await Task.Delay(250);
            check("Brightness indicator preference disables automatic popups", !runtime.DiagnosticFlyout.IsVisible && slider.Value == 60, "");
            window.SetBrightness(BrightnessState.Unavailable());
            check("Unsupported displays disable the settings brightness slider", !slider.IsEnabled && ((TextBlock)window.FindName("DisplayBrightnessText")).Text == "—", "");
            check("Tray menu contains only settings and exit", runtime.DiagnosticTrayMenu.Items.Count == 2 && runtime.DiagnosticTrayMenu.Items[0].Name == "Settings" && runtime.DiagnosticTrayMenu.Items[1].Name == "Exit" && runtime.DiagnosticTrayMenu.Items[0].Text == "Luma settings" && runtime.DiagnosticTrayMenu.Items[1].Text == "Exit", "Fresh installations use English; Turkish localization is covered separately.");
            ((CheckBox)window.FindName("BrightnessToggle")).IsChecked = true;
            window.SetBrightness(controlDevice.Read());
            window.Show();
            window.Navigate("Display");
            ((ScrollViewer)window.FindName("PreferencesScroll")).ScrollToTop();
            await Task.Delay(150);
            Render(window, Path.Combine(directory, "luma-brightness-settings.png"));
        }
    }

    private sealed class DiagnosticBrightnessDevice : IBrightnessDevice
    {
        private BrightnessState state = new(true, 40, "internal-a", Array.AsReadOnly(new[] { 0, 20, 40, 60, 80, 100 }), "Dahili ekran · örnek aygıt");
        public readonly ManualResetEventSlim ReadEntered = new();
        public readonly ManualResetEventSlim ReadReleased = new();
        public volatile bool BlockReads;
        public volatile bool FailWrites;
        public volatile bool Available = true;
        public int Writes;
        public int Value => Volatile.Read(ref state).Value;
        public event Action<BrightnessSignal>? Changed;
        public BrightnessState Read()
        {
            var captured = Volatile.Read(ref state);
            if (BlockReads) { ReadEntered.Set(); ReadReleased.Wait(4000); }
            return Available ? captured : BrightnessState.Unavailable();
        }
        public void Emit(string id, int value)
        {
            if (id == state.InstanceId) Volatile.Write(ref state, state with { Value = value });
            Changed?.Invoke(new(id, value));
        }
        public void SetSilently(int value) => Volatile.Write(ref state, state with { Value = value });
        public bool SetBrightness(int value)
        {
            Interlocked.Increment(ref Writes);
            if (FailWrites || !Available) return false;
            Emit(state.InstanceId, state.Snap(value)); return true;
        }
        public void Dispose() { ReadReleased.Set(); ReadEntered.Dispose(); ReadReleased.Dispose(); }
    }
}
