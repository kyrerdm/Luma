using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Luma.Services;

// Owns COM objects and a message pump without occupying the UI dispatcher.
internal sealed class StaWorker : IDisposable
{
    private readonly Thread thread;
    private readonly Dispatcher dispatcher;
    private int disposed;

    public StaWorker(string name)
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        thread = new Thread(() =>
        {
            var current = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(current));
            ready.SetResult(current);
            Dispatcher.Run();
        }) { IsBackground = true, Name = name };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        dispatcher = ready.Task.GetAwaiter().GetResult();
    }

    public void Post(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (Volatile.Read(ref disposed) == 0 && !dispatcher.HasShutdownStarted) dispatcher.BeginInvoke(priority, action);
    }

    public Task RunAsync(Action action) => dispatcher.InvokeAsync(action).Task;
    public Task RunAsync(Func<Task> action) => dispatcher.InvokeAsync(action).Task.Unwrap();
    public Task<T> RunAsync<T>(Func<Task<T>> action) => dispatcher.InvokeAsync(action).Task.Unwrap();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        // Previously posted cleanup runs before shutdown. Never join a driver-blocked thread on the UI.
        dispatcher.BeginInvokeShutdown(DispatcherPriority.ContextIdle);
    }
}
