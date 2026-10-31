using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Companion;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Companion.Tests;

public sealed class RunDiagnosticsTests
{
    [Fact]
    public void OutputLimitHasAnExplicitDiagnosticWhileUnknownCodesRemainPrivate()
    {
        var entries = new List<CompanionRunDiagnostic>();
        var run = new CompanionRunDiagnostics(LocalWorkProtocol.Profile, entries.Add);
        run.WorkFinished("MODEL_OUTPUT_LIMIT");
        Assert.Equal("MODEL_OUTPUT_LIMIT", entries.Last().LastWorkCode);
        run.WorkFinished("SYNTHETIC_PRIVATE_MODEL_OUTPUT_LIMIT");
        Assert.Equal("UNKNOWN_FAILURE", entries.Last().LastWorkCode);
        Assert.DoesNotContain("SYNTHETIC_PRIVATE", JsonSerializer.Serialize(entries));
    }

    [Theory]
    [InlineData(LocalWorkProtocol.Profile)]
    [InlineData(LocalProposalProtocol.Profile)]
    public void HistoricalReceiptPreservesTheFailurePhaseAndNeverReopensATerminalObservation(string profile)
    {
        var entries = new List<CompanionRunDiagnostic>();
        var run = new CompanionRunDiagnostics(profile, entries.Add);
        run.Stage(CompanionRunPhase.Connecting);
        run.Stage(CompanionRunPhase.Claiming);
        run.Stage(CompanionRunPhase.Claiming);
        Assert.Equal(2, entries.Count); // Repeated empty queue polls do not generate disk traffic.
        run.Finish(CompanionRunEnd.HttpRejected, 503);
        run.Finish(CompanionRunEnd.UnconfirmedExit);
        run.Stage(CompanionRunPhase.Generating);
        var end = entries.Last();
        Assert.Equal(3, entries.Count);
        Assert.Equal(CompanionRunPhase.Claiming, end.Phase);
        Assert.Equal(CompanionRunEnd.HttpRejected, end.End);
        Assert.Equal(503, end.HttpStatus);
        Assert.Equal(0, end.HandledJobs);
        Assert.NotNull(end.EndedUtc);
        Assert.Equal(profile, end.ProfileId);
    }

    [Fact]
    public void MetadataHasNoChannelForExceptionMessagesPacketsOrUnrecognizedProtocolContent()
    {
        using var fixture = new PrivateFixture();
        using var ownership = fixture.Secrets.Acquire();
        var credential = new CompanionSession(fixture.Options.Origin.AbsoluteUri, Guid.NewGuid(), "synthetic-refresh-must-stay-encrypted");
        fixture.Secrets.Save(credential);
        var run = new CompanionRunDiagnostics(fixture.Options.Profile, fixture.Secrets.WriteRunDiagnostic);
        run.Stage(CompanionRunPhase.Generating);
        run.WorkFinished("synthetic-private-model-text");
        run.Finish(CompanionRunEnd.OAuthRejected, oauthCode: "synthetic-private-auth-value");
        var receipt = fixture.Secrets.ReadRunDiagnostic()!;
        Assert.Equal("UNKNOWN_FAILURE", receipt.LastWorkCode);
        Assert.Equal("protocol_error", receipt.OAuthCode);
        Assert.Equal(credential, fixture.Secrets.Load(fixture.Options.Origin.AbsoluteUri));
        var json = File.ReadAllText(fixture.DiagnosticPath);
        Assert.DoesNotContain("synthetic-private", json);
        Assert.DoesNotContain(credential.RefreshToken, json);
        Assert.DoesNotContain("example.invalid", json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(["BuildSha256", "End", "EndedUtc", "HandledJobs", "HttpStatus", "LastWorkCode", "OAuthCode", "Phase", "ProcessId", "ProfileId", "StartedUtc", "UpdatedUtc", "Version"],
            document.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Empty(Directory.EnumerateFiles(fixture.DirectoryPath, "pending-*"));
    }

    [Fact]
    public void ReplacingAReceiptKeepsCredentialPersistenceSeparateAndAStatusReadDoesNotChangeTheRecord()
    {
        using var fixture = new PrivateFixture();
        using var ownership = fixture.Secrets.Acquire();
        var run = new CompanionRunDiagnostics(fixture.Options.Profile, fixture.Secrets.WriteRunDiagnostic);
        Assert.Null(fixture.Secrets.ReadRunDiagnostic());
        run.Stage(CompanionRunPhase.Connecting);
        var initial = fixture.Secrets.ReadRunDiagnostic()!;
        Assert.Null(initial.End);
        Assert.Null(initial.EndedUtc); // An unfinished observation is not a liveness assertion.
        run.Stage(CompanionRunPhase.Submitting);
        run.WorkFinished(null);
        run.Finish(CompanionRunEnd.JobLimit);
        var bytes = File.ReadAllBytes(fixture.DiagnosticPath);
        var end = fixture.Secrets.ReadRunDiagnostic()!;
        Assert.Equal(CompanionRunEnd.JobLimit, end.End);
        Assert.Equal(1, end.HandledJobs);
        Assert.Equal("SUCCEEDED", end.LastWorkCode);
        Assert.Equal(bytes, File.ReadAllBytes(fixture.DiagnosticPath));
        Assert.False(fixture.Secrets.Exists);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AFailedDiagnosticSinkDoesNotCancelTheWorkersAuthorizedOperation(bool ioFailure)
    {
        var run = new CompanionRunDiagnostics(LocalProposalProtocol.Profile, _ => {
            if (ioFailure) throw new IOException("synthetic-private-path");
            throw new ArgumentException("synthetic-private-value");
        });
        Assert.Null(Record.Exception(() => run.Stage(CompanionRunPhase.Claiming)));
        Assert.Null(Record.Exception(() => run.Finish(CompanionRunEnd.Idle)));
    }

    [Theory]
    [InlineData("{invalid")]
    [InlineData("{\"Version\":1,\"ProcessId\":7,\"ProfileId\":\"synthetic-private-value\",\"BuildSha256\":null}")]
    [InlineData("{\"Version\":1,\"ProcessId\":7,\"ProfileId\":\"ollama-qwen3-8b\",\"BuildSha256\":null}")]
    public void MalformedOrUnboundLocalRecordsAreNotPrintedAsTrustedDiagnostics(string json)
    {
        using var fixture = new PrivateFixture();
        File.WriteAllText(fixture.DiagnosticPath, json);
        Assert.Null(fixture.Secrets.ReadRunDiagnostic());
    }

    [Theory]
    [InlineData("prompts")]
    [InlineData("nova")]
    public void ARecordCannotBeReadOrReplacedThroughTheOtherCapability(string capability)
    {
        using var fixture = new PrivateFixture(capability);
        using var ownership = fixture.Secrets.Acquire();
        new CompanionRunDiagnostics(fixture.Options.Profile, fixture.Secrets.WriteRunDiagnostic).Stage(CompanionRunPhase.Connecting);
        var original = fixture.Secrets.ReadRunDiagnostic()!;
        var otherProfile = capability == "prompts" ? LocalProposalProtocol.Profile : LocalWorkProtocol.Profile;
        var other = new CompanionSecrets(fixture.Options with { Profile = otherProfile });
        Assert.Null(other.ReadRunDiagnostic());
        Assert.Throws<ArgumentException>(() => other.WriteRunDiagnostic(original));
        Assert.Equal(original, fixture.Secrets.ReadRunDiagnostic());
    }

    private sealed class PrivateFixture : IDisposable
    {
        public CompanionOptions Options { get; }
        public CompanionSecrets Secrets { get; }
        public string DirectoryPath { get; }
        public string DiagnosticPath => Path.Combine(DirectoryPath, "last-run.json");
        public PrivateFixture(string capability = "prompts")
        {
            Options = CompanionOptions.Parse(["status", "--server", "https://example.invalid/", "--slot", "qa-" + Guid.NewGuid().ToString("N"), "--capability", capability]);
            Secrets = new(Options);
            var purpose = SHA256.HashData(Encoding.UTF8.GetBytes("QuickPrompt.Companion/1|" + Options.Origin.AbsoluteUri + "|" + Options.Slot));
            DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickPromptNova", "companion", Convert.ToHexString(purpose));
        }
        public void Dispose()
        {
            var parent = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickPromptNova", "companion"));
            var target = Path.GetFullPath(DirectoryPath);
            if (!target.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || new DirectoryInfo(target).LinkTarget is not null) throw new IOException("Synthetic cleanup path escaped its expected private root.");
            Directory.Delete(target, recursive: true);
        }
    }
}
