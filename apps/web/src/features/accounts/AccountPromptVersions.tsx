import { useEffect, useMemo, useState } from 'react';
import { Dialog } from '../../components/ui/Dialog';
import type { AccountPrompt, AccountPromptVersion, AccountService } from '../../services/accounts';
import { downloadPreparedText } from '../../services/textExport';
import { historyDate, SavedText } from '../prompts/HistoryResult';
import { useAccountOperation } from './useAccountOperation';

/** Reads private snapshots. Restoring content creates a new revision through the normal optimistic writer. */
export function AccountPromptVersions({ prompt, service, onExpired, onSaved, onClose }: {
  prompt: AccountPrompt; service: AccountService; onExpired: () => void; onSaved: (prompt: AccountPrompt) => void; onClose: () => void;
}) {
  const [versions, setVersions] = useState<AccountPromptVersion[]>();
  const [revision, setRevision] = useState(prompt.revision);
  const [confirm, setConfirm] = useState(false);
  const [refresh, setRefresh] = useState(0);
  const scope = useMemo(() => ({ prompt, service }), [prompt, service]);
  const operation = useAccountOperation(scope, onExpired);
  useEffect(() => {
    const controller = new AbortController(); setVersions(undefined); operation.clear();
    service.versions(prompt, controller.signal).then(items => {
      if (!controller.signal.aborted) { setVersions(items); setRevision(items[0]?.revision ?? prompt.revision); }
    }).catch(failure => { if (!controller.signal.aborted) operation.fail(failure); });
    return () => controller.abort();
  }, [prompt, service, refresh, operation.clear, operation.fail]);
  const selected = versions?.find(item => item.revision === revision);
  return <Dialog open title={`Versiones de ${prompt.title}`} description="Consulta hasta 200 versiones privadas. Recuperar el título y contenido crea una versión nueva; no ejecuta la instrucción ni restaura valores o permisos."
    onOpenChange={open => { if (!open && !operation.busy) onClose(); }}>
    {!versions && !operation.error && <p role="status">Cargando versiones…</p>}
    {versions && <><label htmlFor="account-prompt-version">Versión guardada</label><select id="account-prompt-version" disabled={operation.busy} value={revision} onChange={event => { setRevision(Number(event.target.value)); setConfirm(false); operation.clear(); }}>
      {versions.map(item => <option key={item.revision} value={item.revision}>Versión {item.revision} · {historyDate(item.createdAt)}</option>)}
    </select>{versions.length === 0 && <p>No hay versiones disponibles.</p>}</>}
    {selected && <><h3>{selected.title}</h3><SavedText text={selected.template} kind="instrucción" filename={`quickprompt-${prompt.id}-v${selected.revision}.txt`}
      onCopy={text => { void operation.run(() => navigator.clipboard.writeText(text), () => {}, 'Instrucción de esta versión copiada.'); }} onDownload={downloadPreparedText} />
      {selected.revision < prompt.revision && (confirm ? <div role="group" aria-label="Confirmar recuperación de contenido"><p>Crearás una versión nueva con el título y contenido de la versión {selected.revision}. Se conservarán las versiones anteriores.</p>
        <button className="primary" disabled={operation.busy} onClick={() => void operation.run(signal => service.savePrompt(prompt.libraryId, prompt, { title: selected.title, template: selected.template }, signal), onSaved)}>Crear nueva versión con este contenido</button>
        <button className="secondary" disabled={operation.busy} onClick={() => setConfirm(false)}>Conservar versión actual</button></div>
        : <button className="secondary" onClick={() => setConfirm(true)}>Recuperar título y contenido</button>)}</>}
    {operation.error && <p role="alert" className="error">{operation.error}{!versions && <button className="text-button" onClick={() => setRefresh(value => value + 1)}>Reintentar versiones</button>}</p>}
    {operation.notice && <p role="status">{operation.notice}</p>}
  </Dialog>;
}
