using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace Luma.Services;

internal sealed class Win32OsdPlatform : INativeOsdPlatform
{
    private const string HostClass = "XamlExplorerHostIslandWindow";
    private readonly WinEventProc callback;
    private Action<IntPtr>? shown;
    private IntPtr hook;
    private GCHandle callbackRoot;

    public Win32OsdPlatform() => callback = OnWinEvent;
    public bool Install(Action<IntPtr> shown)
    {
        this.shown = shown;
        callbackRoot = GCHandle.Alloc(callback);
        hook = SetWinEventHook(0x8002, 0x8002, IntPtr.Zero, callback, 0, 0, 2); // SHOW, OUTOFCONTEXT | SKIPOWNPROCESS.
        return hook != IntPtr.Zero;
    }
    private void OnWinEvent(IntPtr source, uint type, IntPtr handle, int objectId, int childId, uint thread, uint time)
    {
        if (handle == IntPtr.Zero || objectId != 0 || childId != 0) return;
        var root = GetAncestor(handle, 2);
        if (root == IntPtr.Zero) root = handle;
        if (ClassName(root) == HostClass) shown?.Invoke(root);
    }

    public IReadOnlyList<NativeOsdWindow> Enumerate()
    {
        var result = new List<NativeOsdWindow>();
        var handle = IntPtr.Zero;
        while ((handle = FindWindowEx(IntPtr.Zero, handle, HostClass, null)) != IntPtr.Zero && result.Count < 64)
        {
            if (!IsWindowVisible(handle)) continue;
            var window = Inspect(handle);
            if (window is not null) result.Add(window);
        }
        return result;
    }

    public NativeOsdWindow? Inspect(IntPtr handle) => InspectCore(handle, true);
    private NativeOsdWindow? InspectCore(IntPtr handle, bool visibleOnly)
    {
        if (!IsWindow(handle) || ClassName(handle) != HostClass || (visibleOnly && !IsWindowVisible(handle)) || !GetWindowRect(handle, out var rect)) return null;
        GetWindowThreadProcessId(handle, out var id);
        string path;
        try
        {
            using var process = Process.GetProcessById((int)id);
            path = process.MainModule?.FileName ?? "";
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException) { return null; }
        var title = new StringBuilder(256); GetWindowText(handle, title, title.Capacity);
        var bridge = FindWindowEx(handle, IntPtr.Zero, "Windows.UI.Composition.DesktopWindowContentBridge", "DesktopWindowXamlSource");
        return new(handle, HostClass, title.ToString(), path, unchecked((uint)GetWindowLongPtr(handle, -16).ToInt64()), unchecked((uint)GetWindowLongPtr(handle, -20).ToInt64()),
            new Rect(rect.Left, rect.Top, Math.Max(0, rect.Right - rect.Left), Math.Max(0, rect.Bottom - rect.Top)), GetDpiForWindow(handle), bridge != IntPtr.Zero, IsWindowVisible(handle));
    }

    internal NativeOsdWindow? FindIdleOsdForDiagnostics()
    {
        var handle = IntPtr.Zero;
        while ((handle = FindWindowEx(IntPtr.Zero, handle, HostClass, null)) != IntPtr.Zero)
        {
            var window = InspectCore(handle, false);
            if (window is not null && !window.Visible && NativeBrightnessOsdSuppressor.IsShellOsd(window with { Visible = true })) return window;
        }
        return null;
    }
    internal bool ShowForDiagnostics(IntPtr handle)
    {
        var window = InspectCore(handle, false);
        return window is not null && NativeBrightnessOsdSuppressor.IsShellOsd(window with { Visible = true }) && ShowWindowAsync(handle, 8);
    }
    internal bool VisibleForDiagnostics(IntPtr handle) => IsWindowVisible(handle);

    public bool Hide(IntPtr handle)
    {
        // Revalidate before changing a foreign window; handles may be recycled.
        var current = Inspect(handle);
        return current is not null && NativeBrightnessOsdSuppressor.IsShellOsd(current) && ShowWindowAsync(handle, 0);
    }
    private static string ClassName(IntPtr handle) { var text = new StringBuilder(128); GetClassName(handle, text, text.Capacity); return text.ToString(); }
    public void Dispose()
    {
        if (hook != IntPtr.Zero) { UnhookWinEvent(hook); hook = IntPtr.Zero; }
        if (callbackRoot.IsAllocated) callbackRoot.Free();
        shown = null;
    }

    private delegate void WinEventProc(IntPtr hook, uint type, IntPtr window, int objectId, int childId, uint thread, uint time);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int capacity);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint id);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window, int command);
}
