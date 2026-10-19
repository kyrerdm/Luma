using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Luma.Services;

internal sealed record DisplayInfo(string Id, string DeviceName, string Label, string Description, Rectangle Bounds, bool Primary);

internal static class MonitorCatalog
{
    private static DisplayInfo[] displays = Enumerate();
    public static DisplayInfo[] Current => Volatile.Read(ref displays);
    public static event Action? Changed;

    internal static string DisplayLabel(DisplayInfo display) => display.Label.StartsWith("Ekran ", StringComparison.Ordinal) ? Strings.T("Ekran") + display.Label[5..] : display.Label;
    internal static string DisplayDescription(DisplayInfo display)
    {
        var description = display.Description;
        const string primarySuffix = " · Ana ekran";
        if (display.Primary && description.EndsWith(primarySuffix, StringComparison.Ordinal)) description = description[..^primarySuffix.Length];
        if (description == display.Label) description = DisplayLabel(display);
        return description + (display.Primary ? " · " + Strings.T("Ana ekran") : "");
    }

    static MonitorCatalog() => SystemEvents.DisplaySettingsChanged += (_, _) => Refresh();

    public static void Refresh()
    {
        var next = Enumerate();
        if (Current.SequenceEqual(next)) return;
        Volatile.Write(ref displays, next);
        Changed?.Invoke();
    }

    private static DisplayInfo[] Enumerate() => Forms.Screen.AllScreens.Select((screen, index) =>
    {
        var device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
        var identified = EnumDisplayDevices(screen.DeviceName, 0, ref device, 1);
        var id = identified && !string.IsNullOrWhiteSpace(device.DeviceId) ? device.DeviceId : screen.DeviceName;
        var number = new string(screen.DeviceName.Where(char.IsDigit).ToArray());
        if (number.Length == 0) number = (index + 1).ToString();
        var label = $"Ekran {number} · {screen.Bounds.Width}×{screen.Bounds.Height}";
        return new DisplayInfo(id, screen.DeviceName, label, (identified ? device.Description : label) + (screen.Primary ? " · Ana ekran" : ""), screen.Bounds, screen.Primary);
    }).ToArray();

    internal static DisplayInfo? SelectTarget(string target, DisplayInfo[] available)
    {
        if (target == "Cursor") return null;
        if (target != "Primary")
        {
            var selected = available.FirstOrDefault(d => d.Id.Equals(target, StringComparison.OrdinalIgnoreCase) || d.DeviceName.Equals(target, StringComparison.OrdinalIgnoreCase));
            if (selected is not null) return selected;
        }
        return available.FirstOrDefault(d => d.Primary) ?? available.FirstOrDefault();
    }

    public static IntPtr Resolve(string target, int cursorX, int cursorY)
    {
        if (target == "Cursor") return MonitorFromPoint(new NativePoint { X = cursorX, Y = cursorY }, 2);
        var selected = SelectTarget(target, Current);
        return selected is null ? MonitorFromWindow(IntPtr.Zero, 1)
            : MonitorFromPoint(new NativePoint { X = selected.Bounds.Left + selected.Bounds.Width / 2, Y = selected.Bounds.Top + selected.Bounds.Height / 2 }, 1);
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool EnumDisplayDevices(string device, uint index, ref DisplayDevice result, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
}
