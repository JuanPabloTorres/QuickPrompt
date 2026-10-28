using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

// One owner for safe suspension; this does not interpret natural language.
public static class TaskClarification
{
    public static SemanticInterpretation Apply(SemanticInterpretation plan, string question, string reason = "UNRESOLVED_SOURCE") => plan with {
        Intentions = [SemanticOperation.ASK_CLARIFICATION], ProposedSteps = [new(1, SemanticOperation.ASK_CLARIFICATION, [])],
        DesiredOutcome = SemanticOutcome.CLARIFICATION, MissingInformation = [question], ResolvedTargetId = null, ExecutionEvidence = "", ValidationNotes = [..plan.ValidationNotes ?? [], reason],
        DeferredGoals = plan.DeferredGoals ?? plan.Intentions.Where(SemanticPlanCompiler.Goals.Contains).ToArray(), DeferredOutcome = plan.DeferredOutcome ?? plan.DesiredOutcome };
}
