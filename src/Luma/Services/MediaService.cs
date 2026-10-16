using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Luma.Models;

namespace Luma.Services;

public enum MediaCommand { TogglePlayback, Previous, Next }

public sealed class MediaService : IDisposable
{
    private readonly Dispatcher dispatcher;
    private readonly StaWorker worker = new("Luma media");
    private readonly Func<IMediaSessionProvider> providerFactory;
    private IMediaSessionProvider? provider;
    private IReadOnlyList<IMediaSession> sessions = Array.Empty<IMediaSession>();
    private IMediaSession? selected;
    private string? manualSourceId;
    private int selectionRevision;
    private DispatcherTimer? timer;
    private TaskCompletionSource? refreshCompletion;
    private bool pending;
    private volatile bool disposed;
    private MediaState state = MediaState.Empty;
    public MediaState State => Volatile.Read(ref state);
    public string? Error { get; private set; }
    public event Action<MediaState>? Changed;

    public MediaService(Dispatcher dispatcher) : this(dispatcher, () => new WindowsMediaSessionProvider()) { }
    internal MediaService(Dispatcher dispatcher, Func<IMediaSessionProvider> providerFactory)
    {
        this.dispatcher = dispatcher;
        this.providerFactory = providerFactory;
    }

    public Task StartAsync() => worker.RunAsync(StartCoreAsync);
    private async Task StartCoreAsync()
    {
        try
        {
            provider = providerFactory();
            await provider.InitializeAsync();
            if (disposed) return;
            provider.Changed += QueueRefresh;
            await RefreshAsync();
            if (disposed) return;
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) => QueueRefresh();
            timer.Start();
        }
        catch (Exception ex) when (IsMediaError(ex)) { Error = ex.Message; Publish(MediaState.Empty); }
    }

    private void QueueRefresh()
    {
        if (!disposed) worker.Post(async () => await RefreshAsync());
    }

    internal Task RefreshForDiagnosticsAsync() => worker.RunAsync(RefreshAsync);

    private Task RefreshAsync()
    {
        if (disposed || provider is null) return Task.CompletedTask;
        if (refreshCompletion is not null) { pending = true; return refreshCompletion.Task; }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        refreshCompletion = completion;
        _ = RefreshCoreAsync(completion);
        return completion.Task;
    }

    private async Task RefreshCoreAsync(TaskCompletionSource completion)
    {
        try
        {
            do
            {
                pending = false;
                var revision = selectionRevision;
                var available = provider!.GetSessions();
                foreach (var removed in sessions.Except(available)) removed.Changed -= QueueRefresh;
                foreach (var added in available.Except(sessions)) added.Changed += QueueRefresh;
                sessions = available;
                var states = new List<MediaState>();
                foreach (var entry in available)
                {
                    try
                    {
                        var snapshot = await entry.ReadAsync(false);
                        if (disposed) return;
                        if (snapshot.Available) states.Add(snapshot);
                    }
                    catch (Exception ex) when (IsMediaError(ex)) { }
                }
                if (revision != selectionRevision) { pending = true; continue; }
                if (manualSourceId is not null && states.All(s => s.SessionId != manualSourceId)) manualSourceId = null;
                var target = ChooseSource(states, manualSourceId, provider.CurrentSessionId);
                selected = available.FirstOrDefault(s => s.Id == target?.SessionId);
                if (target is null || selected is null) { Publish(MediaState.Empty); continue; }
                try { target = await selected.ReadAsync(true); }
                catch (Exception ex) when (IsMediaError(ex)) { target = target with { Artwork = null }; }
                if (disposed) return;
                if (revision != selectionRevision) { pending = true; continue; }
                var sources = Array.AsReadOnly(states.Select(s => new MediaSourceInfo(s.SessionId, s.AppName, s.Title, s.Artist, s.IsPlaying)).ToArray());
                Publish(target with { Sources = sources, AutomaticSource = manualSourceId is null });
                Error = null;
            } while (pending && !disposed);
        }
        catch (Exception ex) when (IsMediaError(ex))
        {
            Error = ex.Message; selected = null;
            Publish(MediaState.Empty);
        }
        catch (Exception ex) { completion.TrySetException(ex); }
        finally { refreshCompletion = null; completion.TrySetResult(); }
    }

    internal static MediaState? ChooseSource(IReadOnlyList<MediaState> available, string? manual, string? current)
    {
        if (manual is not null && available.FirstOrDefault(s => s.SessionId == manual) is { } chosen) return chosen;
        var active = available.FirstOrDefault(s => s.SessionId == current);
        return active is { IsPlaying: true } ? active : available.FirstOrDefault(s => s.IsPlaying) ?? active ?? available.FirstOrDefault();
    }

    public async Task<bool> SelectSourceAsync(string? id)
    {
        if (disposed) return false;
        try
        {
            return await worker.RunAsync(async () =>
            {
                if (disposed || (id is not null && sessions.All(s => s.Id != id))) return false;
                manualSourceId = id; selectionRevision++;
                await RefreshAsync();
                return !disposed && (id is null ? State.AutomaticSource : State.SessionId == id);
            });
        }
        catch (Exception ex) when (ex is OperationCanceledException || IsMediaError(ex)) { return false; }
    }

    public async Task<bool> ExecuteAsync(MediaCommand command, string? expectedSourceId = null)
    {
        if (disposed) return false;
        try
        {
            return await worker.RunAsync(async () =>
            {
                var target = selected;
                if (disposed || target is null || (expectedSourceId is not null && expectedSourceId != target.Id)) return false;
                var result = await target.ExecuteAsync(command);
                QueueRefresh();
                return result;
            });
        }
        catch (Exception ex) when (ex is OperationCanceledException || IsMediaError(ex)) { QueueRefresh(); return false; }
    }

    public async Task<bool> SeekAsync(TimeSpan position, string? expectedSourceId = null)
    {
        if (disposed) return false;
        try
        {
            return await worker.RunAsync(async () =>
            {
                var target = selected;
                if (disposed || target is null || !State.CanSeek || (expectedSourceId is not null && expectedSourceId != target.Id)) return false;
                var result = await target.SeekAsync(position);
                QueueRefresh();
                return result;
            });
        }
        catch (Exception ex) when (ex is OperationCanceledException || IsMediaError(ex)) { QueueRefresh(); return false; }
    }

    private void Publish(MediaState next)
    {
        if (disposed) return;
        Volatile.Write(ref state, next);
        // A queued callback must not repaint an earlier selection over a newer one.
        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { if (!disposed) Changed?.Invoke(State); }));
    }

    internal static bool IsMediaError(Exception ex) => ex is COMException or UnauthorizedAccessException or InvalidOperationException or ArgumentException;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        worker.Post(() =>
        {
            timer?.Stop();
            foreach (var session in sessions) session.Changed -= QueueRefresh;
            if (provider is not null) { provider.Changed -= QueueRefresh; provider.Dispose(); }
        });
        worker.Dispose();
    }
}
