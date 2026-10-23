using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

// Review is an agent capability. Committing deletion is a separate human action,
// never a model tool. Identity, revision, run and display are bound together.
public sealed partial class LiveRuns
{
    public LiveDeletion ProposeDeletion(Guid id, PromptDocument source)
    {
        var control=Active(id);lock(control.Gate) {
            var run=Get(id);var plan=control.Semantic;
            control.DeletionDeadlineTick ??= Environment.TickCount64 + (long)TimeSpan.FromMinutes(8).TotalMilliseconds;
            if(Environment.TickCount64>=control.DeletionDeadlineTick)throw new ConflictException("Venció el plazo de esta ejecución. Inicia una revisión nueva.");
            if(Terminal(run.Status)||run.PauseRequested||control.ApprovalConsumed||run.EffectCommitted||control.Proposal is not null)
                throw new ConflictException("La ejecución no admite una revisión de eliminación.");
            if(plan?.Deletes!=true||!plan.RequiresConfirmation||plan.ResolvedTargetId!=source.Id||plan.TargetReference==TargetReference.NONE
                ||plan.Constraints.ForbiddenOperations.Contains(SemanticOperation.REQUEST_DELETE))
                throw new ValidationException("La eliminación no está solicitada para este prompt.");
            var directive=GroundedVariableResolver.InstructionDirective(control.AuthorizedTaskRequest??control.Input.Message,source.VariableNames);
            if(!SemanticPlanValidator.DirectEvidence(directive,plan.DeletionEvidence??""))
                throw new ValidationException("La revisión necesita una petición de eliminación fundamentada, no datos del prompt.");
            if(plan.Intentions.Any(operation=>operation is not (SemanticOperation.SEARCH_PROMPT or SemanticOperation.RESOLVE_PROMPT or SemanticOperation.OPEN_PROMPT or SemanticOperation.REQUEST_DELETE)))
                throw new ValidationException("Revisa la eliminación por separado de las modificaciones o ejecuciones del prompt.");
            if(control.Input.SourceId is {} bound && (bound!=source.Id||control.Input.SourceRevision!=source.Revision))
                throw new ConflictException("El destino no coincide con el prompt elegido.");
            if(plan.Goal is {} goal && (!goal.RequestedEffects.Contains(SemanticOperation.REQUEST_DELETE)
                ||!goal.ApprovalRequirements.Contains(SemanticOperation.REQUEST_DELETE)
                ||goal.TargetReferences is not [var target]||target.Id!=source.Id||target.Revision!=source.Revision))
                throw new ValidationException("La eliminación no coincide con el objetivo autorizado.");
            var current=library.Get(source.Id);
            if(current.Revision!=source.Revision||current.Title!=source.Title)throw new ConflictException("El prompt cambió antes de la revisión.");
            if(control.Deletion is {} existing) {
                if(existing.PromptId!=source.Id||existing.Revision!=source.Revision)throw new ConflictException("Esta ejecución ya revisa otro destino.");
                return existing;
            }
            var approvalId=Guid.NewGuid();
            var proposal=new LiveDeletion(approvalId,Hash(new {kind="DELETE_PROMPT",runId=id,approvalId,source.Id,source.Revision,source.Title}),source.Id,source.Revision,source.Title);
            control.Deletion=proposal;
            Emit(id,LiveStatus.AWAITING_APPROVAL,LiveActor.SYSTEM_VALIDATION,"Revisa el prompt antes de confirmar su eliminación.","DeletionReview",target:source.Id,
                update:record=>record with {TargetId=source.Id,Revision=source.Revision,Approval="PREPARING"});
            return proposal;
        }
    }

    public void DeletionPresented(Guid id, UiVisibleState state)
    {
        var control=Active(id);lock(control.Gate) {
            var run=Get(id);var proposal=control.Deletion??throw new ConflictException("No hay una revisión de eliminación.");
            if(control.DeletionDeadlineTick is not {} deadline||Environment.TickCount64>=deadline)throw new ConflictException("Venció el plazo de la revisión.");
            if(Terminal(run.Status)||control.ApprovalConsumed||run.PauseRequested||run.Approval!="PREPARING"
                ||state.DeletionRunId!=id||state.DeletionPromptId!=proposal.PromptId||state.DeletionRevision!=proposal.Revision)
                throw new ConflictException("La revisión visible no corresponde a esta eliminación.");
            if(library.Get(proposal.PromptId).Revision!=proposal.Revision)throw new ConflictException("El prompt cambió durante la revisión.");
            control.Observation=state;
            Emit(id,LiveStatus.AWAITING_APPROVAL,LiveActor.SYSTEM_VALIDATION,"Revisión presentada. Esperando tu confirmación.","DeletionReview",target:proposal.PromptId,
                update:record=>record with {Approval="PENDING"});
        }
    }

    public LiveRecord DecideDeletion(Guid id, LiveApprovalInput input)
    {
        var control=Active(id);lock(control.Gate) {
            var run=Get(id);var proposal=control.Deletion??throw new ConflictException("No hay una eliminación pendiente.");
            if(input.ApprovalId!=proposal.Id||input.Hash!=proposal.Hash||input.Revision!=proposal.Revision)
                throw new ValidationException("La confirmación no corresponde a esta eliminación.");
            if(!input.Confirmed) {
                if(control.ApprovalConsumed||run.EffectCommitted)throw new ConflictException("La eliminación ya fue confirmada.");
                if(!Terminal(run.Status))Emit(id,LiveStatus.CANCELLED,LiveActor.USER_SELECTED,"Eliminación cancelada. El prompt se conserva.","DeletionReview",target:proposal.PromptId,
                    update:record=>record with {Approval="REJECTED"});
                control.DeletionDecision.TrySetResult(false);control.Cancellation.Cancel();return Get(id);
            }
            if(Terminal(run.Status)||run.PauseRequested||run.Approval!="PENDING"||control.ApprovalConsumed)
                throw new ConflictException("La eliminación no tiene una confirmación vigente.");
            if(control.DeletionDeadlineTick is not {} deadline||Environment.TickCount64>=deadline)
                throw new ConflictException("La confirmación venció. Inicia una nueva revisión.");
            if(control.Semantic?.Deletes!=true||control.Semantic.Constraints.ForbiddenOperations.Contains(SemanticOperation.REQUEST_DELETE))
                throw new ValidationException("El objetivo no permite esta eliminación.");
            control.Cancellation.Token.ThrowIfCancellationRequested();
            library.Delete(proposal.PromptId,new(proposal.Revision,true));
            control.ApprovalConsumed=true;
            Emit(id,LiveStatus.UI_UPDATED,LiveActor.USER_SELECTED,"Eliminación confirmada y aplicada al prompt revisado.","DeletionCommit",target:proposal.PromptId,
                update:record=>record with {Approval="CONSUMED",EffectCommitted=true});
            control.DeletionDecision.TrySetResult(true);return Get(id);
        }
    }
}
