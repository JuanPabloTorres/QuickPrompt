using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class PreparationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "goal07-" + Guid.NewGuid());
    private readonly LocalStore store;
    private readonly PreparedDraftStore drafts;
    private readonly NovaPromptStore prompts;
    public PreparationTests() { store = new(Path.Combine(directory, "test.sqlite")); drafts = new(store); prompts = new(store); }
    private static PromptProposal New(string text = "Explica con claridad.") => new(PreparationDecision.CREATE_NEW, null, "Explicar", "", text, [], "", "No hay una plantilla adecuada.");
    private static PrepareInput Input(bool remember = false) => new(Guid.NewGuid(), "Explica con claridad.", RememberDraft: remember);
    private static PreparedDraft Evaluate(PrepareInput input, PromptProposal proposal, RetrievalCandidate[]? candidates = null) => new DeterministicPromptEvaluator().Evaluate(input, new(PreparationIntent.CREATE_PROMPT, "explica", ""), proposal, candidates ?? [], []);
    [Fact] public async Task SearchesBeforeGenerationAndStopsAtAReviewedDraftWithoutSavingPrompt()
    {
        var log = new List<string>(); var inference = new Fake(log); var engine = Engine(inference);
        var input = Input(); var states = new List<PreparationState>();
        var result = await engine.Prepare(input, state => { states.Add(state); return Task.CompletedTask; }, (tool, _, _) => log.Add(tool), default);
        Assert.Equal(PreparationState.READY, result.State); Assert.Equal("Explica con claridad.", result.CompiledText); Assert.Empty(result.Variables);
        Assert.True(log.IndexOf("search_prompts") < log.IndexOf("generation")); Assert.Empty(store.Templates()); Assert.Empty(drafts.Recent());
        Assert.Equal(new[] { PreparationState.RECEIVED, PreparationState.UNDERSTANDING, PreparationState.SEARCHING, PreparationState.SELECTING_OR_GENERATING, PreparationState.VALIDATING, PreparationState.FILLING, PreparationState.READY }, states);
        await engine.Prepare(input, _ => Task.CompletedTask, (_, _, _) => { }, default); Assert.Equal(1, inference.Generations);
        await Assert.ThrowsAsync<ConflictException>(() => engine.Prepare(input with { Message = "Otra cosa" }, _ => Task.CompletedTask, (_, _, _) => { }, default));
    }
    [Fact] public async Task ContextDependentRequestWithoutAnIdentifiableSourceAsksOneQuestionInsteadOfUsingUnrelatedRecentPrompts()
    {
        new PromptLibrary(prompts, prompts).Create(new("Entrevistas", "Pregunta por <puesto>"));
        var inference = new ContextFake(); var engine = new AgentOrchestrator(inference, new SqlitePromptRetriever(store), inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(), drafts, new());
        var result = await engine.Prepare(Input() with { Message = "Hazlo mejor." }, _ => Task.CompletedTask, (_, _, _) => { }, default);
        Assert.Equal(PreparationDecision.ASK_CLARIFICATION, result.Decision); Assert.Equal(PreparationState.NEEDS_INFORMATION, result.State); Assert.Empty(result.TemplateText); Assert.Equal(0, inference.Calls);
    }
    [Fact] public void ValuesAreLiteralGroundedAndExplicitValuesTakePriorityOverDefaults()
    {
        var input = Input() with { Message = "Texto: α\nβ. Idioma: español", Values = new() { ["idioma"] = "English" } };
        var proposal = New("Analiza <texto> en <idioma>; <secreto>.") with { Values = [new("texto", "α\nβ", "Texto: α\nβ"), new("idioma", "español", "Idioma: español"), new("secreto", "inventado", "inventado")] };
        var values = new GroundedVariableResolver().Resolve(input, proposal, null);
        Assert.Equal("α\nβ", values.Single(v => v.Name == "texto").Value); Assert.Equal("English", values.Single(v => v.Name == "idioma").Value); Assert.DoesNotContain(values, v => v.Name == "secreto");
        var draft = new DeterministicPromptEvaluator().Evaluate(input, new(PreparationIntent.COMPLETE_PROMPT, "", ""), proposal, [], values);
        Assert.Equal(PreparationState.NEEDS_INFORMATION, draft.State); Assert.Equal(new[] { "secreto" }, draft.MissingVariables); Assert.Null(draft.CompiledText);
    }
    [Fact] public void DefaultsDoNotInventFactsAndNoVariablesAreRequiredForSimplePrompts()
    {
        var source = new PromptLibrary(prompts, prompts).Create(new("Default", "Explica <tema>", Defaults: new() { ["tema"] = "SQLite" }));
        var values = new GroundedVariableResolver().Resolve(Input(), New(source.TemplateText), source);
        Assert.Equal("TEMPLATE_DEFAULT", Assert.Single(values).ValueSource); Assert.Empty(Evaluate(Input(), New()).Variables);
    }
    [Fact] public void CurrentLiteralInputsOverrideOldSessionValuesWhileExplicitCurrentUiValuesRemainAuthoritative()
    {
        var input = Input() with { Message = "tema: NUEVO", ContextValues = new() { ["tema"] = "ANTERIOR" }, AuthorizedPendingRequest = "tema: MAS_ANTIGUO" };
        var resolver = new GroundedVariableResolver(); var proposal = New("Explica <tema>.");
        Assert.Equal("NUEVO", Assert.Single(resolver.Resolve(input, proposal, null)).Value);
        Assert.Equal("ACTUAL_UI", Assert.Single(resolver.Resolve(input with { Values = new() { ["tema"] = "ACTUAL_UI" } }, proposal, null)).Value);
        Assert.Equal("ANTERIOR", Assert.Single(resolver.Resolve(input with { Message = "Usa los datos disponibles." }, proposal, null)).Value);
    }
    [Fact] public void LiteralLabeledSourceTextDoesNotDependOnModelExtraction()
    {
        var input = Input() with { Message = "Analiza esto. Texto: α\nβ", Values = null };
        var values = new GroundedVariableResolver().Resolve(input, New("Analiza <texto>"), null);
        var value = Assert.Single(values); Assert.Equal("α\nβ", value.Value); Assert.Equal("REQUEST_LABEL", value.ValueSource);
        Assert.Empty(new GroundedVariableResolver().Resolve(input with { Message = "texto: uno; texto: dos" }, New("Analiza <texto>"), null));
        Assert.Equal("α\nβ", Assert.Single(new GroundedVariableResolver().Resolve(input, New("Analiza. Texto: <documento>"), null)).Value);
        Assert.Equal("α\nβ", Assert.Single(new GroundedVariableResolver().Resolve(input, New("Analyze <document>"), null)).Value);
        Assert.Empty(new GroundedVariableResolver().Resolve(input, New("Compare <documento_a> and <documento_b>"), null));
        var code = "  if (x) {\n    work();\n  }\n";
        Assert.Equal(code, Assert.Single(new GroundedVariableResolver().Resolve(input with { Message = "Texto:\n" + code }, New("Analiza <texto>"), null)).Value);
    }
    [Fact] public void LiteralQuoteForAnUnrelatedFieldIsNotAcceptedAsSummaryLength()
    {
        var input = Input() with { Message = "Señala la idea central en una frase. Texto: Contenido" };
        var proposal = New("Resume <texto> en <extensión>.") with { Values = [new("extensión", "idea central", "idea central")] };
        Assert.DoesNotContain(new GroundedVariableResolver().Resolve(input, proposal, null), value => value.Name == "extensión");
        var valid = proposal with { Values = [new("extensión", "una frase", "una frase")] };
        Assert.Equal("una frase", new GroundedVariableResolver().Resolve(input, valid, null).Single(value => value.Name == "extensión").Value);
        var accented = valid with { Values = [new("extensión", "dos párrafos", "dos párrafos")] };
        Assert.Equal("dos párrafos", new GroundedVariableResolver().Resolve(input with { Message = "Resume en dos párrafos." }, accented, null).Single().Value);
    }
    [Fact] public void InventedOriginsMalformedMarkersDuplicateVariablesAndUnboundedValuesAreRejected()
    {
        Assert.Throws<ValidationException>(() => Evaluate(Input(), New() with { Decision = PreparationDecision.REUSE_EXISTING, SourceId = Guid.NewGuid() }));
        Assert.Throws<ValidationException>(() => Evaluate(Input(), New("<sin cerrar")));
        var material = Evaluate(Input(), New("Compara los documentos.") with { RequiredInputs = ["documento_a", "documento_b"] });
        Assert.Equal(PreparationState.NEEDS_INFORMATION, material.State); Assert.Equal(new[] { "documento_a", "documento_b" }, material.MissingVariables); Assert.Null(material.CompiledText);
        Assert.Throws<ValidationException>(() => Evaluate(Input(), New() with { Values = [new("x", "one", "one"), new("x", "two", "two")] }));
        Assert.Throws<ValidationException>(() => AgentOrchestrator.Validate(Input() with { Values = new() { ["x"] = new string('x', 8001) } }));
    }
    [Fact] public void ExplicitRecoverySurvivesRestartAndParallelSaveIsIdempotentAndNeverPersistsFilledDefaults()
    {
        var input = Input(true); var draft = Evaluate(input, New("Explica <tema>")); drafts.Put(draft, "hash");
        var restarted = new PreparedDraftStore(new(Path.Combine(directory, "test.sqlite")));
        Assert.Equal(draft.Id, Assert.Single(restarted.Recent()).Id);
        var ids = Enumerable.Range(0, 12).AsParallel().Select(_ => restarted.Save(draft.Id).Id).ToArray();
        Assert.Single(ids.Distinct()); Assert.Single(store.Templates()); Assert.Empty(store.Templates()[0].Variables); Assert.Single(store.Versions(ids[0]));
        restarted.Forget(draft.Id); Assert.Empty(restarted.Recent());
    }
    [Fact] public void ExactContentDeduplicationIsIndependentOfOperationAndAdaptationPreservesOriginal()
    {
        var library = new PromptLibrary(prompts, prompts); var original = library.Create(new("Entrevista", "Prepara preguntas para <puesto>."));
        var candidate = new RetrievalCandidate(original, 5, "Título");
        var draft = Evaluate(Input(), New("Prepara preguntas técnicas para <puesto>.") with { Decision = PreparationDecision.ADAPT_EXISTING, SourceId = original.Id }, [candidate]);
        drafts.Put(draft, "one"); var saved = drafts.Save(draft.Id); Assert.NotEqual(original.Id, saved.Id); Assert.Equal(1, library.Get(original.Id).Revision); Assert.Single(store.Versions(original.Id));
        var another = draft with { Id = Guid.NewGuid() }; drafts.Put(another, "two"); Assert.Equal(saved.Id, drafts.Save(another.Id).Id); Assert.Equal(2, store.Templates().Count);
        library.Delete(saved.Id, new(saved.Revision, true)); Assert.Throws<ConflictException>(() => drafts.Save(draft.Id));
    }
    [Fact] public void AdaptationRetainsCompatibleAuthorizedDefaultsButDoesNotPersistTaskSpecificOverrides()
    {
        var library = new PromptLibrary(prompts, prompts); var original = library.Create(new("Entrevista", "Pregunta a <puesto>", Defaults: new() { ["puesto"] = "backend" }));
        var draft = Evaluate(Input(), New("Prepara preguntas técnicas para <puesto>.") with { Decision = PreparationDecision.ADAPT_EXISTING, SourceId = original.Id }, [new(original, 1, "origen")])
            with { Values = [new("puesto", "PRIVATE_TASK_OVERRIDE", "USER_INPUT", "VALIDATED")] };
        drafts.Put(draft, "hash"); var saved = drafts.Save(draft.Id); Assert.Equal("backend", saved.Defaults["puesto"]); Assert.DoesNotContain("PRIVATE_TASK_OVERRIDE", store.Export());
    }
    [Fact] public void PhysicalBackupContainsOptedDraftsAndEphemeralDraftIsAbsentFromSQLite()
    {
        var ephemeral = Evaluate(Input(), New("PRIVATE_SENTINEL")); drafts.Put(ephemeral, "privateHash");
        var opted = Evaluate(Input(true), New("RECOVERABLE")); drafts.Put(opted, "optedHash");
        Assert.Null(new PreparedDraftStore(new(Path.Combine(directory, "test.sqlite"))).Get(ephemeral.Id));
        var backupName = store.NovaBackup(); var restored = new LocalStore(Path.Combine(directory, "backups", backupName));
        Assert.Equal(opted.Id, Assert.Single(restored.RecentPreparations()).Id); Assert.Null(restored.GetPreparation(ephemeral.Id));
    }
    [Fact] public void HashOnlyReceiptAndExplicitSaveRemainIdempotentAfterRestartWithoutRememberingValues()
    {
        var draft = Evaluate(Input(), New()); drafts.Put(draft, "fingerprint"); var saved = drafts.Save(draft.Id);
        var reopened = new PreparedDraftStore(new(Path.Combine(directory, "test.sqlite")));
        Assert.Null(reopened.Get(draft.Id)); Assert.Equal("fingerprint", reopened.Fingerprint(draft.Id)); Assert.Equal(saved.Id, reopened.Save(draft.Id).Id);
        Assert.Empty(reopened.Recent()); Assert.Single(store.Templates());
    }
    [Fact] public void RestorePreviewCannotSilentlyDiscardANewDraftCreatedAfterReview()
    {
        var archive = store.Export(); var snapshot = store.NovaSnapshot(); var draft = Evaluate(Input(true), New()); drafts.Put(draft, "new");
        Assert.NotEqual(snapshot, store.NovaSnapshot());
        var data = System.Text.Json.JsonSerializer.SerializeToElement(System.Text.Json.JsonSerializer.Deserialize<object>(archive));
        Assert.Throws<ConflictException>(() => store.NovaRestore(new(data, true, snapshot))); Assert.Equal(draft.Id, Assert.Single(drafts.Recent()).Id);
    }
    [Fact] public async Task RegistryRejectsUnknownToolsExtraArgumentsWrongPermissionsAndStepOverflow()
    {
        var schema = ContractSchema.Object(new() { ["query"] = ContractSchema.Text(10) }, "query");
        var registry = new PreparationTools(1, 1, (_, _, _) => { }); registry.Register<QueryArgs, string>("search_prompts", ToolPermission.Read, schema, (q, _) => Task.FromResult(q.Query));
        await Assert.ThrowsAsync<PreparationToolException>(() => registry.Call<QueryArgs, string>("shell", new("x"), ToolPermission.Read, default));
        await Assert.ThrowsAsync<PreparationToolException>(() => registry.Call<QueryArgs, string>("search_prompts", new("x"), ToolPermission.SavePrompt, default));
        Assert.Throws<ValidationException>(() => ContractSchema.Validate(System.Text.Json.JsonSerializer.SerializeToElement(new { query = "x", sql = "DROP" }), schema));
        Assert.Equal("x", await registry.Call<QueryArgs, string>("search_prompts", new("x"), ToolPermission.Read, default));
        await Assert.ThrowsAsync<PreparationToolException>(() => registry.Call<QueryArgs, string>("search_prompts", new("x"), ToolPermission.Read, default));
    }
    [Fact] public void GenerationSchemaAllowsOnlyExactRetrievedIdsAndSelectedSource()
    {
        var real = Guid.NewGuid(); var other = Guid.NewGuid(); var schema = PreparationInference.ProposalFor([real, other], real).GetProperty("properties").GetProperty("sourceId");
        ContractSchema.Validate(System.Text.Json.JsonSerializer.SerializeToElement(real.ToString()), schema);
        Assert.Throws<ValidationException>(() => ContractSchema.Validate(System.Text.Json.JsonSerializer.SerializeToElement(other.ToString()), schema));
        Assert.Throws<ValidationException>(() => ContractSchema.Validate(System.Text.Json.JsonSerializer.SerializeToElement(Guid.NewGuid().ToString()), schema));
        ContractSchema.Validate(System.Text.Json.JsonSerializer.SerializeToElement((string?)null), PreparationInference.ProposalFor([], null).GetProperty("properties").GetProperty("sourceId"));
    }
    [Theory]
    [InlineData("documento", true)]
    [InlineData("aplicación React", true)]
    [InlineData("資料", true)]
    [InlineData("<documento>", false)]
    [InlineData("documento>otro", false)]
    [InlineData("", false)]
    [InlineData("documento\notro", false)]
    public void GeneratedInputMetadataRequiresBareNamesWithoutChangingTheDomainParser(string name, bool valid)
    {
        var item = PreparationInference.ProposalFor([], null).GetProperty("properties").GetProperty("requiredInputs").GetProperty("items");
        var value = System.Text.Json.JsonSerializer.SerializeToElement(name);
        if (valid) { ContractSchema.Validate(value, item); Assert.Equal(name, Assert.Single(TemplateCompiler.Names($"<{name}>"))); }
        else Assert.Throws<ValidationException>(() => ContractSchema.Validate(value, item));
    }
    [Fact] public async Task TimeoutCancellationAndToolFailureNeverCreateAnIncompleteDraft()
    {
        var registry = new PreparationTools(2, 1, (_, _, _) => { }); registry.Register<QueryArgs, string>("slow", ToolPermission.Prepare, ContractSchema.Object(new() { ["query"] = ContractSchema.Text(10) }, "query"), async (_, token) => { await Task.Delay(5000, token); return "late"; });
        var timeout = await Assert.ThrowsAsync<PreparationToolException>(() => registry.Call<QueryArgs, string>("slow", new("x"), ToolPermission.Prepare, default)); Assert.Equal("TIMEOUT", timeout.Code);
        var failing = new Fake([]) { Fail = true }; await Assert.ThrowsAsync<ValidationException>(() => Engine(failing).Prepare(Input(true), _ => Task.CompletedTask, (_, _, _) => { }, default)); Assert.Empty(drafts.Recent());
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Engine(new Fake([])).Prepare(Input(true), _ => Task.CompletedTask, (_, _, _) => { }, cancelled.Token)); Assert.Empty(drafts.Recent());
    }
    [Fact] public void ParserSlotsAreAuthoritativeAndMetadataCannotCreateDuplicateSynonymInputs()
    {
        var result = Evaluate(Input(), New("Compara <documentos>.") with { RequiredInputs = ["documento_a", "documento_b"] });
        Assert.Equal(new[] { "documentos" }, result.MissingVariables);
        Assert.Throws<ValidationException>(() => Evaluate(Input(), New() with { RequiredInputs = ["bad>input"] }));
        var original = new PromptLibrary(prompts, prompts).Create(new("Fijo", "Responde LISTO."));
        var reused = Evaluate(Input(), New() with { Decision = PreparationDecision.REUSE_EXISTING, SourceId = original.Id, RequiredInputs = ["texto"] }, [new(original, 1, "real")]);
        Assert.Equal(original.TemplateText, reused.CompiledText); Assert.Empty(reused.Variables);
    }
    [Fact] public void RetrievalUsesGroupsFavoritesAndRealIdsWithBoundedCandidates()
    {
        var library = new PromptLibrary(prompts, prompts); var item = library.Create(new("Sinónimo", "Pregunta breve.")); var group = store.SaveGroup(new() { Name = "Arquitectura" }); store.AssignGroup(group.Id, "template", [item.Id]);
        for (var i = 0; i < 20; i++) library.Create(new("Arquitectura " + i, "Otra pregunta."));
        var candidates = new SqlitePromptRetriever(store).Retrieve("arquitectura", item.Id); Assert.InRange(candidates.Length, 1, 6); Assert.Equal(item.Id, candidates[0].Prompt.Id); Assert.All(candidates, c => Assert.NotNull(store.FindTemplate(c.Prompt.Id)));
    }
    private AgentOrchestrator Engine(Fake inference) => new(inference, new SqlitePromptRetriever(store), inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(), drafts, new());
    private record QueryArgs(string Query);
    private sealed class Fake(List<string> log) : IIntentInterpreter, IPromptGenerator
    {
        public bool Fail { get; init; } public int Generations { get; private set; }
        public Task<Understanding> Understand(string message, string model, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(new Understanding(PreparationIntent.CREATE_PROMPT, "explica", "")); }
        public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct) { ct.ThrowIfCancellationRequested(); if (Fail) throw new ValidationException("Synthetic failure"); Generations++; log.Add("generation"); return Task.FromResult(New()); }
    }
    private sealed class ContextFake : IIntentInterpreter, IPromptGenerator
    {
        public int Calls { get; private set; }
        public Task<Understanding> Understand(string message, string model, CancellationToken ct) => Task.FromResult(new Understanding(PreparationIntent.IMPROVE_PROMPT, "mejorar", ""));
        public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct) { Calls++; return Task.FromResult(New()); }
    }
    public void Dispose() { Directory.Delete(directory, true); }
}
