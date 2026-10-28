using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

// A test-owned adapter into the existing reviewer. No new runtime critic or
// public application API; the actual creation context and proposal are fixed.
internal static class AuthoringReviewEvaluation
{
    private sealed record Case(string Id, string Message, string ReviewOnlyBody, string ExpectedReview, string? Source);
    private sealed record Dataset(bool SyntheticOnly, Case[] Cases);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task Run(string[] args)
    {
        if (args.Length != 3 || args[2] is not ("production" or "qwen-thinking-sampling"))
            throw new ArgumentException("Use --authoring-review development.json new-report.json production|qwen-thinking-sampling");
        var root = Path.GetFullPath(Environment.CurrentDirectory);
        var fixture = Path.GetFullPath(args[0]); var output = Path.GetFullPath(args[1]);
        if (!fixture.Equals(Path.Combine(root, "Tests", "nova", "datasets", "authoring-authority-v1", "development.json"), StringComparison.OrdinalIgnoreCase)
            || !output.StartsWith(Path.Combine(root, "docs", "evidence") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || File.Exists(output)) throw new ArgumentException("Only the frozen synthetic development set and a new owned output are allowed.");
        var bytes = await File.ReadAllBytesAsync(fixture);
        var data = JsonSerializer.Deserialize<Dataset>(bytes, Json) ?? throw new ArgumentException("Invalid fixture");
        if (!data.SyntheticOnly || data.Cases.Length == 0 || data.Cases.Any(item => item.Source is not null || item.ExpectedReview is not ("ACCEPT" or "REJECT"))
            || data.Cases.Select(item => item.Id).Distinct().Count() != data.Cases.Length) throw new ArgumentException("Creation-only synthetic cases required.");
        var reviewer = typeof(PreparationInference).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method => method.Name == "ReviewDraftFidelity" && method.GetParameters().Length == 5 && method.ReturnType == typeof(Task));
        await using var lease = new FileStream(Path.Combine(Path.GetTempPath(), "QuickPrompt.Nova.SemanticModel.lease"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var gateway = new HttpClient { BaseAddress = new("http://127.0.0.1:11434"), Timeout = TimeSpan.FromSeconds(5) };
        var tags = JsonSerializer.Deserialize<JsonElement>(await gateway.GetStringAsync("api/tags"));
        var model = tags.GetProperty("models").EnumerateArray().Single(item => item.GetProperty("name").GetString() == "qwen3:8b").Clone();
        var sampling = args[2] == "qwen-thinking-sampling";
        var rows = new List<object>(); var correct = 0;
        async Task Save(string state) => await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new {
            state, scope = "ACTUAL_CREATION_REVIEWER_CONTROLLED_ARTIFACT_NOT_GENERATION_OR_UI", syntheticOnly = true, acceptanceComplete = false,
            fixtureSha256 = Convert.ToHexString(SHA256.HashData(bytes)), profile = args[2], qwenThinkingSampling = sampling,
            semanticThinking = true, contextLength = 12288, model,
            reviewerAssemblySha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(typeof(DraftFidelityReviewer).Assembly.Location))),
            evaluatorAssemblySha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(typeof(AuthoringReviewEvaluation).Assembly.Location))),
            evaluated = rows.Count, correct, total = data.Cases.Length, rows
        }, Json));
        await Save("RUNNING");
        foreach (var item in data.Cases) {
            using var inference = new PreparationInference("http://127.0.0.1:11434", semanticThinking: true, qwenThinkingSampling: sampling, authoringUseContract: true);
            var reviews = new List<JsonElement>();
            var repairReceipts = new List<JsonElement>();
            inference.SemanticDiagnostic = (stage, text, _) => {
                if (stage is not (190 or 191)) return;
                var target = stage == 190 ? reviews : repairReceipts;
                try { target.Add(JsonSerializer.Deserialize<JsonElement>(text)); }
                catch (JsonException) { target.Add(JsonSerializer.SerializeToElement(new { invalidReview = text })); }
            };
            var plan = SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT], TargetReference.NONE, "", null, "", item.Message,
                new([], true, null, null, [], [], false), SemanticOutcome.NEW_DRAFT, [], false, "", null, "Controlled creation-context review")) with {
                Authoring = new(InstructionAuthorship.CREATE_TEMPLATE, item.Message, item.Message, LiveRuns.Hash(item.Message))
            };
            plan = plan with { Goal = TaskGoal.FromAuthoring(item.Message, SemanticContext.Empty, plan) };
            var input = new PrepareInput(Guid.NewGuid(), item.Message) { Interpretation = plan };
            var proposal = new PromptProposal(PreparationDecision.CREATE_NEW, null, "Artefacto sintético", "", item.ReviewOnlyBody, [], "", "Controlled artifact",
                TemplateCompiler.Names(item.ReviewOnlyBody));
            var timer = Stopwatch.StartNew(); var accepted = false; var validVerdict = true; string? error = null; string? code = null;
            try {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                await (Task)reviewer.Invoke(inference, [input, proposal, Array.Empty<RetrievalCandidate>(), timeout.Token, null])!;
                accepted = true;
            } catch (ValidationException failure) {
                code = failure.Data["NovaValidationCode"] as string; error = failure.Message;
                validVerdict = code == "CONTENT_FIDELITY_MISMATCH";
            } catch (Exception failure) {
                validVerdict = false; error = failure.GetBaseException().GetType().Name;
            }
            var match = validVerdict && accepted == (item.ExpectedReview == "ACCEPT");
            if (match) correct++;
            rows.Add(new { item.Id, item.Message, candidate = item.ReviewOnlyBody, expected = item.ExpectedReview, accepted, validVerdict,
                correct = match, error, code, elapsedMs = timer.ElapsedMilliseconds, reviews, repairReceipts });
            Console.WriteLine(JsonSerializer.Serialize(new { item.Id, profile = args[2], accepted, validVerdict, correct = match, elapsedMs = timer.ElapsedMilliseconds }));
            await Save("RUNNING");
        }
        await Save(correct == data.Cases.Length ? "PASS_REVIEW_COMPONENT_ONLY" : "FAIL_REVIEW_COMPONENT");
        Environment.ExitCode = correct == data.Cases.Length ? 0 : 2;
    }
}
