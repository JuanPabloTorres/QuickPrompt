import { useApplicationServices } from '../../components/ServiceContext';
import { AppIcon } from '../../components/ui/Icon';
import { useEffect, useId, useRef, useState, useSyncExternalStore } from 'react';
import { createPortal } from 'react-dom';
import { visualState } from '../../design/nova-state';
import { NovaLogo } from '../../design/NovaLogo';
import { Check, ChevronDown, CircleAlert, Pause, Play, Square, X } from 'lucide-react';
import { ActivityLog } from './ActivityLog';
import { AgentRuntimePanel } from './AgentRuntimePanel';
import '../../design/runtime.css';
import { getActivity, operationLabels, stageLabels, subscribeActivity } from './activity';
import { Dialog } from '../../components/ui/Dialog';
import { errorMessage } from '../../services/api';
import { liveEventSchema, terminalLive, type LiveEvent, type LiveView, type LiveEvents } from '../../services/live';

const noActivitySubscription = () => () => {};

// Keep activity inside an open dialog's accessible focus scope, anchored to the viewport.
function useActivityHost() {
  const [host, setHost] = useState<HTMLElement | null>(null);
  useEffect(() => {
    const update = () => setHost(previous => {
      const dialogs = document.querySelectorAll<HTMLElement>('[data-nova-activity-host]');
      const next = dialogs.item(dialogs.length - 1) ?? document.querySelector<HTMLElement>('[data-nova-dock-live-host]') ?? document.body;
      return previous === next ? previous : next;
    });
    update();
    const observer = new MutationObserver(update); observer.observe(document.body, { childList: true });
    return () => observer.disconnect();
  }, []);
  return host;
}

export function NovaLive({ runId, worker, onCommit, onReview, notice = '' }: {
  runId: string | null; worker: boolean; onCommit: () => Promise<void>; onReview: (view: LiveView) => void;
  notice?: string;
}) {
  const { live, visibleSession } = useApplicationServices();
  const { surfaceCapability } = visibleSession;
  const [view, setView] = useState<LiveView | null>(null), [events, setEvents] = useState<LiveEvent[]>([]);
  const [step, setStep] = useState(0), [expanded, setExpanded] = useState(false);
  const [connection, setConnection] = useState('');
  const currentRun = useRef(runId); currentRun.current = runId;
  const [assistantVisible, setAssistantVisible] = useState(false);
  const host = useActivityHost(), panelId = useId();
  const detailsVisible = expanded && (!host?.hasAttribute('data-nova-dock-live-host') || assistantVisible);
  useEffect(() => {
    const visibility = (event: Event) => {
      const visible = (event as CustomEvent<boolean>).detail;
      setAssistantVisible(visible);
      // Reopening the assistant exposes task choices. Completed history stays
      // available through its explicit control instead of covering quick actions.
      // An active task retains its existing disclosure and pause controls.
      if (visible && view && terminalLive(view.run.status)) setExpanded(false);
    };
    window.addEventListener('nova-assistant-visibility', visibility);
    return () => window.removeEventListener('nova-assistant-visibility', visibility);
  }, [view?.run.status]);
  useEffect(() => {
    window.dispatchEvent(new CustomEvent('nova-agent-status', { detail: view ? view.run.pauseRequested ? 'PAUSED' : view.run.status : '' }));
  }, [view?.run.status, view?.run.pauseRequested]);
  const observedStage = events.at(-1)?.stage, observedTool = events.at(-1)?.toolName;
  const visual = connection ? 'idle' : visualState(view?.run.status, observedStage, observedTool, view?.run.pauseRequested);
  useEffect(() => { window.dispatchEvent(new CustomEvent('nova-visual-state', { detail: visual })); }, [visual]);
  const actions = useSyncExternalStore(detailsVisible ? subscribeActivity : noActivitySubscription, () => getActivity(runId));
  useEffect(() => {
    const open = () => { if (runId) setExpanded(true); };
    const close = () => {
      setExpanded(false);
      if (view && terminalLive(view.run.status)) document.querySelector<HTMLButtonElement>('[data-nova-history]')?.focus();
      else trigger.current?.focus();
    };
    window.addEventListener('nova-open-activity', open);
    window.addEventListener('nova-close-activity', close);
    return () => { window.removeEventListener('nova-open-activity', open); window.removeEventListener('nova-close-activity', close); };
  }, [runId, view?.run.status]);
  const [startNotice, setStartNotice] = useState(false);
  useEffect(() => {
    let timer: ReturnType<typeof setTimeout> | undefined;
    const started = () => {
      clearTimeout(timer); setStartNotice(true);
      timer = setTimeout(() => setStartNotice(false), 5000);
    };
    window.addEventListener('nova-agent-started', started);
    return () => { clearTimeout(timer); window.removeEventListener('nova-agent-started', started); };
  }, []);
  const trigger = useRef<HTMLButtonElement>(null), floating = useRef<HTMLElement>(null);
  useEffect(() => {
    if (!expanded) return;
    const outside = (event: PointerEvent) => {
      if (!(event.target instanceof Node)) return;
      // Inside the assistant, closing activity on pointerdown would shrink its
      // anchored panel and move the clicked control before pointerup/click.
      if (document.querySelector('.nova-dock')?.contains(event.target)) return;
      if (!floating.current?.contains(event.target)) setExpanded(false);
    };
    document.addEventListener('pointerdown', outside);
    return () => document.removeEventListener('pointerdown', outside);
  }, [expanded]);
  const [error, setError] = useState(''), [review, setReview] = useState(false), [busy, setBusy] = useState(false);
  const last = useRef(0), generation = useRef(0);
  useEffect(() => {
    if (!runId) return;
    const current = ++generation.current; const controller = new AbortController(); last.current = 0; setEvents([]); setStep(0); setView(null); setReview(false); setExpanded(false); setBusy(false); setError(''); setConnection('');
    let refreshFlight: Promise<void> | null = null;
    let terminalObserved = false;
    let refreshTimer: ReturnType<typeof setTimeout> | undefined;
    function scheduleRefresh() {
      if (refreshTimer !== undefined || controller.signal.aborted) return;
      refreshTimer = setTimeout(() => { refreshTimer = undefined; void refresh(); }, 120);
    }
    function refresh(): Promise<void> {
      if (refreshFlight) return refreshFlight;
      const requestedSequence = last.current;
      refreshFlight = (async () => {
        try {
          const next = await live.get(runId!, controller.signal);
          if (next.run.runId !== runId) throw new Error('RunId no coincide.');
          if (!controller.signal.aborted && generation.current === current) {
            setView(previous => !previous || next.run.sequence >= previous.run.sequence ? next : previous);
            if (terminalLive(next.run.status)) { terminalObserved = true; setConnection(''); }
            if (next.run.sequence >= last.current) { clearTimeout(refreshTimer); refreshTimer = undefined; }
            if (terminalLive(next.run.status) && !next.run.browserOpen && last.current >= next.run.sequence) stream.close();
            if (last.current > requestedSequence && next.run.sequence < last.current) scheduleRefresh();
          }
        } catch (error) { if (!controller.signal.aborted) setError(errorMessage(error)); }
        finally { refreshFlight = null; }
      })();
      return refreshFlight;
    }
    void refresh();
    let stream: LiveEvents;
    function connect() {
    stream = live.events(runId!, last.current);
    stream.addEventListener('activity', event => {
      if (controller.signal.aborted || generation.current !== current) return;
      try {
        const item = liveEventSchema.parse(JSON.parse((event as MessageEvent<string>).data));
        if (item.runId !== runId || item.sequence <= last.current) return;
        if (item.sequence !== last.current + 1) { setConnection('Recuperando la conexión con NOVA…'); stream.close(); connect(); return; }
        last.current = item.sequence; setConnection(''); setEvents(previous => [...previous, item].slice(-100)); if (item.stage === 'Decision') setStep(previous => previous + 1); scheduleRefresh();
        if (terminalLive(item.status)) terminalObserved = true;
        if (item.stage === 'UiActionDispatched' || terminalLive(item.status) || item.status === 'PAUSED') window.dispatchEvent(new CustomEvent('nova-ui-state-available', { detail: runId }));
        // The last cleanup event follows COMPLETED/CANCELLED; close only once persisted BrowserOpen is false.
      } catch { setError('No se puede actualizar el progreso. Detén NOVA y vuelve a intentarlo.'); }
    });
    stream.onopen = () => { if (!controller.signal.aborted && generation.current === current) setConnection(''); };
    stream.onerror = () => { if (!controller.signal.aborted && generation.current === current) { if (!terminalObserved) setConnection('Conexión interrumpida. El estado de NOVA no está confirmado.'); void refresh(); } };
    }
    connect();
    const openReview = () => { window.dispatchEvent(new CustomEvent('nova-open-assistant', { detail: { focus: false } })); setExpanded(false); void refresh().then(() => setReview(true)); }; window.addEventListener('nova-review-approval', openReview);
    return () => { controller.abort(); clearTimeout(refreshTimer); stream.close(); window.removeEventListener('nova-review-approval', openReview); };
  }, [runId]);
  async function control(action: 'pause' | 'resume' | 'cancel') {
    if (!runId) return; const controlledId = runId; if (action !== 'resume') window.dispatchEvent(new CustomEvent('nova-ui-control', { detail: action })); setBusy(true); setError('');
    try { const controlled = await live.control(controlledId, action); if (currentRun.current !== controlledId) return; if (action === 'resume' && !controlled.pauseRequested && !terminalLive(controlled.status)) window.dispatchEvent(new CustomEvent('nova-ui-control', { detail: 'resume' })); const next = await live.get(controlledId); if (currentRun.current === controlledId && next.run.runId === controlledId) setView(previous => previous?.run.runId === controlledId && previous.run.sequence > next.run.sequence ? previous : next); }
    catch (error) { if (currentRun.current === controlledId) setError(errorMessage(error)); } finally { if (currentRun.current === controlledId) setBusy(false); }
  }
  async function approve() {
    if (!runId || !view?.proposal) return; const approvedId = runId; setBusy(true); setError('');
    try { const p = view.proposal; await live.approve(approvedId, { approvalId: p.id, hash: p.hash, revision: p.revision, confirmed: true }); if (currentRun.current !== approvedId) return; setReview(false); const next = await live.get(approvedId); if (currentRun.current === approvedId && next.run.runId === approvedId) setView(previous => previous?.run.runId === approvedId && previous.run.sequence > next.run.sequence ? previous : next); }
    catch (error) { if (currentRun.current === approvedId) setError(errorMessage(error)); } finally { if (currentRun.current === approvedId) setBusy(false); }
  }
  const startToast = startNotice && (!view || !terminalLive(view.run.status)) && host ? createPortal(<aside className="nova-start-toast" data-testid="nova-start-toast" role="status" aria-live="polite" aria-atomic="true"><NovaLogo state="thinking" /><div><strong>NOVA en marcha</strong><p>Estoy preparando tu solicitud.</p></div><button type="button" className="icon-button" aria-label="Cerrar aviso de NOVA" onClick={() => setStartNotice(false)}><AppIcon glyph={X} size={20} aria-hidden="true"  /></button></aside>, host.hasAttribute('data-nova-dock-live-host') ? document.body : host) : null;
  if (!runId || !view || view.run.runId !== runId) return <>{startToast}{error && <p role="alert" className="error">No se pudo consultar la actividad de NOVA. Vuelve a intentarlo.</p>}</>;
  const run = view.run, active = !terminalLive(run.status), recent = events.at(-1);
  if (!active && !expanded) return <>{startToast}</>;
  const awaitingReview = run.status === 'AWAITING_APPROVAL';
  const savingCopy = view.proposal?.promptId == null;
  const paused = run.pauseRequested || run.status === 'PAUSED';
  const openApprovalReview = () => { setExpanded(false); setReview(true); };
  const reviewAction = active && view.proposal && <button type="button" className="primary" disabled={busy || paused || run.approval !== 'PENDING'} onClick={openApprovalReview}>{savingCopy ? 'Revisar y guardar copia' : 'Revisar y guardar versión'}</button>;
  const operation = recent?.toolName ? operationLabels[recent.toolName] : undefined;
  const outcomes: Record<string, string> = { COMPLETED: 'Tarea completada y comprobada', CANCELLED: 'Ejecución detenida', FAILED: 'No se pudo completar', BLOCKED: 'Necesito tu atención' };
  const status = !active ? outcomes[run.status] : paused ? 'En pausa' : awaitingReview ? 'Borrador listo para revisar' :
    (recent && stageLabels[recent.stage]) || operation || (run.status === 'INTERPRETING' ? 'Entendiendo tu petición' : 'Preparando tu solicitud');
  function closeActivity() {
    setExpanded(false);
    if (active) { trigger.current?.focus(); requestAnimationFrame(() => trigger.current?.focus()); }
    else document.querySelector<HTMLButtonElement>('[data-nova-history]')?.focus();
  }
  const runtimeControls = active && <><div className="live-controls">
          {(run.mode !== 'SAME_TAB' || worker || surfaceCapability(runId)) && <button className="secondary" disabled={busy} onClick={() => void control(paused ? 'resume' : 'pause')}>{paused ? <AppIcon glyph={Play} size={16} aria-hidden="true"  /> : <AppIcon glyph={Pause} size={16} aria-hidden="true"  />}{paused ? 'Continuar' : 'Pausar'}</button>}
          <button className="secondary" disabled={busy} onClick={() => void control('cancel')}><AppIcon glyph={Square} size={16} aria-hidden="true"  />Detener NOVA</button>
        </div>
        {active && view.proposal && <div className="live-review-actions">{reviewAction}<button className="text-button" disabled={busy} onClick={() => void control('cancel')}>Cancelar modificación</button></div>}
      </>;
  // A transformed/scrollable dock clips viewport-sized content. Keep the compact
  // control in its dock, but anchor expanded runtime to the document. Dialog
  // hosts remain inside their existing accessible focus scope.
  const renderHost = host?.hasAttribute('data-nova-dock-live-host') && (!assistantVisible || detailsVisible && !!view.runtime) ? document.body : host;
  return <>
    {startToast}
    {renderHost && createPortal(<aside className="nova-live" ref={floating} data-testid="nova-live" data-run-id={runId} data-status={run.status} data-approval={run.approval} data-worker={worker} onKeyDown={event => {
      if (event.key === 'Escape' && expanded) { event.preventDefault(); event.stopPropagation(); closeActivity(); }
    }}>
      {detailsVisible && <section className="nova-live-popover" id={panelId} aria-labelledby="nova-live-heading">
        <header className="nova-live-heading"><div><p className="eyebrow">NOVA · {active ? 'EN VIVO' : 'ÚLTIMA ACTIVIDAD'}{step > 0 ? ` · Paso ${step}` : ''}</p><h2 id="nova-live-heading">{status}</h2></div><button className="icon-button" aria-label="Cerrar actividad" onClick={closeActivity}><AppIcon glyph={X} size={20} aria-hidden="true"  /></button></header>
        {view.observation?.title && <p className="live-subject">{view.observation.title}</p>}
        {active && (notice || connection) && <p className="notice">{notice || connection}</p>}
        {view.question && <p className="notice">{view.question}</p>}
        <AgentRuntimePanel view={view} status={status} disconnected={!!connection} controls={runtimeControls} />
        <ActivityLog view={view} events={events} actions={actions} />

        {active && worker && run.approval === 'APPROVED' && <button data-nova-control="approval.apply" className="primary" onClick={() => { void onCommit().catch(error => setError(errorMessage(error))); }}>Aplicar versión aprobada</button>}
        {run.mode === 'BROWSER' && view.observation?.promptId && <button className="text-button" onClick={() => onReview(view)}>Ver el resultado</button>}
        {error && <p className="error" role="alert">No se pudo completar esa acción. Revisa el borrador y vuelve a intentarlo.</p>}
      </section>}
      <button ref={trigger} type="button" className="nova-live-trigger" aria-label={active ? 'Actividad de NOVA' : 'Última actividad de NOVA'} aria-expanded={detailsVisible} aria-controls={detailsVisible ? panelId : undefined} onClick={() => { if (!assistantVisible && host?.hasAttribute('data-nova-dock-live-host')) window.dispatchEvent(new CustomEvent('nova-open-assistant', { detail: { focus: false } })); setExpanded(!detailsVisible); }}>
        {!active ? (run.status === 'COMPLETED' ? <AppIcon glyph={Check} size={20} aria-hidden="true"  /> : run.status === 'CANCELLED' ? <AppIcon glyph={Square} size={20} aria-hidden="true"  /> : <AppIcon glyph={CircleAlert} size={20} aria-hidden="true"  />) : paused ? <AppIcon glyph={Pause} size={20} aria-hidden="true"  /> : awaitingReview ? <AppIcon glyph={Check} size={20} aria-hidden="true"  /> : <NovaLogo state={visual} />}
        <span><strong>NOVA</strong><span>{status}</span></span><AppIcon glyph={ChevronDown} size={16} className={expanded ? 'nova-activity-expanded' : ''} aria-hidden="true"  />
      </button>
      <span className="sr-only" role="status" aria-atomic="true">NOVA: {status}</span>
      {active && !detailsVisible && <div className="nova-runtime-quick">{awaitingReview && reviewAction}<button type="button" className="secondary" disabled={busy} onClick={() => void control('cancel')}><AppIcon glyph={Square} size={16} aria-hidden="true" />Detener NOVA</button></div>}
    </aside>, renderHost)}
    <Dialog className="nova-approval-dialog" open={review && !!view.proposal} onOpenChange={setReview} title={savingCopy ? 'Guardar copia separada' : 'Confirmar nueva versión'} description={savingCopy ? 'Se creará un prompt separado. El original conservará su contenido y versión.' : 'Se guardará una nueva versión del mismo prompt. Se conservarán las versiones anteriores.'}>
      {view.proposal && <div data-nova-approval="true"><p><strong>{view.proposal.draft.title}</strong>{view.proposal.revision ? ` · versión ${view.proposal.revision}` : ''}</p>{view.proposal.originalText && <details><summary>Ver original</summary><pre>{view.proposal.originalText}</pre></details>}<h3>Propuesta que vas a guardar</h3><pre className="prepared-text">{view.proposal.draft.templateText}</pre><div className="dialog-actions"><button className="primary" disabled={busy || paused || run.approval !== 'PENDING'} onClick={() => void approve()}>{savingCopy ? 'Confirmar guardado de esta copia' : 'Confirmar guardado de esta versión'}</button><button className="secondary" disabled={busy} onClick={() => setReview(false)}>Seguir revisando</button><button className="text-button" disabled={busy} onClick={() => { setReview(false); void control('cancel'); }}>Rechazar propuesta</button></div></div>}
      {error && <p role="alert" className="error">No se pudo confirmar el guardado. Revisa el borrador y vuelve a intentarlo.</p>}
    </Dialog>
  </>;
}
