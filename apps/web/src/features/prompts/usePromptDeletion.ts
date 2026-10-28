import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { errorMessage, type Prompt } from '../../services/api';
import type { PromptLibraryClient } from '../../services/library';
import type { LiveClient } from '../../services/live';

interface PromptDeletionReview {
  runId: string; id: string; hash: string; promptId: string; revision: number;
}
export interface PromptDeletionHost {
  saving: boolean;
  setSaving(value: boolean): void;
  setError(message: string): void;
  ensureAvailable(): Promise<void>;
  onDeleted(prompt: Prompt): void;
}
export interface PromptDeletionController {
  readonly target: Prompt | null;
  readonly review: Readonly<Pick<PromptDeletionReview, 'runId'>> | null;
  readonly ready: boolean;
  open(source: Prompt): Promise<void>;
  confirm(): Promise<void>;
  cancel(): Promise<void>;
}

// Owns review identity and presentation only. The domain endpoints authorize effects.
export function usePromptDeletion(prompts: Pick<PromptLibraryClient, 'get' | 'remove'>,
  live: Pick<LiveClient, 'get' | 'deletion'>, host: PromptDeletionHost): PromptDeletionController {
  const [target, setTarget] = useState<Prompt | null>(null);
  const review = useRef<PromptDeletionReview | null>(null);
  const opening = useRef(0);
  const [ready, setReady] = useState(false);
  const setError = host.setError;
  const reportError = useRef(setError);
  useLayoutEffect(() => { reportError.current = setError; }, [setError]);

  useEffect(() => {
    review.current = null; setTarget(null); setReady(false);
    const requestReview = (event: Event) => {
      const detail = (event as CustomEvent<{ runId: string; promptId: string; revision: number; resolve: () => void; reject: (error: Error) => void }>).detail;
      const attempt = ++opening.current;
      void (async () => {
        try {
          if (document.querySelector('[data-testid="prompt-delete-review"]')) throw new Error('UI_PRECONDITION');
          const view = await live.get(detail.runId, AbortSignal.timeout(10000));
          if (attempt !== opening.current) throw new Error('UI_IDENTITY_MISMATCH');
          const proposal = view.deletion;
          if (!proposal || proposal.promptId !== detail.promptId || proposal.revision !== detail.revision || view.run.approval !== 'PREPARING') throw new Error('UI_IDENTITY_MISMATCH');
          const fresh = await prompts.get(proposal.promptId, AbortSignal.timeout(10000));
          if (fresh.revision !== proposal.revision || fresh.title !== proposal.title || attempt !== opening.current) throw new Error('UI_IDENTITY_MISMATCH');
          review.current = { ...proposal, runId: detail.runId };
          flushSync(() => { reportError.current(''); setReady(false); setTarget(fresh); }); detail.resolve();
        } catch (error) { detail.reject(error instanceof Error ? error : new Error('UI_PRECONDITION')); }
      })();
    };
    const enable = (event: Event) => {
      const detail = (event as CustomEvent<{ runId: string; promptId: string; revision: number }>).detail;
      const pending = review.current;
      if (pending?.runId === detail.runId && pending.promptId === detail.promptId && pending.revision === detail.revision)
        flushSync(() => setReady(true));
    };
    const abort = () => {
      opening.current++;
      if (review.current) { review.current = null; setTarget(null); setReady(false); }
    };
    window.addEventListener('nova-review-deletion', requestReview); window.addEventListener('nova-enable-deletion', enable); window.addEventListener('nova-abort-ui-operation', abort);
    return () => { opening.current++; window.removeEventListener('nova-review-deletion', requestReview); window.removeEventListener('nova-enable-deletion', enable); window.removeEventListener('nova-abort-ui-operation', abort); };
  }, [prompts, live]);

  async function open(source: Prompt) {
    const attempt = ++opening.current;
    await host.ensureAvailable();
    if (attempt !== opening.current) throw new Error('UI_IDENTITY_MISMATCH');
    const fresh = await prompts.get(source.id);
    if (attempt !== opening.current) throw new Error('UI_IDENTITY_MISMATCH');
    review.current = null; setReady(false); setError(''); setTarget(fresh);
  }
  async function confirm() {
    if (!target || host.saving) return;
    const pending = review.current;
    if (pending && (!ready || pending.promptId !== target.id || pending.revision !== target.revision)) return;
    const attempt = opening.current;
    host.setSaving(true); setError('');
    try {
      if (pending) await live.deletion(pending.runId, { approvalId: pending.id, hash: pending.hash, revision: pending.revision, confirmed: true });
      else await prompts.remove(target, true);
      if (attempt !== opening.current) return;
      review.current = null; setReady(false); setTarget(null); host.onDeleted(target);
    }
    catch (error) { if (attempt === opening.current) setError(errorMessage(error)); } finally { host.setSaving(false); }
  }
  async function cancel() {
    if (host.saving) return;
    const pending = review.current;
    const attempt = ++opening.current; review.current = null; setReady(false); setTarget(null);
    if (pending) try {
      await live.deletion(pending.runId, { approvalId: pending.id, hash: pending.hash, revision: pending.revision, confirmed: false });
    } catch (error) { if (attempt === opening.current) setError(errorMessage(error)); }
  }
  return { target, review: review.current ? { runId: review.current.runId } : null, ready, open, confirm, cancel };
}
