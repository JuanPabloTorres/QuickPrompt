using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public class GroundedUiPlanTests
{
    [Theory]
    [InlineData(SemanticOperation.OPEN_PROMPT)]
    [InlineData(SemanticOperation.EXPLAIN)]
    [InlineData(SemanticOperation.PROPOSE_ADAPTATION)]
    public void NonBindingGoalsDoNotReintroduceIncidentalValuesOrAskForFutureCaseData(SemanticOperation operation)
    {
        var plan=SemanticPlanCompiler.Compile(new([operation],TargetReference.SELECTED,source.Title,source.Id,"","",
            new([],true,null,null,[],[],false),SemanticOutcome.READ,[],false,"","",""));
        var state=Discover() with {Mode="focus",PromptId=source.Id,Revision=source.Revision,TemplateText=source.TemplateText,
            VariableNames=["tema"],Values=new(){["tema"]=""}};
        var input=new LiveStartInput(Guid.NewGuid(),Guid.NewGuid(),"Petición independiente.\ntema: valor incidental",
            SourceId:source.Id,SourceRevision:source.Revision,Values:new(){["tema"]="valor incidental"});
        Assert.Empty(UiToolCatalog.GroundedValues(input,state,plan));
        var available=UiToolCatalog.All.Where(tool=>tool.Name=="ask_clarification").ToArray();
        Assert.Null(UiToolCatalog.BoundGoal(available,state,plan,[]));
    }

    [Theory]
    [InlineData(SemanticOperation.COPY_PROMPT)]
    [InlineData(SemanticOperation.PREPARE_PROVIDER)]
    public void BindingGoalsStillReceiveLiteralDataAndAskOnlyForUnfilledInputs(SemanticOperation operation)
    {
        var plan=SemanticPlanCompiler.Compile(new([operation],TargetReference.SELECTED,source.Title,source.Id,"","",
            new([],true,null,null,[],[],false),SemanticOutcome.READ,[],false,"","",""));
        var state=Discover() with {Mode="focus",PromptId=source.Id,Revision=source.Revision,TemplateText=source.TemplateText,
            VariableNames=["tema"],Values=new(){["tema"]=""}};
        var input=new LiveStartInput(Guid.NewGuid(),Guid.NewGuid(),"Prepara el resultado.\ntema: dato literal",SourceId:source.Id,SourceRevision:source.Revision);
        Assert.Equal("dato literal",UiToolCatalog.GroundedValues(input,state,plan)["tema"]);
        Assert.Equal("ask_clarification",UiToolCatalog.BoundGoal(UiToolCatalog.All.Where(t=>t.Name=="ask_clarification").ToArray(),state,plan,[])?.ToolName);
    }

    private readonly PromptDocument source = new(Guid.NewGuid(), "Un título real", "", "Explica <tema>.", 0, false, false, 3, DateTime.UtcNow, DateTime.UtcNow, 0, null, ["tema"], new Dictionary<string, string>());
    private UiVisibleState Discover() => new("discover", [new(source.Id, source.Title, source.Revision)], null, null, null, null, [], [], false, false, null, null, false, false);
    [Fact] public void NavigationUsesTheResolvedTitleAndOnlyOpensItsVisibleCurrentIdentity()
    {
        var available = UiToolCatalog.All.Where(t => t.Name is "ui_search_prompt" or "ui_open_prompt").ToArray();
        var first = UiToolCatalog.BoundNavigation(available, Discover(), source, false)!;
        Assert.Equal("ui_search_prompt", first.ToolName); Assert.Equal(source.Title, first.Arguments.GetProperty("query").GetString());
        var next = UiToolCatalog.BoundNavigation(available, Discover(), source, true)!;
        Assert.Equal("ui_open_prompt", next.ToolName); Assert.Equal(source.Id, next.Arguments.GetProperty("promptId").GetGuid());
        Assert.Null(UiToolCatalog.BoundNavigation(available, Discover() with { Candidates = [] }, source, true));
        Assert.Null(UiToolCatalog.BoundNavigation(available, Discover() with { Candidates = [new(source.Id, source.Title, 2)] }, source, true));
        Assert.Null(UiToolCatalog.BoundNavigation([], Discover(), source, false));
        Assert.Null(UiToolCatalog.BoundNavigation(available, Discover(), null, false));
        Assert.Null(UiToolCatalog.BoundNavigation(available, Discover() with { Mode = "create" }, source, false));
    }
    [Fact] public void BoundValuesStillPassTheSameEvidenceAndPermissionValidation()
    {
        var state = Discover() with { Mode = "focus", PromptId = source.Id, Revision = source.Revision, TemplateText = source.TemplateText, VariableNames = ["tema"], Values = new() { ["tema"] = "" } };
        var available = UiToolCatalog.All.Where(t => t.Name == "ui_fill_variable").ToArray();
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Completa el campo.\ntema: React 🚀", SourceId: source.Id, SourceRevision: source.Revision);
        var decision = UiToolCatalog.BoundInput(available, state, new Dictionary<string, string> { ["tema"] = "React 🚀" })!;
        UiToolCatalog.Validate(decision, available, state, input);
        var forged = decision with { Arguments = JsonSerializer.SerializeToElement(new { promptId = source.Id, name = "tema", value = "Inventado", evidence = "Inventado" }) };
        Assert.Throws<ValidationException>(() => UiToolCatalog.Validate(forged, available, state, input));
        Assert.Null(UiToolCatalog.BoundInput(available, state, new Dictionary<string, string> { ["desconocido"] = "React" }));
        Assert.Null(UiToolCatalog.BoundInput([], state, new Dictionary<string, string> { ["tema"] = "React" }));
        Assert.Null(UiToolCatalog.BoundInput(available, state with { Values = new() { ["tema"] = "React 🚀" } }, new Dictionary<string, string> { ["tema"] = "React 🚀" }));
    }
    [Fact] public void ResolvedSourceNavigatesFromAnotherFocusWithoutInventingArgumentsOrDiscardingAnEditor()
    {
        var available = UiToolCatalog.All.Where(tool => tool.Name == "ui_navigate_library").ToArray();
        var other = Discover() with { Mode = "focus", PromptId = Guid.NewGuid() };
        var decision = UiToolCatalog.BoundNavigation(available, other, source, false)!;
        Assert.Equal("ui_navigate_library", decision.ToolName);
        ContractSchema.Validate(decision.Arguments, available[0].ArgumentsSchema);
        Assert.Empty(decision.Arguments.EnumerateObject());
        Assert.Null(UiToolCatalog.BoundNavigation(available, other with { Mode = "create" }, source, false));
        Assert.Null(UiToolCatalog.BoundNavigation(available, other with { PromptId = source.Id }, source, false));
    }
    [Fact] public void LegacyValidatedPlanUsesTheSameFiniteExecutionTransitionsWithoutReclassifyingEachClick()
    {
        var plan = SemanticPlanCompiler.Compile(new([SemanticOperation.EXECUTE_LOCAL], TargetReference.SELECTED, source.Title, source.Id, "", "",
            new([], true, null, null, [], [], false), SemanticOutcome.LOCAL_RESPONSE, [], false, "Ejecuta ahora.", "ollama", "Ejecutar."));
        Assert.Null(plan.Goal);
        var state = Discover() with { Mode = "focus", PromptId = source.Id, Revision = source.Revision, CompiledReady = true };
        UiToolContract[] Only(string name) => UiToolCatalog.All.Where(tool => tool.Name == name).ToArray();
        Assert.Equal("ui_open_provider_picker", UiToolCatalog.BoundGoal(Only("ui_open_provider_picker"), state, plan, [])!.ToolName);
        var execute = UiToolCatalog.BoundGoal(Only("ui_execute_local"), state, plan, [])!;
        Assert.Equal(source.Id, execute.Arguments.GetProperty("promptId").GetGuid());
        Assert.Null(UiToolCatalog.BoundGoal(Only("ui_execute_local"), state, plan, ["ui_execute_local"]));
    }
}
