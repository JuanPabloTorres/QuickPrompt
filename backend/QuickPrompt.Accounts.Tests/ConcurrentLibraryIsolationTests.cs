using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Accounts.Libraries;
using Xunit;
using Xunit.Abstractions;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Bounded pooled-database contention probe, not a production HTTP load certification.</summary>
public sealed class ConcurrentLibraryIsolationTests(PostgresFixture postgres, ITestOutputHelper output) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ConcurrentOwnersWithIdenticalNamesCannotLeakAcrossPooledTransactions()
    {
        var fixtures = new List<(LibraryActor Actor, Guid Library, AccountPrompt Prompt)>();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        for (var index = 0; index < 16; index++) {
            var owner = await postgres.CreateUser();
            var actor = LibraryActor.FromPrincipal(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, owner.ToString())], "qa-authenticated")));
            await using var scope = postgres.Services.CreateAsyncScope();
            var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
            var library = await access.CreateLibrary(actor, "Mismo nombre", lifetime.Token);
            var prompt = await access.SavePrompt(actor, library.Id, null, 0, "Mismo título", "Contenido exclusivo QA " + index, lifetime.Token);
            fixtures.Add((actor, library.Id, prompt));
        }
        var measurements = new List<double>();
        for (var wave = 0; wave < 4; wave++) {
            var durations = await Task.WhenAll(fixtures.Select(async (fixture, index) => {
                var timer = Stopwatch.StartNew();
                await using var scope = postgres.Services.CreateAsyncScope();
                var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
                var foreign = fixtures[(index + 1) % fixtures.Count];
                Assert.Equal(fixture.Library, Assert.Single(await access.ListLibraries(fixture.Actor, lifetime.Token)).Id);
                Assert.Equal(fixture.Prompt, Assert.Single(await access.ListPrompts(fixture.Actor, fixture.Library, cancellation: lifetime.Token)));
                Assert.Equal(fixture.Prompt.Template, Assert.Single(await access.ListVersions(fixture.Actor, fixture.Library, fixture.Prompt.Id, lifetime.Token)).Template);
                await Assert.ThrowsAsync<LibraryUnavailableException>(() => access.GetPrompt(fixture.Actor, foreign.Library, foreign.Prompt.Id, lifetime.Token));
                await Assert.ThrowsAsync<LibraryUnavailableException>(() => access.GetPrompt(fixture.Actor, fixture.Library, foreign.Prompt.Id, lifetime.Token));
                return timer.Elapsed.TotalMilliseconds;
            }));
            measurements.AddRange(durations);
        }
        var ordered = measurements.Order().ToArray();
        output.WriteLine("16 simultaneous owners; 4 waves; 320 authorized/negative domain calls against actual pooled PostgreSQL. All ownership assertions passed.");
        output.WriteLine($"Measured local duration per five-call sequence: p50={ordered[ordered.Length / 2]:F1}ms; p95={ordered[(int)Math.Ceiling(ordered.Length * .95) - 1]:F1}ms. No production SLA inferred.");
    }
}
