using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Luma.Services;

public sealed record AudioOutput(string Id, string Name, bool Selected);
public sealed record AudioApp(string Id, string Name, string DeviceId, string[] SessionIds, int Volume, bool Muted);
public sealed record AudioRoutingState(AudioOutput[] Outputs, AudioApp[] Apps, string Status, bool Available = true)
{
    public static AudioRoutingState Empty => new(Array.Empty<AudioOutput>(), Array.Empty<AudioApp>(), "Ses çıkışları yükleniyor…", false);
}
public interface IAudioRoutingDevice : IDisposable
{
    AudioRoutingState ReadRouting();
    bool SelectOutput(string id);
    bool SetAppVolume(AudioApp app, int value);
    bool SetAppMute(AudioApp app, bool muted);
}

public sealed partial class AudioService
{
    public void InvalidateEndpoint() => nextEndpointCheck = 0;
    internal bool? VerifyCurrentOutputPolicy()
    {
        var original = Read();
        if (!original.Available) return null;
        IMMDevice? console = null;
        try
        {
            Marshal.ThrowExceptionForHR(enumerator!.GetDefaultAudioEndpoint(0, 0, out console));
            Marshal.ThrowExceptionForHR(console.GetId(out var id));
            if (id != original.DeviceId) return null; // Preserve distinct Windows roles.
            return SelectOutput(id) && Read() == original;
        }
        finally { Release(console); }
    }

    public AudioRoutingState ReadRouting()
    {
        var outputs = new List<AudioOutput>();
        IMMDeviceCollection? collection = null;
        try
        {
            InvalidateEndpoint();
            var state = Read();
            enumerator ??= (IMMDeviceEnumerator)new MMDeviceEnumerator();
            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(0, 1, out collection));
            Marshal.ThrowExceptionForHR(collection.GetCount(out var count));
            for (uint i = 0; i < count; i++)
            {
                IMMDevice? output = null;
                try
                {
                    Marshal.ThrowExceptionForHR(collection.Item(i, out output));
                    Marshal.ThrowExceptionForHR(output.GetId(out var id));
                    outputs.Add(new(id, ReadDeviceName(output), id == state.DeviceId));
                }
                catch (COMException) { }
                finally { Release(output); }
            }
            var sessions = ReadSessions();
            var apps = sessions.GroupBy(s => s.Group).Select(group => new AudioApp(group.Key, group.First().Name, state.DeviceId,
                group.Select(s => s.Id).Order(StringComparer.Ordinal).ToArray(), (int)Math.Round(group.Average(s => s.Volume)), group.All(s => s.Muted)))
                .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            return new(outputs.OrderByDescending(o => o.Selected).ThenBy(o => o.Name).ToArray(), apps,
                state.Available ? (apps.Length == 0 ? "Bu ses çıkışında açık bir uygulama oturumu yok." : "Seçili çıkıştaki uygulamalar") : "Kullanılabilir ses çıkışı bulunamadı.", state.Available);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException) { return new(outputs.ToArray(), Array.Empty<AudioApp>(), "Ses çıkışları okunamadı. Tekrar açarak deneyebilirsin.", false); }
        finally { Release(collection); }
    }

    public bool SelectOutput(string id)
    {
        if (!ReadRouting().Outputs.Any(o => o.Id == id)) return false;
        object? policy = null;
        try
        {
            policy = new PolicyConfigClient();
            var config = (IPolicyConfig)policy;
            // Leave the separate communications role (calls) unchanged.
            Marshal.ThrowExceptionForHR(config.SetDefaultEndpoint(id, 0));
            Marshal.ThrowExceptionForHR(config.SetDefaultEndpoint(id, 1));
            InvalidateEndpoint();
            return Read().DeviceId == id;
        }
        catch (COMException) { return false; }
        finally { Release(policy); }
    }

    public bool SetAppVolume(AudioApp app, int value) => ApplyToApp(app, volume =>
    {
        var context = Guid.Empty;
        Marshal.ThrowExceptionForHR(volume.SetMasterVolume(Math.Clamp(value, 0, 100) / 100f, ref context));
    });
    public bool SetAppMute(AudioApp app, bool muted) => ApplyToApp(app, volume =>
    {
        var context = Guid.Empty;
        Marshal.ThrowExceptionForHR(volume.SetMute(muted, ref context));
    });
    private bool ApplyToApp(AudioApp app, Action<ISimpleAudioVolume> apply)
    {
        InvalidateEndpoint();
        if (Read().DeviceId != app.DeviceId) return false;
        var matched = 0;
        try
        {
            VisitSessions((control, volume) =>
            {
                Marshal.ThrowExceptionForHR(control.GetSessionInstanceIdentifier(out var id));
                if (!app.SessionIds.Contains(id, StringComparer.Ordinal)) return;
                apply(volume); matched++;
            });
            return matched > 0 && matched == app.SessionIds.Length;
        }
        catch (COMException) { return false; }
    }
    private sealed record Session(string Id, string Group, string Name, int Volume, bool Muted);
    private List<Session> ReadSessions()
    {
        var result = new List<Session>();
        VisitSessions((control, volume) =>
        {
            if (control.GetState(out var state) < 0 || state == 2) return;
            Marshal.ThrowExceptionForHR(control.GetSessionInstanceIdentifier(out var id));
            Marshal.ThrowExceptionForHR(control.GetProcessId(out var pid));
            Marshal.ThrowExceptionForHR(volume.GetMasterVolume(out var level));
            Marshal.ThrowExceptionForHR(volume.GetMute(out var muted));
            var name = "Uygulama " + pid;
            var group = pid.ToString();
            if (control.IsSystemSoundsSession() == 0) { name = "Sistem sesleri"; group = "system"; }
            else
            {
                try { using var process = Process.GetProcessById((int)pid); name = process.ProcessName; group = name.ToLowerInvariant(); }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
            }
            name = name.Equals("chrome", StringComparison.OrdinalIgnoreCase) ? "Google Chrome" : name.Equals("msedge", StringComparison.OrdinalIgnoreCase) ? "Microsoft Edge" : name;
            result.Add(new(id, group, name, Math.Clamp((int)Math.Round(level * 100), 0, 100), muted));
        });
        return result;
    }
    private void VisitSessions(Action<IAudioSessionControl2, ISimpleAudioVolume> visit)
    {
        EnsureEndpoint();
        if (device is null) return;
        object? activated = null;
        IAudioSessionEnumerator? sessions = null;
        try
        {
            var iid = typeof(IAudioSessionManager2).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(ref iid, 23, IntPtr.Zero, out activated));
            var manager = (IAudioSessionManager2)activated;
            Marshal.ThrowExceptionForHR(manager.GetSessionEnumerator(out sessions));
            Marshal.ThrowExceptionForHR(sessions.GetCount(out var count));
            for (var i = 0; i < count; i++)
            {
                object? session = null;
                try
                {
                    Marshal.ThrowExceptionForHR(sessions.GetSession(i, out session));
                    visit((IAudioSessionControl2)session, (ISimpleAudioVolume)session);
                }
                catch (COMException) { }
                finally { Release(session); }
            }
        }
        finally { Release(sessions); Release(activated); }
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection { [PreserveSig] int GetCount(out uint count); [PreserveSig] int Item(uint index, out IMMDevice device); }
    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr guid, uint flags, out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr guid, uint flags, out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
    }
    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object session);
    }
    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
        [PreserveSig] int GetGroupingParam(out Guid group);
        [PreserveSig] int SetGroupingParam(ref Guid group, ref Guid context);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr notify);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr notify);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetProcessId(out uint pid);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }
    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float value, ref Guid context);
        [PreserveSig] int GetMasterVolume(out float value);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
    }
    // Windows has no public default-output setter. This isolated Windows policy
    // interface is compatibility-dependent; failures are reported to the user.
    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")] private class PolicyConfigClient { }
    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat(IntPtr id, IntPtr format);
        [PreserveSig] int GetDeviceFormat(IntPtr id, int defaults, IntPtr format);
        [PreserveSig] int ResetDeviceFormat(IntPtr id);
        [PreserveSig] int SetDeviceFormat(IntPtr id, IntPtr endpoint, IntPtr mix);
        [PreserveSig] int GetProcessingPeriod(IntPtr id, int defaults, IntPtr period, IntPtr minimum);
        [PreserveSig] int SetProcessingPeriod(IntPtr id, IntPtr period);
        [PreserveSig] int GetShareMode(IntPtr id, IntPtr mode);
        [PreserveSig] int SetShareMode(IntPtr id, IntPtr mode);
        [PreserveSig] int GetPropertyValue(IntPtr id, IntPtr key, IntPtr value);
        [PreserveSig] int SetPropertyValue(IntPtr id, IntPtr key, IntPtr value);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
        [PreserveSig] int SetEndpointVisibility(IntPtr id, int visible);
    }
}
