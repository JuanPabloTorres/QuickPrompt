using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Cloud;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Explicit hardware/provider campaign; ordinary CI reports it skipped, never a synthetic provider PASS.</summary>
public sealed class RealQwenFactAttribute : FactAttribute
{
    public RealQwenFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_REAL_QWEN") != "1")
            Skip = "Requires explicit local Qwen campaign configuration and its selected provider.";
    }
}

public sealed partial class AccountHttpTests
{
    [RealQwenFact]
    [Trait("Category", "RealQwen")]
    public async Task CanonicalPrivateAuthoringWithRealQwenCreatesThreeActualVariablesWithoutWritingLibrary()
    {
        var provider = RealProvider();
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        var grant = await ProposalDevice(owner, device); var actor = await ProposalActor(owner);
        await using var scope = host.App.Services.CreateAsyncScope();
        using var nova = new AccountNovaInference(scope.ServiceProvider.GetRequiredService<AccountModelProposals>(), actor, source.Library, grant.Connection.Id);
        const string request = "Crea un prompt para conceptualizar ideas de software: webs, apps, móvil, AI y Spatial Web Apps 3D. Debe describir el propósito detallado, estructura de carpetas y archivos, principios SOLID, patrón Factory y otros patrones apropiados según https://refactoring.guru/design-patterns, habilidades, agentes, reglas, librerías y tecnologías. Si incluye IA debe analizar patrones RAG y Agentic. El resultado debe estar en español, formato Markdown. Las variables de entrada son nombre del proyecto, propósito y tipo de sistema.";
        string[] names = ["nombre_proyecto", "proposito", "tipo"];
        var command = new LiveCommand(SemanticOperation.CREATE_DRAFT, request) { InputNames = names };
        var plan = command.Compile(request, SemanticContext.Empty);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        var work = nova.Generator.Generate(new(Guid.NewGuid(), request) { Interpretation = plan }, plan.Preparation(), [], lifetime.Token);
        var calls = await ServeRealModelCalls(device, work, provider, lifetime);
        var result = await work;
        Assert.Equal(PreparationDecision.CREATE_NEW, result.Decision);
        Assert.Equal(names.Order(), TemplateCompiler.Names(result.TemplateText).Order());
        Assert.Empty(result.Values);
        foreach (var requirement in new[] { "SOLID", "Factory", "RAG", "Agentic" }) Assert.Contains(requirement, result.TemplateText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://refactoring.guru/design-patterns", result.TemplateText, StringComparison.Ordinal);
        var rendered = TemplateCompiler.Render(result.TemplateText, new Dictionary<string, string> { ["nombre_proyecto"] = "Proyecto Iris QA", ["proposito"] = "Organizar una biblioteca de instrucciones", ["tipo"] = "Web" });
        Assert.Contains("Proyecto Iris QA", rendered); Assert.Contains("Organizar una biblioteca de instrucciones", rendered); Assert.Contains("Web", rendered);
        Assert.Equal(source.Prompt, await owner.GetFromJsonAsync<QuickPrompt.Accounts.Libraries.AccountPrompt>($"/api/accounts/v1/libraries/{source.Library}/prompts/{source.Prompt.Id}"));
        SaveRealProviderEvidence("canonical-private-authoring", new
        {
            status = "passed",
            model = "qwen3:8b",
            calls,
            result.Title,
            result.TemplateText,
            actualNames = TemplateCompiler.Names(result.TemplateText),
            scope = "Real canonical authoring over private queue; visible orchestration and approval not exercised"
        });
    }

    internal static AdaptiveCoreModelTransport RealProvider()
    {
        string Required(string key) => Environment.GetEnvironmentVariable(key) is { Length: > 0 } value ? value
            : throw new InvalidOperationException("Missing explicit campaign configuration: " + key);
        var provider = Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_PROVIDER") ?? "ollama";
        var endpoint = Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_MODEL_ENDPOINT")
            ?? Required("QUICKPROMPT_TEST_OLLAMA_ENDPOINT");
        var configuredThinking = Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_THINKING");
        if (configuredThinking is not (null or "true" or "false")) throw new InvalidOperationException("Campaign thinking must be true or false.");
        var options = new AdaptiveCoreOptions(Required("QUICKPROMPT_TEST_CORE_PYTHON"), Required("QUICKPROMPT_TEST_CORE_WORKER"),
            Required("QUICKPROMPT_TEST_CORE_SDK"), endpoint, Thinking: configuredThinking is null ? provider == "ollama" : configuredThinking == "true", Provider: provider,
            PhysicalModel: Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_PHYSICAL_MODEL"));
        options.Validate(); return new(options);
    }

    [RealQwenFact]
    [Trait("Category", "RealQwen")]
    public async Task RealSourceRolesKeepAFutureReferenceAndExcludeTheCurrentReviewLinkInAnotherDomain()
    {
        var provider = RealProvider();
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        var grant = await ProposalDevice(owner, device); var actor = await ProposalActor(owner);
        await using var scope = host.App.Services.CreateAsyncScope();
        using var nova = new AccountNovaInference(scope.ServiceProvider.GetRequiredService<AccountModelProposals>(), actor, source.Library, grant.Connection.Id);
        const string future = "https://reference.example.test/lectura?edition=2#resumen";
        const string current = "https://session.example.test/review";
        const string request = "Crea un prompt reutilizable para resumir textos de estudio. El futuro asistente debe resumir el texto aportado, distinguir ideas principales de detalles y no inventar información. Usa como referencia de lectura " + future
            + ". El resultado del lector futuro debe estar en español. Cuando termines de redactar este borrador, revísalo conmigo en " + current + " antes de guardarlo. Ese segundo enlace sólo es para esta revisión actual, no para el lector futuro.";
        var plan = new LiveCommand(SemanticOperation.CREATE_DRAFT, request) { InputNames = ["texto"] }.Compile(request, SemanticContext.Empty);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        var work = nova.Generator.Generate(new(Guid.NewGuid(), request) { Interpretation = plan }, plan.Preparation(), [], lifetime.Token);
        var calls = await ServeRealModelCalls(device, work, provider, lifetime, "study-reference-scopes");
        var result = await work;
        Assert.Equal(PreparationDecision.CREATE_NEW, result.Decision);
        Assert.Equal(new[] { "texto" }, TemplateCompiler.Names(result.TemplateText));
        Assert.Contains(future, result.TemplateText, StringComparison.Ordinal); Assert.DoesNotContain(current, result.TemplateText, StringComparison.Ordinal);
        var contract = plan.Goal!.ReferenceCoverage!;
        Assert.Equal(AuthoringReferenceRole.FUTURE_TASK, contract.Uses.Single(use => contract.References[use.Id - 1] == future).Role);
        Assert.Equal(AuthoringReferenceRole.CURRENT_SESSION, contract.Uses.Single(use => contract.References[use.Id - 1] == current).Role);
        Assert.Equal(source.Prompt, await owner.GetFromJsonAsync<QuickPrompt.Accounts.Libraries.AccountPrompt>($"/api/accounts/v1/libraries/{source.Library}/prompts/{source.Prompt.Id}"));
        SaveRealProviderEvidence("study-reference-scopes", new { status = "passed", model = "qwen3:8b", calls, result.TemplateText,
            expectedFuture = future, excludedCurrent = current, roles = contract.Uses, scope = "Real second-domain authoring, not approval or external navigation" });
    }

    [RealQwenFact]
    [Trait("Category", "RealQwen")]
    public async Task RealProviderConnectionFailureTerminatesThePrivateRequestWithoutRetainingAResult()
    {
        // Reserve then release an ephemeral loopback port; do not stop or reconfigure the user's Ollama.
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        string Required(string key) => Environment.GetEnvironmentVariable(key) is { Length: > 0 } value ? value
            : throw new InvalidOperationException("Missing explicit campaign configuration: " + key);
        var options = new AdaptiveCoreOptions(Required("QUICKPROMPT_TEST_CORE_PYTHON"), Required("QUICKPROMPT_TEST_CORE_WORKER"),
            Required("QUICKPROMPT_TEST_CORE_SDK"), $"http://127.0.0.1:{port}");
        options.Validate(); var provider = new AdaptiveCoreModelTransport(options);
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        var grant = await ProposalDevice(owner, device); var actor = await ProposalActor(owner);
        await using var scope = host.App.Services.CreateAsyncScope();
        using var nova = new AccountNovaInference(scope.ServiceProvider.GetRequiredService<AccountModelProposals>(), actor, source.Library, grant.Connection.Id);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var pending = nova.InputValues.ExtractInputs(new(Guid.NewGuid(), "tema: caso de conexión rechazada"), "Explica <tema>.", ["tema"], lifetime.Token);
        var lease = await AwaitProposal(device);
        await Assert.ThrowsAsync<LlmUnavailableException>(() => provider.Send(lease.Packet, lifetime.Token, "qa.real.connection-failure"));
        Assert.Equal(HttpStatusCode.NoContent, (await FinishProposal(device, lease, null, "MODEL_UNAVAILABLE")).StatusCode);
        await Assert.ThrowsAsync<LlmUnavailableException>(() => pending);
        watch.Stop(); Assert.InRange(watch.Elapsed.TotalSeconds, 0, 30);
        Assert.Equal(0L, await ProposalRows(actor.Id, lease.Id));
        using var next = await ClaimProposal(device); Assert.Equal(HttpStatusCode.NoContent, next.StatusCode);
        SaveRealProviderEvidence("canonical-private-model-unavailable", new
        {
            status = "passed",
            expectedFailure = "MODEL_UNAVAILABLE",
            model = "qwen3:8b",
            seconds = watch.Elapsed.TotalSeconds,
            retainedResult = false,
            scope = "Real SDK connection failure over private queue; visible NOVA error UI not exercised"
        });
    }

    [RealQwenFact]
    [Trait("Category", "RealQwen")]
    public async Task RealVisibleAccountEngineAuthorsReviewsAndCommitsOnlyTheApprovedPrivatePrompt()
    {
        var provider = RealProvider();
        await using var host = await Start(); using var owner = Client(host.App); using var other = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); await Login(other, await Register(other, host.Mail));
        var own = await LocalLibrary(owner); var foreign = await LocalLibrary(other); var grant = await ProposalDevice(owner, device);
        const string message = "Crea un prompt reutilizable para conceptualizar ideas de software web, apps, IA y Spatial Web Apps 3D. Debe describir el propósito detallado, estructura de carpetas y archivos, principios SOLID, patrón Factory y otros patrones apropiados según https://refactoring.guru/design-patterns, habilidades, agentes, reglas, librerías y tecnologías. Si incluye IA debe analizar patrones RAG y Agentic. El resultado debe estar en español, formato Markdown. Las variables de entrada son nombre del proyecto, propósito y tipo de sistema.";
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), message, Command: new(SemanticOperation.CREATE_DRAFT, message)
            { InputNames = ["nombre_proyecto", "proposito", "tipo"] });
        VisibleSurface(owner, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
        using var started = await owner.PostAsJsonAsync(VisiblePath(own.Library) + "/live", new AccountLiveStart(grant.Connection.Id, input));
        started.EnsureSuccessStatusCode();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        var work = DriveVisibleCreation(owner, own.Library, input.RunId, lifetime.Token);
        var calls = await ServeRealModelCalls(device, work, provider, lifetime, "visible-private-creation");
        var result = await work;
        Assert.True(result.Run.Status == LiveStatus.COMPLETED, "Visible engine did not complete: " + result.Run.ErrorCode + "; " + result.Question);
        Assert.True(result.Run.EffectCommitted); Assert.Equal("CONSUMED", result.Run.Approval); Assert.False(result.Run.BrowserOpen);
        var id = result.Run.TargetId!.Value;
        var saved = (await owner.GetFromJsonAsync<AccountPrompt>($"/api/accounts/v1/libraries/{own.Library}/prompts/{id}"))!;
        Assert.Equal(1, saved.Revision); Assert.Equal(result.Proposal!.Draft.TemplateText, saved.Template);
        Assert.Equal(new[] { "nombre_proyecto", "proposito", "tipo" }, TemplateCompiler.Names(saved.Template).Order());
        Assert.Contains("https://refactoring.guru/design-patterns", saved.Template, StringComparison.Ordinal);
        foreach (var requirement in new[] { "SOLID", "Factory", "RAG", "Agentic" }) Assert.Contains(requirement, saved.Template, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/accounts/v1/libraries/{own.Library}/prompts/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(VisiblePath(foreign.Library) + $"/live/{input.RunId}")).StatusCode);
        var duplicate = await owner.PostAsJsonAsync(VisiblePath(own.Library) + $"/live/{input.RunId}/commit", result.Proposal.Draft);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(2, (await owner.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{own.Library}/prompts"))!.Length);
        SaveRealProviderEvidence("visible-private-creation", new { status = "passed", calls, model = "qwen3:8b", runId = input.RunId, saved.Id, saved.Revision,
            actualInputs = TemplateCompiler.Names(saved.Template), saved.Template, runStatus = result.Run.Status, result.Run.EffectCommitted,
            scope = "Authenticated HTTP, real engine/DB/model and simulated UI receipts; not a real DOM or human acceptance" });
    }

    // Test-only device adapter uses the actual claim/heartbeat/finish boundary and real SDK.
    // This is not a replacement production worker or evidence of DPAPI/native-client acceptance.
    private static async Task<int> ServeRealModelCalls(HttpClient device, Task work, AdaptiveCoreModelTransport provider, CancellationTokenSource lifetime,
        string evidencePrefix = "canonical-private-authoring")
    {
        var calls = 0;
        try
        {
            while (!work.IsCompleted)
            {
                lifetime.Token.ThrowIfCancellationRequested();
                using var claim = await ClaimProposal(device);
                if (claim.StatusCode == HttpStatusCode.NoContent) { await Task.WhenAny(work, Task.Delay(50, lifetime.Token)); continue; }
                Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
                var lease = (await claim.Content.ReadFromJsonAsync<LocalProposalLease>())!;
                Assert.True(++calls <= 12, "The real campaign exceeded its bounded inference stages.");
                using var modelLimit = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                modelLimit.CancelAfter(TimeSpan.FromSeconds(lease.MaximumSeconds));
                try
                {
                    var inference = provider.Send(lease.Packet, modelLimit.Token, "qa.private.canonical");
                    while (!inference.IsCompleted)
                    {
                        if (await Task.WhenAny(inference, Task.Delay(TimeSpan.FromSeconds(5), modelLimit.Token)) == inference) break;
                        modelLimit.Token.ThrowIfCancellationRequested();
                        using var pulse = await device.PostAsJsonAsync($"/api/companion/v1/proposals/{lease.Id}/heartbeat", new LocalLeaseToken(lease.Token), modelLimit.Token);
                        Assert.Equal(HttpStatusCode.OK, pulse.StatusCode);
                        var receipt = (await pulse.Content.ReadFromJsonAsync<LocalLeasePulse>())!;
                        Assert.True(receipt.Owned); Assert.False(receipt.CancelRequested);
                    }
                    using var reply = await inference;
                    Assert.Equal(HttpStatusCode.NoContent, (await FinishProposal(device, lease, reply.RootElement)).StatusCode);
                }
                catch
                {
                    modelLimit.Cancel();
                    using var failed = await FinishProposal(device, lease, null, "MODEL_FAILED");
                    throw;
                }
            }
            await work; return calls;
        }
        catch (Exception error)
        {
            lifetime.Cancel();
            SaveRealProviderEvidence(evidencePrefix + "-failure", new
            {
                status = "failed",
                model = "qwen3:8b",
                calls,
                error = error.GetType().Name,
                code = error is PreparationToolException tool ? tool.Code : null,
                scope = "Real provider campaign, failure preserved"
            });
            try { await work; } catch { /* Observe the parent task without replacing the first failing boundary. */ }
            throw;
        }
    }

    internal static void SaveRealProviderEvidence(string name, object result)
    {
        var directory = Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_REAL_EVIDENCE")
            ?? throw new InvalidOperationException("An explicit synthetic evidence directory is required.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name + ".json"), JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
