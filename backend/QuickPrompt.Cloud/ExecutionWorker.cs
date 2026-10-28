using Npgsql;
using QuickPrompt.Accounts.Executions;

namespace QuickPrompt.Cloud;

/// <summary>Bounded dispatcher. Processing holds no database transaction; losing the lease cancels the handler.</summary>
public sealed class ExecutionWorker(ExecutionQueue queue, IAccountExecutionHandler handler, ILogger<ExecutionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        var active = new List<Task>();
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                active.RemoveAll(task => task.IsCompleted);
                if (active.Count < queue.Limits.GlobalRunning)
                {
                    ExecutionLease? lease = null;
                    try { lease = await queue.Claim(stopping); }
                    catch (Exception error) when (error is NpgsqlException or TimeoutException)
                    { logger.LogWarning("Execution dispatch unavailable: {Type}", error.GetType().Name); }
                    if (lease is not null) { active.Add(Process(lease, stopping)); continue; }
                }
                await Task.Delay(500, stopping);
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        finally { await Task.WhenAll(active); }
    }

    private async Task Process(ExecutionLease lease, CancellationToken stopping)
    {
        using var work = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        work.CancelAfter(TimeSpan.FromSeconds(queue.Limits.MaximumSeconds));
        using var monitor = CancellationTokenSource.CreateLinkedTokenSource(stopping, work.Token);
        var lost = 0;
        var heartbeat = Monitor();
        try
        {
            var initial = await queue.Heartbeat(lease, work.Token);
            if (!initial.Owned) { Interlocked.Exchange(ref lost, 1); return; }
            if (initial.CancelRequested) work.Cancel();
            work.Token.ThrowIfCancellationRequested();
            var pending = handler.Execute(lease, work.Token);
            // Stop awaiting a misbehaving asynchronous adapter at the deadline. A late value
            // has no continuation capable of committing it; external effects need their own fencing.
            _ = pending.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            var result = await pending.WaitAsync(work.Token);
            work.Token.ThrowIfCancellationRequested();
            if (Volatile.Read(ref lost) == 0) await queue.Finish(lease, result, cancellation: CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            if (Volatile.Read(ref lost) == 0) await RecordFailure(stopping.IsCancellationRequested ? "HOST_STOPPED" : "TIMEOUT", stopping.IsCancellationRequested);
        }
        catch (Exception error)
        {
            logger.LogWarning("Execution {ExecutionId} failed: {Type}", lease.Id, error.GetType().Name);
            if (Volatile.Read(ref lost) == 0) await RecordFailure("PROCESSING_FAILED", false);
        }
        finally
        {
            monitor.Cancel();
            try { await heartbeat; } catch (OperationCanceledException) when (monitor.IsCancellationRequested) { }
        }

        async Task RecordFailure(string code, bool interrupted)
        {
            try { await queue.Finish(lease, null, code, interrupted, CancellationToken.None); }
            catch (Exception error) { logger.LogWarning("Execution receipt {ExecutionId} unconfirmed: {Type}", lease.Id, error.GetType().Name); }
        }
        async Task Monitor()
        {
            try
            {
                while (!monitor.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(2, queue.Limits.LeaseSeconds / 3d)), monitor.Token);
                    var pulse = await queue.Heartbeat(lease, monitor.Token);
                    if (!pulse.Owned) { Interlocked.Exchange(ref lost, 1); work.Cancel(); return; }
                    if (pulse.CancelRequested) { work.Cancel(); return; }
                }
            }
            catch (OperationCanceledException) when (monitor.IsCancellationRequested) { }
            catch (Exception error)
            {
                logger.LogWarning("Execution lease {ExecutionId} lost: {Type}", lease.Id, error.GetType().Name);
                Interlocked.Exchange(ref lost, 1); work.Cancel();
            }
        }
    }
}
