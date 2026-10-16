using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Luma.Services;

internal static class FullscreenDetector
{
    internal static bool Covers(System.Drawing.Rectangle window, System.Drawing.Rectangle monitor) => window.Left <= monitor.Left + 1 && window.Top <= monitor.Top + 1 && window.Right >= monitor.Right - 1 && window.Bottom >= monitor.Bottom - 1;
    public static bool IsTargetFullscreen(string target)
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero || IsIconic(handle)) return false;
        GetWindowThreadProcessId(handle, out var pid);
        if (pid == Environment.ProcessId) return false;
        var className = new StringBuilder(128); GetClassName(handle, className, className.Capacity);
        if (className.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        var cursor = System.Windows.Forms.Cursor.Position;
        var monitor = MonitorCatalog.Resolve(target, cursor.X, cursor.Y);
        if (MonitorFromWindow(handle, 2) != monitor) return false;
        var screen = System.Windows.Forms.Screen.FromHandle(handle).Bounds;
        if (DwmGetWindowAttribute(handle, 9, out var rect, Marshal.SizeOf<NativeRect>()) != 0 && !GetWindowRect(handle, out rect)) return false;
        return Covers(System.Drawing.Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom), screen);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int size);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out NativeRect rect, int size);
}
