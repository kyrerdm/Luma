using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Luma.Services;

public sealed record ExternalBrightness(string Id, string Name, int Value, bool Available, string Status, bool WriteFailed = false);
internal interface IExternalBrightnessDevice : IDisposable
{
    ExternalBrightness[] Scan();
    ExternalBrightness? Set(string id, int value);
}
internal sealed class ExternalBrightnessDevice : IExternalBrightnessDevice
{
    private sealed record Monitor(string Id, string Name, IntPtr Handle, uint Minimum, uint Maximum, bool Vcp);
    private readonly List<IntPtr> handles = new();
    private readonly List<Monitor> monitors = new();
    public ExternalBrightness[] Scan()
    {
        Dispose();
        var result = new List<ExternalBrightness>();
        MonitorEnum callback = (IntPtr handle, IntPtr dc, IntPtr rect, IntPtr data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(handle, ref info)) return true;
            var display = MonitorCatalog.Current.FirstOrDefault(d => d.DeviceName == info.Device);
            if (display is null || !GetNumberOfPhysicalMonitorsFromHMONITOR(handle, out var count) || count is 0 or > 32) return true;
            var physical = new PhysicalMonitor[count];
            if (!GetPhysicalMonitorsFromHMONITOR(handle, count, physical)) return true;
            for (var index = 0; index < physical.Length; index++)
            {
                var monitor = physical[index]; handles.Add(monitor.Handle);
                var id = "ddc:" + display.Id + ":" + index;
                var name = display.Label + (physical.Length > 1 ? $" · {index + 1}" : "");
                var supported = GetMonitorBrightness(monitor.Handle, out var minimum, out var current, out var maximum) && maximum > minimum;
                var vcp = false;
                if (!supported)
                {
                    supported = GetVCPFeatureAndVCPFeatureReply(monitor.Handle, 0x10, out _, out current, out maximum) && maximum > 0 && current <= maximum;
                    minimum = 0; vcp = supported;
                }
                if (supported)
                {
                    monitors.Add(new(id, name, monitor.Handle, minimum, maximum, vcp));
                    result.Add(new(id, name, Percent(current, minimum, maximum), true, "DDC/CI · " + monitor.Description));
                }
                else result.Add(new(id, name, 0, false, "DDC/CI yanıtı alınamadı. Monitör menüsünü ve bağlantıyı kontrol et; dahili ekran için yukarıdaki kaydırıcıyı kullan."));
            }
            return true;
        };
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return result.ToArray();
    }
    internal static int Percent(uint value, uint minimum, uint maximum) => maximum <= minimum ? 0 : Math.Clamp((int)Math.Round(((double)value - minimum) * 100 / (maximum - minimum)), 0, 100);
    internal static uint Level(int percent, uint minimum, uint maximum) => minimum + (uint)Math.Round(Math.Clamp(percent, 0, 100) / 100d * (maximum - minimum));
    public ExternalBrightness? Set(string id, int value)
    {
        var monitor = monitors.FirstOrDefault(m => m.Id == id);
        if (monitor is null) return null;
        var target = Level(value, monitor.Minimum, monitor.Maximum);
        var success = monitor.Vcp ? SetVCPFeature(monitor.Handle, 0x10, target) : SetMonitorBrightness(monitor.Handle, target);
        uint current = 0;
        var read = monitor.Vcp ? GetVCPFeatureAndVCPFeatureReply(monitor.Handle, 0x10, out _, out current, out _) : GetMonitorBrightness(monitor.Handle, out _, out current, out _);
        return new(id, monitor.Name, read ? Percent(current, monitor.Minimum, monitor.Maximum) : Math.Clamp(value, 0, 100), read,
            success && read ? "DDC/CI · " + monitor.Name : "Parlaklık doğrulanamadı. Monitörü tekrar tara.", !success || !read);
    }
    public void Dispose() { foreach (var handle in handles) DestroyPhysicalMonitor(handle); handles.Clear(); monitors.Clear(); }
    private delegate bool MonitorEnum(IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct PhysicalMonitor { public IntPtr Handle; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description; }
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr rect, MonitorEnum callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("dxva2.dll")] private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);
    [DllImport("dxva2.dll", CharSet = CharSet.Unicode)] private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count, [Out] PhysicalMonitor[] physical);
    [DllImport("dxva2.dll")] private static extern bool GetMonitorBrightness(IntPtr monitor, out uint minimum, out uint current, out uint maximum);
    [DllImport("dxva2.dll")] private static extern bool SetMonitorBrightness(IntPtr monitor, uint value);
    [DllImport("dxva2.dll")] private static extern bool DestroyPhysicalMonitor(IntPtr monitor);
    [DllImport("dxva2.dll")] private static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr monitor, byte code, out uint type, out uint current, out uint maximum);
    [DllImport("dxva2.dll")] private static extern bool SetVCPFeature(IntPtr monitor, byte code, uint value);
}

internal sealed class BackgroundExternalBrightness : IDisposable
{
    private readonly StaWorker worker = new("Luma external monitor brightness");
    private readonly Func<IExternalBrightnessDevice> factory;
    private IExternalBrightnessDevice? device;
    private volatile bool disposed;
    private long revision;
    private long sequence;
    private readonly ConcurrentDictionary<string, long> pending = new();
    public BackgroundExternalBrightness(Func<IExternalBrightnessDevice>? factory = null) => this.factory = factory ?? (() => new ExternalBrightnessDevice());
    public Task<ExternalBrightness[]> ScanAsync() => Run(d => d.Scan());
    public Task<ExternalBrightness?> SetAsync(string id, int value)
    {
        var generation = System.Threading.Volatile.Read(ref revision);
        var request = System.Threading.Interlocked.Increment(ref sequence); pending[id] = request;
        return Run(d => generation != System.Threading.Volatile.Read(ref revision) || !pending.TryGetValue(id, out var latest) || latest != request ? null : d.Set(id, value));
    }
    private Task<T> Run<T>(Func<IExternalBrightnessDevice, T> action)
    {
        var ready = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (disposed) { ready.TrySetException(new ObjectDisposedException(nameof(BackgroundExternalBrightness))); return ready.Task; }
        worker.Post(() => { if (disposed) { ready.TrySetException(new ObjectDisposedException(nameof(BackgroundExternalBrightness))); return; } try { device ??= factory(); ready.TrySetResult(action(device)); } catch (Exception ex) { ready.TrySetException(ex); } });
        return ready.Task;
    }
    public void Suspend() { System.Threading.Interlocked.Increment(ref revision); pending.Clear(); worker.Post(() => { device?.Dispose(); device = null; }); }
    public void Dispose() { disposed = true; worker.Post(() => device?.Dispose()); worker.Dispose(); }
}
