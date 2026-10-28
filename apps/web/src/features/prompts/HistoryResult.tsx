import { Copy, Download, FileText, MessageSquare } from 'lucide-react';
import { AppIcon } from '../../components/ui/Icon';
import type { HistoryPage } from '../../services/history';
import '../../design/history.css';

type HistoryItem = HistoryPage['items'][number];
type TextActions = { onCopy: (text: string) => void; onDownload: (text: string, filename: string) => void };

export function historyDate(value: string) {
  return new Date(value).toLocaleString('es', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
}

export function SavedText({ text, kind, filename, onCopy, onDownload }: TextActions & { text: string; kind: 'instrucción' | 'respuesta'; filename: string }) {
  return <>
    <pre className="history-text">{text}</pre>
    <div className="history-text-actions">
      <button className="secondary" onClick={() => onCopy(text)}><AppIcon glyph={Copy} size={16} />Copiar {kind} guardada</button>
      <button className="text-button" onClick={() => onDownload(text, filename)}><AppIcon glyph={Download} size={16} />Descargar {kind}</button>
    </div>
  </>;
}

/** Reads a stored snapshot; never regenerates content or records another use. */
export function HistoryResult({ item, onCopy, onDownload }: TextActions & { item: HistoryItem }) {
  return <section className="history-reader" aria-label="Detalle del resultado" tabIndex={0}>
    <header className="history-reader-heading">
      <span className="eyebrow">Resultado guardado</span>
      <h3>{item.title}</h3>
      <div className="history-result-meta"><span>Versión {item.revision}</span><time dateTime={item.createdAt}>{historyDate(item.createdAt)}</time></div>
    </header>
    <section className="history-content-block" aria-label="Instrucción utilizada">
      <h4><AppIcon glyph={FileText} size={20} />Instrucción utilizada</h4>
      <p className="history-block-help">El texto exacto que utilizaste, con sus valores de ese momento.</p>
      <SavedText text={item.text} kind="instrucción" filename={`quickprompt-instruccion-${item.id}.txt`} onCopy={onCopy} onDownload={onDownload} />
    </section>
    {item.executions.length ? item.executions.map((execution, index) => <section className="history-content-block history-response" key={execution.id} aria-label={`Respuesta de ${execution.model}`}>
      <div className="history-response-heading"><h4><AppIcon glyph={MessageSquare} size={20} />Respuesta local {item.executions.length > 1 ? index + 1 : ''}</h4><span className="history-model">{execution.model}</span></div>
      <div className="history-result-meta"><time dateTime={execution.createdAt}>{historyDate(execution.createdAt)}</time><span>{(execution.elapsedMs / 1000).toLocaleString('es', { maximumFractionDigits: 1 })} s</span></div>
      <SavedText text={execution.response} kind="respuesta" filename={`quickprompt-respuesta-${execution.id}.txt`} onCopy={onCopy} onDownload={onDownload} />
    </section>) : <div className="history-no-response"><AppIcon glyph={MessageSquare} size={20} /><div><strong>Sin respuesta local guardada</strong><p>Este registro conserva la instrucción utilizada. Las respuestas obtenidas en servicios externos no se guardan automáticamente aquí.</p></div></div>}
  </section>;
}
