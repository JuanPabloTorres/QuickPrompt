using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Exhaustive typed-contract combinations test domain boundaries, not language accuracy.
public sealed class SemanticCapabilityMatrixTests
{
    [Fact]
    public void ComposedObjectivesCannotEscalateCapabilitiesAcrossDomainsOrSourceIdentities()
    {
        var accepted = 0; var rejected = 0;
        foreach (var domain in new[] { "Investigación", "Atención al cliente", "Software" })
        foreach (var saved in new[] { false, true })
        foreach (var access in Enum.GetValues<ReadObjective>())
        foreach (var draft in Enum.GetValues<DraftObjective>())
        foreach (var execution in Enum.GetValues<ExecutionObjective>())
        foreach (var control in Enum.GetValues<ControlObjective>())
        foreach (var save in Enum.GetValues<SaveObjective>())
        foreach (var values in Enum.GetValues<ValuesObjective>())
        {
            var entity = new SemanticEntity(Guid.NewGuid(), 7, domain, "Explica <tema>.", false);
            var context = saved ? SemanticContext.Empty with { Selected = entity } : SemanticContext.Empty;
            var objectives = new SemanticObjectives(access, draft, execution, control, save,
                saved ? TargetReference.SELECTED : TargetReference.NONE, domain, saved ? entity.Id : null, domain,
                "Reorientar la explicación", control == ControlObjective.CLARIFY ? ["Indica el origen."] : [],
                "Ejecución expresamente solicitada", execution == ExecutionObjective.SHOW_EXTERNAL_DESTINATIONS ? "chatgpt" : null,
                "Contrato generado; no es una inferencia de Qwen.", values);
            SemanticInterpretation plan;
            try {
                plan = SemanticPlanCompiler.Compile(objectives.WithConstraints(new([], false, null, null, [], [], false, "", ""))
                    .Meaning("Ejecución expresamente solicitada", context));
                SemanticPlanValidator.Validate(plan, context, "Ejecución expresamente solicitada");
            } catch (PreparationToolException) { rejected++; continue; }
            accepted++;
            Assert.True(execution == ExecutionObjective.RUN_LOCAL_QWEN_NOW || !plan.Executes);
            Assert.True(draft == DraftObjective.REWRITE_INSTRUCTION_TEXT || !plan.Adapts);
            Assert.True(draft == DraftObjective.CREATE_DISTINCT_INSTRUCTION || !plan.Creates);
            Assert.True(save == SaveObjective.REVIEW_PERSIST_EXISTING || !plan.Has(SemanticOperation.REQUEST_SAVE_VERSION));
            Assert.True(save == SaveObjective.REVIEW_PERSIST_COPY || !plan.Has(SemanticOperation.REQUEST_SAVE_COPY));
            Assert.True(saved ? plan.ResolvedTargetId == entity.Id : plan.ResolvedTargetId is null);
            Assert.False(SemanticPlanValidator.ToolAllowed(plan, "save_prompt"));
            Assert.False(SemanticPlanValidator.ToolAllowed(plan, "execute_sql"));
            if (plan.DesiredOutcome == SemanticOutcome.CLARIFICATION)
                Assert.Equal(new[] { SemanticOperation.ASK_CLARIFICATION }, plan.Intentions);
            if (plan.Adapts || plan.Creates) {
                Assert.Contains(SemanticOperation.VALIDATE_DRAFT, plan.Intentions);
                Assert.Contains(SemanticOperation.COMPILE_PROMPT, plan.Intentions);
            }
            if (execution != ExecutionObjective.RUN_LOCAL_QWEN_NOW) {
                Assert.False(SemanticPlanValidator.ToolAllowed(plan, "execute_local_llm"));
                Assert.False(SemanticPlanValidator.ToolAllowed(plan, "ui_execute_local"));
            }
        }
        Assert.True(accepted > 100, "La matriz debe cubrir planes ejecutables, no solo rechazos.");
        Assert.True(rejected > 100, "Las combinaciones contradictorias deben cerrarse antes de usar herramientas.");
    }
}
