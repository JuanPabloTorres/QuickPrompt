using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Cloud;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    [Fact, Trait("Category", "SyntheticHttpCapacity")]
    public Task TenHttpOwnersAcrossTwoHostsKeepPrivateResultsAndBoundedQueueDuringRejectionAndRecovery() => CapacityQueueScenario(null);

    [Theory, Trait("Category", "SyntheticHttpCapacityContrast")]
    [InlineData(true)]
    [InlineData(false)]
    public Task HandlerFailureRecoveryIsDiscriminatedForBusyAndOtherOwners(bool failureOwnerIsBusy) => CapacityQueueScenario(failureOwnerIsBusy);

    private async Task CapacityQueueScenario(bool? failureOwnerIsBusy)
    {
        var measurements = new CapacityMeasurements("http-queue-two-hosts");
        var handler = new CapacityHandler(); var log = new OperationalTestLog();
        var owners = new List<CapacityOwner>(); var passed = false;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        void Services(IServiceCollection services)
        {
            services.AddSingleton<IAccountExecutionHandler>(handler);
            services.AddLogging(logging => logging.AddProvider(log));
        }
        await using var first = await Start(configureServices: Services);
        await using var second = await Start(configureServices: Services);
        try
        {
            var compileEndpoint = ((IEndpointRouteBuilder)first.App).DataSources.SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>().Single(endpoint => endpoint.RoutePattern.RawText!.EndsWith("/prompts/{promptId:guid}/compile", StringComparison.Ordinal));
            measurements.Facts["compileRateLimitPolicy"] = compileEndpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName ?? "none";
            for (var index = 0; index < 10; index++)
            {
                var fixture = await CapacityAccount(index % 2 == 0 ? first : second, index);
                owners.Add(fixture); handler.Owners.TryAdd(fixture.Actor, 0);
            }
            measurements.Owners = owners.Count;
            for (var wave = 0; wave < 3; wave++)
                await Task.WhenAll(owners.Select((owner, index) => measurements.Measure("private-five-request-sequence", async () =>
                {
                    var foreign = owners[(index + 1) % owners.Count];
                    var visible = (await owner.Client.GetFromJsonAsync<JsonElement[]>("/api/accounts/v1/libraries/", timeout.Token))!;
                    Assert.Contains(visible, item => item.GetProperty("id").GetGuid() == owner.Library);
                    Assert.DoesNotContain(visible, item => item.GetProperty("id").GetGuid() == foreign.Library);
                    Assert.Equal(owner.Prompt, Assert.Single((await owner.Client.GetFromJsonAsync<AccountPrompt[]>(owner.Path + "/prompts", timeout.Token))!));
                    Assert.Equal(owner.Prompt.Id, (await owner.Client.GetFromJsonAsync<AccountPrompt>(owner.Path + $"/prompts/{owner.Prompt.Id}", timeout.Token))!.Id);
                    Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.GetAsync(foreign.Path + "/prompts", timeout.Token)).StatusCode);
                    Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.GetAsync(owner.Path + $"/prompts/{foreign.Prompt.Id}", timeout.Token)).StatusCode);
                })));

            var submissions = owners.ToDictionary(owner => owner.Actor, _ => new CompileSubmission(Guid.NewGuid(), 1, new() { ["tema"] = "HTTP sintético" }));
            var initial = await Task.WhenAll(owners.Select(owner => SubmitCapacity(owner, submissions[owner.Actor], timeout.Token, measurements, log, "initial")));
            await CapacityAwait(() => handler.Active == 2, timeout.Token);
            Assert.Equal(2, handler.Active);
            var busy = owners.Single(owner => owner.Actor == handler.ActiveOwners.Keys.First());
            var faultOwner = failureOwnerIsBusy switch { true => busy, false => owners.First(owner => owner != busy), null => owners[0] };
            measurements.Facts["failureOwnerSelection"] = failureOwnerIsBusy switch { true => "fixed-busy", false => "fixed-other", null => "observed-owner-zero" };
            measurements.Facts["failureRecoveryOwnerWasQueueBoundaryOwner"] = busy == faultOwner;
            var quotaRequests = Enumerable.Range(0, 5).Select(_ => new CompileSubmission(Guid.NewGuid(), 1, new() { ["tema"] = "Cuota sintética" })).ToArray();
            var quotaResponses = await Task.WhenAll(quotaRequests.Select(input => busy.Client.PostAsJsonAsync(busy.Path + $"/prompts/{busy.Prompt.Id}/compile", input, timeout.Token)));
            var acceptedQuota = quotaResponses.Count(response => response.StatusCode == HttpStatusCode.Accepted);
            var rejectedQuota = quotaResponses.Count(response => response.StatusCode == HttpStatusCode.TooManyRequests);
            foreach (var response in quotaResponses.Where(response => response.StatusCode == HttpStatusCode.TooManyRequests))
                measurements.Rejections.Enqueue(await CapacityRejectionDetails(busy, response, log, "expected-owner-queue-boundary", timeout.Token));
            foreach (var response in quotaResponses.Where(response => response.StatusCode == HttpStatusCode.Accepted))
                measurements.Track(busy, (await response.Content.ReadFromJsonAsync<ExecutionView>(timeout.Token))!);
            foreach (var response in quotaResponses) response.Dispose();
            Assert.Equal(4, acceptedQuota); Assert.Equal(1, rejectedQuota);
            Assert.Equal(initial[owners.IndexOf(busy)].Id, (await SubmitCapacity(busy, submissions[busy.Actor], timeout.Token, measurements, log, "initial-replay")).Id);
            var cancelled = owners.First(owner => !handler.ActiveOwners.ContainsKey(owner.Actor));
            var cancelledJob = initial[owners.IndexOf(cancelled)];
            (await cancelled.Client.PostAsJsonAsync(cancelled.Path + $"/executions/{cancelledJob.Id}/cancel", new { }, timeout.Token)).EnsureSuccessStatusCode();
            handler.Release.TrySetResult();

            await Task.WhenAll(owners.Select((owner, index) => measurements.Measure("execution-terminal-observation", async () =>
            {
                var completed = await CapacityTerminal(owner, initial[index].Id, timeout.Token);
                if (owner == cancelled)
                {
                    Assert.Equal(ExecutionStatus.Cancelled, completed.Status);
                    Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.GetAsync(owner.Path + $"/executions/{completed.Id}/result", timeout.Token)).StatusCode);
                }
                else
                {
                    Assert.Equal(ExecutionStatus.Completed, completed.Status);
                    var result = (await owner.Client.GetFromJsonAsync<ExecutionResultView>(owner.Path + $"/executions/{completed.Id}/result", timeout.Token))!;
                    Assert.Equal(owner.Prompt.Template.Replace("<tema>", "HTTP sintético", StringComparison.Ordinal), result.Text);
                }
                var foreign = owners[(index + 1) % owners.Count];
                Assert.Equal(HttpStatusCode.NotFound, (await foreign.Client.GetAsync(owner.Path + $"/executions/{completed.Id}/result", timeout.Token)).StatusCode);
            })));
            var fault = new CompileSubmission(Guid.NewGuid(), 1, new() { ["tema"] = CapacityHandler.PrivateFailure });
            measurements.Facts["queueBeforeHandlerFailure"] = await CapacityQueueSnapshot(faultOwner, timeout.Token);
            var failed = await SubmitCapacity(faultOwner, fault, timeout.Token, measurements, log, "controlled-handler-failure");
            var terminalFailure = await CapacityTerminal(faultOwner, failed.Id, timeout.Token);
            Assert.Equal(ExecutionStatus.Failed, terminalFailure.Status); Assert.Equal("PROCESSING_FAILED", terminalFailure.ErrorCode);
            Assert.Equal(failed.Id, (await SubmitCapacity(faultOwner, fault, timeout.Token, measurements, log, "failed-receipt-replay")).Id);
            measurements.Facts["queueBeforeHandlerRecovery"] = await CapacityQueueSnapshot(faultOwner, timeout.Token);
            var recovery = await SubmitCapacity(faultOwner, fault with { OperationId = Guid.NewGuid(), Values = new() { ["tema"] = "Recuperado" } }, timeout.Token, measurements, log, "recovery-after-handler-failure");
            Assert.Equal(ExecutionStatus.Completed, (await CapacityTerminal(faultOwner, recovery.Id, timeout.Token)).Status);
            Assert.InRange(handler.Peak, 1, 2); Assert.Equal(1, handler.PeakPerOwner);
            Assert.All(handler.Attempts.Values, attempts => Assert.Equal(1, attempts));
            Assert.Contains(log.Events, item => item.Id == 5331);
            Assert.Contains(log.Events, item => item.Id == 5334);
            Assert.All(log.Events, item => Assert.DoesNotContain(CapacityHandler.PrivateFailure, item.Message));
            measurements.Facts["maximumObservedCompileHandlers"] = handler.Peak;
            measurements.Facts["maximumObservedHandlersPerOwner"] = handler.PeakPerOwner;
            measurements.Facts["ownerQueueAccepted"] = 4;
            measurements.Facts["ownerQueueRejected"] = 1;
            measurements.Facts["duplicateHandlerInvocations"] = handler.Attempts.Values.Count(value => value != 1);
            measurements.Facts["cancelledRequestHadNoResult"] = true;
            measurements.Facts["handlerFailureRecoveredWithoutAutomaticReplay"] = true;
            passed = true;
        }
        finally
        {
            handler.Release.TrySetResult();
            var cleaned = await CleanupCapacityJobs(measurements);
            foreach (var owner in owners) owner.Client.Dispose();
            measurements.Save(passed && cleaned);
            Assert.True(!passed || cleaned, "The scenario's own jobs must be terminal before its hosts are disposed.");
        }
    }

    [Fact, Trait("Category", "SyntheticHttpCapacityContrast")]
    public async Task GlobalQueueInterferenceRejectsAnOwnerWithNoQueuedJobsAndRecoversAfterOnlyOwnWorkIsCancelled()
    {
        var measurements = new CapacityMeasurements("http-queue-two-hosts-global-interference");
        var handler = new CapacityHandler(); var log = new OperationalTestLog();
        var owners = new List<CapacityOwner>(); var passed = false;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        void Services(IServiceCollection services)
        {
            services.AddSingleton<IAccountExecutionHandler>(handler);
            services.AddLogging(logging => logging.AddProvider(log));
        }
        await using var first = await Start(configureServices: Services);
        await using var second = await Start(configureServices: Services);
        try
        {
            // Sixteen owners provide the unchanged 64 queued slots (4 each); the seventeenth probes an empty personal queue.
            for (var index = 0; index < 17; index++)
            {
                var owner = await CapacityAccount(index % 2 == 0 ? first : second, index);
                owners.Add(owner); handler.Owners.TryAdd(owner.Actor, 0);
            }
            measurements.Owners = owners.Count;
            var holders = await Task.WhenAll(owners.Take(2).Select(owner => SubmitCapacity(owner,
                new(Guid.NewGuid(), 1, new() { ["tema"] = "Ocupación sintética controlada" }), timeout.Token, measurements, log, "global-control-workers")));
            await CapacityAwait(() => handler.Active == 2, timeout.Token);
            var probe = owners[^1]; var before = await CapacityQueueSnapshot(probe, timeout.Token);
            measurements.Facts["baselineQueueWhileOwnHandlersBlocked"] = before;
            var limits = first.App.Services.GetRequiredService<ExecutionQueue>().Limits;
            var remaining = limits.GlobalQueued - before.Queued;
            Assert.InRange(remaining, 1, limits.GlobalQueued);
            var queued = new List<(CapacityOwner Owner, ExecutionView Job)>();
            foreach (var owner in owners.Take(16))
                for (var count = 0; count < limits.OwnerQueued && remaining > 0; count++, remaining--)
                    queued.Add((owner, await SubmitCapacity(owner, new(Guid.NewGuid(), 1, new() { ["tema"] = "Interferencia global sintética" }),
                        timeout.Token, measurements, log, "fill-global-control")));
            Assert.Equal(0, remaining);
            var full = await CapacityQueueSnapshot(probe, timeout.Token);
            Assert.Equal(limits.GlobalQueued, full.Queued); Assert.Equal(0, full.OwnerQueued); Assert.Equal(0, full.OwnerAdmissionsToday);
            measurements.Facts["fullGlobalQueueForEmptyOwner"] = full;
            var request = new CompileSubmission(Guid.NewGuid(), 1, new() { ["tema"] = "Recuperación tras interferencia global" });
            using (var rejected = await probe.Client.PostAsJsonAsync(probe.Path + $"/prompts/{probe.Prompt.Id}/compile", request, timeout.Token))
            {
                measurements.Rejections.Enqueue(await CapacityRejectionDetails(probe, rejected, log, "expected-global-queue-boundary", timeout.Token));
                Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
                Assert.Contains(log.Events, item => item.Id == 5331 && item.Message == "Execution quota rejected: reason=QUEUE_FULL");
            }
            foreach (var (owner, job) in queued)
            {
                using var cancel = await owner.Client.PostAsJsonAsync(owner.Path + $"/executions/{job.Id}/cancel", new { }, timeout.Token);
                cancel.EnsureSuccessStatusCode();
            }
            handler.Release.TrySetResult();
            // The fixture deliberately has a one-connection account pool. Observe the
            // cancelled receipts sequentially instead of inventing a 64-reader load test.
            foreach (var item in queued)
                Assert.Equal(ExecutionStatus.Cancelled, (await CapacityTerminal(item.Owner, item.Job.Id, timeout.Token)).Status);
            await Task.WhenAll(holders.Select((job, index) => CapacityTerminal(owners[index], job.Id, timeout.Token)));
            var released = await CapacityQueueSnapshot(probe, timeout.Token);
            measurements.Facts["queueAfterOnlyOwnWorkReleased"] = released;
            Assert.True(released.Queued < limits.GlobalQueued); Assert.Equal(0, released.OwnerQueued);
            var recovered = await SubmitCapacity(probe, request, timeout.Token, measurements, log, "global-queue-explicit-recovery");
            Assert.Equal(ExecutionStatus.Completed, (await CapacityTerminal(probe, recovered.Id, timeout.Token)).Status);
            measurements.Facts["ownQueuedJobsCancelled"] = queued.Count;
            measurements.Facts["foreignCancellationRequests"] = 0;
            measurements.Facts["foreignRowsDeleted"] = 0;
            measurements.Facts["globalQuotaThenExplicitRecovery"] = true;
            passed = true;
        }
        finally
        {
            handler.Release.TrySetResult();
            var cleaned = await CleanupCapacityJobs(measurements);
            foreach (var owner in owners) owner.Client.Dispose();
            measurements.Save(passed && cleaned);
            Assert.True(!passed || cleaned, "The interference control must finish only its own work before disposal.");
        }
    }

    [Fact, Trait("Category", "SyntheticHttpCapacity")]
    public async Task TenVisibleAgentReadsAcrossTwoHostsRemainOwnerBoundWithoutCallingAModelOrWriting()
    {
        var measurements = new CapacityMeasurements("visible-agent-read-two-hosts");
        var owners = new List<CapacityOwner>(); var devices = new List<HttpClient>(); var passed = false;
        await using var first = await Start(); await using var second = await Start();
        try
        {
            var grants = new List<Guid>();
            for (var index = 0; index < 10; index++)
            {
                var host = index % 2 == 0 ? first : second;
                var owner = await CapacityAccount(host, index); owners.Add(owner);
                var device = Client(host.App); devices.Add(device);
                grants.Add((await ProposalDevice(owner.Client, device)).Connection.Id);
                VisibleSurface(owner.Client, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
            }
            measurements.Owners = owners.Count;
            await Task.WhenAll(owners.Select((owner, index) => measurements.Measure("visible-agent-read", async () =>
            {
                var request = OpenVisible(grants[index], owner.Prompt);
                (await owner.Client.PostAsJsonAsync(VisiblePath(owner.Library) + "/live", request)).EnsureSuccessStatusCode();
                var result = await DriveVisibleRead(owner.Client, owner.Library, request.Request.RunId, owner.Prompt);
                Assert.Equal(LiveStatus.COMPLETED, result.Run.Status);
                Assert.False(result.Run.EffectCommitted); Assert.False(result.Run.BrowserOpen);
                Assert.Equal(owner.Prompt.Template, result.Observation!.TemplateText);
                var foreign = owners[(index + 1) % owners.Count];
                Assert.Equal(HttpStatusCode.NotFound, (await foreign.Client.GetAsync(VisiblePath(owner.Library) + $"/live/{request.Request.RunId}")).StatusCode);
                Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(devices[index])).StatusCode);
                Assert.Equal(owner.Prompt, Assert.Single((await owner.Client.GetFromJsonAsync<AccountPrompt[]>(owner.Path + "/prompts"))!));
            })));
            measurements.Facts["visibleReadsCompleted"] = owners.Count;
            measurements.Facts["modelProposalQueueClaims"] = 0;
            measurements.Facts["effectsCommitted"] = 0;
            measurements.Facts["browserDomDriven"] = false;
            passed = true;
        }
        finally
        {
            foreach (var owner in owners) owner.Client.Dispose(); foreach (var device in devices) device.Dispose();
            measurements.Save(passed);
        }
    }

    [Fact, Trait("Category", "SyntheticOperationalFault")]
    public async Task ExhaustingOnlyTheHostsOwnAccountPoolSignalsNotReadyAndRecoversAfterRelease()
    {
        var measurements = new CapacityMeasurements("account-pool-starvation-recovery") { Owners = 1 };
        var log = new OperationalTestLog(); var passed = false;
        await using var host = await Start(delegation: false, configureServices: services => services.AddLogging(logging => logging.AddProvider(log)));
        using var owner = Client(host.App);
        using var anonymous = Client(host.App);
        await Login(owner, await Register(owner, host.Mail));
        var before = (await owner.GetFromJsonAsync<JsonElement[]>("/api/accounts/v1/libraries/"))!;
        (await owner.GetAsync("/health")).EnsureSuccessStatusCode();
        var pool = host.App.Services.GetRequiredService<NpgsqlDataSource>();
        try
        {
            await using (var held = await pool.OpenConnectionAsync())
            {
                using var forged = new HttpRequestMessage(HttpMethod.Get, "/health");
                forged.Headers.Add("Origin", "https://synthetic-untrusted.example.invalid");
                var forgedStatus = (await owner.SendAsync(forged)).StatusCode;
                var anonymousStatus = (await anonymous.GetAsync("/health")).StatusCode;
                var authenticatedStatus = (await owner.GetAsync("/health")).StatusCode;
                measurements.Facts["anonymousHealthDuringPoolStarvation"] = (int)anonymousStatus;
                measurements.Facts["authenticatedHealthDuringPoolStarvation"] = (int)authenticatedStatus;
                measurements.Facts["forgedOriginDuringPoolStarvation"] = (int)forgedStatus;
                Assert.Equal(HttpStatusCode.Forbidden, forgedStatus);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, anonymousStatus);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, authenticatedStatus);
                Assert.Equal(HttpStatusCode.InternalServerError, (await owner.GetAsync("/api/accounts/v1/libraries/")).StatusCode);
                Assert.Contains(log.Events, item => item.Id == 5330);
            }
            (await owner.GetAsync("/health")).EnsureSuccessStatusCode();
            var after = (await owner.GetFromJsonAsync<JsonElement[]>("/api/accounts/v1/libraries/"))!;
            Assert.Equal(before.Select(item => item.GetProperty("id").GetGuid()), after.Select(item => item.GetProperty("id").GetGuid()));
            Assert.All(log.Events, item => Assert.DoesNotContain(Password, item.Message));
            measurements.Facts["healthBefore"] = 200; measurements.Facts["healthDuringPoolStarvation"] = 503;
            measurements.Facts["authenticatedReadWhilePoolUnavailable"] = 500;
            measurements.Facts["healthAfterRelease"] = 200; measurements.Facts["privateReadRecovered"] = true;
            measurements.Facts["postgresServerStopped"] = false;
            passed = true;
        }
        finally
        {
            measurements.Facts["readinessUnavailableSignals"] = log.Events.Count(item => item.Id == 5330);
            measurements.Facts["safeBoundaryDiagnostics"] = log.Events.Where(item => item.Id == 5330
                || item.Message.StartsWith("Account operation failed:", StringComparison.Ordinal)).Select(item => item.Message).ToArray();
            measurements.Save(passed);
        }
    }

    private sealed record CapacityOwner(HttpClient Client, Guid Actor, Guid Library, AccountPrompt Prompt)
    { public string Path => $"/api/accounts/v1/libraries/{Library}"; }

    private static async Task<CapacityOwner> CapacityAccount(RunningHost host, int index)
    {
        var client = Client(host.App);
        try
        {
            await Login(client, await Register(client, host.Mail));
            var actor = (await client.GetFromJsonAsync<JsonElement>("/account/me")).GetProperty("id").GetGuid();
            var library = await CreateLibrary(client);
            using var response = await client.PostAsJsonAsync($"/api/accounts/v1/libraries/{library}/prompts", new PromptContent("Mismo título QA", $"Propietario sintético {index}: explica <tema>.", 0));
            response.EnsureSuccessStatusCode();
            return new(client, actor, library, (await response.Content.ReadFromJsonAsync<AccountPrompt>())!);
        }
        catch { client.Dispose(); throw; }
    }

    private async Task<ExecutionView> SubmitCapacity(CapacityOwner owner, CompileSubmission input, CancellationToken cancellation,
        CapacityMeasurements measurements, OperationalTestLog log, string phase)
    {
        using var response = await owner.Client.PostAsJsonAsync(owner.Path + $"/prompts/{owner.Prompt.Id}/compile", input, cancellation);
        if (response.StatusCode != HttpStatusCode.Accepted)
        {
            var diagnostic = await CapacityRejectionDetails(owner, response, log, phase, cancellation);
            measurements.Rejections.Enqueue(diagnostic);
            Assert.Fail("Expected Accepted; diagnostic=" + JsonSerializer.Serialize(diagnostic));
        }
        var accepted = (await response.Content.ReadFromJsonAsync<ExecutionView>(cancellation))!;
        measurements.Track(owner, accepted); return accepted;
    }

    /// <summary>Only typed, bounded public problem fields and aggregate synthetic queue counts; never raw bodies, actor IDs or secrets.</summary>
    private async Task<object> CapacityRejectionDetails(CapacityOwner owner, HttpResponseMessage response, OperationalTestLog log, string phase, CancellationToken cancellation)
    {
        var body = await response.Content.ReadAsByteArrayAsync(cancellation);
        var title = "none"; int? problemStatus = null;
        try
        {
            using var problem = JsonDocument.Parse(body);
            if (problem.RootElement.TryGetProperty("status", out var status) && status.TryGetInt32(out var value)) problemStatus = value;
            if (problem.RootElement.TryGetProperty("title", out var text) && text.ValueKind == JsonValueKind.String)
                title = text.GetString() switch {
                    "Tu cola está llena. Espera o detén una preparación antes de reintentar." => "OWNER_QUEUE_FULL",
                    "La cola del servicio está llena. Vuelve a intentarlo en unos momentos." => "QUEUE_FULL",
                    "Alcanzaste el límite diario de preparaciones. Tus datos se conservan en el formulario." => "DAILY_LIMIT",
                    _ => "unrecognized-public-title"
                };
        }
        catch (JsonException) { title = body.Length == 0 ? "empty-body" : "non-json-body"; }
        CapacityQueueState? queue = null; string? queueErrorType = null;
        try { queue = await CapacityQueueSnapshot(owner, cancellation); }
        catch (Exception error) when (error is NpgsqlException or TimeoutException or OperationCanceledException) { queueErrorType = error.GetType().Name; }
        return new { phase, httpStatus = (int)response.StatusCode, contentType = response.Content.Headers.ContentType?.MediaType,
            retryAfterSeconds = response.Headers.RetryAfter?.Delta?.TotalSeconds,
            body = new { status = problemStatus, titleCode = title, bytes = body.Length, sha256 = Convert.ToHexString(SHA256.HashData(body)) },
            layer = problemStatus == 429 ? "domain-quota-problem" : response.StatusCode == HttpStatusCode.TooManyRequests && body.Length == 0 ? "http-limiter-empty-body" : "unclassified",
            queue, queueErrorType, quotaSignals = log.Events.Where(item => item.Id == 5331).Select(item => item.Message).Distinct().ToArray() };
    }

    private sealed record CapacityQueueState(long Queued, long Running, long OwnerQueued, long OwnerRunning, long OwnerAdmissionsToday, long QueuedModelProposals);

    private async Task<CapacityQueueState> CapacityQueueSnapshot(CapacityOwner owner, CancellationToken cancellation)
    {
        await using var connection = new NpgsqlConnection(postgres.ExecutionConnection);
        await connection.OpenAsync(cancellation);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT count(*) FILTER (WHERE "Status"='Queued'),count(*) FILTER (WHERE "Status"='Running'),
              count(*) FILTER (WHERE "OwnerId"=@owner AND "Status"='Queued'),count(*) FILTER (WHERE "OwnerId"=@owner AND "Status"='Running'),
              count(*) FILTER (WHERE "OwnerId"=@owner AND "CreatedAt">=@day),count(*) FILTER (WHERE "Status"='Queued' AND "Kind"='ModelProposal')
            FROM qp_operations.executions
            """;
        command.Parameters.AddWithValue("owner", owner.Actor);
        command.Parameters.AddWithValue("day", new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero));
        await using var reader = await command.ExecuteReaderAsync(cancellation); Assert.True(await reader.ReadAsync(cancellation));
        return new(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4), reader.GetInt64(5));
    }

    /// <summary>Only receipts captured by this scenario are cancelled/observed; never delete rows or modify other fixtures' jobs.</summary>
    private static async Task<bool> CleanupCapacityJobs(CapacityMeasurements measurements)
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            var tracked = measurements.TrackedJobs.Values.ToArray();
            foreach (var (owner, job) in tracked)
            {
                using var response = await owner.Client.PostAsJsonAsync(owner.Path + $"/executions/{job.Id}/cancel", new { }, cleanup.Token);
                response.EnsureSuccessStatusCode();
            }
            foreach (var (owner, job) in tracked) await CapacityTerminal(owner, job.Id, cleanup.Token);
            measurements.Facts["ownJobsTerminalAtCleanup"] = tracked.Length;
            measurements.Facts["ownCleanupCompleted"] = true;
            return true;
        }
        catch (Exception error) when (error is OperationCanceledException or HttpRequestException or JsonException)
        {
            // Preserve the first failed assertion. Teardown does not turn an unknown result into PASS.
            measurements.Facts["ownCleanupCompleted"] = false;
            measurements.Facts["ownCleanupErrorType"] = error.GetType().Name;
            return false;
        }
    }

    private static async Task CapacityAwait(Func<bool> condition, CancellationToken cancellation)
    { while (!condition()) await Task.Delay(30, cancellation); }

    private static async Task<ExecutionView> CapacityTerminal(CapacityOwner owner, Guid id, CancellationToken cancellation)
    {
        while (true)
        {
            var view = (await owner.Client.GetFromJsonAsync<ExecutionView>(owner.Path + $"/executions/{id}", cancellation))!;
            if (view.Status is not (ExecutionStatus.Queued or ExecutionStatus.Running)) return view;
            await Task.Delay(40, cancellation);
        }
    }

    private sealed class CapacityHandler : IAccountExecutionHandler
    {
        public const string PrivateFailure = "SYNTHETIC_PRIVATE_FAILURE_MUST_NOT_REACH_LOGS";
        public ConcurrentDictionary<Guid, byte> Owners { get; } = new();
        public ConcurrentDictionary<Guid, byte> ActiveOwners { get; } = new();
        public ConcurrentDictionary<Guid, int> Attempts { get; } = new();
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int active; private int peak; private int peakPerOwner;
        public int Active => Volatile.Read(ref active);
        public int Peak => Volatile.Read(ref peak);
        public int PeakPerOwner => Volatile.Read(ref peakPerOwner);
        public async Task<ExecutionOutput> Execute(ExecutionLease lease, CancellationToken cancellation)
        {
            if (!Owners.ContainsKey(lease.OwnerId)) return await new CompileExecutionHandler().Execute(lease, cancellation);
            Attempts.AddOrUpdate(lease.Id, 1, (_, count) => count + 1);
            var ownerAdded = ActiveOwners.TryAdd(lease.OwnerId, 0);
            InterlockedExtensions.Max(ref peakPerOwner, ownerAdded ? 1 : 2);
            InterlockedExtensions.Max(ref peak, Interlocked.Increment(ref active));
            try
            {
                await Release.Task.WaitAsync(cancellation);
                if (lease.Payload.Contains(PrivateFailure, StringComparison.Ordinal)) throw new InvalidOperationException(PrivateFailure);
                return await new CompileExecutionHandler().Execute(lease, cancellation);
            }
            finally { ActiveOwners.TryRemove(lease.OwnerId, out _); Interlocked.Decrement(ref active); }
        }
    }

    private static class InterlockedExtensions
    {
        public static void Max(ref int target, int value)
        {
            int current;
            do { current = Volatile.Read(ref target); if (current >= value) return; }
            while (Interlocked.CompareExchange(ref target, value, current) != current);
        }
    }

    private sealed class CapacityMeasurements(string scenario)
    {
        private readonly ConcurrentQueue<(string Phase, double Milliseconds)> samples = new();
        public ConcurrentQueue<object> Rejections { get; } = new();
        public ConcurrentDictionary<Guid, (CapacityOwner Owner, ExecutionView Job)> TrackedJobs { get; } = new();
        public void Track(CapacityOwner owner, ExecutionView job) => TrackedJobs.TryAdd(job.Id, (owner, job));
        public int Owners { get; set; }
        public Dictionary<string, object> Facts { get; } = new();
        public async Task Measure(string phase, Func<Task> operation)
        { var watch = Stopwatch.StartNew(); await operation(); samples.Enqueue((phase, watch.Elapsed.TotalMilliseconds)); }
        public void Save(bool passed)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(System.IO.Path.Combine(root.FullName, "QuickPrompt.Nova.sln"))) root = root.Parent;
            if (root is null) throw new InvalidOperationException("Cannot identify synthetic QA evidence root.");
            var folder = System.IO.Path.Combine(root.FullName, ".playwright", "operational-http");
            Directory.CreateDirectory(folder);
            var phases = samples.GroupBy(sample => sample.Phase).Select(group =>
            {
                var ordered = group.Select(sample => sample.Milliseconds).Order().ToArray();
                return new { phase = group.Key, samples = ordered.Length, p50Ms = ordered[(int)Math.Ceiling(ordered.Length * .50) - 1], p95Ms = ordered[(int)Math.Ceiling(ordered.Length * .95) - 1], maximumMs = ordered[^1] };
            });
            var receipt = new { kind = "SYNTHETIC_TESTSERVER_HTTP_CAPACITY", scenario, status = passed ? "passed" : "failed", owners = Owners,
                accountHosts = scenario == "account-pool-starvation-recovery" ? 1 : 2, observedAtUtc = DateTimeOffset.UtcNow,
                phases, facts = Facts, rejections = Rejections.ToArray(), productionRequests = 0, realModelCalls = 0, privateDataUsed = false,
                limitations = new[] { "TestServer HTTP and real synthetic PostgreSQL, not public-network or browser DOM load.", "Controlled compilation handler and explicit agent reads do not measure Qwen capacity or semantic fidelity.", "No commercial SLA, deployment readiness or external alert delivery is certified." } };
            File.WriteAllText(System.IO.Path.Combine(folder, scenario + "-" + Guid.NewGuid().ToString("N") + ".json"), JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
