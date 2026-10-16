using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Luma.Services;

internal sealed record NativeOsdWindow(IntPtr Handle, string ClassName, string Title, string ProcessPath, uint Style, uint ExtendedStyle, Rect Bounds, uint Dpi, bool HasXamlBridge, bool Visible);

internal interface INativeOsdPlatform : IDisposable
{
    bool Install(Action<IntPtr> shown);
    IReadOnlyList<NativeOsdWindow> Enumerate();
    NativeOsdWindow? Inspect(IntPtr handle);
    bool Hide(IntPtr handle);
}

// Briefly dismiss the duplicate shell popup after a brightness event. No shell
// settings, styles, process memory, or persistent child-window state is changed.
internal sealed class NativeBrightnessOsdSuppressor : IDisposable
{
    private readonly StaWorker worker = new("Luma native brightness OSD");
    private readonly TaskCompletionSource<bool> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Func<long> clock;
    private INativeOsdPlatform? platform;
    private DispatcherTimer? retry;
    private long until;
    private volatile bool enabled;
    private volatile bool disposed;
    private int hiddenCount;
    internal Task<bool> Ready => ready.Task;
    internal int HiddenCount => Volatile.Read(ref hiddenCount);
    public event Action? CandidateShown;
    internal Task FlushAsync() => worker.RunAsync(() => { });

    public NativeBrightnessOsdSuppressor(Func<INativeOsdPlatform>? factory = null, Func<long>? clock = null)
    {
        this.clock = clock ?? (() => Environment.TickCount64);
        worker.Post(() =>
        {
            if (disposed) return;
            platform = factory is null ? new Win32OsdPlatform() : factory();
            var installed = platform.Install(OnShown);
            retry = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(40) };
            retry.Tick += (_, _) => { if (Armed) HideVisible(); else retry.Stop(); };
            ready.TrySetResult(installed);
        });
    }

    public void SetEnabled(bool value)
    {
        enabled = value;
        worker.Post(() => { if (!enabled) { until = 0; retry?.Stop(); } });
    }

    public void NotifyBrightnessActivity()
    {
        if (!enabled || disposed) return;
        worker.Post(() =>
        {
            if (!enabled || disposed) return;
            until = clock() + 900;
            HideVisible(); retry?.Start();
        }, DispatcherPriority.Send);
    }

    private bool Armed => !disposed && enabled && clock() < until;
    private void OnShown(IntPtr handle)
    {
        if (!enabled || disposed) return;
        worker.Post(() =>
        {
            if (!enabled || disposed || platform is null) return;
            var window = platform.Inspect(handle);
            if (window is null || !IsShellOsd(window)) return;
            CandidateShown?.Invoke();
            if (Armed) HideIfMatched(window);
        }, DispatcherPriority.Send);
    }
    private void HideVisible()
    {
        if (!Armed || platform is null) return;
        foreach (var window in platform.Enumerate()) HideIfMatched(window);
    }
    private void HideIfMatched(NativeOsdWindow window)
    {
        if (!Armed || platform is null || !IsShellOsd(window)) return;
        if (platform.Hide(window.Handle)) Interlocked.Increment(ref hiddenCount);
    }

    internal static bool IsShellOsd(NativeOsdWindow window)
    {
        const uint popup = 0x80000000, caption = 0x00C00000;
        const uint required = 0x08000000 | 0x80 | 0x08; // NOACTIVATE, TOOLWINDOW, TOPMOST.
        if (!window.Visible || window.ClassName != "XamlExplorerHostIslandWindow" || window.Title.Length != 0 || !window.HasXamlBridge
            || (window.Style & popup) == 0 || (window.Style & caption) != 0 || (window.ExtendedStyle & required) != required) return false;
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var explorer = Path.Combine(windows, "explorer.exe");
        var shell = Path.Combine(windows, "SystemApps", "ShellExperienceHost_cw5n1h2txyewy", "ShellExperienceHost.exe");
        var shellHost = Path.Combine(windows, "SystemApps", "MicrosoftWindows.Client.CBS_cw5n1h2txyewy", "ShellHost.exe");
        if (!window.ProcessPath.Equals(explorer, StringComparison.OrdinalIgnoreCase)
            && !window.ProcessPath.Equals(shell, StringComparison.OrdinalIgnoreCase)
            && !window.ProcessPath.Equals(shellHost, StringComparison.OrdinalIgnoreCase)) return false;
        var scale = (window.Dpi == 0 ? 96 : window.Dpi) / 96d;
        // Exclude Task View, the desktop, Start, and Quick Settings hosts.
        return window.Bounds.Width / scale is >= 120 and <= 720 && window.Bounds.Height / scale is >= 32 and <= 160;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; enabled = false; ready.TrySetCanceled();
        worker.Post(() => { retry?.Stop(); platform?.Dispose(); });
        worker.Dispose();
    }
}
