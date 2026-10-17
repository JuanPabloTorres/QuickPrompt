import { useEffect, useId, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Check, ChevronDown, LoaderCircle, Pause, Play, Square, X } from 'lucide-react';
import { Dialog } from '../../components/ui/Dialog';
import { errorMessage, post, request } from '../../services/api';
import { liveEventSchema, liveRecordSchema, liveViewSchema, terminalLive, type LiveEvent, type LiveView } from '../../services/live';

const operationLabels: Record<string, string> = {
  start: 'Preparando tu espacio de trabajo', ui_navigate_library: 'Abriendo tu biblioteca',
  ui_search_prompt: 'Buscando en tu biblioteca', ui_open_prompt: 'Abriendo el prompt',
  ui_open_create_prompt: 'Abriendo un nuevo borrador', ui_prepare_creation: 'Escribiendo el borrador',
  ui_load_active_draft: 'Recuperando tu borrador', ui_open_editor: 'Abriendo el editor',
  ui_fill_variable: 'Completando tus datos', ui_prepare_update: 'Preparando los cambios',
  ui_show_compiled_prompt: 'Preparando la vista previa', ui_open_provider_picker: 'Preparando el destino',
  ui_execute_local: 'Generando la respuesta', ui_read_visible_state: 'Revisando el contenido',
  ui_verify_prompt_state: 'Comprobando el resultado', ui_request_save_version: 'Preparando la revisión',
  prepare_review: 'Preparando la revisión', commit_approved: 'Guardando la versión aprobada',
  finish: 'Comprobando el resultado', ask_clarification: 'Necesito un dato más'
};

// Keep activity inside an open dialog's accessible focus scope, anchored to the viewport.
function useActivityHost() {
  const [host, setHost] = useState<HTMLElement | null>(null);
  useEffect(() => {
    const update = () => setHost(previous => {
      const dialogs = document.querySelectorAll<HTMLElement>('[data-nova-activity-host]');
      const next = dialogs.item(dialogs.length - 1) ?? document.body;
      return previous === next ? previous : next;
    });
    update();
    const observer = new MutationObserver(update); observer.observe(document.body, { childList: true });
    return () => observer.disconnect();
  }, []);
  return host;
}

export function NovaLive({ runId, worker, onCommit, onReview, speed = 'normal', onSpeedChange, notice = '' }: {
  runId: string | null; worker: boolean; onCommit: () => Promise<void>; onReview: (view: LiveView) => void;
  speed?: 'normal' | 'fast'; onSpeedChange?: (speed: 'normal' | 'fast') => void; notice?: string;
}) {
  const [view, setView] = useState<LiveView | null>(null), [events, setEvents] = useState<LiveEvent[]>([]);
  const [step, setStep] = useState(0), [expanded, setExpanded] = useState(false);
  const host = useActivityHost(), panelId = useId();
  const trigger = useRef<HTMLButtonElement>(null), floating = useRef<HTMLElement>(null);
  useEffect(() => {
    if (!expanded) return;
    const outside = (event: PointerEvent) => { if (event.target instanceof Node && !floating.current?.contains(event.target)) setExpanded(false); };
    document.addEventListener('pointerdown', outside);
    return () => document.removeEventListener('pointerdown', outside);
  }, [expanded]);
  const [error, setError] = useState(''), [connection, setConnection] = useState(''), [review, setReview] = useState(false), [busy, setBusy] = useState(false);
  const last = useRef(0), generation = useRef(0);
  useEffect(() => {
    if (!runId) return;
    const current = ++generation.current; const controller = new AbortController(); last.current = 0; setEvents([]); setStep(0); setView(null); setReview(false); setExpanded(false); setError(''); setConnection('');
    async function refresh() { try { const next = await request(`/nova/live/${runId}`, liveViewSchema, { signal: controller.signal }); if (generation.current === current) { setView(previous => !previous || next.run.sequence >= previous.run.sequence ? next : previous); if (terminalLive(next.run.status) && !next.run.browserOpen && last.current >= next.run.sequence) stream.close(); } } catch (error) { if (!controller.signal.aborted) setError(errorMessage(error)); } }
    void refresh();
    let stream: EventSource;
    function connect() {
    stream = new EventSource(`/api/v1/nova/live/${runId}/events?after=${last.current}`);
    stream.addEventListener('activity', event => {
      if (controller.signal.aborted || generation.current !== current) return;
      try {
        const item = liveEventSchema.parse(JSON.parse((event as MessageEvent<string>).data));
        if (item.runId !== runId || item.sequence <= last.current) return;
        if (item.sequence !== last.current + 1) { setConnection('Recuperando la conexión con NOVA…'); stream.close(); connect(); return; }
        last.current = item.sequence; setConnection(''); setEvents(previous => [...previous, item].slice(-100)); if (item.stage === 'Decision') setStep(previous => previous + 1); void refresh();
        // The last cleanup event follows COMPLETED/CANCELLED; close only once persisted BrowserOpen is false.
      } catch { setError('No se puede actualizar el progreso. Detén NOVA y vuelve a intentarlo.'); }
    });
    stream.onopen = () => setConnection(''); stream.onerror = () => { if (!controller.signal.aborted) { setConnection('Conexión interrumpida. NOVA está esperando para continuar.'); void refresh(); } };
    }
    connect();
    const openReview = () => { setExpanded(false); void refresh().then(() => setReview(true)); }; window.addEventListener('nova-review-approval', openReview);
    return () => { controller.abort(); stream.close(); window.removeEventListener('nova-review-approval', openReview); };
  }, [runId]);
  async function control(action: 'pause' | 'resume' | 'cancel') {
    if (!runId) return; if (action !== 'resume') window.dispatchEvent(new CustomEvent('nova-ui-control', { detail: action })); setBusy(true); setError('');
    try { const controlled = await request(`/nova/live/${runId}/${action}`, liveRecordSchema, post({})); if (action === 'resume' && !controlled.pauseRequested && !terminalLive(controlled.status)) window.dispatchEvent(new CustomEvent('nova-ui-control', { detail: 'resume' })); setView(await request(`/nova/live/${runId}`, liveViewSchema)); }
    catch (error) { setError(errorMessage(error)); } finally { setBusy(false); }
  }
  async function approve() {
    if (!runId || !view?.proposal) return; setBusy(true); setError('');
    try { const p = view.proposal; await request(`/nova/live/${runId}/approve`, liveRecordSchema, post({ approvalId: p.id, hash: p.hash, revision: p.revision, confirmed: true })); setReview(false); setView(await request(`/nova/live/${runId}`, liveViewSchema)); }
    catch (error) { setError(errorMessage(error)); } finally { setBusy(false); }
  }
  if (!runId || !view) return error ? <p role="alert" className="error">No se pudo consultar la actividad de NOVA. Vuelve a intentarlo.</p> : null;
  const run = view.run, active = !terminalLive(run.status), recent = events.at(-1);
  if (!active) return run.status === 'BLOCKED' || run.status === 'FAILED'
    ? <p className="notice nova-outcome" role="status">{notice || view.question || 'NOVA no pudo terminar esta tarea. Revisa el contenido y vuelve a intentarlo.'}</p> : null;
  const awaitingReview = run.status === 'AWAITING_APPROVAL';
  const paused = run.pauseRequested || run.status === 'PAUSED';
  const operation = recent?.toolName ? operationLabels[recent.toolName] : undefined;
  const status = paused ? 'En pausa' : awaitingReview ? 'Borrador listo para revisar' : run.status === 'INTERPRETING' ? 'Entendiendo tu petición' : operation ?? 'Preparando tu solicitud';
  const steps = events.filter(event => event.toolName && operationLabels[event.toolName] && ['TOOL_PROPOSED', 'UI_UPDATED'].includes(event.status))
    .map(event => ({ id: event.eventId, label: operationLabels[event.toolName!] }))
    .filter((item, index, items) => index === 0 || item.label !== items[index - 1].label).slice(-4);
  return <>
    {host && createPortal(<aside className="nova-live" ref={floating} data-testid="nova-live" data-run-id={runId} data-status={run.status} onKeyDown={event => {
      if (event.key === 'Escape' && expanded) { event.preventDefault(); event.stopPropagation(); setExpanded(false); trigger.current?.focus(); }
    }}>
      {expanded && <section className="nova-live-popover" id={panelId} aria-labelledby="nova-live-heading">
        <header className="nova-live-heading"><div><p className="eyebrow">NOVA · EN VIVO{step > 0 ? ` · Paso ${step}` : ''}</p><h2 id="nova-live-heading">{status}</h2></div><button className="icon-button" aria-label="Cerrar actividad" onClick={() => { setExpanded(false); trigger.current?.focus(); }}><X size={18} aria-hidden="true" /></button></header>
        {view.observation?.title && <p className="live-subject">{view.observation.title}</p>}
        {steps.length > 0 && <ol className="live-steps" aria-label="Actividad reciente de NOVA">{steps.map(item => <li key={item.id}>{item.label}</li>)}</ol>}
        {(notice || connection) && <p className="notice">{notice || connection}</p>}
        {view.question && <p className="notice">{view.question}</p>}
        {run.mode === 'SAME_TAB' && onSpeedChange && <label className="live-speed">Velocidad de preparación<select aria-label="Velocidad de NOVA" value={speed} onChange={event => onSpeedChange(event.target.value as 'normal' | 'fast')}><option value="normal">Visible</option><option value="fast">Rápida</option></select></label>}
        <div className="live-controls">
          {(run.mode !== 'SAME_TAB' || worker) && <button className="secondary" disabled={busy} onClick={() => void control(paused ? 'resume' : 'pause')}>{paused ? <Play size={16} aria-hidden="true" /> : <Pause size={16} aria-hidden="true" />}{paused ? 'Continuar' : 'Pausar'}</button>}
          <button className="secondary" disabled={busy} onClick={() => void control('cancel')}><Square size={16} aria-hidden="true" />Detener NOVA</button>
        </div>
        {view.proposal && <div className="live-review-actions"><button className="primary" disabled={busy || paused || run.approval !== 'PENDING'} onClick={() => { setExpanded(false); setReview(true); }}>Revisar y guardar versión</button><button className="text-button" disabled={busy} onClick={() => void control('cancel')}>Cancelar modificación</button></div>}
        {worker && run.approval === 'APPROVED' && <button className="primary" onClick={() => { void onCommit().catch(error => setError(errorMessage(error))); }}>Aplicar versión aprobada</button>}
        {run.mode === 'BROWSER' && view.observation?.promptId && <button className="text-button" onClick={() => onReview(view)}>Ver el resultado</button>}
        {error && <p className="error" role="alert">No se pudo completar esa acción. Revisa el borrador y vuelve a intentarlo.</p>}
      </section>}
      <button ref={trigger} type="button" className="nova-live-trigger" aria-label="Actividad de NOVA" aria-expanded={expanded} aria-controls={expanded ? panelId : undefined} onClick={() => setExpanded(previous => !previous)}>
        {paused ? <Pause size={19} aria-hidden="true" /> : awaitingReview ? <Check size={19} aria-hidden="true" /> : <LoaderCircle className="nova-working" size={19} aria-hidden="true" />}
        <span><strong>NOVA</strong><span>{status}</span></span><ChevronDown size={16} className={expanded ? 'nova-activity-expanded' : ''} aria-hidden="true" />
      </button>
      <span className="sr-only" role="status" aria-atomic="true">NOVA: {status}</span>
    </aside>, host)}
    <Dialog className="nova-approval-dialog" open={review && !!view.proposal} onOpenChange={setReview} title={view.proposal?.revision ? 'Confirmar nueva versión' : 'Guardar nuevo prompt'} description="Revisa el contenido antes de confirmar. Solo se guardará esta propuesta.">
      {view.proposal && <div data-nova-approval="true"><p><strong>{view.proposal.draft.title}</strong>{view.proposal.revision ? ` · versión ${view.proposal.revision}` : ''}</p>{view.proposal.originalText && <details><summary>Ver original</summary><pre>{view.proposal.originalText}</pre></details>}<h3>Propuesta que vas a guardar</h3><pre className="prepared-text">{view.proposal.draft.templateText}</pre><div className="dialog-actions"><button className="primary" disabled={busy || paused || run.approval !== 'PENDING'} onClick={() => void approve()}>Confirmar guardado de esta versión</button><button className="secondary" disabled={busy} onClick={() => setReview(false)}>Seguir revisando</button><button className="text-button" disabled={busy} onClick={() => { setReview(false); void control('cancel'); }}>Rechazar propuesta</button></div></div>}
      {error && <p role="alert" className="error">No se pudo confirmar el guardado. Revisa el borrador y vuelve a intentarlo.</p>}
    </Dialog>
  </>;
}
