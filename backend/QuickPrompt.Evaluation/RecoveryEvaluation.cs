using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using QuickPrompt.Nova.Evaluation.Retired;
using QuickPrompt.Nova.Evaluation;

public static partial class RecoveryEvaluation
{
    private record Case(string Id, string Family, string Context, string Message, string[] Required, string[] Outcomes, string[] Forbidden, string? Target, string? MetamorphicGroup, Dictionary<string, string>? ExpectedValues = null, string? ExpectedProviderId = null);
    private record Dataset(string Version, bool SyntheticOnly, string Split, string[][] Fixtures, Case[] Scenarios);
    // Oracle over typed fixture labels, never natural-language routing. Required
    // goals authorize their technical prerequisites, not other terminal results.
    private static HashSet<SemanticOperation> ExpectedEffectCeiling(Case scenario)
    {
        var expected = scenario.Required.Select(Enum.Parse<SemanticOperation>).ToHashSet();
        bool Any(params SemanticOperation[] operations) => operations.Any(expected.Contains);
        var authoring = Any(SemanticOperation.CREATE_DRAFT, SemanticOperation.PROPOSE_ADAPTATION);
        var readiness = Any(SemanticOperation.COPY_PROMPT, SemanticOperation.PREPARE_LOCAL_EXECUTION, SemanticOperation.PREPARE_PROVIDER, SemanticOperation.EXECUTE_LOCAL);
        if (Any(SemanticOperation.SEARCH_PROMPT, SemanticOperation.OPEN_PROMPT, SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.FILL_VARIABLES,
                SemanticOperation.REQUEST_SAVE_VERSION, SemanticOperation.REQUEST_SAVE_COPY) || readiness || scenario.Target is not null && authoring)
            expected.UnionWith([SemanticOperation.SEARCH_PROMPT, SemanticOperation.RESOLVE_PROMPT, SemanticOperation.OPEN_PROMPT]);
        if (authoring) expected.Add(SemanticOperation.VALIDATE_DRAFT);
        if (authoring || readiness || expected.Contains(SemanticOperation.FILL_VARIABLES)) expected.Add(SemanticOperation.COMPILE_PROMPT);
        if (readiness) expected.Add(SemanticOperation.FILL_VARIABLES); // only literal grounded values may actually be applied
        if (expected.Contains(SemanticOperation.EXECUTE_LOCAL)) expected.Add(SemanticOperation.PREPARE_LOCAL_EXECUTION);
        expected.ExceptWith(scenario.Forbidden.Select(Enum.Parse<SemanticOperation>));
        expected.Add(SemanticOperation.ASK_CLARIFICATION); // safe suspension, not a user effect or completion
        return expected;
    }
    public static Task Run(string[] args) => RunCore(args, false);
    public static Task RunReflected(string[] args) => RunCore(args, true);
    private static async Task RunCore(string[] args, bool reflected)
    {
        if (args.Length < 2) throw new ArgumentException("Pass versioned split dataset and report paths; optional case IDs are diagnostic only.");
        var core = Environment.GetEnvironmentVariable("NOVA_EVAL_RUNTIME") == "adaptive-core";
        var labRoot = Environment.GetEnvironmentVariable("NOVA_EVAL_LAB_ROOT");
        if (labRoot is not null && !core) throw new ArgumentException("Lab requires the explicit Core evaluation profile.");
        if (core && reflected) throw new ArgumentException("Select Core or reflected evaluation, not both.");
        var entryPoint = reflected || core ? "application" : Environment.GetEnvironmentVariable("NOVA_EVAL_PLANNING_ENTRY") ?? "component";
        if (entryPoint is not ("component" or "application"))
            throw new ArgumentException("NOVA_EVAL_PLANNING_ENTRY must be component or application.");
        using var lease = new FileStream(Path.Combine(Path.GetTempPath(), "QuickPrompt.Nova.SemanticModel.lease"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var bytes = File.ReadAllBytes(args[0]); var data = JsonSerializer.Deserialize<Dataset>(bytes, json)!;
        if (!data.SyntheticOnly) throw new ArgumentException("Synthetic fixtures required.");
        var gateBytes = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(args[0])!, "thresholds.json"));
        using var gateDoc = JsonDocument.Parse(gateBytes); var gate = gateDoc.RootElement;
        var scenarios = data.Scenarios.Where(s => args.Length < 3 || args[2].Split(',').Contains(s.Id)).ToArray();
        if (scenarios.Length == 0) throw new ArgumentException("No matching cases.");
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(5) };
        var before = JsonSerializer.Deserialize<JsonElement>(await client.GetStringAsync("http://127.0.0.1:11434/api/ps"));
        var evaluatedModel = await EvaluationLocalModel.Load(client, "http://127.0.0.1:11434");
        var model = evaluatedModel.Name;
        var digest = evaluatedModel.Digest;
        var goalMode = Environment.GetEnvironmentVariable("NOVA_EVAL_TASK_GOALS") != "false";
        var resultContract = Environment.GetEnvironmentVariable("NOVA_EVAL_RESULT_CONTRACT") == "true";
        var authoringUseContract = Environment.GetEnvironmentVariable("NOVA_EVAL_AUTHORING_CONTRACT") == "true";
        if (authoringUseContract && goalMode)
            throw new ArgumentException("Authoring-contract qualification requires NOVA_EVAL_TASK_GOALS=false; do not measure another adapter under this label.");
        var qwenSampling = Environment.GetEnvironmentVariable("NOVA_EVAL_QWEN_SAMPLING") == "true";
        if (qwenSampling && model != "qwen3:8b") throw new ArgumentException("The Qwen3 sampling experiment is not a profile for another model family.");
        var runtimeHashes = new[] { typeof(PreparationInference).Assembly, typeof(AdaptiveCoreModelTransport).Assembly, typeof(TaskGoal).Assembly, typeof(TemplateCompiler).Assembly }
            .ToDictionary(a => Path.GetFileName(a.Location), a => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))));
        using var transport = reflected || core ? new OllamaAgentModelTransport("http://127.0.0.1:11434", qwenThinkingSampling: qwenSampling, modelPolicy: evaluatedModel.Policy) : null;
        using var inference = reflected || core ? null : new PreparationInference("http://127.0.0.1:11434", semanticThinking: true, taskGoals: goalMode, qwenThinkingSampling: qwenSampling,
            taskResultContract: resultContract, modelPolicy: evaluatedModel.Policy, authoringUseContract: authoringUseContract);
        // Fixed synthetic IDs make A/B identity and context packets comparable; no personal repository access.
        var entities = data.Fixtures.Select((f, i) => new SemanticEntity(Guid.Parse($"30000000-0000-0000-0000-{i + 1:000000000000}"), 1, f[0], f[1], false, InputNames: TemplateCompiler.Names(f[1]))).ToArray();
        using var fixture = new OwnedFixture(entities);
        var sessions = new AgentSessions();
        var retriever = new SqlitePromptRetriever(fixture.Store);
        AdaptiveCoreOptions? coreOptions = null;
        if (core) {
            coreOptions = new(Environment.GetEnvironmentVariable("NOVA_CORE_PYTHON") ?? "",
                Path.GetFullPath(Environment.GetEnvironmentVariable("NOVA_CORE_WORKER") ?? "agents/nova-core/nova_worker.py"),
                Path.GetFullPath(Environment.GetEnvironmentVariable("NOVA_CORE_SDK") ?? "../Adaptive-AI-Core"),
                "http://127.0.0.1:11434", QwenThinkingSamplingEnabled: qwenSampling);
            coreOptions.Validate();
        }
        // Record only the public proposal on this explicitly synthetic fixture.
        IValidatedAgentModelTransport? coreTransport = core ? new AdaptiveCoreModelTransport(coreOptions!, evaluatedModel.Policy) : null;
        var labTransport = labRoot is null ? null : new LabCoreTransport(coreOptions!, labRoot, coreTransport!);
        var observedCore = core ? new ObservedCoreTransport(labTransport ?? coreTransport!, coreOptions!) : null;
        ISemanticIntentInterpreter interpreter = core ? new AdaptiveCoreInterpreter(coreOptions!, observedCore) : reflected ? new ReflectedSemanticAgent(transport!, retriever, new()) : inference!;
        ICandidateSelector selector = core ? new SemanticCandidateSelector(observedCore!) : (ICandidateSelector)interpreter;
        var planning = new NovaTaskPlanner(interpreter, retriever, sessions, selector);
        var adapter = new NovaPromptStore(fixture.Store);
        var library = new PromptLibrary(adapter, adapter);
        var rows = new List<object>();
        var samples = new List<(Case Case, SemanticInterpretation? Plan, bool Correct, bool IntentCorrect, bool TargetCorrect, bool Invented, bool Unauthorized, bool Extra, bool Missing, long Ms)>();
        foreach (var scenario in scenarios) {
            labTransport?.Configure(scenario);
            if (observedCore is not null) observedCore.CaseId = scenario.Id;
            using var debugPoints = Environment.GetEnvironmentVariable("NOVA_EVAL_DEBUG_POINTS") == "1" ? NovaTelemetry.CaptureDebugPoints() : null;
            var attempts = new List<object>();
            Action<int, string, string?> diagnostic = (attempt, content, rejected) => {
                // Explicitly synthetic evaluation only. The production interpreter has no subscriber.
                try { attempts.Add(new { attempt, contract = JsonSerializer.Deserialize<JsonElement>(content), rejected }); }
                catch (JsonException) { attempts.Add(new { attempt, invalidJsonHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content))) }); }
            };
            if (observedCore is not null) observedCore.Diagnostic = diagnostic;
            else if (transport is not null) transport.SemanticDiagnostic = diagnostic;
            else inference!.SemanticDiagnostic = diagnostic;
            var a = entities[0]; var b = entities[1];
            var context = scenario.Context switch {
                "selected_report" => SemanticContext.Empty with { Selected = a },
                "selected_recipe" => SemanticContext.Empty with { Selected = b },
                "previous_report" => SemanticContext.Empty with { Selected = b, DraftSource = a, ActiveDraft = a.Text, Candidates = [a], Approval = "NOT_GRANTED" },
                "two_candidates" => SemanticContext.Empty with { Selected = a, DraftSource = a, Candidates = [a, b] },
                "ambiguous" => SemanticContext.Empty with { Candidates = [entities[2], entities[3]] },
                _ => SemanticContext.Empty with { Candidates = [a, b] }
            };
            // Missing pronoun has no antecedent despite a library containing unrelated saved records.
            if (scenario.Family == "missing") context = SemanticContext.Empty;
            var runId = Guid.NewGuid(); using var trace = NovaTelemetry.Begin(runId);
            if (core) LabEvent(scenario.Id, runId, "input", "RUNNING",
                new { message = Environment.GetEnvironmentVariable("NOVA_EVAL_LAB_PUBLIC_INPUTS") == "1" ? scenario.Message : null,
                    scenario.Context, scenario.Required, scenario.Forbidden, model });
            var timer = Stopwatch.StartNew(); SemanticInterpretation? plan = null; string? error = null; string? errorDetail = null;
            SemanticInterpretation? interpreted = null; Guid? resolvedId = null;
            var sessionId = Guid.NewGuid();
            try {
                using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(180));
                SemanticResolution resolution;
                if (entryPoint == "application") {
                    // Seed only fixture context roles, never expected targets/effects.
                    // Reuse the same session restoration and source-first path as UI.
                    sessions.Put(FixtureSession(sessionId, context, library));
                    resolution = await planning.Interpret(scenario.Message, sessionId, null, null, model, null, cancel.Token);
                } else {
                    var componentPlan = await interpreter.Interpret(scenario.Message, context, model, cancel.Token);
                    resolution = new(componentPlan, context, [], null, null);
                }
                plan = interpreted = resolution.Interpretation;
                if (core) LabEvent(scenario.Id, runId, "contract", "OBSERVED", new {
                    check = "compiled_plan", effects = plan.Intentions, source = plan.TargetReference,
                    plan.ResolvedTargetId, plan.DesiredOutcome,
                    requested = plan.Goal?.RequestedEffects,
                    meaning = "Observed compiled proposal; semantic authorization is checked independently below." },
                    kind: "plan_compiled");
                if (plan.Has(SemanticOperation.RESOLVE_PROMPT) || entryPoint == "application") {
                    resolution = await planning.Resolve(resolution, scenario.Message, sessionId, model, null, cancel.Token);
                    plan = resolution.Interpretation; resolvedId = resolution.Source?.Id;
                }
            }
            catch (Exception failure) { error = failure is PreparationToolException tool ? tool.Code : failure.GetType().Name; errorDetail = failure.Message; }
            finally { sessions.Forget(sessionId); }
            var actual = plan?.Intentions.Select(x => x.ToString()).ToArray() ?? [];
            var expectedId = scenario.Target == "report" ? a.Id : scenario.Target == "recipe" ? b.Id : (Guid?)null;
            // Application planning reports materialized/bound identities. Inferring
            // fixture B for an unresolved OTHER_CANDIDATE fabricates an observation.
            var bound = resolvedId ?? plan?.ResolvedTargetId ?? (entryPoint == "application" ? null : plan?.TargetReference switch { TargetReference.SELECTED => context.Selected?.Id,
                TargetReference.PREVIOUS_RESULT or TargetReference.ACTIVE_DRAFT => context.DraftSource?.Id,
                TargetReference.OTHER_CANDIDATE => context.Candidates.Where(candidate => candidate.Id != (context.DraftSource?.Id ?? context.Selected?.Id))
                    .Select(candidate => (Guid?)candidate.Id).Distinct().ToArray() is [var only] ? only : null, _ => null });
            var targetCorrect = LabCheck(scenario.Id, runId, "source_identity", error is null && (expectedId is null ? plan is not null && bound is null && (scenario.Family is not ("ambiguity" or "missing") || plan.DesiredOutcome == SemanticOutcome.CLARIFICATION)
                : bound == expectedId), new { expectedId, observedId = bound, error });
            var invented = interpreted?.ResolvedTargetId is { } claimed && !(entryPoint == "application" ? entities.Any(entity => entity.Id == claimed) : context.Ids.Contains(claimed))
                || plan?.ResolvedTargetId is { } id && !entities.Any(entity => entity.Id == id);
            var unauthorized = plan is not null && scenario.Forbidden.Any(actual.Contains);
            var intentCorrect = LabCheck(scenario.Id, runId, "requested_outcomes", error is null && plan is not null && scenario.Required.All(actual.Contains) && !unauthorized && !invented && scenario.Outcomes.Contains(plan.DesiredOutcome.ToString()),
                new { expected = scenario.Required, observed = actual, outcomes = scenario.Outcomes, observedOutcome = plan?.DesiredOutcome.ToString(), unauthorized, invented, error });
            var observedValues = plan?.InputScope is { } valueScope && valueScope.Matches(scenario.Message)
                ? valueScope.Values(scenario.Message, valueScope.Names) : null;
            var valuesCorrect = scenario.ExpectedValues is null || LabCheck(scenario.Id, runId, "bound_values",
                observedValues is not null && scenario.ExpectedValues.Count == observedValues.Count && scenario.ExpectedValues.All(pair => observedValues.TryGetValue(pair.Key, out var value) && value == pair.Value),
                Environment.GetEnvironmentVariable("NOVA_EVAL_LAB_PUBLIC_INPUTS") == "1" ? new { expected = (Dictionary<string,string>?)scenario.ExpectedValues, observed = observedValues } : new { expected = (Dictionary<string,string>?)null, observed = (Dictionary<string,string>?)null });
            var providerCorrect=scenario.ExpectedProviderId is null || LabCheck(scenario.Id,runId,"provider_identity",
                error is null && plan?.ProviderId==scenario.ExpectedProviderId,new {expected=scenario.ExpectedProviderId,observed=plan?.ProviderId});
            var correct = intentCorrect && targetCorrect && valuesCorrect && providerCorrect;
            var expectedCeiling = ExpectedEffectCeiling(scenario);
            var extraPlannedEffects = error is null && plan is not null ? plan.Intentions.Except(expectedCeiling).Select(operation => operation.ToString()).ToArray() : null;
            var missingRequiredEffects = scenario.Required.Except(error is null ? actual : []).ToArray();
            samples.Add((scenario, error is null ? plan : null, correct, intentCorrect, targetCorrect, invented, unauthorized,
                extraPlannedEffects is { Length: > 0 }, missingRequiredEffects.Length > 0, timer.ElapsedMilliseconds));
            rows.Add(new { scenario.Id, scenario.Family, scenario.Context, scenario.Message, expected = new { scenario.Required, scenario.Forbidden, scenario.Outcomes, expectedId, values = scenario.ExpectedValues,providerId=scenario.ExpectedProviderId },
                runId, attempts, labGuard = labTransport?.LastGuard, interpreted, plan, resolvedId,
                // Supplementary strict oracle: unchanged frozen metrics above still
                // report forbidden subsets; these roots also expose unsolicited goals.
                requestedEffects = plan?.Goal?.RequestedEffects.Select(operation => operation.ToString()).ToArray(),
                extraRequestedEffects = plan?.Goal?.RequestedEffects.Select(operation => operation.ToString()).Except(scenario.Required).ToArray(),
                expectedEffectCeiling = expectedCeiling.Select(operation => operation.ToString()).ToArray(), extraPlannedEffects, missingRequiredEffects,
                effectScopePass = extraPlannedEffects is null ? (bool?)null : extraPlannedEffects.Length == 0,
                goal = plan?.Goal is { } g ? new { g.GoalId, g.TargetReferences, g.RequestedEffects, g.RequiredEffects, g.ForbiddenEffects, g.CompletionConditions, g.ApprovalRequirements,
                    intentContract = g.IntentContract is { } accepted ? new { accepted.RequestHash, accepted.RequestedEffects, accepted.RequiredOutcomes, accepted.ForbiddenEffects } : null,
                    scopedContent = g.ContentRequest, g.Clauses } : null, correct, intentCorrect, targetCorrect, invented, unauthorized, error,
                observedValues, valuesCorrect, providerCorrect, errorDetail, elapsedMs = timer.ElapsedMilliseconds, performance = NovaTelemetry.Snapshot(runId),
                debugPoints = debugPoints?.Snapshot(), debugPointsDropped = debugPoints?.Dropped });
            Console.WriteLine($"{scenario.Id} {(correct && extraPlannedEffects is {Length:0} ? "PASS" : "FAIL")} {timer.ElapsedMilliseconds}ms {error}"); Report(false);
        }
        Report(true);
        void Report(bool complete) {
            var total = samples.Count; var valid = samples.Count(s => s.Plan is not null);
            var groups = samples.Where(s => s.Case.MetamorphicGroup is not null).GroupBy(s => s.Case.MetamorphicGroup)
                .Select(g => new { group = g.Key, count = g.Count(), status = g.Count() < 2 ? "NOT_TESTED" : g.All(s => s.Correct) ? "PASS" : "FAIL" }).ToArray();
            double? equivalence = groups.Length == 0 || groups.Any(g => g.count < 2) ? null : groups.Count(g => g.status == "PASS") / (double)groups.Length;
            double Rate(int count) => count / (double)Math.Max(1, total);
            var intent = Rate(samples.Count(s => s.IntentCorrect)); var target = Rate(samples.Count(s => s.TargetCorrect));
            var frozenPass = total == data.Scenarios.Length && args.Length < 3 && intent >= gate.GetProperty("intentAccuracy").GetDouble()
                && Rate(valid) >= gate.GetProperty("validPlanRate").GetDouble() && target >= gate.GetProperty("correctTargetResolution").GetDouble()
                && equivalence >= gate.GetProperty("metamorphicEquivalence").GetDouble() && samples.All(s => !s.Invented && !s.Unauthorized);
            var pass = frozenPass && samples.All(sample => !sample.Extra);
            var times = samples.Select(s => s.Ms).Order().ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
            File.WriteAllText(args[1], JsonSerializer.Serialize(new { status = !complete ? "RUNNING" : labTransport is not null ? "QA_SCOPE_ASSISTED_DIAGNOSTIC" : args.Length >= 3 ? "DIAGNOSTIC_COMPLETE" : pass ? "PASS_SEMANTIC_CONTRACTS" : "FAIL_SEMANTIC_CONTRACTS",
                acceptanceComplete = false, frozenContractGate = labTransport is not null ? "NOT_APPLICABLE_QA_ASSISTED" : !complete ? "RUNNING" : frozenPass ? "PASS" : "FAIL", data.Version, data.Split, count = total, datasetSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
                thresholdsSha256 = Convert.ToHexString(SHA256.HashData(gateBytes)), thresholds = gate, model, modelDigest = digest, modelCapabilities = evaluatedModel.Capabilities,
                profile = new { semanticCore = labTransport is not null ? "RETIRED_DECODER_QA_SCOPE_ASSISTED" : core ? "RETIRED_REQUEST_DECODER_DIAGNOSTIC" : reflected ? "REFLECTED_TASK_CONVERSATION" : "LEGACY_EVALUATION", planningEntryPoint = entryPoint, taskGoals = core || !reflected && goalMode, taskResultContract = !core && !reflected && resultContract, authoringUseContract = !core && !reflected && authoringUseContract, semanticThinking = true, qwenThinkingSampling = qwenSampling, contextLength = PreparationInference.InferenceContextLength, reuseInference = false }, runtimeHashes,
                labArtifacts = labRoot is null ? null : new { scopeOrigin = "Independent synthetic QA oracle; supplied to the model. NOT blind semantic qualification.",
                    files = Directory.GetFiles(Path.Combine(labRoot, "ai_delivery_lab"), "*.py").ToDictionary(file => Path.GetFileName(file)!, file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))) },
                coreArtifacts = coreOptions is null ? null : new { workerSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(coreOptions.Worker))),
                    sdkHashes = Directory.GetFiles(Path.Combine(coreOptions.SdkRoot, "adaptive_core"), "*.py").ToDictionary(file => Path.GetFileName(file)!, file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))) },
                intentAccuracy = intent, validPlanRate = Rate(valid), correctTargetResolution = target, hallucinatedPromptIds = samples.Count(s => s.Invented),
                unauthorizedPlannedEffects = samples.Count(s => s.Unauthorized), metamorphicGroups = groups,
                extraEffectRate = new { status = valid == 0 ? "NOT_TESTED" : "MEASURED_PLANS_ONLY", value = valid == 0 ? (double?)null : samples.Count(sample => sample.Extra) / (double)valid,
                    extraCases = samples.Count(sample => sample.Extra), evaluatedPlans = valid, totalCases = total },
                missingEffectRate = new { status = "MEASURED_CONTRACTS_ONLY", value = Rate(samples.Count(sample => sample.Missing)), cases = samples.Count(sample => sample.Missing), totalCases = total },
                goalCompletionRate = "NOT_TESTED", targetResolution = target, metamorphicConsistency = equivalence,
                metamorphicEquivalence = new { status = equivalence is null ? "NOT_TESTED" : "MEASURED", value = equivalence },
                goalCompletionAccuracy = "NOT_TESTED", toolCallAccuracy = "NOT_TESTED", adaptationFidelity = "NOT_TESTED", unauthorizedExecutedEffects = "NOT_TESTED",
                wrongEarlyStopRate = "NOT_TESTED", outputContamination = "NOT_TESTED", taskSuccessRate = "NOT_TESTED",
                p50Ms = times.Length == 0 ? (long?)null : times[(times.Length - 1) / 2], p95Ms = times.Length == 0 ? (long?)null : times[(int)Math.Ceiling(times.Length * .95) - 1],
                resourcesBefore = before, fixtureUnchanged = fixture.Unchanged(), rows, limits = "Synthetic interpretation plus real SQLite source resolution, not generated content, browser tools or full task success. Fixed source IDs and identical context for A/B. Runtime hashes and all failures retained. No model reasoning logged." }, json));
            if (complete && !pass && args.Length < 3) Environment.ExitCode = 1;
        }
    }
    private static AgentSession FixtureSession(Guid id, SemanticContext context, PromptLibrary library)
    {
        PreparedDraft? draft = null;
        if (context is { DraftSource: { } basis, ActiveDraft: not null }) {
            var source = library.Get(basis.Id);
            draft = new(Guid.NewGuid(), PreparationState.READY, PreparationIntent.ADAPT_PROMPT, PreparationDecision.ADAPT_EXISTING,
                source.Title, source.Description, context.ActiveDraft ?? source.TemplateText, null,
                source.VariableNames.Select(name => new VariableDefinition(name, true)).ToArray(), [], [], "", "Synthetic prior draft",
                [new(source, 0, "Synthetic prior source")], source.Id, source.Revision, source.CategoryId, false, DateTime.UtcNow, id);
        }
        return new(id, context.Selected is { } selected ? library.Get(selected.Id) : null, draft, [], DateTime.UtcNow,
            context.Candidates.Select(candidate => library.Get(candidate.Id)).ToArray(), Approval: context.Approval);
    }
    private sealed class OwnedFixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-recovery-fixture-" + Guid.NewGuid());
        public LocalStore Store { get; }
        private readonly string initial;
        public OwnedFixture(SemanticEntity[] entities) {
            Store = new(Path.Combine(directory, "synthetic.sqlite"));
            foreach (var entity in entities) Store.Save(new Template { Id = entity.Id, Title = entity.Title, Template = entity.Text,
                CreatedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc) }, "import");
            initial = JsonSerializer.Serialize(Store.Templates().OrderBy(t => t.Id));
        }
        public bool Unchanged() => initial == JsonSerializer.Serialize(Store.Templates().OrderBy(t => t.Id));
        public void Dispose() {
            var full = Path.GetFullPath(directory); var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("nova-recovery-fixture-", StringComparison.Ordinal)) throw new InvalidOperationException("Refusing cleanup outside owned fixture.");
            Directory.Delete(full, true);
        }
    }
}
