import { useEffect, useLayoutEffect, useMemo, useState } from 'react';
import { Dialog } from '../../components/ui/Dialog';
import type { AccountService } from '../../services/accounts';
import type { FactualDocument, FactualDocumentSummary } from '../../services/accountFactual';
import { downloadPreparedText } from '../../services/textExport';
import { historyDate } from '../prompts/HistoryResult';
import { useAccountOperation } from './useAccountOperation';
import '../../design/factual.css';

/** Durable, approved documents are distinct from seven-day raw executions and reusable prompts. */
export function AccountFactualDocuments({ libraryId, service, onExpired }: { libraryId: string; service: AccountService; onExpired: () => void }) {
  const [open, setOpen] = useState(false);
  return <section className="account-remembered" aria-label="Documentos aprobados"><div className="account-section-heading"><div><span className="eyebrow">HISTORIAL DE DOCUMENTOS</span><h3>Documentos aprobados</h3></div><button className="secondary" aria-expanded={open} onClick={() => setOpen(value => !value)}>{open ? 'Ocultar documentos' : 'Ver documentos aprobados'}</button></div>
    <p className="muted">Textos que revisaste y aprobaste mediante transformación factual. Se conservan separados de las respuestas sin revisar y de tus plantillas. Eliminar el prompt de origen también elimina estos documentos derivados.</p>
    {open && <FactualDocumentList key={libraryId} libraryId={libraryId} service={service} onExpired={onExpired} />}
  </section>;
}

function FactualDocumentList({ libraryId, service, onExpired }: { libraryId: string; service: AccountService; onExpired: () => void }) {
  const api = useMemo(() => service.factual(libraryId), [service, libraryId]);
  const operation = useAccountOperation(api, onExpired);
  const [page, setPage] = useState(0);
  const [reload, setReload] = useState(0);
  const [items, setItems] = useState<FactualDocumentSummary[]>();
  const [total, setTotal] = useState(0);
  const [selected, setSelected] = useState<string>();
  useLayoutEffect(() => { setItems(undefined); setTotal(0); setSelected(undefined); setPage(0); }, [api]);
  useEffect(() => {
    const controller = new AbortController(); setItems(undefined); operation.clear();
    api.documents(page, controller.signal).then(result => { if (!controller.signal.aborted) { setItems(result.items); setTotal(result.total); } }).catch(error => { if (!controller.signal.aborted) operation.fail(error); });
    return () => controller.abort();
  }, [api, page, reload, operation.clear, operation.fail]);
  return <><button className="text-button" onClick={() => setReload(value => value + 1)}>Actualizar documentos</button>
    {operation.error && <p role="alert" className="error">{operation.error}</p>}
    {!items ? !operation.error && <p role="status">Consultando documentos aprobados…</p> : !items.length ? <p>Aún no hay documentos aprobados en esta página.</p> : <ul className="account-memory-list">{items.map(item => <li key={item.id}><div><strong>{item.title}</strong><span>Documento aprobado · prompt versión {item.promptRevision} · {historyDate(item.createdAt)}</span><span>{item.edited ? 'Con modificaciones en revisión' : 'Hechos confirmados en revisión'}</span></div><button className="secondary" onClick={() => setSelected(item.id)}>Abrir documento {item.title}</button></li>)}</ul>}
    {(page > 0 || total > 20) && <nav className="pagination" aria-label="Páginas de documentos"><button className="secondary" disabled={page === 0} onClick={() => setPage(value => value - 1)}>Documentos anteriores</button><span>Página {page + 1}</span><button className="secondary" disabled={(page + 1) * 20 >= total} onClick={() => setPage(value => value + 1)}>Documentos siguientes</button></nav>}
    {selected && <FactualDocumentReader key={selected} id={selected} api={api} onExpired={onExpired} onClose={() => setSelected(undefined)} onDeleted={() => { setSelected(undefined); setReload(value => value + 1); }} />}
  </>;
}

function FactualDocumentReader({ id, api, onExpired, onClose, onDeleted }: { id: string; api: ReturnType<AccountService['factual']>; onExpired: () => void; onClose: () => void; onDeleted: () => void }) {
  const operation = useAccountOperation(api, onExpired);
  const [document, setDocument] = useState<FactualDocument>();
  const [deleting, setDeleting] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  useLayoutEffect(() => { setDocument(undefined); setDeleting(false); setConfirmed(false); }, [api, id]);
  useEffect(() => {
    const controller = new AbortController();
    api.document(id, controller.signal).then(value => { if (!controller.signal.aborted) setDocument(value); }).catch(error => { if (!controller.signal.aborted) operation.fail(error); });
    return () => controller.abort();
  }, [api, id, operation.fail]);
  return <Dialog open onOpenChange={open => { if (!open && !operation.busy) onClose(); }} title={document?.title ?? 'Documento aprobado'} description="Lectura del texto exacto que aprobaste. Abrirlo no consulta a NOVA, no registra otro uso y no modifica su contenido.">
    <div className="account-factual">{!document && !operation.error && <p role="status">Recuperando documento…</p>}
      {document && <><p className="muted">Aprobado {historyDate(document.createdAt)} · origen versión {document.promptRevision} · {document.edited ? 'Con modificaciones en revisión' : 'Hechos confirmados en revisión'}</p><pre className="history-text">{document.text}</pre>
        <div className="account-actions"><button className="secondary" disabled={operation.busy} onClick={() => void operation.run(async () => navigator.clipboard.writeText(document.text), () => {}, 'Documento copiado.')}>Copiar documento</button><button className="text-button" onClick={() => downloadPreparedText(document.text, `quickprompt-documento-${document.id}.txt`)}>Descargar documento</button></div>
        <details><summary>Hechos confirmados y trazabilidad</summary><p className="muted">Confirmaste estos hechos durante la revisión. La existencia de un extracto no demuestra por sí sola que respalde el hecho o su rol.</p><ol>{document.facts.map((fact, index) => <li key={index}><strong>{fact.role}</strong><p>{fact.text}</p>{!fact.sourceField && !fact.quote ? <p className="muted">Sin fuente documental; confirmado en revisión.</p> : <><p className="muted">Campo: {fact.sourceField}</p><blockquote>{fact.quote}</blockquote></>}</li>)}</ol></details>
        <details><summary>Propuesta original de NOVA · IA</summary><p className="muted">Propuesta del modelo anterior a la confirmación. Puede contener afirmaciones sin fuente y no equivale a hechos verificados.</p>{document.proposalFacts.length ? <ol>{document.proposalFacts.map((fact, index) => <li key={index}><strong>{fact.role}</strong><p>{fact.text}</p>{fact.sourceField || fact.quote ? <><p className="muted">Campo propuesto: {fact.sourceField}</p><blockquote>{fact.quote}</blockquote></> : <p className="muted">Sin fuente documental en la propuesta de IA.</p>}</li>)}</ol> : <p>No se conservaron hechos en la propuesta original.</p>}</details>
        <details><summary>Fuentes seleccionadas conservadas</summary><p className="muted">Texto original que seleccionaste para esta revisión, conservado con el documento aprobado. No se vuelve a consultar el prompt ni al modelo.</p>{Object.entries(document.sourceFields).map(([name, text]) => <section key={name} aria-label={`Fuente guardada ${name}`}><h4>{name}</h4><pre className="history-text">{text}</pre></section>)}</details>
        {!deleting ? <button className="text-button" onClick={() => setDeleting(true)}>Eliminar documento</button> : <section className="account-revoke-confirm" aria-label="Confirmar eliminación de documento"><p>Se eliminarán definitivamente este documento y su revisión factual. Tu prompt y otras preparaciones se conservan.</p><label className="factual-check"><input type="checkbox" checked={confirmed} disabled={operation.busy} onChange={event => setConfirmed(event.target.checked)} />Confirmo eliminar sólo este documento aprobado.</label><div className="account-actions"><button className="secondary" disabled={operation.busy} onClick={() => { setDeleting(false); setConfirmed(false); }}>Conservar documento</button><button className="destructive" disabled={!confirmed || operation.busy} onClick={() => void operation.run(signal => api.remove(id, signal), onDeleted)}>Eliminar documento definitivamente</button></div></section>}
      </>}
      {operation.error && <p role="alert" className="error">{operation.error}</p>}{operation.notice && <p role="status">{operation.notice}</p>}
    </div>
  </Dialog>;
}
