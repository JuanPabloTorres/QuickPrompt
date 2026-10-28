import { useAgentRuntimeServices } from '../../components/ServiceContext';
import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { useUiPortalScope } from '../../components/ui/UiPortalScope';
import { terminalLive, type LiveUiReceipt } from '../../services/live';
import { AgentUiDriver, readUiState, type CursorTarget, type UiAction, type UiState } from './AgentUiDriver';
import { createAgentProposalSession } from './AgentProposalSession';
import { useAgentProposalSession } from './AgentUiContext';
import { beginActivity, confirmActivity, progressActivity } from './activity';
import { NovaAgentCursor } from './NovaAgentCursor';

export function SameTabAgent({ runId, onActive, speed = 'fast', onNotice }: { runId: string | null; onActive: (active: boolean) => void; speed?: 'normal' | 'fast'; onNotice?: (notice: string) => void }) {
  const { live, visibleSession } = useAgentRuntimeServices();
  const inheritedSession = useAgentProposalSession();
  const [standaloneSession] = useState(createAgentProposalSession);
  const proposalSession = inheritedSession ?? standaloneSession;
  const { releaseSurface, surfaceCapability, finishSurface } = visibleSession;
  const [target, setTarget] = useState<CursorTarget | null>(null);
  const [notice, setNotice] = useState('');
  useEffect(() => { onNotice?.(notice); }, [notice, onNotice]);
  const onActiveRef = useRef(onActive); onActiveRef.current = onActive;
  const driverRef = useRef<AgentUiDriver | null>(null);
  const speedRef = useRef(speed); speedRef.current = speed;
  const scopeActive = useUiPortalScope()?.active !== false;
  const scopeActiveRef = useRef(scopeActive); scopeActiveRef.current = scopeActive;
  const refreshPauseRef = useRef<(() => void) | null>(null);
  useLayoutEffect(() => { refreshPauseRef.current?.(); }, [scopeActive]);
  useEffect(() => { if (driverRef.current) driverRef.current.speed = speed; }, [speed]);
  useEffect(() => {
    setNotice('');
    if (!runId) return;
    const capability = surfaceCapability(runId); if (!capability) {
      const metadata = new AbortController();
      void live.record(runId, metadata.signal).then(({ run }) => { if (!metadata.signal.aborted && run.mode === 'SAME_TAB' && !terminalLive(run.status)) setNotice('Se recargó la pestaña y su conexión anterior está en pausa. Detén NOVA, revisa el borrador e inicia una nueva petición; las acciones pendientes no se repetirán.'); }).catch(() => {}); return () => metadata.abort();
    }
    const lifecycle = new AbortController();
    let currentAction: UiAction | null = null;
    let progressRequest: AbortController | null = null;
    const driver = new AgentUiDriver(setTarget, executionStatus => {
      if (currentAction) {
        progressActivity(currentAction, executionStatus === 'TARGET_LOCATED' ? 'target' : 'checking');
        // Progress is best-effort telemetry. Neither its latency nor its failure
        // may block DOM work or the authoritative, retriable receipt.
        if (!progressRequest) {
          const pending = new AbortController(); progressRequest = pending;
          const timer = setTimeout(() => pending.abort(), 750);
          void live.progress(runId, capability, { actionId: currentAction.actionId, sequence: currentAction.sequence, executionStatus }, pending.signal)
            .catch(() => {}).finally(() => { clearTimeout(timer); if (progressRequest === pending) progressRequest = null; });
        }
      }
    }); driverRef.current = driver; driver.speed = speedRef.current; const releaseProposal = proposalSession.register(driver);
    let disposed = false, executing = false, lastSequence = 0, active = true, connectionLost = false, localPauseRequested = false, serverPauseRequested = false;
    let wakePending = false, wakePoll: (() => void) | null = null;
    const wake = () => { if (wakePoll) wakePoll(); else wakePending = true; };
    const refreshPause = (notify = true) => {
      driver.paused = localPauseRequested || serverPauseRequested || connectionLost || !scopeActiveRef.current;
      if (notify) wake();
    };
    refreshPauseRef.current = refreshPause; refreshPause();
    const stateAvailable = (event: Event) => { if ((event as CustomEvent<string>).detail === runId) wake(); };
    const waitForState = () => new Promise<void>(resolve => {
      if (wakePending) { wakePending = false; resolve(); return; }
      const finish = () => { clearTimeout(timer); wakePoll = null; resolve(); };
      const timer = setTimeout(finish, executing ? 400 : 1000); wakePoll = finish;
    });
    // SSE only wakes the authenticated poll. It never supplies an executable action.
    // A bounded fallback still recovers lost/replayed/disconnected event streams.
    window.addEventListener('nova-ui-state-available', stateAvailable);
    const receipts = new Map<string, LiveUiReceipt>();
    const beforeStates = new Map<string, UiState>();
    setNotice(''); onActiveRef.current(true);
    const control = (event: Event) => {
      const action = (event as CustomEvent<string>).detail;
      if (action === 'cancel') {
        active = false; driver.stop(); lifecycle.abort(); progressRequest?.abort();
        releaseSurface(runId); releaseProposal(); onActiveRef.current(false);
      } else { localPauseRequested = action === 'pause'; refreshPause(); if (action === 'resume' && !driver.intervention) setNotice(''); }
      wake();
    };
    // Focus, selection, scrolling and tab visibility are not requests to pause.
    // Identity and actual value conflicts are still checked before writing.
    window.addEventListener('nova-ui-control', control);
    async function acknowledge(receipt: LiveUiReceipt) { await live.receipt(runId!, capability!, receipt); }
    async function execute(action: UiAction) {
      executing = true; currentAction = action;
      let receipt = receipts.get(action.actionId);
      if (!receipt) {
        if (action.sequence !== lastSequence + 1) { driver.stop(); throw new Error('UI_SEQUENCE_GAP'); }
        if (action.tool === 'commit_approved') window.dispatchEvent(new CustomEvent('nova-open-assistant', { detail: { focus: false } }));
        beginActivity(action); beforeStates.set(action.actionId, readUiState());
        try { const observation = await driver.execute(action); receipt = { actionId: action.actionId, sequence: action.sequence, executionStatus: 'EXECUTED', verificationStatus: 'VERIFIED', observation }; }
        catch (error) { receipt = { actionId: action.actionId, sequence: action.sequence, executionStatus: 'INTERRUPTED', verificationStatus: 'UNKNOWN', observation: null, errorCode: error instanceof Error ? error.message : 'UI_OUTCOME_UNKNOWN' }; }
        receipts.set(action.actionId, receipt); lastSequence = action.sequence;
      }
      // Retrying delivery retries this receipt, never the DOM action.
      while (!disposed && active && !scopeActiveRef.current) await new Promise(resolve => setTimeout(resolve, 40));
      if (disposed || !active || lifecycle.signal.aborted) { executing = false; return; }
      try {
        progressRequest?.abort(); progressRequest = null;
        await acknowledge(receipt);
        wake();
        if (!disposed) {
          const before = beforeStates.get(action.actionId);
          if (receipt.verificationStatus === 'VERIFIED' && receipt.observation && before) confirmActivity(action, before, receipt.observation);
          else progressActivity(action, 'interrupted', receipt.errorCode);
          if (active && receipt.verificationStatus === 'VERIFIED' && !driver.intervention) setNotice('');
        }
      }
      catch { if (!disposed && active) { progressActivity(action, 'confirming'); setNotice('Esperando confirmación de la conexión; las acciones no se repetirán.'); } }
      executing = false;
    }
    async function poll() {
      while (!disposed && active) {
        if (!scopeActiveRef.current) { await waitForState(); continue; }
        try {
          const data = await live.poll(runId!, capability!, document.visibilityState !== 'hidden', lifecycle.signal);
          if (disposed || !active) break;
          if (connectionLost) { connectionLost = false; if (!driver.intervention) setNotice(''); }
          serverPauseRequested = data.run.pauseRequested; refreshPause(false);
          if (terminalLive(data.run.status)) {
            active = false; driver.stop();
            if (finishSurface) finishSurface(runId!, data.run.status === 'BLOCKED' && data.run.errorCode === 'NEEDS_CLARIFICATION' && data.run.approval === 'NONE' && !data.run.effectCommitted);
            else releaseSurface(runId!);
            releaseProposal(); onActiveRef.current(false); break;
          }
          if (data.action && !driver.paused && !executing) void execute(data.action).catch(() => { driver.intervene(); setNotice('No se pudo confirmar el último paso. Detén NOVA y revisa el borrador.'); });
        } catch { if (disposed || !active) break; connectionLost = true; refreshPause(false); setNotice('Recuperando la conexión. NOVA continuará automáticamente sin repetir acciones.'); }
        await waitForState();
      }
    }
    void poll();
    return () => { disposed = true; active = false; wakePoll?.(); lifecycle.abort(); progressRequest?.abort(); driver.stop(); releaseProposal(); if (driverRef.current === driver) driverRef.current = null; if (refreshPauseRef.current === refreshPause) refreshPauseRef.current = null; onActiveRef.current(false); window.removeEventListener('nova-ui-control', control); window.removeEventListener('nova-ui-state-available', stateAvailable); };
  }, [runId, proposalSession, live, releaseSurface, surfaceCapability, finishSurface]);
  return <NovaAgentCursor target={target} />;
}
