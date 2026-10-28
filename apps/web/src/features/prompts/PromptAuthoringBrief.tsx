import { useLayoutEffect, useMemo, useState } from 'react';
import { Dialog } from '../../components/ui/Dialog';
import { useUiPortalScope } from '../../components/ui/UiPortalScope';
import { errorMessage } from '../../services/api';
import { useBoundOperation } from '../../services/useBoundOperation';
import type { AuthoringBriefContent, AuthoringBriefReply, AuthoringBriefReview } from '../../services/live';

/** AI suggestions become a contract only through this explicit, editable human decision. */
export function PromptAuthoringBrief({ runId, sequence, review, identity, onConfirm, onClarify }: {
  runId: string; sequence: number; review: AuthoringBriefReview; identity: object;
  onConfirm: (reply: AuthoringBriefReply) => Promise<void>; onClarify: (answer: string) => Promise<void>;
}) {
  const [open, setOpen] = useState(true), [content, setContent] = useState<AuthoringBriefContent>(() => structuredClone(review.proposed));
  const [checked, setChecked] = useState(false), [clarifying, setClarifying] = useState(false), [answer, setAnswer] = useState(''), [error, setError] = useState('');
  const scope = useMemo(() => ({ runId, sequence, basis: review.basisHash, identity }), [runId, sequence, review.basisHash, identity]);
  const operation = useBoundOperation(scope), portal = useUiPortalScope();
  useLayoutEffect(() => { if (portal?.active === false) operation.cancel(); }, [portal?.active, operation.cancel]);
  const ready = checked && !!content.task.trim() && !!content.title.trim() && content.references.every(item => item.role !== null);
  const change = <K extends keyof AuthoringBriefContent>(key: K, value: AuthoringBriefContent[K]) => { setChecked(false); setContent(current => ({ ...current, [key]: value })); };
  function close() { if (!operation.busy) setOpen(false); }
  async function submit() {
    if (portal?.active === false || (clarifying ? !answer.trim() : !ready)) return;
    const pending = operation.begin(); if (!pending) return;
    setError('');
    try {
      if (clarifying) await onClarify(answer);
      else await onConfirm({ basisHash: review.basisHash, content, confirmed: true });
      if (operation.isCurrent(pending)) setOpen(false);
    } catch (failure) { if (operation.isCurrent(pending)) setError(errorMessage(failure)); }
    finally { operation.finish(pending); }
  }
  return <>{!open && <button type="button" className="secondary" data-nova-user-control="true" onClick={() => setOpen(true)}>Revisar tarea de NOVA</button>}
    <Dialog open={open} onOpenChange={next => { if (!next) close(); }} title="Confirma la tarea del futuro asistente"
      description="NOVA propuso una interpretación. Compárala con tu petición y corrige cualquier omisión antes de continuar.">
      <form className="account-prompt-form" data-nova-command="true" onSubmit={event => { event.preventDefault(); void submit(); }}>
        <details open><summary>Tu petición original</summary><pre className="content-review-text">{review.originalRequest}</pre></details>
        {review.source && <details><summary>Origen: {review.source.title} · versión {review.source.revision}</summary><pre className="content-review-text">{review.source.templateText}</pre></details>}
        <p className="muted">Variables elegidas: {review.inputs.length ? review.inputs.map(name => `<${name}>`).join(', ') : 'sin variables'}. No se piden sus valores ahora. Los marcadores que falten se incorporarán al componer la plantilla.</p>
        {clarifying ? <label>Aclaración de tu encargo<textarea rows={4} maxLength={2000} value={answer} disabled={operation.busy} onChange={event => setAnswer(event.target.value)} /></label> : <>
          <label>Nombre de la plantilla<input maxLength={160} value={content.title} disabled={operation.busy} onChange={event => change('title', event.target.value)} /></label>
          <label>Descripción<input maxLength={2000} value={content.description} disabled={operation.busy} onChange={event => change('description', event.target.value)} /></label>
          <label>Tarea que realizará el futuro asistente<textarea rows={4} maxLength={8000} value={content.task} disabled={operation.busy} onChange={event => change('task', event.target.value)} /></label>
          {(['steps', 'constraints', 'outputRequirements'] as const).map(key => <label key={key}>
            {{ steps: 'Acciones que debe realizar (una por línea)', constraints: 'Condiciones y prohibiciones (una por línea)', outputRequirements: 'Qué debe entregar (un requisito por línea)' }[key]}
            <textarea rows={4} value={content[key].join('\n')} disabled={operation.busy} onChange={event => change(key, event.target.value.split('\n'))} onBlur={() => change(key, content[key].filter(item => item.trim()))} />
          </label>)}
          {content.references.length > 0 && <fieldset><legend>Indica el papel de cada enlace</legend>{content.references.map((item, index) => <label key={item.uri}>
            <span className="content-review-text">{item.uri}</span>
            <select aria-label={`Uso de ${item.uri}`} value={item.role ?? ''} disabled={operation.busy} onChange={event => change('references', content.references.map((reference, current) => current === index
              ? { ...reference, role: event.target.value as NonNullable<typeof item.role> } : reference))}>
              <option value="" disabled>Elige dónde se usará</option><option value="FUTURE_TASK">El futuro asistente lo necesita</option>
              <option value="CURRENT_SESSION">Sólo para esta sesión de autoría</option><option value="BOTH">Ambos usos</option>
            </select></label>)}</fieldset>}
          <label><input type="checkbox" checked={checked} disabled={operation.busy} onChange={event => setChecked(event.target.checked)} />Comparé la tarea, los requisitos y los enlaces con mi petición. Este es el contrato que quiero utilizar.</label>
        </>}
        <p className="muted">Confirmar compone la plantilla sin volver a reescribir estos requisitos con IA. Después revisarás el resultado. Esto no autoriza guardar ni ejecutar.</p>
        {error && <p role="alert" className="error">{error} Conservamos tu revisión; la continuación aún no está confirmada.</p>}
        <div className="dialog-actions"><button type="button" className="secondary" disabled={operation.busy} onClick={close}>Ahora no</button>
          <button type="button" className="secondary" disabled={operation.busy} onClick={() => { setClarifying(value => !value); setError(''); }}>{clarifying ? 'Volver al contrato' : 'Necesito aclarar el encargo'}</button>
          <button className="primary" disabled={operation.busy || (clarifying ? !answer.trim() : !ready)}>{operation.busy ? 'Continuando…' : clarifying ? 'Enviar aclaración' : 'Confirmar tarea y componer plantilla'}</button></div>
      </form>
    </Dialog></>;
}
