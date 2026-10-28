import { ApiError, type PromptDraft } from './api';
import type { AccountPrompt, AccountService } from './accounts';

type AgentServices = ReturnType<AccountService['nova']>;

/** A lost write response is resolved by authoritative reads, never another commit. */
export async function commitAccountProposal(ports: AgentServices, accounts: AccountService, libraryId: string,
  runId: string, draft: PromptDraft, signal: AbortSignal): Promise<AccountPrompt> {
  try {
    const saved = await ports.live.commit(runId, draft, signal);
    return { id: saved.id, libraryId, title: saved.title, template: saved.templateText, revision: saved.revision };
  } catch (failure) {
    if (signal.aborted || failure instanceof ApiError && failure.status < 500) throw failure;
    const recovery = AbortSignal.any([signal, AbortSignal.timeout(4000)]);
    try {
      const { run } = await ports.live.record(runId, recovery);
      if (run.runId !== runId || !run.effectCommitted || run.approval !== 'CONSUMED' || !run.targetId || !run.revision) throw failure;
      const saved = await accounts.prompt(libraryId, run.targetId, recovery);
      if (saved.libraryId !== libraryId || saved.id !== run.targetId || saved.revision !== run.revision
        || saved.title !== draft.title || saved.template !== draft.templateText) throw failure;
      return saved;
    } catch (recoveryFailure) {
      if (recoveryFailure instanceof ApiError && recoveryFailure.status === 401) throw recoveryFailure;
      throw failure;
    }
  }
}
