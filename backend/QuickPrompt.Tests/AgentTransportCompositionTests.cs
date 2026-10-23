using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Calls the actual host composition function, without starting a host/model or DB.
public sealed class AgentTransportCompositionTests
{
    [Fact] public void TransportCanResolveWhenTheLegacyInterpreterCannotBeConstructed()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["QuickPrompt:Ollama"] = "http://127.0.0.1:1"
        }).Build();
        var path = Path.Combine(Path.GetTempPath(), "nova-transport-composition-" + Guid.NewGuid(), "synthetic.sqlite");
        var module = Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", throwOnError: true)!;
        var factory = module.GetMethod("AddNovaAgentModule", BindingFlags.Public | BindingFlags.Static)!;
        factory.Invoke(null, [services, configuration, path]);
        // If the transport still aliases PreparationInference, resolution fails.
        services.AddSingleton<PreparationInference>(_ => throw new InvalidOperationException("The legacy interpreter is deliberately unavailable."));
        using var provider = services.BuildServiceProvider();
        var transport = provider.GetRequiredService<IAgentModelTransport>();
        Assert.IsType<AdaptiveCoreModelTransport>(transport);
        Assert.Same(transport, provider.GetRequiredService<IValidatedAgentModelTransport>());
        Assert.Same(transport, provider.GetRequiredService<AdaptiveCoreModelTransport>());
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
    }
}
