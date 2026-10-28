using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;
using static QuickPrompt.Accounts.Tests.AccountHttpTests;

namespace QuickPrompt.Accounts.Tests;

public sealed class CoverageRealProviderTests
{
    private const string SoftwareRequest = "Crea un prompt reutilizable para conceptualizar ideas de software web, apps, IA y Spatial Web Apps 3D. Debe describir el propósito detallado, estructura de carpetas y archivos, principios SOLID, patrón Factory y otros patrones apropiados según https://refactoring.guru/design-patterns, habilidades, agentes, reglas, librerías y tecnologías. Si incluye IA debe analizar patrones RAG y Agentic. El resultado debe estar en español, formato Markdown. Las variables de entrada son nombre del proyecto, propósito y tipo de sistema.";

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task ObservedGenericOutlineMustNotPassAsTheDetailedReusableInstruction()
    {
        var root = RepositoryRoot();
        var path = Path.Combine(root, ".playwright/saas-delivery-20261026/market-single-definition-native-b/approved-template.md");
        var body = File.ReadAllText(path);
        // Independent oracle fixed before inference: the observed outline omits
        // skills/files and does not instruct the detailed requested analysis.
        // This expectation is NEVER sent in the reviewer's model context.
        await Review("observed-outline", SoftwareRequest, body, "CONTENT_FIDELITY_MISMATCH");
    }

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task CompleteParaphrasePreservesTheWorkWithoutRepeatingTheOriginalWording()
    {
        const string body = """
            Actúa como arquitecto de software. A partir del nombre <nombre_proyecto>, la finalidad <proposito> y la categoría <tipo>, desarrolla una conceptualización completa de la idea. Admite sitios web, aplicaciones, sistemas con IA y experiencias web espaciales tridimensionales.
            Explica con detalle para qué sirve el proyecto, qué problema resuelve, a quién ayuda, sus objetivos, alcance y funciones principales; relaciona esas decisiones con los tres datos de entrada.
            Presenta un árbol de directorios y archivos clave. Explica la responsabilidad de cada módulo y archivo y cómo aplicar cada principio SOLID en sus dependencias e interfaces.
            Diseña la creación de componentes con Factory, justifica su utilidad en este proyecto y compara otros patrones adecuados consultando https://refactoring.guru/design-patterns y sus propósitos. Evita elegir patrones sin explicar su relación con las necesidades de la idea.
            Identifica las habilidades necesarias, los agentes que conviene definir y las reglas que regirán su trabajo. Para cada uno explica su función, responsabilidades y relación con la arquitectura.
            Evalúa tecnologías y librerías concretas: ventajas, límites, compatibilidad, alternativas y por qué encajan con el tipo y propósito del sistema.
            Si el proyecto utiliza IA, analiza qué partes necesitan recuperación aumentada por generación (RAG), comportamiento Agentic o una combinación. Define su arquitectura, flujo de datos, memoria, herramientas y límites según el problema; justifica la selección.
            Escribe el resultado en español y en Markdown con secciones claras, incluyendo el nombre, el propósito detallado y el tipo del proyecto.
            """;
        await Review("complete-paraphrase", SoftwareRequest, body, null);
    }

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task LegitimateFuturePromptWritingAndJsonAreContentRatherThanCurrentProtocol()
    {
        const string request = "Crea una instrucción reutilizable para que un docente diseñe un prompt de práctica. El prompt futuro debe pedir exactamente dos ejercicios de álgebra con sus soluciones. El docente debe entregar su prompt como JSON con campos taskInstruction y rubric, sin texto fuera del objeto. No generes tú los ejercicios ahora; sólo redacta la instrucción para el docente.";
        const string body = "Diseña un prompt de práctica destinado a estudiantes de álgebra. En ese prompt solicita exactamente dos ejercicios de álgebra y sus respectivas soluciones. Entrega el prompt en un único objeto JSON: taskInstruction contiene su instrucción y rubric sus criterios de evaluación. No escribas texto fuera del objeto JSON.";
        await Review("nested-future-json", request, body, null);
    }

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task SourceConstraintsSurviveAnAuthorizedDeltaAndCurrentWorkflowStaysOutsideTheArtifact()
    {
        const string request = "Adapta la instrucción para preparar cinco preguntas de entrevista en vez de tres. Conserva el idioma español, la salida Markdown y que no invente experiencia del candidato. No ejecutes la entrevista ni guardes cambios todavía.";
        const string source = "Prepara tres preguntas para entrevistar a un candidato. Trabaja en español y entrega la guía en Markdown. No inventes experiencia ni historial del candidato.";
        const string body = "Prepara una guía de entrevista con cinco preguntas para el candidato. Escribe en español usando Markdown. Basa la guía en los datos disponibles del candidato y no inventes su experiencia ni su historial.";
        await Review("source-delta", request, body, null, source);
    }

    private static async Task Review(string name, string request, string body, string? expectedCode, string? source = null)
    {
        var document = source is null ? null : new PromptDocument(Guid.NewGuid(), "Guía de entrevista", "", source,
            0, false, false, 1, DateTime.UtcNow, DateTime.UtcNow, 0, null, [], new Dictionary<string, string>());
        var plan = new LiveCommand(source is null ? SemanticOperation.CREATE_DRAFT : SemanticOperation.PROPOSE_ADAPTATION, request)
            .Compile(request, document is null ? SemanticContext.Empty : SemanticContext.Empty with { Selected = SemanticEntity.From(document) });
        plan.Goal!.BindRequestedTitle(request, null);
        var transport = new Recorded(RealProvider(), name); var diagnostics = new List<object>();
        var proposal = new PromptProposal(source is null ? PreparationDecision.CREATE_NEW : PreparationDecision.ADAPT_EXISTING,
            document?.Id, document?.Title ?? "Conceptualización", "", body, [], "", "");
        using var lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var error = await Record.ExceptionAsync(() => new DraftFidelityReviewer(transport).ReviewDraftFidelity(
            new(Guid.NewGuid(), request, SourcePromptId: document?.Id, SourceRevision: document?.Revision) { Interpretation = plan },
            proposal, document is null ? [] : [new(document, 1, "Synthetic source")], lifetime.Token,
            diagnostic: (stage, content, code) => diagnostics.Add(new { stage, content, code })));
        var actualCode = (error as ValidationException)?.Data["NovaValidationCode"] as string;
        var assessed = plan.Goal.HasContentAssessment(body);
        var passed = expectedCode is null ? error is null && assessed : actualCode == expectedCode && !assessed;
        SaveRealProviderEvidence(name + "-result", new { status = passed ? "passed" : "failed", expected = expectedCode ?? "EXACT_BODY_ASSESSED",
            actual = error is null ? "EXACT_BODY_ASSESSED" : actualCode ?? (error as PreparationToolException)?.Code ?? error.GetType().Name, assessed, transport.Calls, request, source, body, diagnostics,
            scope = "Real Qwen3:8b through Adaptive Core; reviewer only, no PostgreSQL, effects, DOM or human acceptance" });
        if (expectedCode is null) { Assert.Null(error); Assert.True(assessed); }
        else { Assert.Equal(expectedCode, actualCode); Assert.False(assessed); }
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "QuickPrompt.Nova.sln"))) return directory.FullName;
        throw new InvalidOperationException("Synthetic artifact repository was not found.");
    }

    private sealed class Recorded(IValidatedAgentModelTransport inner, string name) : IValidatedAgentModelTransport
    {
        public int Calls { get; private set; }
        public bool UsesThinking(string model) => inner.UsesThinking(model);
        public void Observe(int stage, string content, string? code) => inner.Observe(stage, content, code);
        public void Reject(string? response) => inner.Reject(response);
        public async Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            var number = ++Calls; var request = JsonSerializer.SerializeToElement(packet);
            try {
                var response = await inner.Send(packet, ct, purpose);
                SaveRealProviderEvidence(name + "-call-" + number, new { purpose, request, response = response.RootElement.Clone() });
                return response;
            } catch (Exception error) {
                SaveRealProviderEvidence(name + "-call-" + number, new { purpose, request, failureType = error.GetType().Name, failureCode = (error as PreparationToolException)?.Code ?? (error as ValidationException)?.Data["NovaValidationCode"] as string }); throw;
            }
        }
    }
}
