using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Threading;
using System.Diagnostics;

namespace Luma.Services;

public enum VolumeCommand { Down, Up, Mute }
public readonly record struct VolumeInput(VolumeCommand Command, long Timestamp);

public sealed class KeyboardService : IDisposable
{
    private readonly Dispatcher dispatcher;
    private readonly HookProcedure callback;
    private readonly StaWorker worker = new("Luma keyboard");
    private IntPtr hook;
    private readonly bool[] pressedLocks = new bool[3];
    private volatile bool handleVolume = true;
    private volatile bool showLocks = true;
    private volatile bool audioAvailable;
    private volatile bool disposed;
    public bool HandleVolume { get => handleVolume; set => handleVolume = value; }
    public bool ShowLocks { get => showLocks; set => showLocks = value; }
    public bool AudioAvailable { get => audioAvailable; set => audioAvailable = value; }
    public event Action<VolumeCommand>? VolumePressed;
    public event Action<VolumeInput>? VolumeInput;
    public event Action<string, bool>? LockPressed;

    public KeyboardService(Dispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
        callback = OnKeyboard;
        try
        {
            worker.RunAsync(() =>
            {
                hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
                if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            }).GetAwaiter().GetResult();
        }
        catch { worker.Dispose(); throw; }
    }

    private IntPtr OnKeyboard(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && !disposed)
        {
            var key = Marshal.ReadInt32(data);
            var down = message == (IntPtr)0x100 || message == (IntPtr)0x104;
            var up = message == (IntPtr)0x101 || message == (IntPtr)0x105;
            if (HandleVolume && AudioAvailable && key is >= 0xAD and <= 0xAF && (down || up))
            {
                if (down)
                {
                    var command = key == 0xAD ? VolumeCommand.Mute : key == 0xAE ? VolumeCommand.Down : VolumeCommand.Up;
                    var input = new VolumeInput(command, Stopwatch.GetTimestamp());
                    dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => { if (!disposed) { VolumeInput?.Invoke(input); VolumePressed?.Invoke(command); } }));
                }
                return (IntPtr)1;
            }
            var index = key == 0x14 ? 0 : key == 0x90 ? 1 : key == 0x91 ? 2 : -1;
            if (index >= 0)
            {
                if (ShowLocks && down && !pressedLocks[index])
                {
                    pressedLocks[index] = true;
                    var enabled = (GetKeyState(key) & 1) == 0; // Hook runs before Windows toggles the state.
                    var label = index == 0 ? "Caps Lock" : index == 1 ? "Num Lock" : "Scroll Lock";
                    dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => { if (!disposed) LockPressed?.Invoke(label, enabled); }));
                }
                if (up) pressedLocks[index] = false;
            }
        }
        return CallNextHookEx(hook, code, message, data);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        worker.Post(() => { if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; } });
        worker.Dispose();
    }
    private delegate IntPtr HookProcedure(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProcedure callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern short GetKeyState(int key);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
}
