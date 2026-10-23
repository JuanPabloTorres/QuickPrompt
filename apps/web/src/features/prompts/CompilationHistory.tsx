import { useEffect, useRef, useState } from 'react';
import { Clock3, FileText, MessageSquare, ShieldCheck, Trash2 } from 'lucide-react';
import { useApplicationServices } from '../../components/ServiceContext';
import { Dialog } from '../../components/ui/Dialog';
import { AppIcon } from '../../components/ui/Icon';
import { errorMessage } from '../../services/api';
import type { HistoryPage } from '../../services/history';
import { HistoryResult, historyDate } from './HistoryResult';
import '../../design/history.css';

export function CompilationHistory({ onClose }: { onClose: () => void }) {
  const { history } = useApplicationServices();
  const [page, setPage] = useState(0), [reload, setReload] = useState(0);
  const [result, setResult] = useState<HistoryPage | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [error, setError] = useState(''), [notice, setNotice] = useState('');
  const [review, setReview] = useState<HistoryPage | null>(null), [confirmed, setConfirmed] = useState(false), [clearing, setClearing] = useState(false);
  const reviewPanel = useRef<HTMLElement>(null), clearButton = useRef<HTMLButtonElement>(null), wasReviewing = useRef(false);
  const selected = result?.items.find(item => item.id === selectedId) ?? result?.items[0];
  useEffect(() => {
    if (review) reviewPanel.current?.focus();
    else if (wasReviewing.current) clearButton.current?.focus();
    wasReviewing.current = !!review;
  }, [review]);
  useEffect(() => {
    const controller = new AbortController();
    setResult(null); setSelectedId(null); setError(''); setNotice(''); setReview(null); setConfirmed(false);
    history.list(page, controller.signal).then(value => {
      if (!controller.signal.aborted) {
        if (value.page !== page) throw new Error('La respuesta no corresponde a la página solicitada.');
        setResult(value);
      }
    }).catch(failure => { if (!controller.signal.aborted) setError(errorMessage(failure)); });
    return () => controller.abort();
  }, [history, page, reload]);
  async function copy(text: string) {
    setNotice('');
    try { await navigator.clipboard.writeText(text); setNotice('Resultado del historial copiado.'); }
    catch { setNotice('No se pudo copiar. Puedes seleccionar o descargar el texto.'); }
  }
  function download(text: string, filename: string) {
    const url = URL.createObjectURL(new Blob([text], { type: 'text/plain;charset=utf-8' }));
    const anchor = document.createElement('a'); anchor.href = url; anchor.download = filename; anchor.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    setNotice('Descarga del texto guardado preparada.');
  }
  async function clear() {
    if (!review || !confirmed || clearing) return;
    setClearing(true); setError('');
    try { await history.clear(review.expectedSnapshot); setPage(0); setReload(value => value + 1); }
    catch (failure) { setResult(null); setReview(null); setError(`No se confirmó el borrado. ${errorMessage(failure)} Reintenta la lectura del historial para comprobar su estado.`); }
    finally { setClearing(false); }
  }
  return <Dialog open onOpenChange={open => { if (!open && !clearing) onClose(); }} title="Historial de resultados" description="Recupera las instrucciones utilizadas y sus respuestas locales guardadas." className="history-dialog">
    {result && <dl className="history-stats" aria-label="Resumen del historial">
      <div><dt>Instrucciones</dt><dd>{result.total}</dd></div>
      <div><dt>Respuestas locales</dt><dd>{result.responseCount}</dd></div>
      <div><dt>Transferencias</dt><dd>{result.transferCount}</dd></div>
    </dl>}
    <div className="history-body" aria-busy={!result && !error}>
      {!result && !error && <div className="history-state" role="status"><AppIcon glyph={Clock3} size={32} /><h3>Cargando historial…</h3><p>Recuperando los textos guardados en esta biblioteca.</p></div>}
      {error && <div className="history-state" role="alert"><h3>No se pudo recuperar el historial</h3><p className="error">{error}</p><button className="secondary" onClick={() => setReload(value => value + 1)}>Reintentar historial</button></div>}
      {result && !review && <>
        {!result.total && <div className="history-state"><AppIcon glyph={Clock3} size={32} /><h3>No hay resultados guardados</h3><p>Copia una instrucción para conservar el texto utilizado. Si después la ejecutas con un modelo local, también podrás recuperar aquí su respuesta.</p></div>}
        {!!result.total && <div className="history-workspace">
          <section className="history-browser" aria-label="Elegir un resultado">
            <div className="history-list-heading"><h3>Resultados guardados</h3><span>{page * result.pageSize + 1}–{page * result.pageSize + result.items.length} de {result.total}</span></div>
            <ol className="history-results" aria-label="Resultados guardados">{result.items.map(item => <li key={item.id}>
              <button className="history-choice" aria-pressed={selected?.id === item.id} onClick={() => { setSelectedId(item.id); setNotice(''); }}>
                <span className="history-choice-title"><AppIcon glyph={FileText} size={16} /><strong>{item.title}</strong></span>
                <span className="history-choice-date"><time dateTime={item.createdAt}>{historyDate(item.createdAt)}</time><span>v{item.revision}</span></span>
                <span className="history-choice-kind"><AppIcon glyph={MessageSquare} size={16} />{item.executions.length ? `${item.executions.length} ${item.executions.length === 1 ? 'respuesta local' : 'respuestas locales'}` : 'Sólo instrucción'}</span>
              </button>
            </li>)}</ol>
          </section>
          {selected && <HistoryResult key={selected.id} item={selected} onCopy={copy} onDownload={download} />}
        </div>}
      </>}
      {review && <section ref={reviewPanel} tabIndex={-1} className="history-clear-review" aria-label="Revisión de borrado del historial"><AppIcon glyph={Trash2} size={32} /><h3>Borrar el historial de esta biblioteca</h3><p>Se eliminarán permanentemente {review.total} instrucciones utilizadas, {review.responseCount} respuestas y {review.transferCount} registros de transferencia.</p><p>Se conservan tus plantillas. Los borradores, la actividad de NOVA y los respaldos se gestionan por separado.</p><label><input type="checkbox" checked={confirmed} disabled={clearing} onChange={event => setConfirmed(event.target.checked)} />Confirmo borrar este historial</label><div className="dialog-actions"><button className="secondary" disabled={clearing} onClick={() => { setReview(null); setConfirmed(false); }}>Conservar historial</button><button className="destructive" disabled={!confirmed || clearing} onClick={clear}>{clearing ? 'Borrando…' : 'Confirmar borrado del historial'}</button></div></section>}
    </div>
    <div className="history-footer">
      {!review && <p className="history-privacy"><AppIcon glyph={ShieldCheck} size={16} /><span>Texto y versión originales. Puede incluir los datos que utilizaste.</span></p>}
      <p className="history-notice" role="status">{notice}</p>
      {!!result && !review && !!result.total && <nav className="pagination" aria-label="Páginas del historial"><button className="secondary" disabled={clearing || page === 0} onClick={() => setPage(value => value - 1)}>Anterior</button><span>Página {page + 1} de {Math.ceil(result.total / result.pageSize)}</span><button className="secondary" disabled={clearing || (page + 1) * result.pageSize >= result.total} onClick={() => setPage(value => value + 1)}>Siguiente</button></nav>}
      <div className="history-footer-actions">
        {result && (result.total > 0 || result.responseCount > 0 || result.transferCount > 0) && !review && <button ref={clearButton} className="text-button danger" onClick={() => { setReview(result); setConfirmed(false); }}><AppIcon glyph={Trash2} size={16} />Borrar historial…</button>}
        <button className="secondary history-close" disabled={clearing} onClick={onClose}>Cerrar historial</button>
      </div>
    </div>
  </Dialog>;
}
