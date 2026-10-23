using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class LegacyInferenceRetirementTests
{
    [Fact] public void ProductionAssembliesDoNotContainTheLegacyAggregateOrDependOnEvaluation()
    {
        Assert.Equal("QuickPrompt.Evaluation", typeof(PreparationInference).Assembly.GetName().Name);
        foreach (var assembly in new[] { typeof(PromptContentInterpreter).Assembly, typeof(AgentOrchestrator).Assembly,
            typeof(QuickPrompt.Local.Template).Assembly, Assembly.Load("QuickPrompt.Api") }) {
            Assert.Null(assembly.GetType("QuickPrompt.Web.Infrastructure.PreparationInference", false));
            Assert.Null(assembly.GetType("QuickPrompt.Web.Infrastructure.SemanticIntentInterpreter", false));
            Assert.Null(assembly.GetType("QuickPrompt.Web.Infrastructure.AdaptiveCoreInterpreter", false));
            Assert.Null(assembly.GetType("QuickPrompt.Nova.Evaluation.Retired.AdaptiveCoreInterpreter", false));
            Assert.Null(assembly.GetType("QuickPrompt.Web.Infrastructure.PromptActionProtocol", false));
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => reference.Name == "QuickPrompt.Evaluation");
        }
    }

    [Fact] public void ProductionFactoryHasNoLegacyAggregateRegistrationAndKeepsIndependentPorts()
    {
        var services = new ServiceCollection(); var config = new ConfigurationBuilder().Build();
        Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", true)!
            .GetMethod("AddNovaAgentModule", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [services, config, Path.Combine(Path.GetTempPath(), "nova-retirement-" + Guid.NewGuid(), "synthetic.sqlite")]);
        Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", true)!
            .GetMethod("AddVisibleExecutionModule", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [services, config]);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(PreparationInference));
        using var provider = services.BuildServiceProvider();
        var semantic = Assert.IsType<PromptContentInterpreter>(provider.GetRequiredService<ISemanticContentInterpreter>());
        Assert.IsType<ActionChoiceInterpreter>(provider.GetRequiredService<ISemanticIntentInterpreter>());
        Assert.NotSame(semantic, provider.GetRequiredService<ISemanticIntentInterpreter>());
        Assert.IsType<PromptAdviceResponder>(provider.GetRequiredService<ISemanticAdviceResponder>());
        Assert.IsType<PromptDraftGenerator>(provider.GetRequiredService<IPromptGenerator>());
        Assert.IsType<UiDecisionModel>(provider.GetRequiredService<IUiDecisionModel>());
    }
}
