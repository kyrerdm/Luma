using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Luma.Services;

public interface IAudioDevice : IDisposable
{
    AudioState Read();
    bool SetVolume(int volume);
    bool ToggleMute();
}

public readonly record struct AudioUpdate(AudioState State, long Sequence);

public sealed class BackgroundAudioService : IDisposable
{
    private sealed record Snapshot(AudioState State);
    private readonly Dispatcher ui;
    private readonly StaWorker worker = new("Luma audio");
    private readonly TaskCompletionSource<AudioState> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Snapshot snapshot = new(new(false, 0, false, "Ses aygıtı bekleniyor"));
    private IAudioDevice? device;
    private DispatcherTimer? timer;
    private long sequence;
    private long appliedSequence;
    private volatile bool disposed;
    public AudioState State => Volatile.Read(ref snapshot).State;
    public Task<AudioState> Ready => ready.Task;
    internal Task FlushAsync() => worker.RunAsync(() => { });
    public event Action<AudioUpdate>? Changed;

    public BackgroundAudioService(Dispatcher ui, Func<IAudioDevice>? factory = null)
    {
        this.ui = ui;
        worker.Post(() =>
        {
            if (disposed) return;
            device = factory is null ? new AudioService() : factory();
            Refresh(true);
            timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += (_, _) => Refresh(false);
            timer.Start();
        });
    }

    public long Adjust(VolumeCommand command, int step) => Enqueue(() =>
    {
        if (command == VolumeCommand.Mute) device!.ToggleMute();
        else device!.SetVolume(State.Volume + (command == VolumeCommand.Up ? step : -step));
    });

    public long SetVolume(int value) => Enqueue(() => device!.SetVolume(value));

    private long Enqueue(Action apply)
    {
        var request = Interlocked.Increment(ref sequence);
        worker.Post(() =>
        {
            if (disposed || device is null) return;
            apply();
            appliedSequence = request;
            Refresh(true);
        });
        return request;
    }

    private void Refresh(bool force)
    {
        if (disposed || device is null) return;
        var next = device.Read();
        var previous = State;
        Volatile.Write(ref snapshot, new Snapshot(next));
        if (!force && previous == next) return;
        var update = new AudioUpdate(next, appliedSequence);
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
        ready.TrySetCanceled();
        worker.Post(() => { timer?.Stop(); device?.Dispose(); });
        worker.Dispose();
    }
}
