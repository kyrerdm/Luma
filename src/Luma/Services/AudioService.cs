using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Luma.Services;

public readonly record struct AudioState(bool Available, int Volume, bool Muted, string DeviceName);

// Calls belong to the audio worker STA. Slow device discovery must never block the UI.
public sealed class AudioService : IAudioDevice
{
    private IMMDeviceEnumerator? enumerator;
    private IMMDevice? device;
    private IAudioEndpointVolume? endpoint;
    private string? deviceId;
    private string deviceName = "Varsayılan ses aygıtı";
    private long nextEndpointCheck;

    public AudioState Read()
    {
        try
        {
            EnsureEndpoint();
            Marshal.ThrowExceptionForHR(endpoint!.GetMasterVolumeLevelScalar(out var level));
            Marshal.ThrowExceptionForHR(endpoint.GetMute(out var muted));
            return new(true, (int)Math.Round(level * 100), muted, deviceName);
        }
        catch (COMException)
        {
            ReleaseEndpoint();
            return new(false, 0, false, "Ses aygıtı bulunamadı");
        }
    }

    private void EnsureEndpoint()
    {
        // Reuse the endpoint for reads and writes. Periodic discovery still handles device switches.
        if (endpoint is not null && Stopwatch.GetTimestamp() < nextEndpointCheck) return;
        enumerator ??= (IMMDeviceEnumerator)new MMDeviceEnumerator();
        Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0, 1, out var current));
        try
        {
            Marshal.ThrowExceptionForHR(current.GetId(out var currentId));
            if (endpoint is null || currentId != deviceId)
            {
                ReleaseEndpoint();
                device = current;
                current = null!;
                deviceId = currentId;
                var iid = typeof(IAudioEndpointVolume).GUID;
                Marshal.ThrowExceptionForHR(device.Activate(ref iid, 23, IntPtr.Zero, out var activated));
                endpoint = (IAudioEndpointVolume)activated;
                deviceName = ReadDeviceName(device);
            }
        }
        finally { Release(current); }
        nextEndpointCheck = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
    }

    public bool SetVolume(int volume)
    {
        try
        {
            EnsureEndpoint();
            var context = Guid.Empty;
            Marshal.ThrowExceptionForHR(endpoint!.SetMasterVolumeLevelScalar(Math.Clamp(volume, 0, 100) / 100f, ref context));
            Marshal.ThrowExceptionForHR(endpoint.SetMute(false, ref context));
            return true;
        }
        catch (COMException) { ReleaseEndpoint(); return false; }
    }

    public bool ToggleMute()
    {
        try
        {
            EnsureEndpoint();
            Marshal.ThrowExceptionForHR(endpoint!.GetMute(out var muted));
            var context = Guid.Empty;
            Marshal.ThrowExceptionForHR(endpoint.SetMute(!muted, ref context));
            return true;
        }
        catch (COMException) { ReleaseEndpoint(); return false; }
    }

    private static string ReadDeviceName(IMMDevice target)
    {
        IPropertyStore? properties = null;
        var value = new PropVariant();
        try
        {
            Marshal.ThrowExceptionForHR(target.OpenPropertyStore(0, out properties));
            var key = new PropertyKey { FormatId = new("a45c254e-df1c-4efd-8020-67d146a850e0"), PropertyId = 14 };
            Marshal.ThrowExceptionForHR(properties.GetValue(ref key, out value));
            return value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) ?? "Ses çıkışı" : "Ses çıkışı";
        }
        catch (COMException) { return "Ses çıkışı"; }
        finally { PropVariantClear(ref value); Release(properties); }
    }

    private void ReleaseEndpoint()
    {
        Release(endpoint); endpoint = null;
        Release(device); device = null; deviceId = null;
    }
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    public void Dispose() { ReleaseEndpoint(); Release(enumerator); enumerator = null; }

    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);
    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { public Guid FormatId; public uint PropertyId; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] private struct PropVariant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] private class MMDeviceEnumerator { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr callback);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr callback);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr activation, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        [PreserveSig] int GetVolumeStepInfo(out uint step, out uint count);
        [PreserveSig] int VolumeStepUp(ref Guid context);
        [PreserveSig] int VolumeStepDown(ref Guid context);
        [PreserveSig] int QueryHardwareSupport(out uint mask);
        [PreserveSig] int GetVolumeRange(out float min, out float max, out float increment);
    }
}
