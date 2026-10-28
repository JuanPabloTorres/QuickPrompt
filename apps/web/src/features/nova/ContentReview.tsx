import type { ContentReview as Review } from '../../services/live';
import '../../design/content-review.css';

const changeLabels: Record<Review['literalChanges']['entries'][number]['kind'], string> = {
  MISSING_SOURCE_CLAUSE: 'Fragmento del original que no aparece literalmente',
  REMOVED_SOURCE_URI: 'Enlace del original que no aparece',
  ADDED_URI: 'Enlace añadido a la propuesta',
};

/** Displays original material and server-owned literal differences without interpreting their meaning. */
export function ContentReview({ review, purpose = 'SAVE', approvedLiteralChangeIds = [], onApproveLiteralChange, disabled = false }: {
  review: Review; purpose?: 'SAVE' | 'CONTENT_ONLY'; approvedLiteralChangeIds?: string[]; onApproveLiteralChange?: (id: string, approved: boolean) => void; disabled?: boolean;
}) {
  return <section className="nova-content-review" aria-label="Revisión del contenido">
    <p className="notice"><strong>El contenido necesita tu revisión.</strong> NOVA preparó una propuesta. Revisa que incluya lo que pediste y conserve lo necesario del original {purpose === 'CONTENT_ONLY' ? 'antes de continuar.' : 'antes de guardarla.'}</p>
    <h3>Tu petición para la plantilla</h3>
    <pre className="content-review-text">{review.originalRequest}</pre>
    {!!review.userClarifications?.length && <section aria-label="Tus aclaraciones del encargo"><h3>Tus aclaraciones</h3>{review.userClarifications.map((item, index) => <div key={index}>
      <p className="muted">{item.question}</p><pre className="content-review-text">{item.answer}</pre>
    </div>)}</section>}
    {review.source.originalText !== null && <details><summary>{review.source.workingDraftId ? 'Borrador de trabajo' : 'Texto de origen'}{!review.source.workingDraftId && review.source.revision ? ` · versión ${review.source.revision}` : ''}</summary><pre className="content-review-text">{review.source.originalText}</pre></details>}
    {review.source.savedOriginalText != null && review.source.savedOriginalText !== review.source.originalText && <details><summary>Versión guardada de origen{review.source.revision ? ` · versión ${review.source.revision}` : ''}</summary><pre className="content-review-text">{review.source.savedOriginalText}</pre></details>}
    <details><summary>Material de referencia</summary>
      <p className="muted">Son fragmentos de la petición y del original para comparar con la propuesta. Los datos del caso se excluyen de la plantilla.</p>
      <ol className="content-review-ledger">{review.ledger.map(item => <li key={item.id}>
        <div className="content-review-role"><strong>{item.role === 'INPUT_DATA' ? 'Datos del caso (excluidos de la plantilla)' : 'Texto de referencia'}</strong><span>{item.source === 'REQUEST' ? 'Petición' : 'Original'}</span></div>
        <pre className="content-review-text">{item.text}</pre>
      </li>)}</ol>
    </details>
    {review.literalChanges.entries.length > 0 && <section className="content-review-changes" aria-label="Diferencias literales para confirmar">
      <h3>Confirma estas diferencias</h3>
      <p className="muted">Esta comparación señala texto y enlaces que cambiaron; no determina si el significado se conserva. Revisa cada diferencia antes de continuar.</p>
      <ul className="content-review-ledger">{review.literalChanges.entries.map((entry, index) => <li key={entry.id}>
        <strong>{changeLabels[entry.kind]}</strong>
        {entry.originalText !== null && <div><span className="muted">Original</span><pre className="content-review-text">{entry.originalText}</pre></div>}
        {entry.proposedText !== null && <div><span className="muted">Propuesta</span><pre className="content-review-text">{entry.proposedText}</pre></div>}
        <label className="content-review-change-choice"><input type="checkbox" checked={approvedLiteralChangeIds.includes(entry.id)} disabled={disabled || !onApproveLiteralChange}
          onChange={event => { if (!disabled) onApproveLiteralChange?.(entry.id, event.target.checked); }} />He revisado y acepto esta diferencia<span className="sr-only"> · Diferencia {index + 1}: {changeLabels[entry.kind]}</span></label>
      </li>)}</ul>
    </section>}
  </section>;
}
