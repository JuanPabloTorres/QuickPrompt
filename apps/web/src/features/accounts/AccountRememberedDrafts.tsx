import { useEffect, useMemo, useState } from 'react';
import { Dialog } from '../../components/ui/Dialog';
import { ApiError } from '../../services/api';
import type { AccountPrompt, AccountService, RememberedContext, SavedEditorContext } from '../../services/accounts';
import { useAccountOperation } from './useAccountOperation';

/** Saved context is separate from published prompts, history and execution approval. */
export function AccountRememberedDrafts({ libraryId, service, onExpired, onRestore }: { libraryId: string; service: AccountService; onExpired: () => void; onRestore: (saved: SavedEditorContext, source: AccountPrompt | null) => void }) {
  const [items, setItems] = useState<RememberedContext[]>();
  const [forgetting, setForgetting] = useState<RememberedContext>();
  const [refresh, setRefresh] = useState(0);
  const scope = useMemo(() => ({ libraryId, service }), [libraryId, service]);
  const operation = useAccountOperation(scope, onExpired);
  useEffect(() => {
    const controller = new AbortController(); operation.clear();
    service.rememberedContexts(libraryId, controller.signal).then(result => { if (!controller.signal.aborted) setItems(result); })
      .catch(error => { if (!controller.signal.aborted) operation.fail(error); });
    return () => controller.abort();
  }, [libraryId, service, refresh, operation.fail, operation.clear]);
  function restore(id: string) {
    void operation.run(async signal => {
      const saved = await service.readContext(libraryId, id, signal);
      let source: AccountPrompt | null = null;
      if (saved.sourceId && saved.sourceRevision) {
        try { source = { ...await service.prompt(libraryId, saved.sourceId, signal), revision: saved.sourceRevision }; }
        catch (error) {
          if (!(error instanceof ApiError && error.status === 404 && saved.sourceStale)) throw error;
          source = { id: saved.sourceId, libraryId, revision: saved.sourceRevision, title: saved.title, template: saved.templateText };
        }
      }
      return { saved, source };
    }, result => onRestore(result.saved, result.source));
  }
  function forget() {
    if (!forgetting) return;
    void operation.run(signal => service.forgetContext(libraryId, forgetting.id, signal), () => {
      setItems(current => current?.filter(item => item.id !== forgetting.id)); setForgetting(undefined);
    }, 'Contexto olvidado. Los prompts de tu biblioteca se conservan.');
  }
  return <section className="account-remembered" aria-label="Borradores conservados">
    <div className="account-section-heading"><div><span className="eyebrow">RETOMAR EL TRABAJO</span><h3>Borradores conservados</h3></div><button className="text-button" disabled={operation.busy} onClick={() => setRefresh(value => value + 1)}>Actualizar borradores</button></div>
    <p className="muted">Sólo se conserva lo que eliges guardar aquí. Caduca a los siete días; no conserva aprobaciones ni ejecuta acciones al recuperarlo.</p>
    {operation.error && <p className="error" role="alert">{operation.error}</p>}{operation.notice && <p role="status">{operation.notice}</p>}
    {!items ? !operation.error && <p role="status">Cargando borradores conservados…</p> : items.length ? <ul className="account-memory-list">{items.map(item => <li key={item.id}><div><strong>{item.title}</strong><span>Disponible hasta {new Date(item.expiresAt).toLocaleDateString()}</span></div><div className="account-actions"><button className="secondary" disabled={operation.busy} onClick={() => restore(item.id)} aria-label={`Recuperar borrador ${item.title}`}>Recuperar</button><button className="text-button" disabled={operation.busy} onClick={() => setForgetting(item)} aria-label={`Olvidar borrador ${item.title}`}>Olvidar</button></div></li>)}</ul> : <p className="muted">Todavía no has conservado ningún borrador.</p>}
    {forgetting && <Dialog open onOpenChange={open => { if (!open && !operation.busy) setForgetting(undefined); }} title="Olvidar borrador conservado" description={`Se eliminarán el borrador «${forgetting.title}» y sus valores conservados. Los prompts guardados y sus versiones se mantienen.`}>
      <div className="dialog-actions"><button className="secondary" disabled={operation.busy} onClick={() => setForgetting(undefined)}>Conservar borrador</button><button className="destructive" disabled={operation.busy} onClick={forget}>Olvidar definitivamente</button></div>
      {operation.error && <p className="error" role="alert">{operation.error}</p>}
    </Dialog>}
  </section>;
}
