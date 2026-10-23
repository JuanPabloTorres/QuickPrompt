using System.Reflection;
using System.Text.Json;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class PersistenceIdentityContractTests
{
    [Theory]
    [InlineData("NONE", SaveObjective.NONE)]
    [InlineData("UPDATE_CURRENT_RECORD", SaveObjective.REVIEW_PERSIST_EXISTING)]
    [InlineData("CREATE_SEPARATE_RECORD", SaveObjective.REVIEW_PERSIST_COPY)]
    [InlineData("DELETE_CURRENT_RECORD", SaveObjective.REVIEW_DELETE_EXISTING)]
    public void PrivateWireIdentityProjectsToTheExistingPublicEffect(string wire, SaveObjective expected)
    {
        var value = JsonSerializer.SerializeToElement(new { access = "NONE", control = "NONE", save = wire, explanation = "Synthetic declaration." });
        ContractSchema.Validate(value, SemanticIntentInterpreter.ObjectiveSchema);
        var type = typeof(SemanticIntentInterpreter).GetNestedType("RequestedWorkflow", BindingFlags.NonPublic)!;
        var declared = JsonSerializer.Deserialize(value, type, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(expected, type.GetProperty("Save")!.GetValue(declared));
        Assert.Equal(4, SemanticIntentInterpreter.ObjectiveSchema.GetProperty("properties").GetProperty("save").GetProperty("enum").GetArrayLength());
    }

    [Theory]
    [InlineData(SaveObjective.REVIEW_PERSIST_EXISTING, "UPDATE_CURRENT_RECORD")]
    [InlineData(SaveObjective.REVIEW_PERSIST_COPY, "CREATE_SEPARATE_RECORD")]
    public void LoadingASourceCannotChangeTheFrozenPersistenceIdentity(SaveObjective objective, string wire)
    {
        var method = typeof(SemanticIntentInterpreter).GetMethod("WorkflowContract", BindingFlags.NonPublic | BindingFlags.Static)!;
        var schema = (JsonElement)method.Invoke(null, [new SemanticReferenceChoice(TargetReference.NAMED, null, "Synthetic source", false, ""),
            new WorkflowGoalSnapshot("Synthetic directive", ReadObjective.FIND_SAVED, ControlObjective.NONE, objective), false, false])!;
        Assert.Equal(new[] { wire }, schema.GetProperty("properties").GetProperty("save").GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
    }

    [Theory]
    [InlineData(SemanticOperation.FILL_VARIABLES, true)]
    [InlineData(SemanticOperation.PROPOSE_ADAPTATION, true)]
    [InlineData(SemanticOperation.COPY_PROMPT, true)]
    [InlineData(SemanticOperation.CREATE_DRAFT, false)]
    [InlineData(SemanticOperation.CANCEL, false)]
    public void RecoveryUsesTheSameSourcePrerequisiteAsTheCompiler(SemanticOperation operation, bool expected)
    {
        var meaning = new SemanticMeaning([operation], TargetReference.NONE, "", null, "", "",
            new([], true, null, null, [], [], false), SemanticOutcome.READ, [], false, "", null, "");
        Assert.Equal(expected, SemanticPlanCompiler.RequiresSource(meaning));
        var compiled = SemanticPlanCompiler.Compile(meaning);
        Assert.Equal(expected, compiled.ValidationNotes?.Contains("MISSING_TARGET_REFERENCE") == true);
        Assert.Null(compiled.ResolvedTargetId);
    }
}
