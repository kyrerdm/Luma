using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Luma.Services;

// UI-dispatcher owned; checks metadata only, never downloads or installs an update.
internal sealed class UpdateMonitor : IDisposable
{
    internal static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(15);
    internal static readonly TimeSpan PollInterval = TimeSpan.FromHours(6);
    private readonly UpdateService service;
    private readonly DispatcherTimer timer;
    private readonly CancellationTokenSource cancellation = new();
    private readonly bool packaged;
    private Task<UpdateResult>? inFlight;
    private bool disposed;
    private int failedChecks;
    public UpdateResult? Current { get; private set; }
    public event Action<UpdateResult>? Changed;
    public event Action<UpdateResult>? AvailableInBackground;
    internal bool AutomaticChecksEnabled => timer.IsEnabled;
    internal TimeSpan NextCheckDelay => timer.Interval;

    public UpdateMonitor(Dispatcher dispatcher, UpdateService? service = null, bool? packaged = null)
    {
        this.packaged = packaged ?? Distribution.IsPackaged;
        this.service = service ?? new UpdateService(packaged: this.packaged);
        timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = StartupDelay };
        timer.Tick += async (_, _) =>
        {
            timer.Interval = PollInterval;
            await CheckInBackgroundAsync();
        };
    }

    public void SetEnabled(bool enabled)
    {
        if (disposed) return;
        enabled &= !packaged;
        if (!enabled) { timer.Stop(); return; }
        if (timer.IsEnabled) return;
        failedChecks = 0;
        timer.Interval = StartupDelay;
        timer.Start();
    }

    public Task<UpdateResult> CheckAsync()
    {
        if (disposed || packaged) return Task.FromResult(new UpdateResult(false, Distribution.StoreUpdatesMessage, null));
        // A background timer and a manual click share one request, including slow/offline checks.
        if (inFlight is { IsCompleted: false }) return inFlight;
        return inFlight = CheckCoreAsync();
    }

    internal async Task CheckInBackgroundAsync()
    {
        if (disposed || !timer.IsEnabled) return;
        var result = await CheckAsync();
        if (!disposed && timer.IsEnabled && result is { Verified: true, Available: true }) AvailableInBackground?.Invoke(result);
    }

    private async Task<UpdateResult> CheckCoreAsync()
    {
        var result = await service.CheckAsync(cancellation.Token);
        if (disposed) return result;
        failedChecks = result.Verified ? 0 : Math.Min(failedChecks + 1, 4);
        if (timer.IsEnabled) timer.Interval = failedChecks switch
        {
            1 => TimeSpan.FromMinutes(1), 2 => TimeSpan.FromMinutes(5), 3 => TimeSpan.FromMinutes(15), _ => PollInterval
        };
        if (result.Verified)
        {
            Current = result;
            Changed?.Invoke(result);
        }
        // Network errors must not erase an update that was already verified.
        return result;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        timer.Stop();
        cancellation.Cancel();
        service.Dispose();
        cancellation.Dispose();
    }
}
