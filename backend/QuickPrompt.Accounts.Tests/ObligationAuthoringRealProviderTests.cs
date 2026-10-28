using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;
using static QuickPrompt.Accounts.Tests.AccountHttpTests;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Actual authoring, not replay of the retired judge. Human and browser acceptance remain untested.</summary>
public sealed class ObligationAuthoringRealProviderTests
{
    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task OriginalSoftwareRequestProducesANewReusableInstructionWithThreeRealFields()
    {
        const string request = "Crea un prompt reutilizable para conceptualizar ideas de software web, apps, IA y Spatial Web Apps 3D. Debe describir el propósito detallado, estructura de carpetas y archivos, principios SOLID, patrón Factory y otros patrones apropiados según https://refactoring.guru/design-patterns, habilidades, agentes, reglas, librerías y tecnologías. Si incluye IA debe analizar patrones RAG y Agentic. El resultado debe estar en español, formato Markdown. Las variables de entrada son nombre del proyecto, propósito y tipo de sistema.";
        var proposal = await Author("software-original", request, ["nombre_proyecto", "proposito", "tipo"], null,
            "Inspect against the complete original request: detailed future purpose; folders AND files; SOLID; Factory and justified patterns/reference; skills, agents, rules, libraries and technologies; conditional RAG/Agentic; Spanish Markdown. A generic outline or filled project response is insufficient.");
        Assert.Contains("https://refactoring.guru/design-patterns", proposal.TemplateText);
        Assert.Equal(new[] { "nombre_proyecto", "proposito", "tipo" }, TemplateCompiler.Names(proposal.TemplateText));
    }

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task AnotherDomainProducesAnActionableClaimEmailInstructionWithoutInventingFacts()
    {
        const string request = "Crea un prompt reutilizable para redactar un correo de reclamo a partir de cliente, pedido, problema y solución esperada. El correo debe explicar los hechos con claridad, mantener un tono firme y cortés, pedir una respuesta en cinco días hábiles y no inventar compras, fechas ni acuerdos.";
        await Author("claim-email", request, ["cliente", "pedido", "problema", "solucion_esperada"], null,
            "Inspect actual usefulness: a future instruction that uses all four inputs, produces a coherent claim email, keeps firm/courteous tone and five-business-day request, and does not invent purchases/dates/agreements. Do not accept field declarations alone as task coverage.");
    }

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task SourceAdaptationIsActuallyShorterWhileTheRealInputAndFixedProhibitionSurvive()
    {
        const string body = "Explica <tema> de una manera clara para personas que empiezan, dedicando una explicación extensa a los antecedentes, una segunda explicación a su importancia, una tercera descripción de su contexto y varias consideraciones adicionales que repiten las mismas ideas desde distintos puntos de vista. Organiza el desarrollo como una guía introductoria, presenta el contexto general y vuelve a explicar ese contexto con otros ejemplos, evitando dar por hecho que el lector conoce previamente la materia. No inventes datos.";
        var source = new PromptDocument(Guid.NewGuid(), "Explicación introductoria", "Guía sintética para una campaña aislada sin persistencia.", body,
            0, false, false, 1, DateTime.UtcNow, DateTime.UtcNow, 0, null, ["tema"], new Dictionary<string, string>());
        var proposal = await Author("shorten-source", "Acorta de verdad esta instrucción para que sea clara y directa, conserva su propósito introductorio, el campo tema y la prohibición de inventar datos.", [], source,
            "Inspect actual rewrite rather than appended source: genuinely shorter, introductory explanatory purpose retained, input tema usable, exact protected prohibition retained. Original source must remain the real source snapshot.");
        Assert.True(proposal.TemplateText.Length < source.TemplateText.Length);
        Assert.Contains("No inventes datos.", proposal.TemplateText); Assert.Equal(new[] { "tema" }, TemplateCompiler.Names(proposal.TemplateText));
        Assert.Equal(source.Id, proposal.SourceId);
    }

    private static async Task<PromptProposal> Author(string name, string request, string[] fields, PromptDocument? source, string independentInspection)
    {
        var directory = Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_REAL_EVIDENCE") ?? throw new InvalidOperationException("Explicit synthetic evidence directory required.");
        Directory.CreateDirectory(directory);
        var root = RepositoryRoot();
        using var lab = new DevelopmentLabObserver(true, Path.Combine(directory, name + "-lab"), root, captureSyntheticContent: true);
        string Required(string key) => Environment.GetEnvironmentVariable(key) is { Length: > 0 } value ? value : throw new InvalidOperationException("Missing campaign configuration: " + key);
        var provider = new AdaptiveCoreModelTransport(new(Required("QUICKPROMPT_TEST_CORE_PYTHON"), Required("QUICKPROMPT_TEST_CORE_WORKER"),
            Required("QUICKPROMPT_TEST_CORE_SDK"), Required("QUICKPROMPT_TEST_OLLAMA_ENDPOINT")), diagnostics: lab);
        var transport = new Recorded(provider, name);
        var generator = new PromptDraftGenerator(transport, new PromptInputValueExtractor(transport));
        var operation = source is null ? SemanticOperation.CREATE_DRAFT : SemanticOperation.PROPOSE_ADAPTATION;
        var context = source is null ? SemanticContext.Empty : SemanticContext.Empty with { Selected = SemanticEntity.From(source) };
        var plan = new LiveCommand(operation, request) { InputNames = fields }.Compile(request, context);
        var input = new PrepareInput(Guid.NewGuid(), request, SourcePromptId: source?.Id, SourceRevision: source?.Revision) { Interpretation = plan };
        var diagnostics = new List<object>(); PromptProposal? proposal = null;
        // Same normal preparation deadline; no increase of model/context/stage budgets.
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(new PreparationOptions().TotalTimeoutSeconds));
        var failure = await Record.ExceptionAsync(async () => proposal = await generator.Generate(input, plan.Preparation(),
            source is null ? [] : [new(source, 1, "Synthetic real-provider source")], lifetime.Token,
            (stage, content, code) => diagnostics.Add(new { stage, content, code })));
        if (proposal is not null) File.WriteAllText(Path.Combine(directory, name + "-body.md"), proposal.TemplateText);
        var ready = failure is null && proposal?.Decision is PreparationDecision.CREATE_NEW or PreparationDecision.ADAPT_EXISTING;
        SaveRealProviderEvidence(name + "-result", new { technicalProposal = ready ? "READY_FOR_INSPECTION" : "FAILED_OR_CLARIFICATION",
            semanticAcceptance = "UNCONFIRMED_PENDING_INDEPENDENT_INSPECTION", humanAcceptance = "NOT_TESTED", browser = "NOT_TESTED",
            scope = "Real Qwen3:8b one full draft proposal + app-owned source/fields/literal-change review. No semantic judge, database, persistence or effects.",
            independentInspection, request, source, proposal, failureType = failure?.GetType().Name,
            failureCode = (failure as PreparationToolException)?.Code ?? (failure as LlmUnavailableException)?.Code ?? (failure as ValidationException)?.Data["NovaValidationCode"] as string,
            transport.Calls, diagnostics, contract = plan.Goal?.AuthoringContract?.GetContentReview(proposal?.TemplateText ?? ""),
            assessed = plan.Goal?.HasContentAssessment(proposal?.TemplateText ?? ""), humanApproved = plan.Goal?.HasHumanContentApproval(proposal?.TemplateText ?? "") });
        Assert.Null(failure); Assert.NotNull(proposal); Assert.True(ready, "A clarification is safe but is not a completed authoring outcome.");
        Assert.True(plan.Goal!.HasAuthoringStructure(proposal.TemplateText));
        Assert.False(plan.Goal.HasContentAssessment(proposal.TemplateText)); Assert.False(plan.Goal.HasHumanContentApproval(proposal.TemplateText));
        Assert.Equal("REQUIRES_HUMAN_CONTENT_REVIEW", plan.Goal.AuthoringContract!.GetContentReview(proposal.TemplateText).Status);
        return proposal;
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "QuickPrompt.Nova.sln"))) return directory.FullName;
        throw new InvalidOperationException("Repository root not found.");
    }
    private sealed class Recorded(IValidatedAgentModelTransport inner, string name) : IValidatedAgentModelTransport
    {
        public int Calls;
        public async Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            var sequence = ++Calls;
            try {
                var response = await inner.Send(packet, ct, purpose);
                SaveRealProviderEvidence(name + "-stage-" + sequence, new { purpose, request = JsonSerializer.SerializeToElement(packet), response = response.RootElement.Clone() });
                return response;
            } catch (Exception error) {
                SaveRealProviderEvidence(name + "-stage-" + sequence, new { purpose, request = JsonSerializer.SerializeToElement(packet), failureType = error.GetType().Name,
                    failureCode = (error as PreparationToolException)?.Code ?? (error as LlmUnavailableException)?.Code }); throw;
            }
        }
        public bool UsesThinking(string model) => inner.UsesThinking(model);
        public void Observe(int stage, string content, string? code) => inner.Observe(stage, content, code);
        public void Reject(string? answer) => inner.Reject(answer);
    }
}
