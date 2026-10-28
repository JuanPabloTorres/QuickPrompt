import type { PromptDraft } from '../../services/api';

export interface EditorDraftSource { readonly id: string; readonly revision: number }
export interface EditorDraftSession {
  read(source: EditorDraftSource | null): PromptDraft | undefined;
  remember(promptId: string | null, draft: PromptDraft): void;
  forget(promptId: string | null): void;
}

const copyDraft = (draft: PromptDraft): PromptDraft => ({
  ...draft, ...(draft.defaults ? { defaults: { ...draft.defaults } } : {}),
});

// Ephemeral snapshots between editor visits. No persistence or authorization.
class MemoryEditorDraftSession implements EditorDraftSession {
  private readonly drafts = new Map<string, PromptDraft>();
  private newDraft: PromptDraft | undefined;

  read(source: EditorDraftSource | null): PromptDraft | undefined {
    const draft = source ? this.drafts.get(source.id) : this.newDraft;
    if (!draft || source && draft.revision !== source.revision) return undefined;
    return copyDraft(draft);
  }
  remember(promptId: string | null, draft: PromptDraft): void {
    if (promptId === null) this.newDraft = copyDraft(draft);
    else this.drafts.set(promptId, copyDraft(draft));
  }
  forget(promptId: string | null): void {
    if (promptId === null) this.newDraft = undefined;
    else this.drafts.delete(promptId);
  }
}

export function createEditorDraftSession(): EditorDraftSession { return new MemoryEditorDraftSession(); }
