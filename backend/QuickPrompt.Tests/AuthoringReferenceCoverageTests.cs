using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class AuthoringReferenceCoverageTests
{
    [Theory]
    [InlineData(AuthoringReferenceRole.FUTURE_TASK, false, true)]
    [InlineData(AuthoringReferenceRole.CURRENT_SESSION, true, false)]
    public void RetryDiagnosticsDescribeOnlyTheSourceBoundReferenceMismatch(AuthoringReferenceRole role, bool included, bool expected)
    {
        const string uri = "https://reference.example.test/architecture#factory";
        const string directive = "Consulta " + uri + " para esta tarea.";
        var coverage = new AuthoringReferenceCoverage(directive)
            .WithScopes(new([new(1, role, directive)], ""));
        var body = included ? "Texto con " + uri : "Texto sin referencia.";
        var failure = Assert.Throws<ValidationException>(() => coverage.ValidateBody(body));
        var details = JsonSerializer.SerializeToElement(failure.Data["NovaContractDetails"]);
        Assert.Equal("AUTHORING_REFERENCES/1", details.GetProperty("contract").GetString());
        var mismatch = Assert.Single(details.GetProperty("mismatches").EnumerateArray());
        Assert.Equal(1, mismatch.GetProperty("id").GetInt32());
        Assert.Equal(uri, mismatch.GetProperty("uri").GetString());
        Assert.Equal(role.ToString(), mismatch.GetProperty("role").GetString());
        Assert.Equal(expected, mismatch.GetProperty("expectedInBody").GetBoolean());
        Assert.Equal(included, mismatch.GetProperty("observedInBody").GetBoolean());
        Assert.DoesNotContain(body, JsonSerializer.Serialize(details));
        Assert.Equal(role, Assert.Single(coverage.Uses).Role);
    }

    private static AuthoringReferenceCoverage Future(AuthoringReferenceCoverage coverage) => coverage.WithScopes(new(
        coverage.References.Select((uri, index) => new AuthoringReferenceUse(index + 1, AuthoringReferenceRole.FUTURE_TASK, uri)).ToArray(), ""));
    [Theory]
    [InlineData("Consulta [la referencia](https://manual.example.test/path_(one)?q=A%20B#Section).", "https://manual.example.test/path_(one)?q=A%20B#Section")]
    [InlineData("Referencia: «HTTPS://example.test/A/B».", "HTTPS://example.test/A/B")]
    [InlineData("Usa https://example.test/ref)] y conserva su alcance.", "https://example.test/ref")]
    public void LiteralReferencesPreserveIdentityWithoutProseDelimiters(string request, string uri)
    {
        var coverage = Future(new(request));
        Assert.Equal(uri, Assert.Single(coverage.References));
        coverage.ValidateBody("Consulta " + uri);
        var error = Assert.Throws<ValidationException>(() => coverage.ValidateBody("Consulta el dominio de la referencia."));
        Assert.Equal("AUTHORING_REFERENCE_BODY_MISMATCH", error.Data["NovaValidationCode"]); Assert.DoesNotContain(uri, error.Message);
    }

    [Fact]
    public void CaseValuesAreExcludedByTheExistingScopeAndSessionReferencesRemainDataWithoutAuthority()
    {
        const string uri = "https://reference.example.test/manual";
        const string control = "Abre https://session.example.test/review al terminar.";
        const string request = "Redacta usando " + uri + ". " + control + " Documento: https://private.example.test/case";
        var directive = GroundedVariableResolver.DirectiveOutsideInputs(request, ["Documento"]);
        var coverage = new AuthoringReferenceCoverage(directive);
        Assert.Equal(new[] { uri, "https://session.example.test/review" }, coverage.References);
        coverage = coverage.WithScopes(new([new(1, AuthoringReferenceRole.FUTURE_TASK, uri), new(2, AuthoringReferenceRole.CURRENT_SESSION, control)], ""));
        coverage.ValidateBody("Usa la referencia " + uri);
        Assert.Throws<ValidationException>(() => coverage.WithScopes(new([new(1, AuthoringReferenceRole.FUTURE_TASK, uri),
            new(2, AuthoringReferenceRole.CURRENT_SESSION, "Abre otro https://session.example.test/review")], "")));
        Assert.Throws<ValidationException>(() => coverage.ValidateBody("Usa https://private.example.test/case"));
        Assert.Throws<ValidationException>(() => coverage.ValidateBody("Usa " + uri + " y https://session.example.test/review"));
    }

    [Fact]
    public void CoverageIsBoundedDoesNotFetchAndDoesNotInventRequirementsWhenNoLinkWasGiven()
    {
        new AuthoringReferenceCoverage("Redacta una guía de estudio.").ValidateBody("Guía redactada.");
        var repeated = new AuthoringReferenceCoverage("Usa https://example.test/ref y https://example.test/ref.");
        Assert.Single(repeated.References);
        Assert.Throws<ValidationException>(() => new AuthoringReferenceCoverage(string.Join(' ', Enumerable.Range(0, 17).Select(i => $"https://example.test/{i}"))));
    }

    [Theory]
    [InlineData("https://example.test/ref-other")]
    [InlineData("https://example.test/ref?different=true")]
    [InlineData("https://example.test/ref/other")]
    public void SharedUriPrefixDoesNotPreserveTheRequestedResource(string replacement)
    {
        var coverage = Future(new("Usa https://example.test/ref como referencia."));
        Assert.Throws<ValidationException>(() => coverage.ValidateBody("Consulta " + replacement));
    }

    [Fact]
    public void SourceRolesCannotBeInferredFromTheBodyReassignedOnRetryOrGroundedInTheUriAloneAsAControl()
    {
        const string uri = "https://reference.example.test/ref";
        const string request = "Crea un prompt usando " + uri + ".";
        var unresolved = new AuthoringReferenceCoverage(request);
        Assert.Throws<ValidationException>(() => unresolved.ValidateBody("Usa " + uri));
        Assert.Throws<ValidationException>(() => unresolved.WithScopes(new([new(2, AuthoringReferenceRole.FUTURE_TASK, uri)], "")));
        Assert.Throws<ValidationException>(() => unresolved.WithScopes(new([new(1, AuthoringReferenceRole.CURRENT_SESSION, uri)], "")));
        var coverage = Future(unresolved);
        Assert.Throws<ValidationException>(() => coverage.ValidateBody("Sin referencia."));
        var plan = new LiveCommand(SemanticOperation.CREATE_DRAFT, request) { InputNames = [] }.Compile(request, SemanticContext.Empty);
        plan.Goal!.BindReferenceCoverage(coverage);
        plan.Goal.RecordContentAssessment("Usa " + uri);
        Assert.Throws<ValidationException>(() => plan.Goal.RecordContentAssessment("Texto que perdió la referencia."));
        var reclassified = unresolved.WithScopes(new([new(1, AuthoringReferenceRole.CURRENT_SESSION, request)], ""));
        Assert.Throws<ValidationException>(() => plan.Goal.BindReferenceCoverage(reclassified));
        Assert.True(plan.Goal.HasContentAssessment("Usa " + uri)); Assert.False(plan.Executes);
    }

    [Fact]
    public void ExplicitDualRoleRequiresBothTheFutureReferenceAndTheOriginalSessionContext()
    {
        const string uri = "https://example.test/shared";
        const string control = "Revisa conmigo en " + uri + ".";
        var coverage = new AuthoringReferenceCoverage("Crea una guía usando " + uri + ". " + control)
            .WithScopes(new([new(1, AuthoringReferenceRole.BOTH, control)], ""));
        coverage.ValidateBody("Consulta " + uri + " para la guía.");
        Assert.Equal(control, Assert.Single(coverage.Uses).Evidence);
        Assert.Throws<ValidationException>(() => coverage.ValidateBody("Guía sin enlace."));
    }

    [Fact]
    public async Task AnUnboundReferenceRoleRemainsAnUnconfirmedProposalWithoutCallingTheRetiredInterpreter()
    {
        const string request = "Crea un prompt; tengo este enlace https://example.test/ref.";
        var plan = new LiveCommand(SemanticOperation.CREATE_DRAFT, request) { InputNames = [] }.Compile(request, SemanticContext.Empty);
        var transport = new Author("Explica la materia con ejemplos."); using var editor = OllamaClient.ForTransport(transport);
        var reviewer = new ForbiddenReview();
        var generator = new PromptDraftGenerator(transport, editor, new PromptInputValueExtractor(transport), reviewer);
        var proposal = await generator.Generate(new(Guid.NewGuid(), request) { Interpretation = plan }, plan.Preparation(), [], default);
        Assert.Equal(PreparationDecision.ASK_CLARIFICATION, proposal.Decision); Assert.Equal(0, transport.Calls); Assert.Equal(0, transport.DiscoveryCalls);
        Assert.Equal(AuthoringBriefs.EstablishTask, proposal.Brief!.Stage);
        Assert.Equal(0, reviewer.Calls); Assert.Null(plan.Goal!.ReferenceCoverage);
        Assert.False(plan.Goal.HasContentAssessment(proposal.TemplateText)); Assert.False(plan.Goal.HasHumanContentApproval(proposal.TemplateText));
        Assert.Null(plan.Goal.AuthoringContract); Assert.Null(Assert.Single(proposal.Brief!.Proposed.References).Role);
        // There is no automatic meaning classifier in this path. This test does
        // not certify that omitting an unbound link satisfies the user's meaning.
    }

    [Theory]
    [InlineData(AuthoringReferenceRole.FUTURE_TASK, false)]
    [InlineData(AuthoringReferenceRole.FUTURE_TASK, true)]
    [InlineData(AuthoringReferenceRole.CURRENT_SESSION, false)]
    [InlineData(AuthoringReferenceRole.CURRENT_SESSION, true)]
    [InlineData(AuthoringReferenceRole.BOTH, false)]
    [InlineData(AuthoringReferenceRole.BOTH, true)]
    public async Task CanonicalAuthoringEnforcesAnAppBoundReferenceRoleWithoutAskingAModelToChooseIt(AuthoringReferenceRole role, bool preserve)
    {
        const string uri = "https://reference.example.test/design/patterns?edition=2#factory";
        const string control = "Revisa conmigo en " + uri + " antes de guardar.";
        const string request = "Crea una instrucción sobre diseño usando " + uri + ". " + control;
        var plan = new LiveCommand(SemanticOperation.CREATE_DRAFT, request) { InputNames = [] }.Compile(request, SemanticContext.Empty);
        // A controlled app-context binding, not a reply from Author transport.
        plan.Goal!.BindReferenceCoverage(new AuthoringReferenceCoverage(request).WithScopes(new([new(1, role,
            role == AuthoringReferenceRole.FUTURE_TASK ? uri : control)], "")));
        var fingerprint = plan.Goal.ReferenceCoverage!.Fingerprint;
        var body = preserve ? "Estudia diseño con " + uri : "Estudia diseño con reference.example.test.";
        var transport = new Author(body); var reviewer = new ForbiddenReview(); using var editor = OllamaClient.ForTransport(transport);
        var generator = new PromptDraftGenerator(transport, editor, new PromptInputValueExtractor(transport), reviewer);
        var work = generator.GenerateAfterSyntheticReview(new(Guid.NewGuid(), request) { Interpretation = plan }, plan.Preparation(), [], default);
        var requiredInBody = role != AuthoringReferenceRole.CURRENT_SESSION;
        if (role != AuthoringReferenceRole.CURRENT_SESSION || !preserve) {
            var proposal = await work; Assert.Equal(body + (requiredInBody && !preserve ? "\n\n" + uri : ""), proposal.TemplateText);
            Assert.True(plan.Goal.HasAuthoringStructure(proposal.TemplateText)); Assert.False(plan.Goal.HasContentAssessment(proposal.TemplateText));
        } else {
            var failure = await Assert.ThrowsAsync<ValidationException>(() => work);
            Assert.Contains("sólo para esta sesión", failure.Message);
            Assert.False(plan.Goal.HasAuthoringStructure(body));
        }
        Assert.Equal(fingerprint, plan.Goal.ReferenceCoverage.Fingerprint);
        Assert.Equal(2, transport.Calls); Assert.Equal(1, transport.DiscoveryCalls); Assert.Equal(0, reviewer.Calls); Assert.False(plan.Executes);
        using var data = JsonDocument.Parse(transport.Last!.Value.GetProperty("messages")[1].GetProperty("content").GetString()!);
        var references = data.RootElement.GetProperty("references");
        Assert.Equal(role == AuthoringReferenceRole.CURRENT_SESSION ? new[] { uri } : [], references.GetProperty("writing").EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.Equal(role == AuthoringReferenceRole.CURRENT_SESSION ? [] : new[] { uri }, references.GetProperty("future").EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.False(data.RootElement.TryGetProperty("providedReferenceCoverage", out _));
    }

    [Fact]
    public async Task AnAppBoundCurrentSessionQuoteKeepsItsLinkOutOfTheReusableBody()
    {
        const string control = "Revisa conmigo en https://session.example.test/review antes de guardar.";
        const string request = "Crea una instrucción para estudiar matemáticas. " + control;
        var plan = new LiveCommand(SemanticOperation.CREATE_DRAFT, request) { InputNames = [] }.Compile(request, SemanticContext.Empty);
        plan.Goal!.BindReferenceCoverage(new AuthoringReferenceCoverage(request).WithScopes(new([new(1, AuthoringReferenceRole.CURRENT_SESSION, control)], "")));
        var transport = new Author("Explica matemáticas con ejemplos."); var reviewer = new ForbiddenReview();
        using var editor = OllamaClient.ForTransport(transport);
        var generator = new PromptDraftGenerator(transport, editor, new PromptInputValueExtractor(transport), reviewer);
        var proposal = await generator.GenerateAfterSyntheticReview(new(Guid.NewGuid(), request) { Interpretation = plan }, plan.Preparation(), [], default);
        Assert.DoesNotContain("https://", proposal.TemplateText); Assert.Equal(0, reviewer.Calls); Assert.False(plan.Executes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScopedCaseUriIsNotPromotedToReferenceAcrossCurrentOrContinuedWorkOrder(bool continued)
    {
        const string reference = "https://reference.example.test/manual";
        const string caseUri = "https://private.example.test/current-case";
        const string original = "Crea una instrucción para resumir <documento> usando la referencia " + reference + ".";
        var message = (continued ? "Continúa." : original) + " Documento: " + caseUri;
        var envelope = new PrepareInput(Guid.NewGuid(), message) { AuthorizedPendingRequest = continued ? original : null };
        var scope = UserInputScope.Bind(message, null, null, ["documento"], [new("documento", caseUri, caseUri)]);
        var plan = new LiveCommand(SemanticOperation.CREATE_DRAFT, envelope.TaskRequest) { InputNames = ["documento"] }.Compile(envelope.TaskRequest, SemanticContext.Empty);
        var directive = GroundedVariableResolver.DirectiveOutsideInputs(message, ["documento"], scope);
        if (continued) directive = original + "\n" + directive;
        plan.Goal!.BindReferenceCoverage(Future(new AuthoringReferenceCoverage(directive)));
        var transport = new Author("Resume <documento> usando " + reference); using var editor = OllamaClient.ForTransport(transport);
        var generator = new PromptDraftGenerator(transport, editor, new CaseValue(caseUri), new ForbiddenReview());
        var proposal = await generator.GenerateAfterSyntheticReview(envelope with { Interpretation = plan, InputScope = scope }, plan.Preparation(), [], default);
        using var data = JsonDocument.Parse(transport.Last!.Value.GetProperty("messages")[1].GetProperty("content").GetString()!);
        var references = data.RootElement.GetProperty("references");
        Assert.Equal(reference, Assert.Single(references.GetProperty("future").EnumerateArray()).GetString());
        Assert.Empty(references.GetProperty("writing").EnumerateArray());
        Assert.DoesNotContain(caseUri, proposal.TemplateText); Assert.Contains(reference, proposal.TemplateText);
        Assert.DoesNotContain(caseUri, transport.Last.Value.GetRawText());
        Assert.False(plan.Goal!.HasContentAssessment(proposal.TemplateText));
    }

    private sealed class Author(string body) : IValidatedAgentModelTransport
    {
        public int Calls; public int DiscoveryCalls; public JsonElement? Last;
        public bool UsesThinking(string model) => false;
        public void Observe(int stage, string content, string? error) { }
        public void Reject(string? content) { }
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested(); Calls++; var encoded = JsonSerializer.SerializeToElement(packet);
            if (AuthoringSyntheticReview.IsCurrentAuthorDecision(encoded)) {
                DiscoveryCalls++;
                return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new {
                    content = JsonSerializer.Serialize(AuthoringSyntheticReview.CurrentAuthorDecisionReady()) } })));
            }
            Last = encoded;
            Assert.False(AuthoringReviewTestFixture.SchemaFields(Last.Value.GetProperty("format")).TryGetProperty("currentSessionControls", out _));
            Assert.Equal("GenerateDraftProposal", purpose);
            Assert.Equal(new[] { "description", "instruction", "literalReference", "outcome", "question", "title" }, AuthoringReviewTestFixture.SchemaFields(Last.Value.GetProperty("format")).EnumerateObject().Select(property => property.Name).Order().ToArray());
            object result = AuthoringReviewTestFixture.Definition(body, "Guía");
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)) } })));
        }
    }
    private sealed class CaseValue(string value) : IInputValueExtractor
    {
        public Task<InputValueExtraction> ExtractInputs(PrepareInput input, string template, string[] names, CancellationToken ct)
            => Task.FromResult(new InputValueExtraction([new("documento", value, value)], ""));
    }
    private sealed class ForbiddenReview : IDraftFidelityReviewer
    {
        public int Calls;
        public Task<AuthoringObligationContract> ReadAuthoringObligations(AuthoringObligationContract material, string model, CancellationToken ct)
        { Calls++; throw new InvalidOperationException("Retired inventory must not choose reference roles."); }
        public Task<AuthoringReferenceInterpretation> ReadReferenceScopes(AuthoringReferenceCoverage references, string model, CancellationToken ct)
        { Calls++; throw new InvalidOperationException("Retired role interpreter must not run."); }
        public Task<string?> ReadRequestedTitle(string request, string[] names, string model, CancellationToken ct)
        { Calls++; throw new InvalidOperationException("Retired title interpreter must not run."); }
        public Task ReviewDraftFidelity(PrepareInput input, PromptProposal proposal, RetrievalCandidate[] candidates, CancellationToken ct, string? boundTitleLiteral = null, Action<int, string, string?>? diagnostic = null)
        { Calls++; throw new InvalidOperationException("A model reviewer cannot approve the proposed body."); }
    }
}
