using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class EditorConstraintTests
{
    private const string Original = "Explica <tema> con ejemplos detallados y un cierre completo. No inventes información.";
    private static Template Source() => new() { Id = Guid.NewGuid(), Revision = 1, Title = "Ejemplo", Template = Original };
    private sealed class Replies(params string[] texts) : HttpMessageHandler
    {
        public List<JsonElement> Inputs = [];
        public bool ExtraField;
        public string? RawContent;
        public bool SemanticShortening;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)); Inputs.Add(body.RootElement.Clone());
            var text = texts[Math.Min(Inputs.Count - 1, texts.Length - 1)];
            var content = JsonSerializer.Serialize(new { text, reasons = new[] { "Cambio concreto." }, changes = new[] { "Texto reducido." }, warnings = Array.Empty<string>(), variablesPreserved = new[] { "tema" } });
            if(body.RootElement.GetProperty("format").GetProperty("properties").TryGetProperty("shorteningRequested",out _)) {
                var node=System.Text.Json.Nodes.JsonNode.Parse(content)!; node["shorteningRequested"]=SemanticShortening; content=node.ToJsonString();
            }
            if (ExtraField) { var node = System.Text.Json.Nodes.JsonNode.Parse(content)!; node["requiredVerbatimConstraints"] = new System.Text.Json.Nodes.JsonArray(); content = node.ToJsonString(); }
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { model = "qwen3:8b", done = true, message = new { content = RawContent ?? content } }), Encoding.UTF8, "application/json") };
        }
    }
    [Fact] public async Task AnEchoedInputChecklistCannotBecomeAnUndeclaredProposalOutputField()
    {
        using var replies = new Replies("Explica <tema>. No inventes información.") { ExtraField = true }; using var http = new HttpClient(replies);
        await Assert.ThrowsAsync<ValidationException>(() => new OllamaClient(http).Propose("improve", "Hazlo más breve.", Source(), "qwen3:8b", CancellationToken.None, allowRevision: false));
        Assert.Single(replies.Inputs);
    }
    [Fact] public async Task MissingOriginalProhibitionGetsOneRealCorrectionAndMustBePresentInTheReturnedBody()
    {
        using var replies = new Replies("Explica <tema>.", "Explica <tema>. No inventes información."); using var http = new HttpClient(replies);
        var client = new OllamaClient(http, "http://127.0.0.1:11434"); var source = Source(); var corrections = 0; var previous = OllamaClient.OperationNotice.Value;
        OllamaClient.OperationNotice.Value = _ => corrections++;
        try {
            var result = await client.Propose("improve", "Hazlo más breve.", source, "qwen3:8b", CancellationToken.None);
            Assert.Equal(2, replies.Inputs.Count); Assert.Equal(1, corrections); Assert.Contains("No inventes información", result.Text); Assert.True(result.Text.Length < source.Template.Length); Assert.Equal(Original, source.Template);
            Assert.Contains("requiredVerbatimConstraints", replies.Inputs[0].GetProperty("messages")[1].GetProperty("content").GetString());
        } finally { OllamaClient.OperationNotice.Value = previous; }
    }
    [Fact]
    public async Task SemanticEditorBindsKnownInputsWithoutDependingOnModelRepetition()
    {
        using var replies = new Replies("Explica una API. No inventes información.");
        using var http = new HttpClient(replies); var source = Source();
        var result = await new OllamaClient(http).Propose("improve", "Orienta la explicación a una API.", source, "qwen3:8b", default, true, false, new TemplateEditInterpretation("Orientar a una API"));
        Assert.Contains("tema: <tema>", result.Text); Assert.Contains("Entradas / Inputs:", result.Text);
        Assert.Contains(result.Warnings, warning => warning.StartsWith("QuickPrompt incorporó", StringComparison.Ordinal));
        Assert.Single(replies.Inputs); Assert.Equal(Original, source.Template);
    }
    [Fact] public async Task ASecondOmissionIsRejectedRatherThanAppendedSilentlyOrDeclaredSuccessful()
    {
        using var replies = new Replies("Explica <tema>."); using var http = new HttpClient(replies); var source = Source(); var client = new OllamaClient(http, "http://127.0.0.1:11434");
        await Assert.ThrowsAsync<ValidationException>(() => client.Propose("improve", "Hazlo más breve.", source, "qwen3:8b", CancellationToken.None));
        Assert.Equal(2, replies.Inputs.Count); Assert.Equal(Original, source.Template);
    }
    [Fact] public async Task CanvasProposalReportsActualSystemCompositionWithoutAttributingItToTheModel()
    {
        using var replies = new Replies("Evalúa una API y sus riesgos. No inventes información."); using var http = new HttpClient(replies); var source = Source();
        var reservation = new TcpListener(IPAddress.Loopback,0); reservation.Start(); var port=((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var inference = new QuickPrompt.Web.Infrastructure.PreparationInference($"http://127.0.0.1:{port}", localEditor: new OllamaClient(http));
        var plan = SemanticFixture.Plan([SemanticOperation.RESOLVE_PROMPT, SemanticOperation.OPEN_PROMPT, SemanticOperation.PROPOSE_ADAPTATION,
            SemanticOperation.VALIDATE_DRAFT, SemanticOperation.COMPILE_PROMPT], TargetReference.SELECTED, source.Id, outcome: SemanticOutcome.DRAFT);
        var input = new PrepareInput(Guid.NewGuid(), "Reorienta la explicación.", SourcePromptId: source.Id) { Interpretation = plan };
        var pending = inference.Generate(input, plan.Preparation(), [new(PromptLibrary.Document(source), 10, "Origen real")], timeout.Token);
        var review = await listener.GetContextAsync().WaitAsync(timeout.Token);
        using var packet = await JsonDocument.ParseAsync(review.Request.InputStream,cancellationToken:timeout.Token);
        using var data = JsonDocument.Parse(packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Contains("tema: <tema>",data.RootElement.GetProperty("proposedInstruction").GetString());
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {model="qwen3:8b",done=true,message=new {content=JsonSerializer.Serialize(new {
            verdict="ACCEPT",instructionScope="FUTURE_TASK_ONLY",applicationWorkflowQuotes=Array.Empty<string>(),requestedTitleQuote=(string?)null,issues=Array.Empty<object>()
        })}}));
        review.Response.ContentType="application/json";review.Response.ContentLength64=bytes.Length;
        await review.Response.OutputStream.WriteAsync(bytes,timeout.Token);review.Response.Close();
        var proposal = await pending;
        Assert.StartsWith("QuickPrompt incorporó los campos reales", proposal.Reason); Assert.Contains("tema: <tema>", proposal.TemplateText);
        Assert.Single(replies.Inputs); Assert.Equal(Original, source.Template);
    }
    [Fact] public async Task SemanticEditingRetainsCanonicalSafetyClausesByConstructionAndReportsTheSystemRule()
    {
        using var replies = new Replies("Explica <tema> con Rust sin inventar información."); using var http = new HttpClient(replies); var source = Source();
        var notices = new List<string>(); var previous = OllamaClient.OperationNotice.Value; OllamaClient.OperationNotice.Value = notices.Add;
        try {
            var proposal = await new OllamaClient(http).Propose("improve", "Orienta esta explicación a Rust.", source, "qwen3:8b", default, true, false,
                new TemplateEditInterpretation("Orientar a Rust", TargetTerms: ["Rust"]));
            Assert.Single(replies.Inputs); Assert.Contains("Rust", proposal.Text); Assert.Contains("<tema>", proposal.Text); Assert.Contains("No inventes información", proposal.Text);
            Assert.Contains(proposal.Warnings, warning => warning.StartsWith("QuickPrompt conservó", StringComparison.Ordinal));
            Assert.Equal(new[] { "SOURCE_INVARIANTS_RETAINED" }, notices); Assert.Equal(Original, source.Template);
        } finally { OllamaClient.OperationNotice.Value = previous; }
    }
    [Fact] public async Task MerelyDroppingAProtectedClauseCannotBecomeASuccessfulSemanticEdit()
    {
        using var replies = new Replies("Explica <tema> con ejemplos detallados y un cierre completo."); using var http = new HttpClient(replies); var source = Source();
        await Assert.ThrowsAsync<ValidationException>(() => new OllamaClient(http).Propose("improve", "Orienta esta explicación a Rust.", source, "qwen3:8b", default, true, false,
            new TemplateEditInterpretation("Orientar a Rust", TargetTerms: ["Rust"])));
        Assert.Single(replies.Inputs); Assert.Equal(Original, source.Template);
    }
    [Theory]
    [InlineData("{\"text\":")]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task SourceInvariantRetentionCannotTurnMalformedModelOutputIntoAnUnhandledError(string raw)
    {
        using var replies = new Replies(Original) { RawContent = raw }; using var http = new HttpClient(replies); var source = Source();
        await Assert.ThrowsAsync<ValidationException>(() => new OllamaClient(http).Propose("improve", "Reorienta la explicación.", source, "qwen3:8b", default, true, false,
            new TemplateEditInterpretation("Cambiar el enfoque")));
        Assert.Single(replies.Inputs); Assert.Equal(Original, source.Template);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShorteningCannotSilentlyDropARequestedTechnicalTarget(bool omitAgain)
    {
        const string generic = "Explica <tema>. No inventes información.";
        using var replies = new Replies(generic, omitAgain ? generic : "Explica <tema> con Rust. No inventes información.");
        using var http = new HttpClient(replies); var source = Source();
        Task<AiProposal> Act() => new OllamaClient(http).Propose("improve", "Reduce la explicación para Rust.", source, "qwen3:8b", default, true, true,
            new TemplateEditInterpretation("Explicación más breve para Rust", TargetTerms: ["Rust"]));
        if (omitAgain) await Assert.ThrowsAsync<ValidationException>(Act);
        else { var proposal = await Act(); Assert.Contains("Rust", proposal.Text); Assert.True(proposal.Text.Length < Original.Length); }
        Assert.Equal(2, replies.Inputs.Count); Assert.Equal(Original, source.Template);
        using var packet = JsonDocument.Parse(replies.Inputs[1].GetProperty("messages")[1].GetProperty("content").GetString()!);
        using var feedback = JsonDocument.Parse(packet.RootElement.GetProperty("validationFeedback").GetRawText());
        Assert.Equal("Rust", feedback.RootElement.GetProperty("missingTargetTerms")[0].GetString());
    }
    [Fact] public async Task OuterValidationCorrectionProvidesAnActualCandidateAndStructuredFailuresWithoutChangingUserAuthority()
    {
        const string request = "Cambia el enfoque de la explicación.";
        const string rejected = "Explica <tema> con ejemplos para principiantes. No inventes información.";
        var failures = JsonSerializer.Serialize(new { rejectedCandidate = rejected, failures = new[] { "TARGET_CONTEXT" }, requiredTargetTerms = new[] { "Rust" } });
        using var replies = new Replies("Explica <tema> con Rust. No inventes información."); using var http = new HttpClient(replies);
        await new OllamaClient(http).Propose("improve", request, Source(), "qwen3:8b", default, false, false,
            new TemplateEditInterpretation("Reorientar la explicación", failures, TargetTerms: ["Rust"]));
        using var packet = JsonDocument.Parse(Assert.Single(replies.Inputs).GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal(rejected, packet.RootElement.GetProperty("previousDraft").GetString());
        Assert.Equal(JsonValueKind.Object, packet.RootElement.GetProperty("validationFeedback").ValueKind);
        Assert.False(packet.RootElement.GetProperty("validationFeedback").TryGetProperty("rejectedCandidate", out _));
        Assert.Equal("TARGET_CONTEXT", packet.RootElement.GetProperty("validationFeedback").GetProperty("failures")[0].GetString());
        Assert.Equal(request, packet.RootElement.GetProperty("authorizedUserRequest").GetString());
    }
    [Fact] public async Task ACorrectionKeepsOneCandidateAndAllRequirementsWithinTheUnchangedContextBudget()
    {
        var source = Source(); source.Template = "Explica <tema>. " + new string('a', 850) + ". No inventes información.";
        var candidate = "Evalúa <tema> con Rust. " + new string('b', 850) + ". No inventes información.";
        var correction = JsonSerializer.Serialize(new { failures = new[] { "TARGET_CONTEXT" }, rejectedCandidate = candidate,
            requiredTargetTerms = new[] { "Rust" }, requiredVerbatimConstraints = new[] { "No inventes información" },
            correction = new string('x', 600) });
        using var replies = new Replies(candidate); using var http = new HttpClient(replies);
        await new OllamaClient(http).Propose("improve", "Reorienta el análisis hacia Rust.", source, "qwen3:8b", default, false, false,
            new TemplateEditInterpretation("Reorientar a Rust", correction, TargetTerms: ["Rust"]));
        using var packet = JsonDocument.Parse(Assert.Single(replies.Inputs).GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal(candidate, packet.RootElement.GetProperty("previousDraft").GetString());
        Assert.False(packet.RootElement.TryGetProperty("generationBudget", out _));
        var feedback = packet.RootElement.GetProperty("validationFeedback");
        Assert.Equal("Rust", feedback.GetProperty("requiredTargetTerms")[0].GetString());
        Assert.Equal("No inventes información", feedback.GetProperty("requiredVerbatimConstraints")[0].GetString());
        Assert.False(feedback.TryGetProperty("rejectedCandidate", out _)); Assert.False(feedback.TryGetProperty("correction", out _));
    }
    [Fact] public async Task PreparationPipelineOwnsItsSingleRevisionAndIndependentlyRejectsAnUnsafeInitialDraft()
    {
        using var replies = new Replies("Explica <tema>."); using var http = new HttpClient(replies); var source = Source(); var client = new OllamaClient(http, "http://127.0.0.1:11434");
        var initial = await client.Propose("improve", "Hazlo más breve.", source, "qwen3:8b", CancellationToken.None, allowRevision: false); Assert.Single(replies.Inputs);
        var document = PromptLibrary.Document(source); var request = new PrepareInput(Guid.NewGuid(), "Hazlo más breve.");
        var draft = new DeterministicPromptEvaluator().Evaluate(request, new(PreparationIntent.IMPROVE_PROMPT, "", ""),
            new(PreparationDecision.ADAPT_EXISTING, source.Id, source.Title, "", initial.Text, [], "", "Propuesta inicial."), [new(document, 10, "Origen real")], []);
        var requirements = new PromptRequirements(null, null, TemplateConstraints.Required(source.Template), Shortening: true);
        var validation = PromptPreparationPipeline.Validation(draft, requirements); Assert.False(validation.Valid); Assert.Contains("CONSTRAINT", validation.Errors);
    }
    [Fact] public void ShortAndLongSourceProhibitionsAreCheckedCompletelyWithoutTruncatingTheRequirement()
    {
        Assert.NotEmpty(TemplateConstraints.Missing("No IA.", "Usa IA."));
        var clause = "No " + new string('x', 420) + " PROTEGIDO";
        Assert.Equal(clause, Assert.Single(TemplateConstraints.Required(clause))); Assert.NotEmpty(TemplateConstraints.Missing(clause, clause[..300]));
    }
    [Fact] public void ExplicitPreservationOfNamesDatesAndCommitmentsCannotBeRemovedByAnOtherwiseShorterDraft()
    {
        const string source = "Redacta <correo>. Conserva nombres, fechas y compromisos tal como se proporcionan; no inventes información.";
        const string unsafeDraft = "Resume <correo>. No inventes información.";
        Assert.Equal("Conserva nombres, fechas y compromisos tal como se proporcionan;", Assert.Single(TemplateConstraints.Missing(source, unsafeDraft)));
        Assert.Empty(TemplateConstraints.Missing(source, "Resume <correo>. Conserva nombres, fechas y compromisos tal como se proporcionan; no inventes información."));
    }
    [Fact] public async Task AnUnchangedInitialDraftReachesTheOuterPipelineWhichRejectsItBeforePublishingAndOwnsTheRevision()
    {
        using var replies = new Replies(Original); using var http = new HttpClient(replies); var source = Source();
        var initial = await new OllamaClient(http).Propose("improve", "Mejora la claridad.", source, "qwen3:8b", CancellationToken.None, allowRevision: false);
        Assert.Equal(Original, initial.Text); Assert.Single(replies.Inputs);
        var document = PromptLibrary.Document(source); var input = new PrepareInput(Guid.NewGuid(), "Mejora la claridad.");
        var draft = new DeterministicPromptEvaluator().Evaluate(input, new(PreparationIntent.IMPROVE_PROMPT, "", ""), new(PreparationDecision.ADAPT_EXISTING, source.Id, source.Title, "", initial.Text, [], "", "Intento inicial."), [new(document, 10, "Origen real")], []);
        var validation = PromptPreparationPipeline.Validation(draft, new PromptRequirements(null, null, []));
        Assert.False(validation.Valid); Assert.Contains("ADAPTATION_REQUIRED", validation.Errors); Assert.Equal(Original, source.Template);
    }
    [Fact] public async Task DirectEditorCallsStillRejectUnchangedTextInsteadOfPresentingItAsAnImprovement()
    {
        using var replies = new Replies(Original); using var http = new HttpClient(replies);
        await Assert.ThrowsAsync<ValidationException>(() => new OllamaClient(http).Propose("improve", "Mejora la claridad.", Source(), "qwen3:8b", CancellationToken.None)); Assert.Single(replies.Inputs);
    }
    [Fact] public async Task TypedSemanticShorteningIsAuthoritativeInBothDirections()
    {
        using var longer = new Replies(Original + " Añade una conclusión."); using var longHttp = new HttpClient(longer);
        var result = await new OllamaClient(longHttp).Propose("improve", "Hazlo más breve.", Source(), "qwen3:8b", default, true, false);
        Assert.True(result.Text.Length > Original.Length); Assert.Single(longer.Inputs);
        Assert.DoesNotContain("SHORTENING THE TEMPLATE", longer.Inputs[0].GetProperty("messages")[0].GetProperty("content").GetString());
        using var shorter = new Replies("Explica <tema>. No inventes información."); using var shortHttp = new HttpClient(shorter);
        var reduced = await new OllamaClient(shortHttp).Propose("improve", "Aplica el cambio interpretado.", Source(), "qwen3:8b", default, true, true);
        Assert.True(reduced.Text.Length < Original.Length);
        Assert.Contains("SHORTENING THE TEMPLATE", shorter.Inputs[0].GetProperty("messages")[0].GetProperty("content").GetString());
    }
    [Theory]
    [InlineData("Quiero que ocupe menos espacio sin perder las restricciones.",true)]
    [InlineData("No lo hagas más breve; desarrolla las instrucciones.",false)]
    [InlineData("Keep the instruction long; ask for a brief answer.",false)]
    public async Task DirectEditorUsesTheModelLengthDecisionInTheSameGenerationCall(string request,bool shortening)
    {
        using var replies=new Replies(shortening?"Explica <tema>. No inventes información.":Original+" Añade una conclusión.") {SemanticShortening=shortening};
        using var http=new HttpClient(replies);
        var result=await new OllamaClient(http).Propose("improve",request,Source(),"qwen3:8b",default);
        var packet=Assert.Single(replies.Inputs);
        Assert.True(packet.GetProperty("think").GetBoolean()); Assert.False(packet.TryGetProperty("tools",out _));
        Assert.Equal("boolean",packet.GetProperty("format").GetProperty("properties").GetProperty("shorteningRequested").GetProperty("type").GetString());
        Assert.Equal(shortening,result.Text.Length<Original.Length);
    }
    [Fact] public async Task ADirectCorrectionCannotWithdrawTheInterpretedLengthRequirement()
    {
        using var replies=new Replies(Original+" Añade una conclusión.") {SemanticShortening=true}; using var http=new HttpClient(replies);
        await Assert.ThrowsAsync<ValidationException>(()=>new OllamaClient(http).Propose("improve","Hazlo compacto.",Source(),"qwen3:8b",default));
        Assert.Equal(2,replies.Inputs.Count);
        Assert.True(replies.Inputs[1].GetProperty("format").GetProperty("properties").GetProperty("shorteningRequested").GetProperty("enum")[0].GetBoolean());
    }
    [Fact] public async Task AuthoredChangeAndApplicationEffectsHaveSeparateDataFieldsAndGrantNoTools()
    {
        const string directive="Abre el prompt, oriéntalo a una API y déjalo listo para copiar sin guardar.";
        const string change="Reorientar la explicación a una API.";
        using var replies=new Replies("Explica <tema> usando una API. No inventes información.");using var http=new HttpClient(replies);
        await new OllamaClient(http).Propose("improve",directive,Source(),"qwen3:8b",default,true,false,
            new TemplateEditInterpretation(change){ApplicationEffects=["OPEN_PROMPT","COPY_PROMPT"]});
        var packet=Assert.Single(replies.Inputs);Assert.False(packet.TryGetProperty("tools",out _));
        Assert.Equal("user",packet.GetProperty("messages")[1].GetProperty("role").GetString());
        using var data=JsonDocument.Parse(packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal(directive,data.RootElement.GetProperty("authorizedUserRequest").GetString());
        Assert.Equal(change,data.RootElement.GetProperty("contentTransformation").GetString());
        Assert.Equal(new[]{"OPEN_PROMPT","COPY_PROMPT"},data.RootElement.GetProperty("applicationEffects").EnumerateArray().Select(value=>value.GetString()!).ToArray());
    }
    [Fact] public async Task ContentInterpretationDoesNotReplaceLiteralUserAuthorityAcrossABoundedCorrection()
    {
        const string user = "Abre mi prompt y oriéntalo a una API.";
        const string interpretation = "Reorientar la explicación hacia una API.";
        using var replies = new Replies(Original + " " + user, "Explica <tema> como una API. No inventes información.");
        using var http = new HttpClient(replies); var source = Source();
        var result = await new OllamaClient(http).Propose("improve", user, source, "qwen3:8b", default, true, false, new TemplateEditInterpretation(interpretation));
        Assert.Equal(2, replies.Inputs.Count); Assert.Equal(Original, source.Template); Assert.NotEqual(Original, result.Text);
        foreach (var request in replies.Inputs) {
            using var data = JsonDocument.Parse(request.GetProperty("messages")[1].GetProperty("content").GetString()!);
            Assert.Equal(user, data.RootElement.GetProperty("authorizedUserRequest").GetString());
            Assert.Equal(interpretation, data.RootElement.GetProperty("contentTransformation").GetString());
            Assert.Equal(Original, data.RootElement.GetProperty("source").GetString());
            Assert.False(data.RootElement.TryGetProperty("intent", out _));
        }
    }
}
