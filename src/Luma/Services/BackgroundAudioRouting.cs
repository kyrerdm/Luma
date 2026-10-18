using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Luma.Services;

internal sealed class BackgroundAudioRouting : IDisposable
{
    private readonly StaWorker worker = new("Luma audio outputs and apps");
    private readonly Dispatcher ui;
    private readonly Func<IAudioRoutingDevice> factory;
    private IAudioRoutingDevice? device;
    private AudioRoutingState snapshot = AudioRoutingState.Empty;
    private volatile bool disposed;
    private long revision;
    private long generation;
    private readonly ConcurrentDictionary<string, long> pendingVolumes = new();
    public AudioRoutingState State => Volatile.Read(ref snapshot);
    public event Action<AudioRoutingState>? Changed;
    public BackgroundAudioRouting(Dispatcher ui, Func<IAudioRoutingDevice>? factory = null) { this.ui = ui; this.factory = factory ?? (() => new AudioService()); }
    public Task<bool> RefreshAsync() => Run(null);
    public Task<bool> SelectAsync(string id) => Run(d => d.SelectOutput(id));
    public Task<bool> SetVolumeAsync(AudioApp app, int value) => Run(d => d.SetAppVolume(app, value), app.DeviceId + "|" + app.Id);
    public Task<bool> SetMuteAsync(AudioApp app, bool muted) => Run(d => d.SetAppMute(app, muted));
    public void Reset()
    {
        Interlocked.Increment(ref generation);
        pendingVolumes.Clear();
        Volatile.Write(ref snapshot, AudioRoutingState.Empty);
        worker.Post(() => { device?.Dispose(); device = null; }, DispatcherPriority.Send);
    }
    private Task<bool> Run(Func<IAudioRoutingDevice, bool>? command, string? volumeKey = null)
    {
        if (disposed) return Task.FromResult(false);
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = Interlocked.Increment(ref revision);
        var currentGeneration = Volatile.Read(ref generation);
        if (volumeKey is not null) pendingVolumes[volumeKey] = request;
        worker.Post(() =>
        {
            if (disposed || currentGeneration != Volatile.Read(ref generation)) { ready.TrySetResult(false); return; }
            if (volumeKey is not null && pendingVolumes.TryGetValue(volumeKey, out var latest) && latest != request) { ready.TrySetResult(true); return; }
            try
            {
                device ??= factory();
                var success = command?.Invoke(device) ?? true;
                var next = device.ReadRouting();
                if (currentGeneration != Volatile.Read(ref generation)) { ready.TrySetResult(false); return; }
                Volatile.Write(ref snapshot, next);
                ui.BeginInvoke(new Action(() => { if (!disposed && request == Volatile.Read(ref revision)) Changed?.Invoke(next); }));
                ready.TrySetResult(success && next.Available);
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException or UnauthorizedAccessException or InvalidCastException)
            {
                ready.TrySetResult(false);
            }
        });
        return ready.Task;
    }
    public void Dispose() { disposed = true; worker.Post(() => device?.Dispose()); worker.Dispose(); }
}
