import { useEffect, useRef, useState } from 'react';
import { z } from 'zod';
import { emptySchema, post, request } from '../../services/api';
import { liveRecordSchema, releaseSurface, surfaceCapability, terminalLive } from '../../services/live';
import { AgentUiDriver, setAgentDriver, type CursorTarget, type UiAction, type UiState } from './AgentUiDriver';
import { NovaAgentCursor } from './NovaAgentCursor';

const actionSchema = z.object({ runId: z.string().uuid(), actionId: z.string().uuid(), sequence: z.number().int().positive(), tool: z.string(), target: z.string().uuid().nullable(), arguments: z.record(z.string(), z.unknown()), timestamp: z.string(), executionStatus: z.string(), verificationStatus: z.string() });
const pollSchema = z.object({ run: liveRecordSchema, action: actionSchema.nullable() });
type Receipt = { actionId: string; sequence: number; executionStatus: string; verificationStatus: string; observation: UiState | null; errorCode?: string };
const sleep = (ms: number) => new Promise<void>(resolve => setTimeout(resolve, ms));

export function SameTabAgent({ runId, onActive, speed = 'normal', onNotice }: { runId: string | null; onActive: (active: boolean) => void; speed?: 'normal' | 'fast'; onNotice?: (notice: string) => void }) {
  const [target, setTarget] = useState<CursorTarget | null>(null);
  const [notice, setNotice] = useState('');
  useEffect(() => { onNotice?.(notice); }, [notice, onNotice]);
  const onActiveRef = useRef(onActive); onActiveRef.current = onActive;
  const driverRef = useRef<AgentUiDriver | null>(null);
  const speedRef = useRef(speed); speedRef.current = speed;
  useEffect(() => { if (driverRef.current) driverRef.current.speed = speed; }, [speed]);
  useEffect(() => {
    setNotice('');
    if (!runId) return;
    const capability = surfaceCapability(runId); if (!capability) {
      const metadata = new AbortController();
      void request(`/nova/live/${runId}`, z.object({ run: liveRecordSchema }), { signal: metadata.signal }).then(({ run }) => { if (!metadata.signal.aborted && run.mode === 'SAME_TAB' && !terminalLive(run.status)) setNotice('Se recargó la pestaña y su conexión anterior está en pausa. Detén NOVA, revisa el borrador e inicia una nueva petición; las acciones pendientes no se repetirán.'); }).catch(() => {}); return () => metadata.abort();
    }
    const headers = { 'X-Nova-Surface': capability };
    const lifecycle = new AbortController();
    let currentAction: UiAction | null = null;
    const driver = new AgentUiDriver(setTarget, async executionStatus => {
      if (currentAction) await request(`/nova/live/${runId}/actions/progress`, emptySchema, { ...post({ actionId: currentAction.actionId, sequence: currentAction.sequence, executionStatus }), headers });
    }); driverRef.current = driver; driver.speed = speedRef.current; setAgentDriver(driver);
    let disposed = false, executing = false, lastSequence = 0, active = true, recoveryRequired = false, localPauseRequested = false;
    const receipts = new Map<string, Receipt>();
    setNotice(''); onActiveRef.current(true);
    const control = (event: Event) => {
      const action = (event as CustomEvent<string>).detail;
      if (action === 'cancel') driver.stop(); else { localPauseRequested = action === 'pause'; driver.paused = localPauseRequested; if (action === 'resume') { recoveryRequired = false; if (!driver.intervention) setNotice(''); } }
    };
    const intervention = (event: Event) => {
      if (!active || !event.isTrusted || (event.target instanceof Element && event.target.closest('.nova-live, .same-tab-agent, .nova-approval-dialog, [data-nova-approval], [data-nova-command]' ))) return;
      window.dispatchEvent(new CustomEvent('nova-human-intervention', { detail: { type: event.type, targetId: event.target instanceof Element ? event.target.id : '', tag: event.target instanceof Element ? event.target.tagName : '', isTrusted: event.isTrusted } }));
      driver.intervene(); setNotice('Has tomado el control. Se conserva tu edición; detén esta ejecución y revisa el borrador antes de solicitar otro cambio.');
      void request(`/nova/live/${runId}/pause`, liveRecordSchema, post({})).catch(() => {});
    };
    const visibility = () => { if (document.visibilityState === 'hidden') { localPauseRequested = true; driver.paused = true; setNotice('La pestaña está oculta. Al volver, revisa el borrador y pulsa Continuar.'); void request(`/nova/live/${runId}/pause`, liveRecordSchema, post({})).catch(() => {}); } };
    window.addEventListener('nova-ui-control', control);
    document.addEventListener('beforeinput', intervention, true); document.addEventListener('pointerdown', intervention, true); document.addEventListener('keydown', intervention, true); document.addEventListener('visibilitychange', visibility);
    async function acknowledge(receipt: Receipt) { await request(`/nova/live/${runId}/actions/receipt`, emptySchema, { ...post(receipt), headers }); }
    async function execute(action: UiAction) {
      executing = true; currentAction = action;
      let receipt = receipts.get(action.actionId);
      if (!receipt) {
        if (action.sequence !== lastSequence + 1) { driver.stop(); throw new Error('UI_SEQUENCE_GAP'); }
        try { const observation = await driver.execute(action); receipt = { actionId: action.actionId, sequence: action.sequence, executionStatus: 'EXECUTED', verificationStatus: 'VERIFIED', observation }; }
        catch (error) { receipt = { actionId: action.actionId, sequence: action.sequence, executionStatus: 'INTERRUPTED', verificationStatus: 'UNKNOWN', observation: null, errorCode: error instanceof Error ? error.message : 'UI_OUTCOME_UNKNOWN' }; }
        receipts.set(action.actionId, receipt); lastSequence = action.sequence;
      }
      // Retrying delivery retries this receipt, never the DOM action.
      try { await acknowledge(receipt); if (!disposed && active && receipt.verificationStatus === 'VERIFIED' && !driver.intervention) setNotice(''); }
      catch { if (!disposed && active) setNotice('Esperando confirmación de la conexión; las acciones no se repetirán.'); }
      executing = false;
    }
    async function poll() {
      while (!disposed && active) {
        try {
          const data = await request(`/nova/live/${runId}/actions?visible=${document.visibilityState !== 'hidden'}`, pollSchema, { headers, signal: lifecycle.signal });
          if (disposed) break;
          driver.paused = localPauseRequested || recoveryRequired || data.run.pauseRequested;
          if (recoveryRequired && !data.run.pauseRequested && !terminalLive(data.run.status)) { await request(`/nova/live/${runId}/pause`, liveRecordSchema, post({})); continue; }
          if (terminalLive(data.run.status)) { active = false; driver.stop(); releaseSurface(runId!); if (driverRef.current === driver) setAgentDriver(null); onActiveRef.current(false); break; }
          if (data.action && !driver.paused && !executing) void execute(data.action).catch(() => { driver.intervene(); setNotice('No se pudo confirmar el último paso. Detén NOVA y revisa el borrador.'); });
        } catch { if (disposed) break; recoveryRequired = true; driver.paused = true; setNotice('Conexión interrumpida. La escritura está en pausa; usa Continuar cuando se recupere.'); }
        await sleep(400);
      }
    }
    void poll();
    return () => { disposed = true; active = false; lifecycle.abort(); driver.stop(); if (driverRef.current === driver) { driverRef.current = null; setAgentDriver(null); } onActiveRef.current(false); window.removeEventListener('nova-ui-control', control); document.removeEventListener('beforeinput', intervention, true); document.removeEventListener('pointerdown', intervention, true); document.removeEventListener('keydown', intervention, true); document.removeEventListener('visibilitychange', visibility); };
  }, [runId]);
  return <NovaAgentCursor target={target} />;
}
