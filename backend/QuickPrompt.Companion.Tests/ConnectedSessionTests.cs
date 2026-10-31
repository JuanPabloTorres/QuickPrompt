using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Companion;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Companion.Tests;

/// <summary>In-process runner, synthetic HTTP and controlled time only. No worker process, OAuth, real credential, provider or server starts.</summary>
public sealed class ConnectedSessionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CoreOutputLimitBecomesOneSafeReceiptWithoutRetryOrLateAnswer(bool connected)
    {
        await using var fixture = new Fixture { Jobs = 2 };
        var id = Guid.NewGuid().ToString("N");
        var request = AdaptiveCoreModelTransport.CreateRequest(JsonSerializer.SerializeToElement(new {
            model = "qwen3:8b", messages = Array.Empty<object>(), options = new { max_requests = 1 }
        }), id);
        var reply = JsonSerializer.SerializeToElement(new {
            version = AdaptiveCoreModelTransport.Protocol, requestId = id, status = "failed", output = "SYNTHETIC_PRIVATE_PARTIAL_OUTPUT",
            usage = new { requests = 1, inputTokens = 19, outputTokens = 700 }, error = "MODEL_OUTPUT_LIMIT"
        });
        var model = new Model(_ => Task.FromResult(AdaptiveCoreModelTransport.Decode(reply, request, 2)));
        var entries = new List<CompanionRunDiagnostic>();
        var runner = new CompanionRunner(fixture.Connection, model, new(LocalWorkProtocol.Profile, entries.Add));
        if (connected) await Assert.ThrowsAsync<InvalidOperationException>(() => runner.StayConnected(default));
        else Assert.Equal(CompanionRunEnd.JobLimit, await runner.Run(1, default));
        var receipt = Assert.Single(fixture.Receipts);
        Assert.Equal("MODEL_OUTPUT_LIMIT", receipt.GetProperty("errorCode").GetString());
        Assert.Equal(JsonValueKind.Null, receipt.GetProperty("response").ValueKind);
        Assert.Equal("MODEL_OUTPUT_LIMIT", entries.Last().LastWorkCode);
        Assert.Equal(1, model.Calls); Assert.Equal(1, fixture.Claims); Assert.Equal(1, fixture.Stops);
        Assert.DoesNotContain("SYNTHETIC_PRIVATE", JsonSerializer.Serialize(fixture.Receipts) + JsonSerializer.Serialize(entries));
    }

    [Fact]
    public async Task BoundedIdleStillEndsAfterFiveMinutesWithoutCallingTheModel()
    {
        await using var fixture = new Fixture(); var clock = new ControlledClock(); var model = new Model();
        var run = new CompanionRunner(fixture.Connection, model, timeProvider: clock).Run(1, default);
        await clock.TimerScheduled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal(CompanionRunEnd.Idle, await run.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(0, model.Calls); Assert.Equal(2, fixture.Claims); Assert.Equal(1, fixture.Stops);
    }

    [Fact]
    public async Task ConnectedSessionSurvivesIdleThenProcessesTwoDifferentJobsAndWaitsForTheUser()
    {
        await using var fixture = new Fixture { EmptyClaims = 1, Jobs = 2 };
        var clock = new ControlledClock(); var model = new Model(); using var stop = new CancellationTokenSource();
        var run = new CompanionRunner(fixture.Connection, model, timeProvider: clock).StayConnected(stop.Token);
        await clock.TimerScheduled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(0, model.Calls); clock.Advance(TimeSpan.FromMinutes(6));
        await fixture.TwoFinishes.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(run.IsCompleted); Assert.Equal(2, model.Calls); Assert.Equal(2, fixture.FinishedIds.Distinct().Count());
        Assert.All(fixture.Receipts, receipt => Assert.Equal(JsonValueKind.Null, receipt.GetProperty("errorCode").ValueKind));
        stop.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Equal(1, fixture.Stops); Assert.Equal(2, model.Calls);
    }

    [Fact]
    public async Task ConnectedRevocationDuringIdleStopsPollingWithoutCallingTheModel()
    {
        await using var fixture = new Fixture { Rejection = HttpStatusCode.Unauthorized };
        var model = new Model();
        var error = await Assert.ThrowsAsync<CompanionRequestException>(() => new CompanionRunner(fixture.Connection, model).StayConnected(default));
        Assert.Equal(HttpStatusCode.Unauthorized, error.Status); Assert.Equal(1, fixture.Claims); Assert.Equal(0, model.Calls);
        Assert.Equal(1, fixture.Stops); Assert.Empty(fixture.Receipts);
    }

    [Fact]
    public async Task ConnectedRevocationDuringGenerationRejectsALateResultAndStops()
    {
        await using var fixture = new Fixture { Jobs = 2, RejectHeartbeat = true };
        var clock = new ControlledClock();
        var model = new Model(async cancellation => {
            clock.Advance(TimeSpan.FromSeconds(5));
            await fixture.HeartbeatSeen.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
            return Reply();
        });
        var error = await Assert.ThrowsAsync<CompanionRequestException>(() => new CompanionRunner(fixture.Connection, model, timeProvider: clock).StayConnected(default));
        Assert.Equal(HttpStatusCode.Forbidden, error.Status); Assert.Equal(1, fixture.Claims); Assert.Equal(1, model.Calls);
        Assert.Empty(fixture.Receipts); Assert.Equal(1, fixture.Stops);
    }

    [Fact]
    public async Task UserStopDropsAnIgnoredCancellationResultAndNeverClaimsTheNextJob()
    {
        await using var fixture = new Fixture { Jobs = 2 }; using var stop = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new Model(async cancellation => {
            started.SetResult(); var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellation.Register(() => cancelled.TrySetResult());
            await cancelled.Task; return Reply(); // Deliberately late: the runner must discard it.
        });
        var run = new CompanionRunner(fixture.Connection, model).StayConnected(stop.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3)); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(3)));
        var receipt = Assert.Single(fixture.Receipts);
        Assert.Equal("LOCAL_STOPPED", receipt.GetProperty("errorCode").GetString()); Assert.Equal(JsonValueKind.Null, receipt.GetProperty("response").ValueKind);
        Assert.Equal(1, fixture.Claims); Assert.Equal(1, model.Calls); Assert.Equal(1, fixture.Stops);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConnectedStopsAfterAModelFailureWhileBoundedStillConsumesOnlyItsChosenBatch(bool connected)
    {
        await using var fixture = new Fixture { Jobs = 2 };
        var model = new Model(_ => throw new LlmUnavailableException("Synthetic provider unavailable."));
        var runner = new CompanionRunner(fixture.Connection, model);
        if (connected) await Assert.ThrowsAsync<InvalidOperationException>(() => runner.StayConnected(default));
        else Assert.Equal(CompanionRunEnd.JobLimit, await runner.Run(2, default));
        Assert.Equal(connected ? 1 : 2, model.Calls); Assert.Equal(model.Calls, fixture.Claims);
        Assert.All(fixture.Receipts, receipt => Assert.Equal("MODEL_UNAVAILABLE", receipt.GetProperty("errorCode").GetString()));
        Assert.Equal(1, fixture.Stops);
    }

    [Fact]
    public async Task ConnectedDoesNotRemoveThePerWorkDeadline()
    {
        await using var fixture = new Fixture { Jobs = 2, MaximumSeconds = 1 };
        var model = new Model(async cancellation => { await Task.Delay(Timeout.InfiniteTimeSpan, cancellation); return Reply(); });
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CompanionRunner(fixture.Connection, model).StayConnected(default).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("MODEL_TIMEOUT", Assert.Single(fixture.Receipts).GetProperty("errorCode").GetString());
        Assert.Equal(1, model.Calls); Assert.Equal(1, fixture.Claims); Assert.Equal(1, fixture.Stops);
    }

    [Theory]
    [InlineData("unknown", false)]
    [InlineData("connected", true)]
    public void InvalidOrMixedLifetimePoliciesAreRejectedBeforeInferenceConfiguration(string mode, bool maximumJobs)
    {
        var args = new List<string> { "run", "--server", "https://example.invalid/", "--session-mode", mode };
        if (maximumJobs) args.AddRange(["--max-jobs", "1"]);
        Assert.Throws<ArgumentException>(() => CompanionOptions.Parse(args.ToArray()));
    }

    [Fact]
    public void SessionPolicyIsExplicitAndCannotBeSuppliedToStatus()
    {
        var normal = CompanionOptions.Parse(["status", "--server", "https://example.invalid/"]);
        Assert.False(normal.StayConnected); Assert.Equal(1, normal.MaximumJobs);
        Assert.Throws<ArgumentException>(() => CompanionOptions.Parse(["status", "--server", "https://example.invalid/", "--session-mode", "connected"]));
    }

    [Fact]
    public void ConnectedRunIsAnExplicitOptionAndDoesNotReplaceTheBoundedDefault()
    {
        var directory = Path.Combine(Path.GetTempPath(), "QuickPromptSessionOptions-" + Guid.NewGuid().ToString("N"));
        var sdk = Path.Combine(directory, "adaptive_core"); Directory.CreateDirectory(sdk);
        var python = Path.Combine(directory, "python.fixture"); var worker = Path.Combine(directory, "worker.fixture");
        foreach (var file in new[] { python, worker, Path.Combine(sdk, "host.py"), Path.Combine(sdk, "inference.py") }) File.WriteAllText(file, "synthetic; never execute");
        try
        {
            var args = new[] { "run", "--server", "https://example.invalid/", "--python", python, "--worker", worker, "--sdk-root", directory };
            Assert.False(CompanionOptions.Parse(args).StayConnected);
            Assert.True(CompanionOptions.Parse([.. args, "--session-mode", "connected"]).StayConnected);
            var bounded = CompanionOptions.Parse([.. args, "--session-mode", "bounded", "--max-jobs", "2"]);
            Assert.False(bounded.StayConnected); Assert.Equal(2, bounded.MaximumJobs);
        }
        finally
        {
            var target = Path.GetFullPath(directory); var parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(parent, StringComparison.OrdinalIgnoreCase) || new DirectoryInfo(target).LinkTarget is not null) throw new IOException("Unexpected option fixture cleanup target.");
            Directory.Delete(target, true);
        }
    }

    [Fact]
    public async Task ConnectedStillRejectsAnOutOfBudgetLeaseBeforeCallingTheModel()
    {
        await using var fixture = new Fixture { Jobs = 1, MaximumSeconds = 601 }; var model = new Model();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CompanionRunner(fixture.Connection, model).StayConnected(default));
        Assert.Equal(1, fixture.Claims); Assert.Equal(0, model.Calls); Assert.Empty(fixture.Receipts); Assert.Equal(1, fixture.Stops);
    }

    private static JsonDocument Reply() => JsonDocument.Parse("""{"message":{"content":"Synthetic completion"}}""");
    private sealed class Model(Func<CancellationToken, Task<JsonDocument>>? send = null) : IAgentModelTransport
    {
        public int Calls { get; private set; }
        public bool UsesThinking(string model) => false;
        public void Observe(int stage, string content, string? error) { }
        public async Task<JsonDocument> Send(object packet, CancellationToken cancellation, string purpose)
        { Calls++; return send is null ? Reply() : await send(cancellation); }
    }

    private sealed class ControlledClock : TimeProvider
    {
        private readonly object gate = new(); private DateTimeOffset now = DateTimeOffset.UnixEpoch;
        private readonly List<Timer> timers = [];
        public TaskCompletionSource TimerScheduled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override DateTimeOffset GetUtcNow() { lock (gate) return now; }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            lock (gate) { var timer = new Timer(this, callback, state); timers.Add(timer); timer.Change(dueTime, period); TimerScheduled.TrySetResult(); return timer; }
        }
        public void Advance(TimeSpan duration)
        {
            List<Timer> ready;
            lock (gate) { now += duration; ready = timers.Where(timer => timer.Due is { } due && due <= now).ToList(); foreach (var timer in ready) timer.Due = null; }
            foreach (var timer in ready) timer.Fire();
        }
        private sealed class Timer(ControlledClock clock, TimerCallback callback, object? state) : ITimer
        {
            public DateTimeOffset? Due { get; set; }
            private bool disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            { lock (clock.gate) { if (disposed) return false; Due = dueTime == Timeout.InfiniteTimeSpan ? null : clock.now + dueTime; return true; } }
            public void Fire() { lock (clock.gate) { if (disposed) return; } callback(state); }
            public void Dispose() { lock (clock.gate) { disposed = true; Due = null; } }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }

    private sealed class Fixture : HttpMessageHandler, IAsyncDisposable
    {
        private readonly string directory;
        public CompanionConnection Connection { get; }
        public int Jobs { get; set; }
        public int EmptyClaims { get; set; }
        public int MaximumSeconds { get; set; } = 30;
        public HttpStatusCode? Rejection { get; set; }
        public bool RejectHeartbeat { get; set; }
        public int Claims { get; private set; }
        public int Stops { get; private set; }
        public List<JsonElement> Receipts { get; } = [];
        public List<string> FinishedIds { get; } = [];
        public TaskCompletionSource TwoFinishes { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource HeartbeatSeen { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Fixture()
        {
            var options = CompanionOptions.Parse(["status", "--server", "https://example.invalid/", "--slot", "qa-" + Guid.NewGuid().ToString("N")]);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes("QuickPrompt.Companion/1|" + options.Origin.AbsoluteUri + "|" + options.Slot));
            directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickPromptNova", "companion", Convert.ToHexString(hash));
            Connection = new(options, new CompanionSecrets(options));
            ((HttpClient)Field("http").GetValue(Connection)!).Dispose();
            Field("http").SetValue(Connection, new HttpClient(this, false) { BaseAddress = options.Origin });
            Field("accessToken").SetValue(Connection, "synthetic-sentinel-not-a-credential");
            Field("expiresAt").SetValue(Connection, DateTimeOffset.MaxValue);
            Field("lastPresenceObservation").SetValue(Connection, Guid.NewGuid().ToString());
        }
        private static FieldInfo Field(string name) => typeof(CompanionConnection).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("example.invalid", request.RequestUri!.Host);
            var route = request.RequestUri.AbsolutePath;
            if (route == CompanionPresenceProtocol.StopPath) { Stops++; return new(HttpStatusCode.NoContent); }
            if (route.EndsWith("/claim", StringComparison.Ordinal))
            {
                Claims++;
                if (Rejection is { } rejected) return new(rejected);
                var response = new HttpResponseMessage(HttpStatusCode.NoContent);
                response.Headers.Add(CompanionPresenceProtocol.Header, Guid.NewGuid().ToString());
                if (EmptyClaims > 0) { EmptyClaims--; return response; }
                if (Jobs-- > 0) { response.StatusCode = HttpStatusCode.OK; response.Content = JsonContent.Create(new LocalWorkLease(Guid.NewGuid(), Guid.NewGuid(), "Synthetic instruction", LocalWorkProtocol.Model, 15, MaximumSeconds)); }
                return response;
            }
            if (route.EndsWith("/heartbeat", StringComparison.Ordinal))
            {
                HeartbeatSeen.TrySetResult();
                return RejectHeartbeat ? new(HttpStatusCode.Forbidden) : new(HttpStatusCode.OK) { Content = JsonContent.Create(new LocalLeasePulse(true, false)) };
            }
            Assert.EndsWith("/finish", route);
            Receipts.Add(JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(cancellationToken)));
            FinishedIds.Add(route.Split('/')[^2]); if (Receipts.Count == 2) TwoFinishes.TrySetResult();
            return new(HttpStatusCode.NoContent);
        }
        public async ValueTask DisposeAsync()
        {
            await Connection.DisposeAsync();
            var parent = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickPromptNova", "companion"));
            var target = Path.GetFullPath(directory);
            if (!target.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || new DirectoryInfo(target).LinkTarget is not null) throw new IOException("Unexpected fixture cleanup target.");
            Directory.Delete(target, true); Dispose();
        }
    }
}
