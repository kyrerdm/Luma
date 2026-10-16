using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Luma.Services;

internal readonly record struct BrightnessUpdate(BrightnessState State, long Sequence, bool WriteFailed = false);

internal sealed class BackgroundBrightnessService : IDisposable
{
    private readonly Dispatcher ui;
    private readonly StaWorker worker = new("Luma brightness");
    private readonly TaskCompletionSource<BrightnessState> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object stateGate = new();
    private BrightnessState snapshot = BrightnessState.Unavailable("Parlaklık desteği kontrol ediliyor…");
    private IBrightnessDevice? device;
    private DispatcherTimer? timer;
    private long sequence;
    private long appliedSequence;
    private long signalRevision;
    private volatile bool disposed;
    public BrightnessState State => Volatile.Read(ref snapshot);
    public Task<BrightnessState> Ready => ready.Task;
    public event Action<BrightnessUpdate>? Changed;
    internal Task FlushAsync() => worker.RunAsync(() => { });
    internal Task RefreshForDiagnosticsAsync() => worker.RunAsync(() => Refresh(false));

    public BackgroundBrightnessService(Dispatcher ui, Func<IBrightnessDevice>? factory = null)
    {
        this.ui = ui;
        SystemEvents.DisplaySettingsChanged += OnDisplaysChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        worker.Post(() =>
        {
            if (disposed) return;
            device = factory is null ? new WmiBrightnessDevice() : factory();
            device.Changed += OnDeviceChanged;
            Refresh(true);
            timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = State.Available ? TimeSpan.FromSeconds(2) : TimeSpan.FromSeconds(10) };
            timer.Tick += (_, _) => { Refresh(false); timer.Interval = State.Available ? TimeSpan.FromSeconds(2) : TimeSpan.FromSeconds(10); };
            timer.Start();
        });
    }

    public long SetBrightness(int value)
    {
        var request = Interlocked.Increment(ref sequence);
        worker.Post(() =>
        {
            if (disposed || device is null || request != Volatile.Read(ref sequence)) return;
            var success = State.Available && device.SetBrightness(State.Snap(value));
            appliedSequence = request;
            Refresh(true, !success);
        });
        return request;
    }

    private void OnDeviceChanged(BrightnessSignal signal)
    {
        // Hardware notifications must not wait behind a slow WMI query.
        lock (stateGate)
        {
            var previous = State;
            if (disposed || !previous.Available || !previous.InstanceId.Equals(signal.InstanceId, StringComparison.OrdinalIgnoreCase)) return;
            signalRevision++;
            Publish(previous with { Value = Math.Clamp(signal.Value, 0, 100) }, false);
        }
    }

    private void OnDisplaysChanged(object? sender, EventArgs e) => worker.Post(() => Refresh(false));
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Resume) worker.Post(() => Refresh(false)); }

    private void Refresh(bool force, bool writeFailed = false)
    {
        if (disposed || device is null) return;
        long revision;
        lock (stateGate) revision = signalRevision;
        var next = device.Read();
        lock (stateGate)
        {
            // A query started before a hardware event cannot roll it back.
            if (revision != signalRevision) next = State;
            Publish(next, force, writeFailed);
        }
    }

    private void Publish(BrightnessState next, bool force, bool writeFailed = false)
    {
        var previous = State;
        Volatile.Write(ref snapshot, next);
        if (!force && previous.Available == next.Available && previous.Value == next.Value && previous.InstanceId == next.InstanceId && previous.Status == next.Status && previous.Levels.SequenceEqual(next.Levels)) return;
        var update = new BrightnessUpdate(next, Volatile.Read(ref appliedSequence), writeFailed);
        ui.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (disposed) return;
            Changed?.Invoke(update);
            ready.TrySetResult(next);
        }));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaysChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        ready.TrySetCanceled();
        worker.Post(() => { timer?.Stop(); if (device is not null) { device.Changed -= OnDeviceChanged; device.Dispose(); } });
        worker.Dispose();
    }
}
