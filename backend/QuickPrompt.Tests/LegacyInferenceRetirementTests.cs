using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class LegacyInferenceRetirementTests
{
    [Fact]
    public void ExistingInferenceTypeIdentitiesForwardToTheSharedModule()
    {
        var forwarded = Assembly.Load("QuickPrompt.Infrastructure").GetForwardedTypes();
        foreach (var type in new[] { typeof(AdaptiveCoreOptions), typeof(AdaptiveCoreModelTransport), typeof(IAgentModelTransport),
            typeof(IValidatedAgentModelTransport), typeof(DevelopmentLabObserver), typeof(ObservedLiveRunStore),
            typeof(OllamaClient), typeof(PromptDraftGenerator), typeof(DraftFidelityReviewer), typeof(PromptInputValueExtractor),
            typeof(PromptInputScopeInterpreter), typeof(SemanticCandidateSelector), typeof(UiDecisionModel), typeof(PromptContentInterpreter),
            typeof(PromptAdviceResponder), typeof(AgentStagePacket), typeof(ValidatedAgentJson), typeof(InferenceObservation) })
            Assert.Contains(type, forwarded);
        Assert.Equal("QuickPrompt.Inference", typeof(AdaptiveCoreModelTransport).Assembly.GetName().Name);
        Assert.Equal("QuickPrompt.Application", typeof(IAgentModelTransport).Assembly.GetName().Name);
        Assert.Equal("QuickPrompt.Application", typeof(IValidatedAgentModelTransport).Assembly.GetName().Name);
        foreach (var type in new[] { typeof(OllamaClient), typeof(PromptDraftGenerator), typeof(DraftFidelityReviewer), typeof(PromptInputValueExtractor),
            typeof(PromptInputScopeInterpreter), typeof(SemanticCandidateSelector), typeof(UiDecisionModel), typeof(PromptContentInterpreter),
            typeof(PromptAdviceResponder), typeof(AgentStagePacket), typeof(ValidatedAgentJson) })
        {
            Assert.Equal("QuickPrompt.Inference", type.Assembly.GetName().Name);
            Assert.Equal(type, Type.GetType(type.FullName + ", QuickPrompt.Infrastructure", throwOnError: true));
        }
        Assert.Equal("QuickPrompt.Application", typeof(InferenceObservation).Assembly.GetName().Name);
        Assert.DoesNotContain(typeof(PromptDraftGenerator).Assembly.GetReferencedAssemblies(), reference => reference.Name is "Microsoft.Data.Sqlite" or "Npgsql" or "QuickPrompt.Infrastructure");
        var inferenceForwarded = Assembly.Load("QuickPrompt.Inference").GetForwardedTypes();
        Assert.Contains(typeof(IAgentModelTransport), inferenceForwarded);
        Assert.Contains(typeof(IValidatedAgentModelTransport), inferenceForwarded);
    }
    [Fact]
    public void ProductionAssembliesDoNotContainTheLegacyAggregateOrDependOnEvaluation()
    {
        Assert.Equal("QuickPrompt.Evaluation", typeof(PreparationInference).Assembly.GetName().Name);
        foreach (var assembly in new[] { typeof(PromptContentInterpreter).Assembly, typeof(AgentOrchestrator).Assembly,
            typeof(QuickPrompt.Local.Template).Assembly, Assembly.Load("QuickPrompt.Api") })
        {
            Assert.Null(assembly.GetType("QuickPrompt.Web.Infrastructure.PreparationInference", false));
            Assert.Null(assembly.GetType("QuickPrompt.Web.Infrastructure.SemanticIntentInterpreter", false));
            Assert.Null(assembly.GetType("QuickPrompt.Web.Infrastructure.AdaptiveCoreInterpreter", false));
            Assert.Null(assembly.GetType("QuickPrompt.Nova.Evaluation.Retired.AdaptiveCoreInterpreter", false));
            Assert.Null(assembly.GetType("QuickPrompt.Web.Infrastructure.PromptActionProtocol", false));
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => reference.Name == "QuickPrompt.Evaluation");
        }
    }

    [Fact]
    public void ProductionFactoryHasNoLegacyAggregateRegistrationAndKeepsIndependentPorts()
    {
        var services = new ServiceCollection(); var config = new ConfigurationBuilder().Build();
        Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", true)!
            .GetMethod("AddNovaAgentModule", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [services, config, Path.Combine(Path.GetTempPath(), "nova-retirement-" + Guid.NewGuid(), "synthetic.sqlite")]);
        Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", true)!
            .GetMethod("AddVisibleExecutionModule", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [services, config]);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(PreparationInference));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IDraftFidelityReviewer));
        // These legacy ports must not even be constructed to resolve production
        // authoring. A reviewer/editor allocation would fail this actual factory.
        services.AddSingleton<IDraftFidelityReviewer>(_ => throw new InvalidOperationException("RETIRED_AUTHORING_REVIEWER_CONSTRUCTED"));
        services.AddSingleton<ILocalLlmClient>(_ => throw new InvalidOperationException("LEGACY_GENERATIVE_EDITOR_CONSTRUCTED"));
        using var provider = services.BuildServiceProvider();
        var semantic = Assert.IsType<PromptContentInterpreter>(provider.GetRequiredService<ISemanticContentInterpreter>());
        Assert.IsType<ActionChoiceInterpreter>(provider.GetRequiredService<ISemanticIntentInterpreter>());
        Assert.NotSame(semantic, provider.GetRequiredService<ISemanticIntentInterpreter>());
        Assert.IsType<PromptAdviceResponder>(provider.GetRequiredService<ISemanticAdviceResponder>());
        Assert.IsType<PromptDraftGenerator>(provider.GetRequiredService<IPromptGenerator>());
        Assert.IsType<UiDecisionModel>(provider.GetRequiredService<IUiDecisionModel>());
    }
}
