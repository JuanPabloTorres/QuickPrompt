using System.Diagnostics.Metrics;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Cloud;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class AccountOperationalSignalsTests
{
    [Fact]
    public void SignalsHaveFiniteLabelsAndNeverLogPrivateIdentifiersOrExceptionMessages()
    {
        const string privateMarker = "SYNTHETIC_PRIVATE_PAYLOAD_DO_NOT_LOG";
        var logger = new OperationalTestLog();
        var measurements = new List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        var testThread = Environment.CurrentManagedThreadId;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, source) =>
        {
            if (instrument.Meter.Name == AccountOperations.MeterName) source.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            // A prior host can finish disposal on another thread during a full integration run.
            if (Environment.CurrentManagedThreadId == testThread) measurements.Add((instrument.Name, value, tags.ToArray()));
        });
        listener.Start();

        AccountOperations.ReadinessUnavailable(logger);
        AccountOperations.QuotaRejected(logger, "OWNER_QUEUE_FULL");
        AccountOperations.QuotaRejected(logger, privateMarker);
        AccountOperations.SubmissionAccepted(logger, "CompilePrompt", ExecutionStatus.Queued);
        AccountOperations.SubmissionAccepted(logger, privateMarker, ExecutionStatus.Completed);
        AccountOperations.ExecutionFinished(logger, "COMPLETED", true);
        AccountOperations.ExecutionFinished(logger, privateMarker, false);
        AccountOperations.WorkerFailed(logger, "processing", new InvalidOperationException(privateMarker));
        AccountOperations.WorkerFailed(logger, privateMarker, new InvalidOperationException(privateMarker));

        Assert.Equal(9, measurements.Count);
        Assert.All(measurements, item =>
        {
            Assert.Equal(1, item.Value);
            Assert.Single(item.Tags);
            Assert.Contains(item.Tags[0].Key, new[] { "dependency", "reason", "kind", "outcome", "stage" });
            Assert.DoesNotContain(privateMarker, item.Tags[0].Value?.ToString());
        });
        Assert.Equal(new[] { 5330, 5331, 5332, 5333, 5334 }, logger.Events.Select(item => item.Id).Distinct().Order());
        Assert.Contains(measurements, item => item.Name == "quickprompt.execution.quota_rejected" && item.Tags[0].Value?.ToString() == "UNKNOWN");
        Assert.Contains(measurements, item => item.Name == "quickprompt.execution.outcomes" && item.Tags[0].Value?.ToString() == "UNCONFIRMED");
        Assert.All(logger.Events, item => { Assert.DoesNotContain(privateMarker, item.Message); Assert.Null(item.Exception); });
    }

}

/// <summary>Captures operational signal assertions in memory; no user context is written to evidence.</summary>
internal sealed class OperationalTestLog : ILoggerProvider, ILogger
{
    internal ConcurrentQueue<(int Id, string Message, Exception? Exception)> Events { get; } = new();
    public ILogger CreateLogger(string categoryName) => this;
    public bool IsEnabled(LogLevel level) => true;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public void Dispose() { }
    public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Events.Enqueue((eventId.Id, formatter(state, exception), exception));
}
