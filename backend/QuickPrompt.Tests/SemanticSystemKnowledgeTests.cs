using System.Text.Json;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class SemanticSystemKnowledgeTests
{
    [Fact]
    public void TheApplicationModelUsesActualMembersWithoutReadingAnyInstance()
    {
        var descriptors = CapabilityDiscovery.Representations(typeof(PromptDocument), typeof(UiVisibleState), typeof(TaskGoal), typeof(InstructionAuthoring));
        Assert.Equal(Enum.GetValues<AgentRepresentation>(), descriptors.Select(item => item.Role));
        Assert.Equal("TaskGoal." + nameof(TaskGoal.UserRequest), descriptors.Single(x => x.Role == AgentRepresentation.UserWorkOrder).Member);
        Assert.Equal("InstructionAuthoring." + nameof(InstructionAuthoring.ContentInstruction), descriptors.Single(x => x.Role == AgentRepresentation.LiteralAuthoringDirective).Member);
        Assert.Equal("PromptDocument." + nameof(PromptDocument.TemplateText), descriptors.Single(x => x.Role == AgentRepresentation.ReusableInstruction).Member);
        Assert.Equal("UiVisibleState." + nameof(UiVisibleState.Values), descriptors.Single(x => x.Role == AgentRepresentation.CaseValues).Member);
        Assert.Equal("UiVisibleState." + nameof(UiVisibleState.CompiledText), descriptors.Single(x => x.Role == AgentRepresentation.RenderedInstruction).Member);
        var metadataOnly = Assert.Single(CapabilityDiscovery.Representations(typeof(ThrowingGetter)));
        Assert.Equal("ThrowingGetter.Secret", metadataOnly.Member);
    }

    [Fact]
    public void ThreeConsumersShareTheSameModelButHaveDistinctResponsibilitiesAndVersions()
    {
        var resources = new[] { (typeof(SemanticIntentInterpreter), "DeclareAuthoringUse"), (typeof(PromptDraftGenerator), "GenerateAuthoredDefinition"),
            (typeof(DraftFidelityReviewer), "ReviewDraftFidelity") }
            .Select(stage => SemanticSystemKnowledge.ForMethod(stage.Item1, stage.Item2)!.Value).ToArray();
        var definitions = resources.Select(item => item.GetProperty("resource").GetProperty("representations").GetRawText()).ToArray();
        Assert.Single(definitions.Distinct());
        Assert.Equal(3, resources.Select(item => item.GetProperty("version").GetString()).Distinct().Count());
        Assert.Equal(3, resources.Select(item => item.GetProperty("resource").GetProperty("responsibility").GetString()).Distinct().Count());
        Assert.All(resources, resource => {
            Assert.True(System.Text.Encoding.UTF8.GetByteCount(resource.GetRawText()) <= 8000);
            Assert.Equal(5, resource.GetProperty("resource").GetProperty("representations").GetArrayLength());
        });
        var again = SemanticSystemKnowledge.ForMethod(typeof(PromptDraftGenerator), "GenerateAuthoredDefinition")!.Value;
        Assert.Equal(resources[1].GetRawText(), again.GetRawText());
    }

    [Fact]
    public void UnregisteredConsumersHaveNoImplicitKnowledgeOrAuthority()
    {
        Assert.Null(SemanticSystemKnowledge.ForMethod(typeof(PreparationInference), "ExtractInputs"));
        Assert.Null(SemanticSystemKnowledge.ForMethod(typeof(PreparationInference), "untrusted_model_selected_name"));
    }

    [Theory]
    [InlineData(typeof(DuplicateRoles))]
    [InlineData(typeof(PrivateRepresentation))]
    [InlineData(typeof(EmptyMeaning))]
    public void InvalidPropertyMetadataCannotBecomeKnowledge(Type declarations)
        => Assert.Throws<InvalidOperationException>(() => CapabilityDiscovery.Representations(declarations));

    [Theory]
    [InlineData(typeof(AmbiguousStage), "Choose")]
    [InlineData(typeof(EmptyStage), "Choose")]
    public void InvalidConsumersFailRatherThanSilentlyChoosingAContract(Type owner, string method)
        => Assert.Throws<InvalidOperationException>(() => SemanticSystemKnowledge.ForMethod(owner, method));

    private sealed class ThrowingGetter
    {
        [AgentRepresentation(AgentRepresentation.CaseValues, "Metadata only; must not execute getter.")]
        public string Secret => throw new InvalidOperationException("PRIVATE_INSTANCE_VALUE_MUST_NEVER_BE_READ");
    }
    private sealed class DuplicateRoles
    {
        [AgentRepresentation(AgentRepresentation.CaseValues, "First")]
        public string First => "";
        [AgentRepresentation(AgentRepresentation.CaseValues, "Second")]
        public string Second => "";
    }
    private sealed class PrivateRepresentation
    {
        [AgentRepresentation(AgentRepresentation.CaseValues, "Private")]
        private string Secret => throw new InvalidOperationException();
    }
    private sealed class EmptyMeaning
    {
        [AgentRepresentation(AgentRepresentation.CaseValues, " ")]
        public string Value => "";
    }
    private sealed class AmbiguousStage
    {
        [AgentStage("First")]
        private void Choose() { }
        [AgentStage("Second")]
        private void Choose(int value) { }
    }
    private sealed class EmptyStage
    {
        [AgentStage(" ")]
        private void Choose() { }
    }
}
