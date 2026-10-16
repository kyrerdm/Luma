using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Luma.Services;

namespace Luma;

internal static partial class Diagnostics
{
    private static async Task VerifyNativeOsdAsync(Action<string, bool> check)
    {
        var sample = new NativeOsdWindow((IntPtr)7, "XamlExplorerHostIslandWindow", "", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), 0x84000000, 0x08200088, new Rect(854, 944, 211, 76), 120, true, true);
        check("The observed Windows brightness OSD is recognized", NativeBrightnessOsdSuppressor.IsShellOsd(sample));
        check("Task View and large shell surfaces are excluded", !NativeBrightnessOsdSuppressor.IsShellOsd(sample with { Bounds = new Rect(0, 0, 1920, 1020) }) && !NativeBrightnessOsdSuppressor.IsShellOsd(sample with { Title = "Task View" }));
        check("Unrelated applications and incomplete host signatures are excluded", !NativeBrightnessOsdSuppressor.IsShellOsd(sample with { ProcessPath = @"C:\Apps\explorer.exe" }) && !NativeBrightnessOsdSuppressor.IsShellOsd(sample with { HasXamlBridge = false }) && !NativeBrightnessOsdSuppressor.IsShellOsd(sample with { ExtendedStyle = 0x00200088 }));
        check("Native OSD recognition respects DPI and visibility", NativeBrightnessOsdSuppressor.IsShellOsd(sample with { Dpi = 240, Bounds = new Rect(0, 0, 532, 152) }) && !NativeBrightnessOsdSuppressor.IsShellOsd(sample with { Visible = false }));

        long now = 1000;
        var platform = new DiagnosticOsdPlatform(sample);
        using (var suppressor = new NativeBrightnessOsdSuppressor(() => platform, () => now))
        {
            await suppressor.Ready.WaitAsync(TimeSpan.FromSeconds(3));
            var candidates = 0;
            suppressor.CandidateShown += () => candidates++;
            suppressor.SetEnabled(true);
            platform.Reveal();
            await suppressor.FlushAsync();
            check("Shell popups request a fresh reading but stay visible without brightness activity", platform.Current.Visible && platform.Hides == 0 && candidates == 1);
            suppressor.NotifyBrightnessActivity();
            await suppressor.FlushAsync();
            check("Brightness activity dismisses the duplicate native OSD", !platform.Current.Visible && platform.Hides == 1);
            platform.Reveal();
            await suppressor.FlushAsync();
            check("A native OSD shown after the brightness event is dismissed", !platform.Current.Visible && platform.Hides == 2);
            now = 2100;
            platform.Reveal();
            await suppressor.FlushAsync();
            check("Unrelated later popups remain visible after suppression expires", platform.Current.Visible && platform.Hides == 2);
            suppressor.SetEnabled(false);
            suppressor.NotifyBrightnessActivity();
            platform.Reveal();
            await suppressor.FlushAsync();
            check("Disabling native suppression leaves Windows OSD available", platform.Current.Visible && platform.Hides == 2);
        }
        await platform.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        check("Native OSD observers are released on exit", platform.Callback is null);
        using var native = new NativeBrightnessOsdSuppressor();
        check("The native Windows show-event observer installs without hiding anything", await native.Ready.WaitAsync(TimeSpan.FromSeconds(3)) && native.HiddenCount == 0);
    }

    private sealed class DiagnosticOsdPlatform(NativeOsdWindow sample) : INativeOsdPlatform
    {
        public NativeOsdWindow Current = sample;
        public int Hides;
        public Action<IntPtr>? Callback;
        public readonly TaskCompletionSource Disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Install(Action<IntPtr> shown) { Callback = shown; return true; }
        public IReadOnlyList<NativeOsdWindow> Enumerate() => new[] { Current };
        public NativeOsdWindow? Inspect(IntPtr handle) => handle == Current.Handle ? Current : null;
        public bool Hide(IntPtr handle) { if (handle != Current.Handle || !Current.Visible) return false; Current = Current with { Visible = false }; Hides++; return true; }
        public void Reveal() { Current = Current with { Visible = true }; Callback?.Invoke(Current.Handle); }
        public void Dispose() { Callback = null; Disposed.TrySetResult(); }
    }

    // A separate diagnostic run observes real Fn input; it never sends keys or
    // writes brightness. It also runs before the normal single-instance guard.
    internal static async Task<int> WatchNativeBrightnessOsdAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "native-osd-live.json");
        using var suppression = new NativeBrightnessOsdSuppressor();
        using var brightness = new BackgroundBrightnessService(Application.Current.Dispatcher);
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = await brightness.Ready.WaitAsync(TimeSpan.FromSeconds(8));
        var hook = await suppression.Ready.WaitAsync(TimeSpan.FromSeconds(3));
        suppression.SetEnabled(active.Available);
        if (Environment.GetEnvironmentVariable("LUMA_VERIFY_NATIVE_OSD_SHOW") == "1")
        {
            using var probe = new Win32OsdPlatform();
            var idle = probe.FindIdleOsdForDiagnostics();
            if (idle is null) { File.WriteAllText(file, "{\"phase\":\"no-known-idle-shell-osd\",\"passed\":false}"); return 1; }
            var controlledHidden = false;
            var availableAfterDisabling = false;
            try
            {
                suppression.NotifyBrightnessActivity();
                await suppression.FlushAsync();
                probe.ShowForDiagnostics(idle.Handle);
                await Task.Delay(300);
                controlledHidden = suppression.HiddenCount > 0 && !probe.VisibleForDiagnostics(idle.Handle);
                suppression.SetEnabled(false);
                await suppression.FlushAsync();
                probe.ShowForDiagnostics(idle.Handle);
                await Task.Delay(200);
                availableAfterDisabling = probe.VisibleForDiagnostics(idle.Handle);
            }
            finally { probe.Hide(idle.Handle); }
            File.WriteAllText(file, JsonSerializer.Serialize(new { phase = "controlled-native-window-check", hook, hidden = controlledHidden, availableAfterDisabling, nativeHideRequests = suppression.HiddenCount, passed = controlledHidden && availableAfterDisabling }, new JsonSerializerOptions { WriteIndented = true }));
            return controlledHidden && availableAfterDisabling ? 0 : 1;
        }
        var activityCount = 0;
        var candidateCount = 0;
        var previousValue = active.Value;
        suppression.CandidateShown += () => { candidateCount++; brightness.RequestRefresh(); };
        brightness.Activity += suppression.NotifyBrightnessActivity;
        brightness.Changed += async update =>
        {
            var changed = update.State.Available && update.State.Value != previousValue;
            previousValue = update.State.Value;
            if (!update.HardwareActivity && !changed) return;
            activityCount++;
            await Task.Delay(200);
            if (suppression.HiddenCount > 0) observed.TrySetResult();
        };
        File.WriteAllText(file, JsonSerializer.Serialize(new { phase = "waiting-for-brightness-key", supported = active.Available, hook }));
        try { await observed.Task.WaitAsync(TimeSpan.FromMinutes(8)); }
        catch (TimeoutException) { }
        await Task.Delay(1200);
        await suppression.FlushAsync();
        var hidden = suppression.HiddenCount;
        suppression.SetEnabled(false);
        File.WriteAllText(file, JsonSerializer.Serialize(new { phase = "complete", supported = active.Available, hook, candidateWindows = candidateCount, brightnessEvents = activityCount, nativeHideRequests = hidden, passed = hidden > 0 }, new JsonSerializerOptions { WriteIndented = true }));
        return hidden > 0 ? 0 : 1;
    }
}
