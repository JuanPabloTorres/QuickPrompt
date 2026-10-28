import { useMemo, useState } from 'react';
import { Dialog } from '../../components/ui/Dialog';
import { ApiError } from '../../services/api';
import type { AccountPrompt, AccountService } from '../../services/accounts';
import { useAccountOperation } from './useAccountOperation';

/** A deletion approval belongs to this exact resource/revision and is never transferred. */
export function AccountPromptDeletion({ prompt, service, onExpired, onDeleted, onClose, onReload }: {
  prompt: AccountPrompt; service: AccountService; onExpired: () => void; onDeleted: () => void; onClose: () => void; onReload: () => void;
}) {
  const [confirmed, setConfirmed] = useState(false);
  const [stale, setStale] = useState(false);
  const scope = useMemo(() => ({ service, libraryId: prompt.libraryId, id: prompt.id, revision: prompt.revision }), [service, prompt.libraryId, prompt.id, prompt.revision]);
  const operation = useAccountOperation(scope, onExpired);
  return <Dialog open onOpenChange={open => { if (!open && !operation.busy) onClose(); }} title="Eliminar prompt privado" description={`Eliminarás «${prompt.title}», versión ${prompt.revision}, de esta biblioteca. Su contenido y sus versiones no se podrán recuperar. Las publicaciones ya compartidas, copias importadas, borradores y ejecuciones se gestionan por separado y se conservan.`}>
    <label className="account-delete-confirm"><input type="checkbox" checked={confirmed} disabled={operation.busy || stale} onChange={event => setConfirmed(event.target.checked)} />Confirmo eliminar este prompt y sus versiones.</label>
    {operation.error && <p role="alert" className="error">{operation.error} No se ha confirmado la eliminación.</p>}
    {stale && <p>La revisión cambió o existe trabajo activo. Actualiza la biblioteca y revisa el recurso de nuevo antes de eliminarlo.</p>}
    <div className="dialog-actions"><button type="button" className="secondary" disabled={operation.busy} onClick={onClose}>Conservar prompt</button>
      {stale ? <button type="button" className="secondary" onClick={onReload}>Cerrar y actualizar biblioteca</button> : <button type="button" className="destructive" disabled={!confirmed || operation.busy} onClick={() => {
        if (!confirmed || operation.busy) return;
        void operation.run(signal => service.deletePrompt(prompt, true, signal), onDeleted, '', failure => { if (failure instanceof ApiError && failure.status === 409) setStale(true); });
      }}>Eliminar definitivamente</button>}</div>
  </Dialog>;
}
