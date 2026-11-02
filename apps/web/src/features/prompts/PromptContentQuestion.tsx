import { useLayoutEffect, useMemo, useState } from 'react';
import { Dialog } from '../../components/ui/Dialog';
import { useUiPortalScope } from '../../components/ui/UiPortalScope';
import { useBoundOperation } from '../../services/useBoundOperation';
import { errorMessage } from '../../services/api';

/** A literal user reply, separate from the original work order and chosen application action. */
export function PromptContentQuestion({ runId, sequence, question, identity, onConfirm }: {
  runId: string; sequence: number; question: string; identity: object; onConfirm: (answer: string, sequence: number) => Promise<void>;
}) {
  const [open, setOpen] = useState(true), [answer, setAnswer] = useState(''), [error, setError] = useState('');
  const scope = useMemo(() => ({ runId, sequence, question, identity }), [runId, sequence, question, identity]);
  const operation = useBoundOperation(scope);
  const portal = useUiPortalScope();
  useLayoutEffect(() => { if (portal?.active === false) operation.cancel(); }, [portal?.active, operation.cancel]);
  function close() { if (!operation.busy) setOpen(false); }
  async function submit() {
    if (!answer.trim() || answer.length > 2000 || portal?.active === false) return;
    const pending = operation.begin(); if (!pending) return;
    setError('');
    try { await onConfirm(answer, sequence); if (operation.isCurrent(pending)) setOpen(false); }
    catch (failure) { if (operation.isCurrent(pending)) setError(errorMessage(failure)); }
    finally { operation.finish(pending); }
  }
  return <>{!open && <button type="button" className="secondary" data-nova-user-control="true" onClick={() => setOpen(true)}>Responder a NOVA</button>}
    <Dialog open={open} onOpenChange={next => { if (!next) close(); }} title="Aclara el contenido del prompt" description={question}>
    <form className="account-prompt-form" data-nova-command="true" onSubmit={event => { event.preventDefault(); void submit(); }}>
      <label htmlFor="nova-content-answer">Tu respuesta</label><textarea id="nova-content-answer" rows={4} maxLength={2000} value={answer} disabled={operation.busy} onChange={event => setAnswer(event.target.value)} />
      <p className="muted">La respuesta precisa tu encargo. La acción, el origen y las variables elegidas se conservan. Cualquier propuesta nueva necesita tu revisión antes de guardarse.</p>
      {error && <p role="alert" className="error">{error} Tu respuesta permanece aquí; no se ha confirmado la continuación.</p>}
      <div className="dialog-actions"><button type="button" className="secondary" disabled={operation.busy} onClick={close}>Ahora no</button><button className="primary" disabled={operation.busy || !answer.trim()}>{operation.busy ? 'Continuando…' : 'Responder y continuar'}</button></div>
    </form>
  </Dialog></>;
}
