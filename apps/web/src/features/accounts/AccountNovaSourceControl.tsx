import type { AccountPrompt } from '../../services/accounts';

/** One visible library-owned target survives hiding the assistant during native control. */
export function AccountNovaSourceControl({ source, visibleIds, focused, editing, onOpen }: {
  source?: AccountPrompt; visibleIds: string[]; focused: boolean; editing: boolean; onOpen: (prompt: AccountPrompt) => void;
}) {
  if (!source || focused || editing || visibleIds.includes(source.id)) return null;
  return <div className="account-nova-source"><p className="muted">Origen elegido para NOVA · versión {source.revision}</p>
    <button type="button" className="text-button" data-testid="library-prompt" data-prompt-id={source.id} data-revision={source.revision} aria-label={source.title} onClick={() => onOpen(source)}>{source.title}</button>
  </div>;
}
