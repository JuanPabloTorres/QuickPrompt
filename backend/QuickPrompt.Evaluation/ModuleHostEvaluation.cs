using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuickPrompt.Local;
using QuickPrompt.Nova.Api;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

namespace QuickPrompt.Nova.Evaluation;

// Synthetic substitution host only, not a production profile or semantic candidate.
internal static class ModuleHostEvaluation
{
    private static readonly Type[] AgentPorts = [ typeof(ILocalLlmClient), typeof(INovaInference),
        typeof(IIntentInterpreter), typeof(ISemanticIntentInterpreter), typeof(ISemanticContentInterpreter),
        typeof(ISemanticAdviceResponder), typeof(ISourceReferenceResolver), typeof(ISemanticClauseReviewer), typeof(IGroundedSourceSelector),
        typeof(IPromptGenerator), typeof(ICandidateSelector), typeof(IInputValueExtractor), typeof(IInputScopeInterpreter),
        typeof(IDraftFidelityReviewer), typeof(IUiDecisionModel),
        typeof(IAgentModelTransport), typeof(IValidatedAgentModelTransport) ];

    public static void Run(string[] args)
    {
        if (args.Length < 2 || args[0] is not ("disabled" or "probe")) throw new ArgumentException("Choose disabled or probe and an owned synthetic database.");
        var mode = args[0]; var hostArgs = args.Skip(1).ToArray();
        var configuration = new ConfigurationBuilder().AddCommandLine(hostArgs).Build();
        var data = Path.GetFullPath(configuration["QuickPrompt:DataPath"] ?? throw new ArgumentException("Explicit synthetic DataPath required."));
        var owned = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, ".playwright"));
        var parent = Directory.GetParent(data)!;
        if (Path.GetFileName(data) != "synthetic.sqlite" || parent.Parent?.FullName != owned
            || !parent.Name.StartsWith("module-host-ui-", StringComparison.Ordinal) || File.Exists(data))
            throw new ArgumentException("Use a new .playwright/module-host-ui-*/synthetic.sqlite fixture.");
        if (configuration.GetValue("QuickPrompt:Demo", false)) throw new ArgumentException("This fixture must start empty.");

        QuickPromptHost.Run(hostArgs, services => {
            foreach (var contract in AgentPorts) {
                services.RemoveAll(contract);
                services.AddSingleton(contract, _ => throw new LlmUnavailableException("El módulo de inferencia está desactivado en esta prueba sintética."));
            }
            // Resolving any old implementation is a failure of substitution, not
            // a permitted fallback. These constructors must never run in this host.
            services.RemoveAll<PreparationInference>();
            services.AddSingleton<PreparationInference>(_ => throw new InvalidOperationException("UNEXPECTED_LEGACY_INFERENCE_CONSTRUCTION"));
            services.RemoveAll<SemanticIntentInterpreter>();
            services.AddSingleton<SemanticIntentInterpreter>(_ => throw new InvalidOperationException("UNEXPECTED_PRODUCTION_INTERPRETER_CONSTRUCTION"));
            services.RemoveAll<OllamaAgentModelTransport>();
            services.AddSingleton<OllamaAgentModelTransport>(_ => throw new InvalidOperationException("UNEXPECTED_MODEL_TRANSPORT_CONSTRUCTION"));
            if (mode == "probe") {
                // Meaning-only fixture: scope is an optional planner capability.
                // Leave it absent instead of constructing the disabled field extractor.
                services.RemoveAll<IInputScopeInterpreter>();
                services.RemoveAll<ISemanticAdviceResponder>();
                services.RemoveAll<ISourceReferenceResolver>();
                services.RemoveAll<ISemanticClauseReviewer>();
                services.RemoveAll<IGroundedSourceSelector>();
                services.AddSingleton(_ => new ClarificationProbe());
                services.RemoveAll<ISemanticIntentInterpreter>();
                services.RemoveAll<IIntentInterpreter>();
                services.RemoveAll<ICandidateSelector>();
                services.AddSingleton<ISemanticIntentInterpreter>(sp => sp.GetRequiredService<ClarificationProbe>());
                services.AddSingleton<IIntentInterpreter>(sp => sp.GetRequiredService<ClarificationProbe>());
                services.AddSingleton<ICandidateSelector>(sp => sp.GetRequiredService<ClarificationProbe>());
            }
        });
    }

    private sealed class ClarificationProbe : ISemanticIntentInterpreter, IIntentInterpreter, ICandidateSelector
    {
        public Task<SemanticInterpretation> Interpret(string message, SemanticContext context, string model, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            const string question = "Módulo sintético: ¿qué información falta?";
            return Task.FromResult(SemanticPlanCompiler.Compile(new([SemanticOperation.ASK_CLARIFICATION], TargetReference.NONE,
                "", null, "", "", new([], true, null, null, [], [], false), SemanticOutcome.CLARIFICATION, [question], false,
                "", null, question)));
        }
        public async Task<Understanding> Understand(string message, string model, CancellationToken ct)
            => (await Interpret(message, SemanticContext.Empty, model, ct)).Preparation();
        public Task<SemanticCandidateChoice> Select(string description, RetrievalCandidate[] candidates, string model, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            throw new LlmUnavailableException("El módulo sintético no ofrece selección de fuentes.");
        }
    }
}
