using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace Luma.Services;

internal sealed record BrightnessState(bool Available, int Value, string InstanceId, IReadOnlyList<int> Levels, string Status)
{
    public static BrightnessState Unavailable(string status = "Desteklenen dahili ekran bulunamadı.") => new(false, 0, "", Array.Empty<int>(), status);
    public int Snap(int value) => Levels.Count == 0 ? Math.Clamp(value, 0, 100)
        : Levels.OrderBy(level => Math.Abs(level - Math.Clamp(value, 0, 100))).ThenBy(level => level).First();
}

internal readonly record struct BrightnessSignal(string InstanceId, int Value);

internal interface IBrightnessDevice : IDisposable
{
    BrightnessState Read();
    bool SetBrightness(int value);
    event Action<BrightnessSignal>? Changed;
}

// All queries and writes belong to the brightness worker, never the UI thread.
internal sealed class WmiBrightnessDevice : IBrightnessDevice
{
    private readonly ManagementScope scope = new(@"\\.\root\WMI", new ConnectionOptions { Timeout = TimeSpan.FromSeconds(2) });
    private BrightnessState state = BrightnessState.Unavailable();
    private ManagementEventWatcher? watcher;
    private volatile bool watching;
    private volatile bool disposed;
    public event Action<BrightnessSignal>? Changed;

    public BrightnessState Read()
    {
        try
        {
            using var search = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM WmiMonitorBrightness WHERE Active = TRUE"), new EnumerationOptions { Timeout = TimeSpan.FromSeconds(2) });
            using var monitors = search.Get();
            BrightnessState? found = null;
            foreach (ManagementObject monitor in monitors)
            {
                using (monitor)
                {
                    var id = Convert.ToString(monitor["InstanceName"]) ?? "";
                    if (string.IsNullOrEmpty(id)) continue;
                    var levels = (monitor["Level"] as byte[] ?? Array.Empty<byte>()).Select(level => (int)level).Where(level => level <= 100).Distinct().Order().ToArray();
                    var candidate = new BrightnessState(true, Math.Clamp(Convert.ToInt32(monitor["CurrentBrightness"]), 0, 100), id, Array.AsReadOnly(levels), "Dahili ekran · değişiklikler anında uygulanır.");
                    // Keep the same internal panel while it remains active.
                    if (found is null || id.Equals(state.InstanceId, StringComparison.OrdinalIgnoreCase)) found = candidate;
                }
            }
            state = found ?? BrightnessState.Unavailable();
            if (state.Available) EnsureWatcher();
            else StopWatcher();
        }
        catch (Exception ex) when (IsDeviceError(ex))
        {
            state = BrightnessState.Unavailable();
            StopWatcher();
        }
        return state;
    }

    public bool SetBrightness(int value)
    {
        if (!state.Available || disposed) return false;
        try
        {
            using var search = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM WmiMonitorBrightnessMethods WHERE Active = TRUE"), new EnumerationOptions { Timeout = TimeSpan.FromSeconds(2) });
            using var methods = search.Get();
            foreach (ManagementObject method in methods)
            {
                using (method)
                {
                    if (!string.Equals(Convert.ToString(method["InstanceName"]), state.InstanceId, StringComparison.OrdinalIgnoreCase)) continue;
                    using var input = method.GetMethodParameters("WmiSetBrightness");
                    input["Timeout"] = 0u;
                    input["Brightness"] = (byte)state.Snap(value);
                    using var result = method.InvokeMethod("WmiSetBrightness", input, new InvokeMethodOptions { Timeout = TimeSpan.FromSeconds(2) });
                    return result is not null && Convert.ToUInt32(result["ReturnValue"]) == 0;
                }
            }
        }
        catch (Exception ex) when (IsDeviceError(ex)) { }
        return false;
    }

    private void EnsureWatcher()
    {
        if (watching || disposed) return;
        StopWatcher();
        try
        {
            watcher = new ManagementEventWatcher(scope, new WqlEventQuery("SELECT * FROM WmiMonitorBrightnessEvent"));
            watcher.EventArrived += OnBrightnessChanged;
            watcher.Stopped += OnWatcherStopped;
            watcher.Start();
            watching = true;
        }
        catch (Exception ex) when (IsDeviceError(ex)) { StopWatcher(); }
    }

    private void OnBrightnessChanged(object sender, EventArrivedEventArgs e)
    {
        using var data = e.NewEvent;
        if (disposed) return;
        try
        {
            var id = Convert.ToString(data["InstanceName"]) ?? "";
            Changed?.Invoke(new(id, Math.Clamp(Convert.ToInt32(data["Brightness"]), 0, 100)));
        }
        catch (Exception ex) when (IsDeviceError(ex)) { }
    }

    private void OnWatcherStopped(object sender, StoppedEventArgs e) => watching = false;
    private void StopWatcher()
    {
        var previous = watcher; watcher = null; watching = false;
        if (previous is null) return;
        previous.EventArrived -= OnBrightnessChanged;
        previous.Stopped -= OnWatcherStopped;
        try { previous.Stop(); } catch (Exception ex) when (IsDeviceError(ex)) { }
        previous.Dispose();
    }

    private static bool IsDeviceError(Exception ex) => ex is ManagementException or COMException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException or InvalidCastException or FormatException or OverflowException;
    public void Dispose() { disposed = true; StopWatcher(); }
}
