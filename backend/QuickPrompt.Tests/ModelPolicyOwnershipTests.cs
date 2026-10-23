using System.Reflection;
using QuickPrompt.Local;
using QuickPrompt.Nova.Api;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class ModelPolicyOwnershipTests
{
    [Fact] public void PolicyIsDomainOwnedAndInfrastructureForwardsTheExistingPublicTypeNames()
    {
        var domain = typeof(Template).Assembly;
        Assert.Same(domain, typeof(LocalModelPolicy).Assembly); Assert.Same(domain, typeof(LocalModelCapability).Assembly);
        var infrastructure = typeof(OllamaClient).Assembly;
        Assert.Same(typeof(LocalModelPolicy), infrastructure.GetType(typeof(LocalModelPolicy).FullName!, true));
        Assert.Same(typeof(LocalModelCapability), infrastructure.GetType(typeof(LocalModelCapability).FullName!, true));
        Assert.Contains(typeof(LocalModelPolicy), infrastructure.GetForwardedTypes());
        Assert.DoesNotContain(typeof(AgentOrchestrator).Assembly.GetReferencedAssemblies(), a => a.Name == infrastructure.GetName().Name);
        Assert.DoesNotContain(domain.GetReferencedAssemblies(), a => a.Name is "QuickPrompt.Application" or "QuickPrompt.Infrastructure" or "QuickPrompt.Api");
    }

    [Fact] public void QualifiedSubsetIsImmutableAndDoesNotExpandProductionOrAdmitUnknownIdentities()
    {
        var policy = new LocalModelPolicy(new LocalModelCapability("synthetic:first", true), new LocalModelCapability("synthetic:second", false));
        var names = new[] { "synthetic:first", "synthetic:second" }; var qualified = policy.WithAgentControl(names);
        names[0] = "tampered";
        Assert.False(policy.AllowsAgentControl("synthetic:first")); Assert.True(qualified.AllowsAgentControl("synthetic:first"));
        Assert.True(qualified.AllowsAgentControl("synthetic:second")); Assert.False(qualified.AllowsAgentControl("tampered"));
        Assert.Equal(policy.DefaultModel, qualified.DefaultModel); Assert.Equal(policy.Names, qualified.Names);
        Assert.False(LocalModelPolicy.Production.Allows("synthetic:first")); Assert.False(qualified.AllowsAgentControl(null!));
        Assert.False(qualified.WithAgentControl().AllowsAgentControl("synthetic:first"));
        Assert.True(qualified.AllowsAgentControl("synthetic:first"));
        Assert.Throws<ArgumentException>(() => policy.WithAgentControl("outside"));
        Assert.Throws<ArgumentException>(() => policy.WithAgentControl("synthetic:first", "synthetic:first"));
        Assert.Throws<ArgumentException>(() => policy.WithAgentControl(null!));
    }

    [Fact] public void CSharpDtoDefaultsUseTheSameExistingDefaultIdentity()
    {
        Type[] contracts = [typeof(PrepareInput), typeof(NovaRequest), typeof(ProposalInput), typeof(LiveRecord),
            typeof(LiveStartInput), typeof(Preferences), typeof(ExecutePreparedInput)];
        foreach (var contract in contracts) {
            var model = contract.GetConstructors().SelectMany(c => c.GetParameters()).Single(p => p.Name == "Model");
            Assert.True(model.HasDefaultValue); Assert.Equal("qwen3:8b", model.DefaultValue);
            Assert.Equal(LocalModelPolicy.DefaultModelName, model.DefaultValue);
        }
        Assert.Equal("qwen3:8b", LocalModelPolicy.Production.DefaultModel);
        Assert.Equal(new[] { "qwen3:8b", "qwen2.5:7b-instruct" }, LocalModelPolicy.Production.Names);
    }

    [Theory]
    [InlineData("qwen3:8b", true)] [InlineData("qwen2.5:7b-instruct", true)]
    [InlineData("unknown:model", false)] [InlineData("QWEN3:8B", false)] [InlineData(null, false)]
    public void PreparationAdmissionUsesTheSameExactCatalog(string? model, bool allowed)
    {
        var input = new PrepareInput(Guid.NewGuid(), "Solicitud sintética.", Model: model!);
        if (allowed) AgentOrchestrator.Validate(input);
        else Assert.Throws<ValidationException>(() => AgentOrchestrator.Validate(input));
        Assert.Equal(allowed, LocalModelPolicy.Production.Allows(model!));
    }

    [Theory]
    [InlineData("qwen3:8b", "LIVE", true)] [InlineData("qwen3:8b", "SAME_TAB", true)] [InlineData("qwen3:8b", "BROWSER", true)]
    [InlineData("qwen2.5:7b-instruct", "LIVE", true)] [InlineData("qwen2.5:7b-instruct", "SAME_TAB", false)] [InlineData("qwen2.5:7b-instruct", "BROWSER", false)]
    [InlineData("unknown:model", "LIVE", false)] [InlineData("unknown:model", "SAME_TAB", false)] [InlineData("unknown:model", "BROWSER", false)]
    public void RunAdmissionRetainsModeQualificationAndRejectsBeforeWriting(string model, string mode, bool allowed)
    {
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nova-model-policy-" + Guid.NewGuid()));
        try {
            var library = new LocalPromptLibrary(Path.Combine(directory, "synthetic.sqlite"));
            var store = new LiveRunStore(library.Store); var runs = new LiveRuns(store, library.Prompts); var id = Guid.NewGuid();
            var input = new LiveStartInput(id, Guid.NewGuid(), "Solicitud sintética.", mode, Model: model);
            if (allowed) Assert.Equal(model, runs.Create(input).Model);
            else { Assert.Throws<ValidationException>(() => runs.Create(input)); Assert.Null(store.Get(id)); }
        }
        finally {
            Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(directory));
            Assert.StartsWith("nova-model-policy-", Path.GetFileName(directory));
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
