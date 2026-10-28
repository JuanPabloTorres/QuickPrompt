using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

// Experimental oracle belongs only to this executable. No product endpoint,
// runtime configuration or model tool can inject it into ordinary execution.
public static partial class RecoveryEvaluation
{
    public static async Task RunGold(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Pass frozen development corpus and new evidence path.");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var bytes = File.ReadAllBytes(args[0]); var data = JsonSerializer.Deserialize<Dataset>(bytes, json)!;
        if (!data.SyntheticOnly || data.Split != "development" || data.Scenarios.Length != 16)
            throw new ArgumentException("Only the complete synthetic development split is permitted.");
        using var lease = new FileStream(Path.Combine(Path.GetTempPath(), "QuickPrompt.Nova.SemanticModel.lease"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var entities = data.Fixtures.Select((f, i) => new SemanticEntity(Guid.Parse($"30000000-0000-0000-0000-{i + 1:000000000000}"), 1, f[0], f[1], false, InputNames: TemplateCompiler.Names(f[1]))).ToArray();
        using var fixture = new OwnedFixture(entities);
        using var inference = new PreparationInference("http://127.0.0.1:11434", semanticThinking: true, taskGoals: true, qwenThinkingSampling: true, taskResultContract: true);
        var planning = new NovaTaskPlanner(inference, new SqlitePromptRetriever(fixture.Store), new AgentSessions(), inference);
        var referenceDecisions = new List<object>();
        // This evaluator owns synthetic fixtures only. Preserve structured source
        // choices for causal diagnosis, never model thinking or private user data.
        inference.SemanticDiagnostic = (stage, content, rejected) => {
            if (stage == 180) referenceDecisions.Add(new { content, rejected });
        };
        var rows = new List<object>(); var passed = 0;
        foreach (var scenario in data.Scenarios)
        {
            referenceDecisions.Clear();
            var context = GoldContext(scenario, entities);
            var target = scenario.Target switch { "report" => entities[0], "recipe" => entities[1], null => null, _ => throw new ArgumentException("Unannotated oracle target.") };
            var required = scenario.Required.Select(Enum.Parse<SemanticOperation>).ToArray();
            var reference = target is null ? TargetReference.NONE : context.Selected?.Id == target.Id ? TargetReference.SELECTED
                : context.DraftSource?.Id == target.Id && context.ActiveDraft is not null ? TargetReference.PREVIOUS_RESULT
                : context.Selected is not null && context.Candidates.Count(candidate => candidate.Id != context.Selected.Id) == 1 ? TargetReference.OTHER_CANDIDATE
                : TargetReference.CANDIDATE;
            var authoring = required.Any(operation => operation is SemanticOperation.CREATE_DRAFT or SemanticOperation.PROPOSE_ADAPTATION);
            var outcome = Enum.Parse<SemanticOutcome>(scenario.Outcomes.Single());
            var constraints = new SemanticConstraints(scenario.Forbidden.Select(Enum.Parse<SemanticOperation>).ToArray(), true, null, null, [], [], false);
            // The frozen corpus lists minimum effects. READ from a Home search
            // also needs the source visible; OPEN is an allowed technical read
            // dependency, not new authoring or persistence authority.
            var compiledGoals = required.Contains(SemanticOperation.SEARCH_PROMPT) && outcome == SemanticOutcome.READ
                && !constraints.ForbiddenOperations.Contains(SemanticOperation.OPEN_PROMPT)
                ? required.Append(SemanticOperation.OPEN_PROMPT).Distinct().ToArray() : required;
            var meaning = new SemanticMeaning(compiledGoals, reference, target?.Title ?? "", target?.Id, target?.Title ?? "",
                authoring ? scenario.Message : "", constraints, outcome,
                outcome == SemanticOutcome.CLARIFICATION ? ["El fixture requiere una aclaración antes de actuar."] : [], false, "", null, "GOLD_GOAL: synthetic annotated effect/reference oracle, not a Qwen decision.");
            var inputPlan = SemanticPlanCompiler.Compile(meaning);
            inputPlan = inputPlan with { Goal = TaskGoal.Create(scenario.Message, context, inputPlan,
                [new(authoring ? TaskClauseRole.CONTENT : TaskClauseRole.WORKFLOW, scenario.Message)], required) };
            var runId = Guid.NewGuid(); using var trace = NovaTelemetry.Begin(runId);
            var timer = Stopwatch.StartNew(); var output = inputPlan; Guid? resolved = null; string? error = null;
            try
            {
                SemanticPlanValidator.Validate(inputPlan, context, scenario.Message);
                if (inputPlan.Has(SemanticOperation.RESOLVE_PROMPT))
                {
                    using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(180));
                    var resolution = await planning.Resolve(new(inputPlan, context, [], null, null), scenario.Message, Guid.NewGuid(), "qwen3:8b", null, cancel.Token);
                    output = resolution.Interpretation; resolved = resolution.Source?.Id;
                }
            }
            catch (Exception failure) { error = failure is PreparationToolException tool ? tool.Code + ": " + failure.Message : failure.Message; }
            var extra = output.Intentions.Except(ExpectedEffectCeiling(scenario)).ToArray();
            var missing = required.Except(output.Intentions).ToArray();
            var correct = error is null && missing.Length == 0 && extra.Length == 0 && output.DesiredOutcome == outcome
                && (target is null ? resolved is null : resolved == target.Id);
            if (correct) passed++;
            rows.Add(new { scenario.Id, scenario.Message, scenario.Context, mode = "GOLD_GOAL", expectedTarget = target?.Id,
                inputPlan, inputGoal = inputPlan.Goal, output, resolved, extra, missing, correct, error,
                elapsedMs = timer.ElapsedMilliseconds, referenceDecisions = referenceDecisions.ToArray(), performance = NovaTelemetry.Snapshot(runId) });
            File.WriteAllText(args[1], JsonSerializer.Serialize(new { status = rows.Count == 16 ? passed == 16 ? "PASS_PLANNING_ONLY" : "FAIL" : "RUNNING",
                mode = "GOLD_GOAL", datasetSha256 = Convert.ToHexString(SHA256.HashData(bytes)), count = rows.Count, passed,
                semanticSuccess = "NOT_TESTED", contentFidelity = "NOT_TESTED", browserExecution = "NOT_TESTED",
                fixtureUnchanged = fixture.Unchanged(), rows,
                limits = "Gold effect/reference annotations bypass only interpretation in this evaluator. Existing source resolver and its Qwen selector remain real. No content generation, React, approval or full task success is claimed." }, json));
            Console.WriteLine($"GOLD {scenario.Id} {(correct ? "PASS" : "FAIL")} {timer.ElapsedMilliseconds}ms");
        }
        if (passed != 16) Environment.ExitCode = 1;
    }

    private static SemanticContext GoldContext(Case scenario, SemanticEntity[] entities)
    {
        var a = entities[0]; var b = entities[1];
        return scenario.Family == "missing" ? SemanticContext.Empty : scenario.Context switch
        {
            "selected_report" => SemanticContext.Empty with { Selected = a },
            "selected_recipe" => SemanticContext.Empty with { Selected = b },
            "previous_report" => SemanticContext.Empty with { Selected = b, DraftSource = a, ActiveDraft = a.Text, Candidates = [a], Approval = "NOT_GRANTED" },
            "two_candidates" => SemanticContext.Empty with { Selected = a, DraftSource = a, Candidates = [a, b] },
            "ambiguous" => SemanticContext.Empty with { Candidates = [entities[2], entities[3]] },
            _ => SemanticContext.Empty with { Candidates = [a, b] }
        };
    }
}
