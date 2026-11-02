using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;
using static QuickPrompt.Accounts.Tests.AccountHttpTests;

namespace QuickPrompt.Accounts.Tests;

/// <summary>
/// Captures one frozen real-provider sample. Test completion is transport evidence;
/// free-text quality is assessed separately against the evaluator-only checks.
/// Never runs authenticated Companion, database effects or a browser.
/// </summary>
public sealed class MvpFunctionalRealProviderTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task CaptureFrozenTwentyCaseMvpSampleWithoutSelectingRetries()
    {
        static string Required(string key) => Environment.GetEnvironmentVariable(key) is { Length: > 0 } value
            ? value : throw new InvalidOperationException("Missing explicit campaign setting: " + key);
        var fixture = Required("QUICKPROMPT_TEST_MVP_CASES");
        var directory = Required("QUICKPROMPT_TEST_MVP_EVIDENCE");
        var sourceRoot = Required("QUICKPROMPT_TEST_SOURCE_ROOT");
        var selectedFamily = Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_MVP_FAMILY");
        if (selectedFamily is not (null or "adapt" or "search" or "execute")) throw new InvalidDataException("Unknown component contrast.");
        var fixtureBytes = await File.ReadAllBytesAsync(fixture);
        var fixtureHash = Convert.ToHexString(SHA256.HashData(fixtureBytes));
        Assert.Equal(Required("QUICKPROMPT_TEST_MVP_CASES_SHA256"), fixtureHash);
        var cases = JsonSerializer.Deserialize<SampleCase[]>(fixtureBytes, Json)!;
        Assert.Equal(20, cases.Length);
        Assert.Equal(20, cases.Select(item => item.Id).Distinct().Count());
        foreach (var group in new[] { "known", "holdout" }) {
            Assert.Equal(10, cases.Count(item => item.Group == group));
            Assert.Equal(4, cases.Count(item => item.Group == group && item.Family == "adapt"));
            Assert.Equal(3, cases.Count(item => item.Group == group && item.Family == "search"));
            Assert.Equal(3, cases.Count(item => item.Group == group && item.Family == "execute"));
        }
        Directory.CreateDirectory(directory);
        var selectedCases = cases.Where(item => selectedFamily is null || item.Family == selectedFamily).ToArray();
        Assert.All(selectedCases, item => Assert.False(File.Exists(Path.Combine(directory, item.Id + ".json")), "Never overwrite a sampled case."));
        using var lease = new FileStream(Path.Combine(Path.GetTempPath(), "QuickPrompt.Nova.SemanticModel.lease"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        await using var profile = await CapturedThinkingProfile.Start(directory);
        var failures = new List<string>();
        foreach (var item in selectedCases) {
            var watch = Stopwatch.StartNew();
            var caseDirectory = Path.Combine(directory, item.Id);
            Directory.CreateDirectory(caseDirectory);
            using var observer = new DevelopmentLabObserver(true, caseDirectory, sourceRoot);
            var maximumCalls = item.Family == "adapt" ? 2 : 1;
            var transport = new CaptureTransport(RealProvider(observer), maximumCalls);
            object? output = null; Exception? error = null;
            try {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                output = item.Family switch {
                    "adapt" => await Adapt(item, transport, deadline.Token),
                    "search" => await Search(item, transport, deadline.Token),
                    "execute" => await Execute(item, transport, deadline.Token),
                    _ => throw new InvalidDataException("Unknown frozen family.")
                };
                Assert.InRange(transport.Calls.Count, 1, maximumCalls);
            } catch (Exception failure) { error = failure; failures.Add(item.Id); }
            watch.Stop();
            var record = new {
                schema = "quickprompt-mvp-real-sample/1", item.Id, item.Group, item.Family, fixtureHash,
                sampleScope = selectedFamily ?? "all-twenty", fullSample = selectedFamily is null,
                status = error is null ? "CAPTURED_REQUIRES_SEMANTIC_REVIEW" : "TECHNICAL_FAILURE",
                semanticPassed = (bool?)null, item, output, calls = transport.Calls,
                maximumTransportCalls = maximumCalls, timeoutSeconds = 90, elapsedMilliseconds = watch.ElapsedMilliseconds,
                error = error?.GetType().Name, message = error?.Message, sampledOnce = true,
                scope = "Production generator/selector/execution packet and real configured provider; synthetic state and human confirmations. No HTTP accounts, database, browser, installed Companion or public acceptance."
            };
            using var file = new FileStream(Path.Combine(directory, item.Id + ".json"), FileMode.CreateNew, FileAccess.Write);
            await JsonSerializer.SerializeAsync(file, record, Json);
        }
        Assert.True(failures.Count == 0, "Technical failures are retained in: " + string.Join(", ", failures));
    }

    private static async Task<object> Adapt(SampleCase item, CaptureTransport transport, CancellationToken cancellation)
    {
        var content = item.Established ?? throw new InvalidDataException("A frozen human task is required.");
        var sourceText = string.Join("\n", new[] { content.Task }.Concat(content.Steps).Concat(content.Constraints)
            .Concat(content.OutputRequirements).Concat(content.References.Select(reference => reference.Uri)));
        var source = Document(content.Title, sourceText, content.Description);
        RetrievalCandidate[] candidates = [new(source, 1, "Fuente sintética elegida explícitamente")];
        var context = SemanticContext.Empty with { Selected = SemanticEntity.From(source) };
        var plan = new LiveCommand(SemanticOperation.PROPOSE_ADAPTATION, item.Request) { InputNames = item.Names! }.Compile(item.Request, context);
        var input = new PrepareInput(Guid.NewGuid(), item.Request, SourcePromptId: source.Id, SourceRevision: source.Revision) { Interpretation = plan };
        var generator = new PromptDraftGenerator(transport, new NoExtraction(),
            translationSourceViews: ApertiumTranslationSourceViews.Docker(
                "sha256:916315150c8a7a047a9bcb9950ede51bf2fc1ea0c0a8cfe3a1b9dc1d2545a880"));
        Task<PromptProposal> Generate(PrepareInput current) => generator.Generate(current, plan.Preparation(), candidates, cancellation);
        var gate = await Generate(input);
        Assert.Equal(AuthoringBriefs.EstablishTask, gate.Brief!.Stage);
        Assert.Empty(transport.Calls);
        input = input with { ConfirmedBrief = AuthoringBriefs.Confirm(gate.Brief, new(gate.Brief.BasisHash, content, true)) };
        var proposed = await Generate(input);
        // An unnecessary model question is retained, never answered with an invented human reply.
        if (proposed.Brief?.Suggested is not { } suggestion)
            return new { complete = false, reason = "NO_FIRST_SUGGESTION", proposed };
        Assert.Equal(LiveRuns.Hash(content), LiveRuns.Hash(proposed.Brief.Proposed));
        Assert.Null(plan.Goal!.AuthoringContract);
        var callsBeforeConfirmation = transport.Calls.Count;
        var selected = AuthoringBriefs.Confirm(proposed.Brief, new(proposed.Brief.BasisHash, suggestion, true));
        var result = await Generate(input with { ConfirmedBrief = selected });
        Assert.Equal(callsBeforeConfirmation, transport.Calls.Count);
        var composedExactly = result.TemplateText == selected.Render();
        Assert.False(plan.Goal.HasHumanContentApproval(result.TemplateText));
        static int InstructionLength(AuthoringBriefContent value) => value.Task.Length
            + value.Steps.Sum(text => text.Length) + value.Constraints.Sum(text => text.Length)
            + value.OutputRequirements.Sum(text => text.Length);
        return new { complete = result.Decision == PreparationDecision.ADAPT_EXISTING, content,
            sourceText, firstSuggestion = suggestion, usefulShortening = InstructionLength(suggestion) < InstructionLength(content),
            originalInstructionLength = InstructionLength(content), proposedInstructionLength = InstructionLength(suggestion),
            syntheticHumanSelectedUnmodifiedSuggestion = true, composedExactly, result,
            noPersistenceOrExecutionApproved = true };
    }

    private static async Task<object> Search(SampleCase item, CaptureTransport transport, CancellationToken cancellation)
    {
        var entries = item.Candidates!.Select(candidate => new { candidate.Key, Prompt = Document(candidate.Title, candidate.Template, candidate.Description) }).ToArray();
        var candidates = entries.Select(entry => new RetrievalCandidate(entry.Prompt, 1, "Candidato sintético del ámbito autorizado")).ToArray();
        var choice = await new SemanticCandidateSelector(transport).Select(new SourceLookupContext(item.Request, item.Request), candidates, "qwen3:8b", cancellation);
        var actualKeys = choice.PromptIds.Select(id => entries.Single(entry => entry.Prompt.Id == id).Key).Order().ToArray();
        return new { choice, actualKeys, expectedKeys = item.ExpectedKeys, complete = actualKeys.SequenceEqual(item.ExpectedKeys!.Order()),
            scope = "Candidate set membership only; catalog recall, account isolation and user resolution are separate acceptance controls." };
    }

    private static async Task<object> Execute(SampleCase item, CaptureTransport transport, CancellationToken cancellation)
    {
        var compiled = EditorTools.Compile(item.Template!, item.Values!);
        Assert.Empty(compiled.MissingVariables); Assert.NotNull(compiled.Text);
        // Explicit work-v2 candidate. The original v1 remains the installed/public default until its separate acceptance.
        var packet = LocalExecutionFrame.Packet(LocalExecutionContext.Create(item.Template!, item.Values!), compiled.Text!);
        using var reply = await transport.Send(packet, cancellation, "companion.inference");
        var text = reply.RootElement.GetProperty("message").GetProperty("content").GetString();
        Assert.False(string.IsNullOrWhiteSpace(text));
        return new { compiledInstruction = compiled.Text, text, complete = true,
            scope = "Execution model packet only; factual review, queue, history and persistence not exercised." };
    }

    private static PromptDocument Document(string title, string body, string description) =>
        new(Guid.NewGuid(), title, description, body, 0, false, false, 1, DateTime.UtcNow, DateTime.UtcNow, 0, null, [], new Dictionary<string, string>());

    private sealed class NoExtraction : IInputValueExtractor
    {
        public Task<InputValueExtraction> ExtractInputs(PrepareInput input, string template, string[] names, CancellationToken cancellation) =>
            throw new InvalidOperationException("This campaign never supplies case values during authoring.");
    }

    private sealed class CaptureTransport(IValidatedAgentModelTransport provider, int maximumCalls) : IValidatedAgentModelTransport
    {
        public List<object> Calls { get; } = [];
        public async Task<JsonDocument> Send(object packet, CancellationToken cancellation, string purpose)
        {
            if (Calls.Count >= maximumCalls) throw new InvalidOperationException("Frozen transport budget exhausted.");
            var sent = JsonSerializer.SerializeToElement(packet, Json);
            var watch = Stopwatch.StartNew();
            try {
                var response = await provider.Send(packet, cancellation, purpose);
                Calls.Add(new { purpose, packet = sent, response = response.RootElement.Clone(), elapsedMilliseconds = watch.ElapsedMilliseconds });
                return response;
            } catch {
                Calls.Add(new { purpose, packet = sent, failed = true, elapsedMilliseconds = watch.ElapsedMilliseconds });
                throw;
            }
        }
        public bool UsesThinking(string model) => provider.UsesThinking(model);
        public void Observe(int stage, string content, string? error) => provider.Observe(stage, content, error);
        public void Reject(string? answer) => provider.Reject(answer);
    }

    private sealed record CandidateCase(string Key, string Title, string Description, string Template);
    private sealed record SampleCase(string Id, string Group, string Family, string Request, string[] Checks, string Origin,
        string[]? Names, AuthoringBriefContent? Established, CandidateCase[]? Candidates, string[]? ExpectedKeys,
        string? Template, Dictionary<string, string>? Values);
}
