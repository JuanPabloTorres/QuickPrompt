import { useAgentRuntimeServices } from '../../components/ServiceContext';
import { AppIcon } from '../../components/ui/Icon';
import { useEffect, useId, useRef, useState, useSyncExternalStore } from 'react';
import { createPortal } from 'react-dom';
import { visualState } from '../../design/nova-state';
import { NovaLogo } from '../../design/NovaLogo';
import { Check, ChevronDown, CircleAlert, Pause, Play, Square, X } from 'lucide-react';
import { ActivityLog } from './ActivityLog';
import { AgentRuntimePanel } from './AgentRuntimePanel';
import { useAgentOverlayHost } from './useAgentOverlayHost';
import '../../design/runtime.css';
import { getActivity, operationLabels, stageLabels, subscribeActivity } from './activity';
import { Dialog } from '../../components/ui/Dialog';
import { errorMessage } from '../../services/api';
import { liveEventSchema, terminalLive, type LiveEvent, type LiveView, type LiveEvents, type AuthoringBriefReply } from '../../services/live';
import { PromptInputQuestion } from '../prompts/PromptInputNames';
import { PromptContentQuestion } from '../prompts/PromptContentQuestion';
import { ContentReview } from './ContentReview';
import { PromptAuthoringBrief } from '../prompts/PromptAuthoringBrief';

const noActivitySubscription = () => () => {};
type Proposal = NonNullable<LiveView['proposal']>;

function sameProposal(current: Proposal | null | undefined, reviewed: Proposal | null) {
  const currentChanges = current?.contentReview?.literalChanges.entries ?? [];
  const reviewedChanges = reviewed?.contentReview?.literalChanges.entries ?? [];
  return !!current && !!reviewed && current.id === reviewed.id && current.hash === reviewed.hash && current.promptId === reviewed.promptId && current.revision === reviewed.revision
    && current.purpose === reviewed.purpose
    && current.draft.title === reviewed.draft.title && current.draft.templateText === reviewed.draft.templateText && current.draft.description === reviewed.draft.description && current.draft.categoryId === reviewed.draft.categoryId
    && current.contentReview?.contractHash === reviewed.contentReview?.contractHash && current.contentReview?.bodyHash === reviewed.contentReview?.bodyHash
    && current.contentReview?.source.promptId === reviewed.contentReview?.source.promptId && current.contentReview?.source.revision === reviewed.contentReview?.source.revision
    && current.contentReview?.source.bodyHash === reviewed.contentReview?.source.bodyHash && current.contentReview?.source.originalText === reviewed.contentReview?.source.originalText
    && current.contentReview?.source.sourceTitle === reviewed.contentReview?.source.sourceTitle && current.contentReview?.source.sourceDescription === reviewed.contentReview?.source.sourceDescription
    && current.contentReview?.source.sourceCategoryId === reviewed.contentReview?.source.sourceCategoryId
    && current.contentReview?.source.savedBodyHash === reviewed.contentReview?.source.savedBodyHash && current.contentReview?.source.workingDraftId === reviewed.contentReview?.source.workingDraftId
    && current.contentReview?.source.savedOriginalText === reviewed.contentReview?.source.savedOriginalText
    && current.contentReview?.source.workingBodyHash === reviewed.contentReview?.source.workingBodyHash
    && currentChanges.length === reviewedChanges.length && currentChanges.every((entry, index) => entry.id === reviewedChanges[index].id && entry.kind === reviewedChanges[index].kind
      && entry.originalText === reviewedChanges[index].originalText && entry.proposedText === reviewedChanges[index].proposedText);
}

export type NovaRuntimeSnapshot = { view: LiveView; status?: string; disconnected: boolean; stop: () => Promise<void>; review?: () => Promise<void>; busy?: boolean };

function observedStatus(view: LiveView, recent?: LiveEvent) {
  const run = view.run;
  const outcomes: Record<string, string> = { COMPLETED: 'Tarea completada y comprobada', CANCELLED: 'Ejecución detenida', FAILED: 'No se pudo completar', BLOCKED: 'Necesito tu atención' };
  return terminalLive(run.status) ? outcomes[run.status] : run.pauseRequested || run.status === 'PAUSED' ? 'En pausa' : run.status === 'AWAITING_APPROVAL' ? 'Borrador listo para revisar' :
    (recent && stageLabels[recent.stage]) || (recent?.toolName && operationLabels[recent.toolName]) || (run.status === 'INTERPRETING' ? 'Entendiendo tu petición' : 'Preparando tu solicitud');
}

export function NovaLive({ runId, worker, onReview, onPrepareReview, onChooseSource, onApprovalChange, onRuntimeChange, centralControl = false, notice = '', inputDefinition, contentClarification }: {
  runId: string | null; worker: boolean; onReview: (view: LiveView) => void;
  onPrepareReview?: () => Promise<void>; onChooseSource?: (source: { id: string; revision: number; title: string }) => Promise<void>;
  onApprovalChange?: (approvedRunId: string | null) => void;
  onRuntimeChange?: (snapshot: NovaRuntimeSnapshot | null) => void;
  centralControl?: boolean;
  notice?: string;
  inputDefinition?: { updating: boolean; confirm: (names: string[]) => Promise<void> };
  contentClarification?: { identity: object; confirm: (answer: string, sequence: number, brief?: AuthoringBriefReply) => Promise<void> };
}) {
  const { live, visibleSession } = useAgentRuntimeServices();
  const { surfaceCapability } = visibleSession;
  const [view, setView] = useState<LiveView | null>(null), [events, setEvents] = useState<LiveEvent[]>([]);
  const [step, setStep] = useState(0), [expanded, setExpanded] = useState(false);
  const [connection, setConnection] = useState('');
  const currentRun = useRef(runId); currentRun.current = runId;
  useEffect(() => {
    onApprovalChange?.(worker && view?.run.runId === runId && view.run.approval === 'APPROVED' && view.proposal?.purpose !== 'CONTENT_ONLY' && !terminalLive(view.run.status) ? runId : null);
  }, [runId, worker, view?.run.approval, view?.run.status, view?.proposal?.purpose, onApprovalChange]);
  const [assistantVisible, setAssistantVisible] = useState(false);
  const host = useAgentOverlayHost(true), panelId = useId();
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
  const [reviewedProposal, setReviewedProposal] = useState<Proposal | null>(null);
  const reviewedSnapshot = useRef<Proposal | null>(null);
  const [approvedLiteralChangeIds, setApprovedLiteralChangeIds] = useState<string[]>([]);
  function captureReview(proposal: Proposal | null) {
    reviewedSnapshot.current = proposal; setReviewedProposal(proposal); setApprovedLiteralChangeIds(previous => previous.length ? [] : previous);
  }
  function invalidateChangedReview(next: LiveView) {
    if (reviewedSnapshot.current && !sameProposal(next.proposal, reviewedSnapshot.current)) setApprovedLiteralChangeIds(previous => previous.length ? [] : previous);
  }
  useEffect(() => {
    if (!review) captureReview(null);
    else if (!reviewedProposal && view?.proposal) captureReview(view.proposal);
  }, [review, reviewedProposal, view?.proposal]);
  const last = useRef(0), generation = useRef(0);
  useEffect(() => {
    if (!runId) return;
    const current = ++generation.current; const controller = new AbortController(); last.current = 0; setEvents([]); setStep(0); setView(null); setReview(false); captureReview(null); setExpanded(false); setBusy(false); setError(''); setConnection('');
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
            invalidateChangedReview(next);
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
    const openReview = () => { window.dispatchEvent(new CustomEvent('nova-open-assistant', { detail: { focus: false } })); setExpanded(false); captureReview(null); void refresh().then(() => setReview(true)); }; window.addEventListener('nova-review-approval', openReview);
    return () => { controller.abort(); clearTimeout(refreshTimer); stream.close(); window.removeEventListener('nova-review-approval', openReview); };
  }, [runId, live]);
  async function control(action: 'pause' | 'resume' | 'cancel') {
    if (!runId) return; const controlledId = runId; if (action !== 'resume') window.dispatchEvent(new CustomEvent('nova-ui-control', { detail: action })); setBusy(true); setError('');
    try { const controlled = await live.control(controlledId, action); if (currentRun.current !== controlledId) return; if (action === 'resume' && !controlled.pauseRequested && !terminalLive(controlled.status)) window.dispatchEvent(new CustomEvent('nova-ui-control', { detail: 'resume' })); const next = await live.get(controlledId); if (currentRun.current === controlledId && next.run.runId === controlledId) { invalidateChangedReview(next); setView(previous => previous?.run.runId === controlledId && previous.run.sequence > next.run.sequence ? previous : next); } }
    catch (error) { if (currentRun.current === controlledId) setError(errorMessage(error)); } finally { if (currentRun.current === controlledId) setBusy(false); }
  }
  const controlRef = useRef(control); controlRef.current = control;
  async function openApprovalReview() {
    if (busy || !runId) return;
    const reviewedId = runId; setBusy(true); setError('');
    try {
      await onPrepareReview?.();
      const next = await live.get(reviewedId);
      if (currentRun.current !== reviewedId) return;
      setView(next); captureReview(next.proposal); setExpanded(false); setReview(true);
    } catch (error) { if (currentRun.current === reviewedId) setError(errorMessage(error)); }
    finally { if (currentRun.current === reviewedId) setBusy(false); }
  }
  const reviewRef = useRef(openApprovalReview); reviewRef.current = openApprovalReview;
  useEffect(() => {
    const stop = (event: Event) => { if (runId && (event as CustomEvent<string>).detail === runId) void controlRef.current('cancel'); };
    window.addEventListener('nova-stop-request', stop);
    return () => window.removeEventListener('nova-stop-request', stop);
  }, [runId]);
  const publishedStatus = view ? observedStatus(view, events.at(-1)) : '';
  useEffect(() => {
    if (!runId || view?.run.runId !== runId) { onRuntimeChange?.(null); return; }
    const publishedId = runId;
    onRuntimeChange?.({ view, status: publishedStatus, disconnected: !!connection, busy, review: view.proposal && view.run.status === 'AWAITING_APPROVAL' ? async () => {
      if (currentRun.current === publishedId) await reviewRef.current();
    } : undefined, stop: async () => {
      if (currentRun.current === publishedId) await controlRef.current('cancel');
    } });
  }, [runId, view, publishedStatus, connection, busy, onRuntimeChange]);
  async function approve() {
    if (busy || !review || !runId || !view || view.run.approval !== 'PENDING' || view.run.pauseRequested || view.run.status === 'PAUSED' || !sameProposal(view.proposal, reviewedProposal) || !reviewedProposal
      || reviewedProposal.purpose === 'CONTENT_ONLY' && !reviewedProposal.contentReview) return;
    const entries = reviewedProposal.contentReview?.literalChanges.entries ?? [];
    if (!entries.every(entry => approvedLiteralChangeIds.includes(entry.id))) return;
    const approvedId = runId; setBusy(true); setError('');
    if (view.run.mode === 'SAME_TAB' && !surfaceCapability(runId)) { setBusy(false); setError('Esta pestaña no conserva el control de la ejecución.'); return; }
    try { const p = reviewedProposal; await live.approve(approvedId, { approvalId: p.id, hash: p.hash, revision: p.revision, confirmed: true,
      ...(p.contentReview ? { contentReviewed: true, contentReviewHash: p.contentReview.contractHash,
        approvedLiteralChangeIds: entries.filter(entry => approvedLiteralChangeIds.includes(entry.id)).map(entry => entry.id) } : {}) }); if (currentRun.current !== approvedId) return; setReview(false); const next = await live.get(approvedId); if (currentRun.current === approvedId && next.run.runId === approvedId) setView(previous => previous?.run.runId === approvedId && previous.run.sequence > next.run.sequence ? previous : next); }
    catch (error) { if (currentRun.current === approvedId) setError(errorMessage(error)); } finally { if (currentRun.current === approvedId) setBusy(false); }
  }
  const startToast = startNotice && (!view || !terminalLive(view.run.status)) && host ? createPortal(<aside className="nova-start-toast" data-testid="nova-start-toast" role="status" aria-live="polite" aria-atomic="true"><NovaLogo state="thinking" /><div><strong>NOVA en marcha</strong><p>Estoy preparando tu solicitud.</p></div><button type="button" className="icon-button" aria-label="Cerrar aviso de NOVA" onClick={() => setStartNotice(false)}><AppIcon glyph={X} size={20} aria-hidden="true"  /></button></aside>, host.hasAttribute('data-nova-dock-live-host') ? document.body : host) : null;
  if (!runId || !view || view.run.runId !== runId) return <>{startToast}{error && <p role="alert" className="error">No se pudo consultar la actividad de NOVA. Vuelve a intentarlo.</p>}</>;
  const run = view.run, active = !terminalLive(run.status);
  // Continuations create a new run. Their workspace owner must adopt that ID
  // and mount its driver; this presentation cannot start a detached run.
  const continuation = contentClarification;
  const fieldQuestion = run.status === 'BLOCKED' && run.errorCode === 'INPUT_FIELDS_NEED_CONFIRMATION' && !run.browserOpen && inputDefinition && view.question
    ? <PromptInputQuestion key={run.runId} question={view.question} updating={inputDefinition.updating} onConfirm={inputDefinition.confirm} /> : null;
  const contentQuestion = run.status === 'BLOCKED' && run.errorCode === 'NEEDS_CLARIFICATION' && !run.browserOpen && run.approval === 'NONE'
    && !run.effectCommitted && continuation && visibleSession.clarificationCapability?.(run.runId) && view.question
    ? view.brief ? <PromptAuthoringBrief key={run.runId} runId={run.runId} sequence={run.sequence} review={view.brief} identity={continuation.identity}
      onConfirm={brief => continuation.confirm('', run.sequence, brief)} onClarify={answer => continuation.confirm(answer, run.sequence)} />
      : <PromptContentQuestion key={run.runId} runId={run.runId} sequence={run.sequence} question={view.question} identity={continuation.identity} onConfirm={continuation.confirm} /> : null;
  const providerFailure = run.status === 'FAILED' && host?.querySelector('[data-testid="prepared-provider-dialog"]')
    ? createPortal(<p className="error" role="alert">NOVA no pudo completar la tarea. El texto preparado se conserva; revisa el destino o abre la última actividad para ver el error.</p>, host) : null;
  if (!active && !expanded) return <>{startToast}{providerFailure}{fieldQuestion}{contentQuestion}</>;
  const awaitingReview = run.status === 'AWAITING_APPROVAL';
  const canControl = run.mode !== 'SAME_TAB' || !!surfaceCapability(runId);
  const savingCopy = view.proposal?.promptId == null;
  const paused = run.pauseRequested || run.status === 'PAUSED';
  const contentOnly = reviewedProposal?.purpose === 'CONTENT_ONLY';
  const contentReviewReady = !contentOnly || !!reviewedProposal?.contentReview;
  const reviewCurrent = sameProposal(view.proposal, reviewedProposal);
  const literalChangesApproved = (reviewedProposal?.contentReview?.literalChanges.entries ?? []).every(entry => approvedLiteralChangeIds.includes(entry.id));
  function approveLiteralChange(id: string, approved: boolean) {
    if (!reviewCurrent || !reviewedProposal?.contentReview?.literalChanges.entries.some(entry => entry.id === id)) return;
    setApprovedLiteralChangeIds(previous => approved ? [...previous.filter(value => value !== id), id] : previous.filter(value => value !== id));
  }
  const reviewAction = active && canControl && view.proposal && <button type="button" className="primary" disabled={busy || paused || !canControl || run.approval !== 'PENDING'} onClick={openApprovalReview}>{view.proposal.purpose === 'CONTENT_ONLY' ? 'Revisar contenido' : savingCopy ? 'Revisar y guardar copia' : 'Revisar y guardar versión'}</button>;
  const status = publishedStatus;
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
    {providerFailure}
    {fieldQuestion}
    {contentQuestion}
    {renderHost && createPortal(<aside className="nova-live" data-nova-user-control="true" ref={floating} data-testid="nova-live" data-run-id={runId} data-status={run.status} data-approval={run.approval} data-worker={worker} onKeyDown={event => {
      if (event.key === 'Escape' && expanded) { event.preventDefault(); event.stopPropagation(); closeActivity(); }
    }}>
      {detailsVisible && <section className="nova-live-popover" id={panelId} aria-labelledby="nova-live-heading">
        <header className="nova-live-heading"><div><p className="eyebrow">NOVA · {active ? 'EN VIVO' : 'ÚLTIMA ACTIVIDAD'}{step > 0 ? ` · Paso ${step}` : ''}</p><h2 id="nova-live-heading">{status}</h2></div><button className="icon-button" aria-label="Cerrar actividad" onClick={closeActivity}><AppIcon glyph={X} size={20} aria-hidden="true"  /></button></header>
        {view.observation?.title && <p className="live-subject">{view.observation.title}</p>}
        {active && (notice || connection) && <p className="notice">{notice || connection}</p>}
        {view.question && <p className="notice">{view.question}</p>}
        {run.status === 'BLOCKED' && onChooseSource && !!view.sourceChoices?.length && <div role="group" aria-label="Elige un prompt para abrir"><p>Elige el prompt que quieres abrir. No se modificará ni ejecutará.</p>{view.sourceChoices.map(source => <button key={source.id} className="secondary candidate-choice" disabled={busy} onClick={async () => { setBusy(true); setError(''); try { await onChooseSource(source); } catch (error) { setError(errorMessage(error)); } finally { setBusy(false); } }}>{source.title}</button>)}</div>}
        <AgentRuntimePanel view={view} status={status} disconnected={!!connection} controls={runtimeControls} />
        <ActivityLog view={view} events={events} actions={actions} />

        {run.mode === 'BROWSER' && view.observation?.promptId && <button className="text-button" onClick={() => onReview(view)}>Ver el resultado</button>}
        {error && <p className="error" role="alert">No se pudo completar esa acción. Revisa el borrador y vuelve a intentarlo.</p>}
      </section>}
      {!centralControl && <button ref={trigger} type="button" className="nova-live-trigger" aria-label={active ? 'Actividad de NOVA' : 'Última actividad de NOVA'} aria-expanded={detailsVisible} aria-controls={detailsVisible ? panelId : undefined} onClick={() => { if (!assistantVisible && host?.hasAttribute('data-nova-dock-live-host')) window.dispatchEvent(new CustomEvent('nova-open-assistant', { detail: { focus: false } })); setExpanded(!detailsVisible); }}>
        {!active ? (run.status === 'COMPLETED' ? <AppIcon glyph={Check} size={20} aria-hidden="true"  /> : run.status === 'CANCELLED' ? <AppIcon glyph={Square} size={20} aria-hidden="true"  /> : <AppIcon glyph={CircleAlert} size={20} aria-hidden="true"  />) : paused ? <AppIcon glyph={Pause} size={20} aria-hidden="true"  /> : awaitingReview ? <AppIcon glyph={Check} size={20} aria-hidden="true"  /> : <NovaLogo state={visual} />}
        <span><strong>NOVA</strong><span>{status}</span></span><AppIcon glyph={ChevronDown} size={16} className={expanded ? 'nova-activity-expanded' : ''} aria-hidden="true"  />
      </button>}
      <span className="sr-only" role="status" aria-atomic="true">NOVA: {status}</span>
      {active && !detailsVisible && !centralControl && <div className="nova-runtime-quick">{awaitingReview && reviewAction}<button type="button" className="secondary" disabled={busy} onClick={() => void control('cancel')}><AppIcon glyph={Square} size={16} aria-hidden="true" />Detener NOVA</button></div>}
    </aside>, renderHost)}
    <Dialog className="nova-approval-dialog" open={review && !!reviewedProposal} onOpenChange={setReview} title={contentOnly ? 'Revisar contenido' : reviewedProposal?.promptId == null ? 'Guardar copia separada' : 'Confirmar nueva versión'} description={contentOnly ? 'Revisa la propuesta antes de continuar con la acción solicitada.' : reviewedProposal?.promptId == null ? 'Se creará un prompt separado. El original conservará su contenido y versión.' : 'Se guardará una nueva versión del mismo prompt. Se conservarán las versiones anteriores.'}>
      {reviewedProposal && <div data-nova-approval="true">
        <p><strong>{reviewedProposal.draft.title}</strong>{reviewedProposal.revision ? ` · versión ${reviewedProposal.revision}` : ''}</p>
        <h3>{contentOnly ? 'Descripción de la propuesta' : 'Descripción que se guardará'}</h3><p className="content-review-text">{reviewedProposal.draft.description || 'Sin descripción.'}</p>
        {reviewedProposal.contentReview ? <ContentReview review={reviewedProposal.contentReview} purpose={reviewedProposal.purpose} approvedLiteralChangeIds={approvedLiteralChangeIds} onApproveLiteralChange={approveLiteralChange}
          disabled={busy || paused || !canControl || run.approval !== 'PENDING' || !reviewCurrent} /> : reviewedProposal.originalText && <details><summary>Ver original</summary><pre>{reviewedProposal.originalText}</pre></details>}
        <h3>{contentOnly ? 'Propuesta para continuar' : 'Propuesta que vas a guardar'}</h3><pre className="prepared-text">{reviewedProposal.draft.templateText}</pre>
        {!contentReviewReady && <p role="alert" className="error">La propuesta no incluye el material de revisión. Vuelve a abrirla antes de continuar.</p>}
        {!sameProposal(view.proposal, reviewedProposal) && <div role="alert"><p>La propuesta cambió mientras la revisabas. Revisa la versión vigente antes de confirmar.</p><button className="secondary" disabled={busy} onClick={() => void openApprovalReview()}>Volver a revisar propuesta</button></div>}
        <div className="dialog-actions"><button className="primary" disabled={busy || paused || !canControl || run.approval !== 'PENDING' || !reviewCurrent || !contentReviewReady || !literalChangesApproved} onClick={() => void approve()}>{contentOnly ? 'Aprobar contenido y continuar' : reviewedProposal.contentReview ? reviewedProposal.promptId == null ? 'Aprobar contenido y guardar esta copia' : 'Aprobar contenido y guardar esta versión' : reviewedProposal.promptId == null ? 'Confirmar guardado de esta copia' : 'Confirmar guardado de esta versión'}</button><button className="secondary" disabled={busy} onClick={() => setReview(false)}>Seguir revisando</button><button className="text-button" disabled={busy} onClick={() => { setReview(false); void control('cancel'); }}>Rechazar propuesta</button></div>
      </div>}
      {error && <p role="alert" className="error">No se pudo confirmar el guardado. Revisa el borrador y vuelve a intentarlo.</p>}
    </Dialog>
  </>;
}
