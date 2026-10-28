import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { Dialog } from '../../components/ui/Dialog';
import type { AccountConnection, AccountPrompt, AccountService } from '../../services/accounts';
import type { FactualFact, FactualReview } from '../../services/accountFactual';
import { PromptVariableFields } from '../prompts/PromptVariableFields';
import { useAccountOperation } from './useAccountOperation';
import '../../design/factual.css';

const waiting = (review: FactualReview) => review.status === 'ADMITTED' || review.status === 'PROPOSING';
const pending = (review: FactualReview) => waiting(review) || review.status === 'REVIEW' || review.status === 'COMPOSED';
const outcomes: Partial<Record<FactualReview['status'], string>> = { CANCELLED: 'Revisión cancelada. No se guardó un documento.', INVALIDATED: 'Cambió el origen o se retiró la conexión. Inicia una revisión nueva.', EXPIRED: 'Esta revisión caducó. Inicia una revisión nueva.', FAILED: 'No se pudo proponer los hechos. No se guardó un documento.' };

/** Explicit factual capability: input, fact confirmation and literal-document approval are separate decisions. */
export function AccountFactualReviewDialog({ prompt, names, initialValues, service, onExpired, onClose }: {
  prompt: AccountPrompt; names: string[]; initialValues: Record<string, string>; service: AccountService; onExpired: () => void; onClose: () => void;
}) {
  const api = useMemo(() => service.factual(prompt.libraryId), [service, prompt.libraryId]);
  const scope = useMemo(() => ({ api, prompt }), [api, prompt]);
  const operation = useAccountOperation(scope, onExpired);
  const [connections, setConnections] = useState<AccountConnection[]>([]);
  const [connectionId, setConnectionId] = useState('');
  const [values, setValues] = useState(() => ({ ...initialValues }));
  const [sources, setSources] = useState<string[]>([]);
  const [title, setTitle] = useState('');
  const [review, setReview] = useState<FactualReview>();
  const [savedDocumentId, setSavedDocumentId] = useState<string>();
  const [facts, setFacts] = useState<FactualFact[]>([]);
  const [factsAccepted, setFactsAccepted] = useState(false);
  const [documentAccepted, setDocumentAccepted] = useState(false);
  const [approvalUncertain, setApprovalUncertain] = useState(false);
  const [correctingFacts, setCorrectingFacts] = useState(false);
  const [closeNotice, setCloseNotice] = useState('');
  const [pollAttempt, setPollAttempt] = useState(0);
  const [clock, setClock] = useState(Date.now());
  const receipt = useRef<{ fingerprint: string; operationId: string } | undefined>(undefined);
  useLayoutEffect(() => {
    setReview(undefined); setSavedDocumentId(undefined); setFacts([]); setFactsAccepted(false); setDocumentAccepted(false); setApprovalUncertain(false); setCorrectingFacts(false); setConnections([]); setConnectionId(''); setSources([]); setTitle(''); setValues({ ...initialValues }); setCloseNotice(''); receipt.current = undefined;
  }, [scope]);
  useEffect(() => {
    if (!review) return;
    const timer = setTimeout(() => setClock(Date.now()), Math.min(2147483647, Math.max(0, Date.parse(review.expiresAt) - Date.now()) + 20));
    return () => clearTimeout(timer);
  }, [review?.expiresAt]);
  useEffect(() => {
    const controller = new AbortController();
    service.connections(controller.signal).then(items => {
      if (!controller.signal.aborted) setConnections(items.filter(item => item.active && !item.revoked && item.profileId === 'ollama-qwen3-8b-proposals'));
    }).catch(error => { if (!controller.signal.aborted) operation.fail(error); });
    return () => controller.abort();
  }, [service, operation.fail]);
  function adopt(value: FactualReview) {
    if (value.promptId !== prompt.id || value.promptRevision !== prompt.revision || value.connectionId !== connectionId) throw new Error('La revisión cambió de origen o conexión.');
    setReview(value); setFactsAccepted(false); setDocumentAccepted(false); setApprovalUncertain(false); setCorrectingFacts(false);
    if (value.status === 'REVIEW') { setFacts(value.proposalFacts.map(fact => ({ ...fact }))); setTitle(value.title); }
  }
  useEffect(() => {
    if (!review || !waiting(review) || Date.parse(review.expiresAt) <= Date.now()) return;
    const controller = new AbortController(); let timer: ReturnType<typeof setTimeout>;
    async function poll() {
      try {
        const value = await api.review(review!.id, controller.signal);
        if (controller.signal.aborted) return;
        if (value.promptId !== prompt.id || value.promptRevision !== prompt.revision || value.connectionId !== connectionId) throw new Error('La revisión cambió de origen o conexión.');
        if (waiting(value)) timer = setTimeout(poll, 3000);
        else adopt(value);
      } catch (error) { if (!controller.signal.aborted) operation.fail(error); }
    }
    timer = setTimeout(poll, 2000);
    return () => { controller.abort(); clearTimeout(timer); };
  }, [api, review?.id, review?.status, prompt, connectionId, pollAttempt, clock, operation.fail]);
  function close() {
    if (operation.busy || (!savedDocumentId && review && pending(review))) { setCloseNotice('Cancela la revisión pendiente antes de cerrar. Cerrar esta ventana no aprueba ni guarda contenido.'); return; }
    operation.cancel(); onClose();
  }
  function start() {
    const input = { promptId: prompt.id, revision: prompt.revision, connectionId, values, sourceFields: sources, title };
    const fingerprint = JSON.stringify(input);
    if (receipt.current?.fingerprint !== fingerprint) receipt.current = { fingerprint, operationId: crypto.randomUUID() };
    void operation.run(signal => api.start({ ...input, operationId: receipt.current!.operationId }, signal), adopt);
  }
  const editable = review?.status === 'REVIEW' || (review?.status === 'COMPOSED' && correctingFacts);
  const expired = review ? Date.parse(review.expiresAt) <= Math.max(clock, Date.now()) : false;
  const sourceBytes = new TextEncoder().encode(sources.map(name => values[name] ?? '').join('')).length;
  return <Dialog open onOpenChange={open => { if (!open) close(); }} title="Transformación factual con revisión" description="NOVA propone hechos a partir de los campos que elijas. Tú confirmas los hechos y, por separado, apruebas el texto exacto que se conservará como documento privado.">
    <div className="account-factual">
      <ol className="factual-stages" aria-label="Etapas de revisión"><li aria-current={!review ? 'step' : undefined}>1 · Elegir fuentes</li><li aria-current={review && ((review.status !== 'COMPOSED' && review.status !== 'APPROVED') || correctingFacts) ? 'step' : undefined}>2 · Confirmar hechos</li><li aria-current={(review?.status === 'COMPOSED' && !correctingFacts) || review?.status === 'APPROVED' ? 'step' : undefined}>3 · Aprobar documento</li></ol>
      {!review && <form className="account-prompt-form" onSubmit={event => { event.preventDefault(); start(); }}><fieldset disabled={operation.busy}>
        <label htmlFor="factual-title">Título del documento</label><input id="factual-title" value={title} maxLength={160} onChange={event => setTitle(event.target.value)} required />
        <label htmlFor="factual-connection">Conexión de NOVA</label><select id="factual-connection" value={connectionId} onChange={event => setConnectionId(event.target.value)} required><option value="">Elige un equipo autorizado</option>{connections.map(item => <option key={item.id} value={item.id}>{item.label}</option>)}</select>
        {!connections.length && <p role="status">Necesitas una conexión activa de propuestas de NOVA. Puedes vincularla desde Conexiones locales.</p>}
        <PromptVariableFields names={names} values={values} idPrefix="factual-value" maxLength={8000} onValueChange={(name, value) => setValues(current => ({ ...current, [name]: value }))} />
        <fieldset className="factual-source-selection"><legend>Campos que contienen los hechos</legend><p className="muted">Selecciona sólo los datos que quieres transformar. La plantilla y los demás campos no se consideran pruebas.</p>{names.map(name => <label className="factual-check" key={name}><input type="checkbox" checked={sources.includes(name)} onChange={event => setSources(current => event.target.checked ? [...current, name] : current.filter(item => item !== name))} />{name}</label>)}{!names.length && <p>Este prompt no tiene variables. Agrega un campo de datos a la plantilla antes de iniciar una transformación factual.</p>}</fieldset>
        <p className={sourceBytes > 4000 ? 'error' : 'muted'} role={sourceBytes > 4000 ? 'alert' : undefined}>Fuentes seleccionadas: {sourceBytes} de 4000 bytes UTF-8. El servidor también valida el paquete completo; no se recorta tu texto.</p>
      </fieldset><button className="primary" disabled={operation.busy || sourceBytes > 4000 || !connectionId || !title.trim() || !sources.length || names.some(name => !values[name]?.trim())}>Solicitar propuesta de hechos</button></form>}
      {review && <><p className="muted">Prompt versión {review.promptRevision} · La revisión vence {new Date(review.expiresAt).toLocaleString('es')}.</p>
        {waiting(review) && <p role="status">NOVA está proponiendo hechos. Puedes cancelar; todavía no hay un documento aprobado.</p>}
        {editable && <><h3>Revisa los hechos propuestos</h3><p>El extracto sólo identifica el texto de origen. Comprueba por separado si respalda el hecho y su rol. Puedes corregir o quitar propuestas antes de confirmar.</p>
          <label htmlFor="factual-review-title">Título del documento</label><input id="factual-review-title" value={title} maxLength={160} disabled={operation.busy} onChange={event => { setTitle(event.target.value); setFactsAccepted(false); }} />
          {!facts.length && <p role="status">Sin hechos propuestos: añade y revisa hechos antes de confirmar.</p>}
          <FactualFacts facts={facts} sources={review.sourceFields} disabled={operation.busy} onChange={next => { setFacts(next); setFactsAccepted(false); }} />
          <label className="factual-check"><input type="checkbox" checked={factsAccepted} disabled={operation.busy} onChange={event => setFactsAccepted(event.target.checked)} />Revisé los hechos, sus roles y sus extractos de origen.</label>
          <button className="primary" disabled={operation.busy || expired || !factsAccepted || !title.trim() || !facts.length || facts.some(fact => !fact.text.trim() || !fact.role.trim() || (fact.quote ? !review.sourceFields[fact.sourceField]?.includes(fact.quote) : !!fact.sourceField))} onClick={() => void operation.run(signal => api.confirm(review.id, review.version, facts, title, signal), adopt)}>Confirmar hechos y ver documento</button></>}
        {review.status === 'COMPOSED' && !savedDocumentId && !correctingFacts && <section aria-label="Documento pendiente de aprobación"><h3>Texto exacto que se guardará</h3><p>Se compuso con los hechos confirmados. Este paso no vuelve a consultar al modelo.</p><pre className="history-text">{review.preview}</pre>
          <p>Se guardarán el documento, los hechos y las fuentes seleccionadas en tu historial hasta que los elimines.</p>
          <button type="button" className="secondary" disabled={operation.busy || expired || approvalUncertain} onClick={() => { setFacts(review.confirmedFacts.map(fact => ({ ...fact }))); setTitle(review.title); setFactsAccepted(false); setDocumentAccepted(false); setCorrectingFacts(true); }}>Corregir hechos</button>
          <label className="factual-check"><input type="checkbox" checked={documentAccepted} disabled={operation.busy} onChange={event => setDocumentAccepted(event.target.checked)} />Apruebo guardar este texto exacto en mi historial de documentos.</label>
          <button className="primary" disabled={operation.busy || expired || approvalUncertain || !documentAccepted || review.preview === null} onClick={() => { setApprovalUncertain(true); void operation.run(signal => api.approve(review.id, review.version, signal), document => {
            if (document.promptId !== prompt.id || document.promptRevision !== prompt.revision || document.text !== review.preview || document.title !== review.title) throw new Error('El recibo no confirma el texto y origen que aprobaste. Consulta el estado antes de reintentar.');
            if (Object.keys(document.sourceFields).length !== Object.keys(review.sourceFields).length || Object.entries(review.sourceFields).some(([name, text]) => document.sourceFields[name] !== text)) throw new Error('El recibo no confirma las fuentes seleccionadas. Consulta el estado antes de reintentar.');
            setSavedDocumentId(document.id); setDocumentAccepted(false); setApprovalUncertain(false);
          }); }}>Guardar documento aprobado</button></section>}
        {(review.status === 'APPROVED' || savedDocumentId) && <p role="status">Documento aprobado y guardado. Lo encontrarás en Documentos aprobados de esta biblioteca.</p>}
        {outcomes[review.status] && <p role="alert">{outcomes[review.status]}{review.errorCode && ` Código: ${review.errorCode}.`}</p>}
        {expired && pending(review) && <p role="alert">La revisión venció; ya no puedes confirmar ni aprobar.</p>}
        {pending(review) && !savedDocumentId && <button className="secondary" disabled={operation.busy} onClick={() => void operation.run(signal => api.cancel(review.id, review.version, signal), adopt)}>Cancelar revisión</button>}
      </>}
      {operation.error && <p role="alert" className="error">{operation.error} No se presupone que la operación haya terminado.</p>}
      {review && operation.error && <button className="secondary" disabled={operation.busy} onClick={() => { operation.clear(); if (waiting(review)) setPollAttempt(value => value + 1); else void operation.run(signal => api.review(review.id, signal), adopt); }}>Consultar estado actual</button>}
      {closeNotice && <p role="status">{closeNotice}</p>}
      <div className="dialog-actions"><button type="button" className="secondary" onClick={close}>Cerrar</button></div>
    </div>
  </Dialog>;
}

function FactualFacts({ facts, sources, disabled, onChange }: { facts: FactualFact[]; sources: Record<string, string>; disabled: boolean; onChange: (facts: FactualFact[]) => void }) {
  function edit(index: number, field: keyof FactualFact, value: string) { onChange(facts.map((fact, position) => position === index ? { ...fact, [field]: value } : fact)); }
  return <fieldset disabled={disabled} className="factual-facts"><legend>Hechos que confirmarás</legend>{facts.map((fact, index) => <fieldset key={index} className="factual-fact"><legend>Hecho {index + 1}</legend>
    <label htmlFor={`fact-role-${index}`}>Rol del hecho {index + 1}</label><input id={`fact-role-${index}`} value={fact.role} maxLength={100} onChange={event => edit(index, 'role', event.target.value)} />
    <label htmlFor={`fact-text-${index}`}>Texto del hecho {index + 1}</label><textarea id={`fact-text-${index}`} value={fact.text} maxLength={1000} onChange={event => edit(index, 'text', event.target.value)} />
    <label htmlFor={`fact-source-${index}`}>Campo de origen del hecho {index + 1}</label><select id={`fact-source-${index}`} value={fact.sourceField} onChange={event => edit(index, 'sourceField', event.target.value)}><option value="">Elige el campo</option>{Object.keys(sources).map(name => <option key={name}>{name}</option>)}</select>
    <label htmlFor={`fact-quote-${index}`}>Extracto literal del hecho {index + 1}</label><textarea id={`fact-quote-${index}`} value={fact.quote} maxLength={2000} onChange={event => edit(index, 'quote', event.target.value)} />
    {!fact.quote && !fact.sourceField && <p className="muted">Sin fuente documental; revisa esta afirmación.</p>}
    <details><summary>Ver el campo de origen completo</summary><pre className="history-text">{sources[fact.sourceField] ?? 'No hay un campo de origen seleccionado.'}</pre></details>
    {fact.quote && !sources[fact.sourceField]?.includes(fact.quote) && <p className="error" role="alert">El extracto no coincide literalmente con el campo seleccionado.</p>}
    <button type="button" className="text-button" onClick={() => onChange(facts.filter((_, position) => position !== index))}>Quitar hecho {index + 1}</button>
  </fieldset>)}<button type="button" className="secondary" disabled={facts.length >= 40} onClick={() => onChange([...facts, { role: '', text: '', sourceField: '', quote: '' }])}>Añadir hecho revisado</button></fieldset>;
}
