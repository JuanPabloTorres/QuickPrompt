using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

namespace QuickPrompt.Nova.Api;

// Composition factories only. Domain state, rules and effects stay with their owners.
// No service locator is passed to the model and no provider is selected by user text.
internal static class ApplicationModules
{
    public static IServiceCollection AddPromptLibraryModule(this IServiceCollection services, string dataPath)
    {
        services.AddSingleton(new LocalPromptLibrary(dataPath));
        services.AddSingleton(sp => sp.GetRequiredService<LocalPromptLibrary>().Store);
        services.AddSingleton(sp => sp.GetRequiredService<LocalPromptLibrary>().PromptStore);
        services.AddSingleton<IPromptReader>(sp => sp.GetRequiredService<NovaPromptStore>());
        services.AddSingleton<IPromptWriter>(sp => sp.GetRequiredService<NovaPromptStore>());
        services.AddSingleton(sp => sp.GetRequiredService<LocalPromptLibrary>().Prompts);
        services.AddSingleton<IPromptCatalog>(sp => sp.GetRequiredService<PromptLibrary>());
        services.AddSingleton<IResultHistory>(sp => sp.GetRequiredService<LocalStore>());
        services.AddSingleton(sp => sp.GetRequiredService<LocalPromptLibrary>().Collections);
        services.AddSingleton<ICollectionReader>(sp => sp.GetRequiredService<NovaCollectionStore>());
        services.AddSingleton<ICollectionWriter>(sp => sp.GetRequiredService<NovaCollectionStore>());
        services.AddSingleton(sp => sp.GetRequiredService<LocalPromptLibrary>().Archives);
        services.AddSingleton<ILibraryArchiveReader>(sp => sp.GetRequiredService<NovaLibraryArchives>());
        services.AddSingleton<ILibraryArchiveWriter>(sp => sp.GetRequiredService<NovaLibraryArchives>());
        services.AddSingleton(sp => sp.GetRequiredService<LocalPromptLibrary>().Sharing);
        services.AddSingleton(sp => sp.GetRequiredService<LocalPromptLibrary>().Transfers);
        services.AddSingleton(sp => sp.GetRequiredService<LocalPromptLibrary>().LegacyConverter);
        return services;
    }

    public static IServiceCollection AddNovaAgentModule(this IServiceCollection services, IConfiguration configuration, string dataPath)
    {
        var settings = NovaAgentSettings.Read(configuration);
        var runtime = settings.Runtime;
        if (runtime != "adaptive-core") throw new InvalidOperationException("La ruta agéntica nativa fue retirada. Configura QuickPrompt:Agent:Runtime=adaptive-core.");
        var core = new AdaptiveCoreOptions(settings.CorePython, Path.GetFullPath(settings.CoreWorker),
            Path.GetFullPath(settings.CoreSdkRoot), settings.Endpoint, settings.SemanticThinking, settings.QwenThinkingSampling);
        // Optional AI must not prevent opening the manual library. Missing Core
        // dependencies fail explicitly when an AI request crosses the boundary.
        services.AddSingleton(core);
        services.AddSingleton<ActionChoiceInterpreter>();
        services.AddSingleton<ILocalEmbeddings>(_ => new OllamaEmbeddings(settings.Endpoint));
        services.AddSingleton<ILocalLlmClient>(sp => OllamaClient.CreateOwned(
            settings.Endpoint, settings.AiTimeout, LocalModelPolicy.AgentContextLength, settings.QwenThinkingSampling,
            modelTransport: sp.GetRequiredService<IAgentModelTransport>()));
        services.AddSingleton<INovaInference>(sp => new NovaInference(sp.GetRequiredService<ILocalLlmClient>(),
            sp.GetRequiredService<ISemanticIntentInterpreter>()));
        services.AddSingleton<NovaAssistant>();
        services.AddSingleton(settings.Budget);
        services.AddSingleton<AdaptiveCoreModelTransport>();
        services.AddSingleton<IAgentModelTransport>(sp => sp.GetRequiredService<AdaptiveCoreModelTransport>());
        services.AddSingleton<IValidatedAgentModelTransport>(sp => sp.GetRequiredService<AdaptiveCoreModelTransport>());
        services.AddSingleton<IInputValueExtractor>(sp => new PromptInputValueExtractor(sp.GetRequiredService<IAgentModelTransport>()));
        services.AddSingleton<IInputScopeInterpreter>(sp => new PromptInputScopeInterpreter(sp.GetRequiredService<IInputValueExtractor>()));
        services.AddSingleton<IDraftFidelityReviewer>(sp => new DraftFidelityReviewer(sp.GetRequiredService<IValidatedAgentModelTransport>(),
            semanticThinking: settings.SemanticThinking,
            includeSystemKnowledge: settings.AuthoringUseContract));
        services.AddSingleton<IPromptGenerator>(sp => new PromptDraftGenerator(sp.GetRequiredService<IValidatedAgentModelTransport>(),
            sp.GetRequiredService<ILocalLlmClient>(), sp.GetRequiredService<IInputValueExtractor>(), sp.GetRequiredService<IDraftFidelityReviewer>(),
            semanticThinking: settings.SemanticThinking,
            includeSystemKnowledge: settings.AuthoringUseContract));
        services.AddSingleton<IIntentInterpreter>(sp => sp.GetRequiredService<ActionChoiceInterpreter>());
        services.AddSingleton<ISemanticIntentInterpreter>(sp => sp.GetRequiredService<ActionChoiceInterpreter>());
        services.AddSingleton<ISemanticContentInterpreter, PromptContentInterpreter>();
        services.AddSingleton<ISemanticAdviceResponder>(sp => new PromptAdviceResponder(sp.GetRequiredService<IValidatedAgentModelTransport>(), settings.SemanticThinking));
        services.AddSingleton<ITaskPlanner>(sp => new NovaTaskPlanner(sp.GetRequiredService<ISemanticIntentInterpreter>(),
            sp.GetRequiredService<IPromptRetriever>(), sp.GetRequiredService<AgentSessions>(), sp.GetRequiredService<ICandidateSelector>(),
            settings.Budget, settings.DirectSourceRead));
        services.AddSingleton<ICandidateSelector>(sp => new SemanticCandidateSelector(sp.GetRequiredService<IAgentModelTransport>()));
        services.AddSingleton<IPromptRetriever, SqlitePromptRetriever>();
        services.AddSingleton<LocalCommands>();
        services.AddSingleton<IVariableResolver, GroundedVariableResolver>();
        services.AddSingleton<IPromptEvaluator, DeterministicPromptEvaluator>();
        services.AddSingleton<IPreparedDraftStore, PreparedDraftStore>();
        services.AddSingleton<IAgentRunStore, AgentRunStore>();
        services.AddSingleton<AgentSessions>();
        services.AddSingleton<IEditorDraftPreparation, EditorDraftPreparation>();
        services.AddSingleton<IPreparedDraftCompletion, PreparedDraftCompletion>();
        services.AddSingleton<IPreparedDraftSaving, PreparedDraftSaving>();
        services.AddSingleton<ILocalPreparedExecutor, LocalPreparedExecutor>();
        services.AddSingleton<IPreparationReadTools, PreparationReadTools>();
        services.AddSingleton<PreparationDiagnostics>();
        services.AddSingleton<IPreparationPreferences, PreparationPreferenceStore>();
        services.AddSingleton<AgentOrchestrator>(sp => ActivatorUtilities.CreateInstance<AgentOrchestrator>(sp,
            settings.StopAfterVerifiedResult, settings.ExecuteGroundedPlan));
        return services;
    }

    public static IServiceCollection AddVisibleExecutionModule(this IServiceCollection services, IConfiguration configuration)
    {
        var worker = configuration["QuickPrompt:Live:Worker"] ?? Path.Combine(Directory.GetCurrentDirectory(), ".playwright/browser-worker-build/worker.cjs");
        var node = configuration["QuickPrompt:Live:Node"] ?? "node";
        services.AddSingleton<ILiveRunStore, LiveRunStore>();
        services.AddSingleton<LiveRuns>();
        services.AddSingleton<SameTabUiDispatcher>();
        services.AddSingleton<IUiDecisionModel>(sp => new UiDecisionModel(sp.GetRequiredService<IAgentModelTransport>()));
        services.AddSingleton<IVisibleBrowserFactory>(_ => new VisibleBrowserFactory(
            Path.GetFullPath(worker), node));
        return services;
    }
}
