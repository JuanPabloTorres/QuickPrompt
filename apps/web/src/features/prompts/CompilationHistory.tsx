import { useEffect, useState } from 'react';
import { useApplicationServices } from '../../components/ServiceContext';
import { Dialog } from '../../components/ui/Dialog';
import { errorMessage } from '../../services/api';
import type { HistoryPage } from '../../services/history';

export function CompilationHistory({ onClose }: { onClose: () => void }) {
  const { history } = useApplicationServices();
  const [page, setPage] = useState(0), [reload, setReload] = useState(0);
  const [result, setResult] = useState<HistoryPage | null>(null);
  const [error, setError] = useState(''), [notice, setNotice] = useState('');
  const [review, setReview] = useState<HistoryPage | null>(null), [confirmed, setConfirmed] = useState(false), [clearing, setClearing] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    setResult(null); setError(''); setNotice(''); setReview(null); setConfirmed(false);
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
  function download(text: string) {
    const url = URL.createObjectURL(new Blob([text], { type: 'text/plain;charset=utf-8' }));
    const anchor = document.createElement('a'); anchor.href = url; anchor.download = 'quickprompt-historial.txt'; anchor.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  async function clear() {
    if (!review || !confirmed || clearing) return;
    setClearing(true); setError('');
    try { await history.clear(review.expectedSnapshot); setPage(0); setReload(value => value + 1); }
    catch (failure) { setResult(null); setReview(null); setError(`No se confirmó el borrado. ${errorMessage(failure)} Reintenta la lectura del historial para comprobar su estado.`); }
    finally { setClearing(false); }
  }
  return <Dialog open onOpenChange={open => { if (!open && !clearing) onClose(); }} title="Historial de resultados" description="Recupera las instrucciones utilizadas y sus respuestas locales guardadas." className="history-dialog">
    <p className="muted">Cada resultado conserva su texto y versión originales, aunque el prompt haya cambiado o se haya eliminado. El historial puede contener los datos que utilizaste.</p>
    {!result && !error && <p role="status">Cargando historial…</p>}
    {error && <div role="alert"><p className="error">{error}</p><button className="secondary" onClick={() => setReload(value => value + 1)}>Reintentar historial</button></div>}
    {result && <>
      {!result.total && <p>No hay resultados guardados. Copia una instrucción para conservar el texto utilizado.</p>}
      <ol className="history-results" aria-label="Resultados guardados">{result.items.map(item => <li key={item.id}>
        <details><summary>{item.title} · versión {item.revision} · {new Date(item.createdAt).toLocaleString()}</summary>
          <h3>Instrucción utilizada</h3><pre>{item.text}</pre>
          <div className="data-actions"><button className="secondary" onClick={() => copy(item.text)}>Copiar instrucción guardada</button><button className="text-button" onClick={() => download(item.text)}>Descargar instrucción</button></div>
          {item.executions.map(execution => <section key={execution.id} aria-label={`Respuesta de ${execution.model}`}>
            <h3>Respuesta de {execution.model}</h3><p className="muted">{new Date(execution.createdAt).toLocaleString()} · {(execution.elapsedMs / 1000).toFixed(1)} s</p>
            <pre>{execution.response}</pre><div className="data-actions"><button className="secondary" onClick={() => copy(execution.response)}>Copiar respuesta guardada</button><button className="text-button" onClick={() => download(execution.response)}>Descargar respuesta</button></div>
          </section>)}
        </details>
      </li>)}</ol>
      {!!result.total && <nav className="pagination" aria-label="Páginas del historial"><button className="secondary" disabled={clearing || page === 0} onClick={() => setPage(value => value - 1)}>Anterior</button><span>Página {page + 1} · {result.total} resultados</span><button className="secondary" disabled={clearing || (page + 1) * result.pageSize >= result.total} onClick={() => setPage(value => value + 1)}>Siguiente</button></nav>}
      {(result.total > 0 || result.responseCount > 0 || result.transferCount > 0) && !review && <button className="text-button danger" onClick={() => { setReview(result); setConfirmed(false); }}>Borrar historial…</button>}
    </>}
    {review && <section aria-label="Revisión de borrado del historial"><h3>Borrar el historial de esta biblioteca</h3><p>Se eliminarán permanentemente {review.total} instrucciones utilizadas, {review.responseCount} respuestas y {review.transferCount} registros de transferencia. Se conservan tus plantillas. Los borradores, la actividad de NOVA y los respaldos se gestionan por separado.</p><label><input type="checkbox" checked={confirmed} disabled={clearing} onChange={event => setConfirmed(event.target.checked)} />Confirmo borrar este historial</label><div className="dialog-actions"><button className="secondary" disabled={clearing} onClick={() => { setReview(null); setConfirmed(false); }}>Conservar historial</button><button className="destructive" disabled={!confirmed || clearing} onClick={clear}>{clearing ? 'Borrando…' : 'Confirmar borrado del historial'}</button></div></section>}
    {notice && <p role="status">{notice}</p>}
    <div className="dialog-actions"><button className="secondary" disabled={clearing} onClick={onClose}>Cerrar historial</button></div>
  </Dialog>;
}
