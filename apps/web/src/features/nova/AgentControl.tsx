import { useEffect } from 'react';
import { createPortal } from 'react-dom';
import { useAgentOverlayHost } from './useAgentOverlayHost';
import type { NovaRuntimeSnapshot } from './NovaLive';

const controlScope = '[data-nova-user-control="true"], .nova-approval-dialog';

function allowedTarget(target: EventTarget | null, runId: string) {
  if (!(target instanceof Element)) return false;
  if (target.closest(controlScope)) return true;
  const deletion = target.closest('[data-testid="prompt-delete-review"]');
  return deletion?.getAttribute('data-delete-run-id') === runId;
}

/** Interaction arbitration only; the domain still authorizes every effect. */
export function blocksUserCommand(event: { isTrusted: boolean; target: EventTarget | null }, runId: string) {
  return event.isTrusted && !allowedTarget(event.target, runId);
}

export function AgentControl({ runId, runtime }: { runId: string | null; runtime: NovaRuntimeSnapshot | null }) {
  const host = useAgentOverlayHost();
  useEffect(() => {
    if (!runId) return;
    const guard = (event: Event) => {
      if (event instanceof KeyboardEvent && event.key === 'Tab' && event.isTrusted) {
        const scope = host ?? document.body;
        const candidates = [...scope.querySelectorAll<HTMLElement>('button, a[href], input, textarea, select, summary, [tabindex="0"]')]
          .filter(element => allowedTarget(element, runId) && !element.matches(':disabled') && element.getClientRects().length > 0);
        event.preventDefault(); event.stopImmediatePropagation();
        const current = candidates.indexOf(document.activeElement as HTMLElement);
        candidates[(current + (event.shiftKey ? -1 : 1) + candidates.length) % candidates.length]?.focus();
        return;
      }
      if (!blocksUserCommand(event, runId)) return;
      event.preventDefault(); event.stopImmediatePropagation();
    };
    // Native form submission may be trusted even when initiated by HTMLElement.click().
    // Arbitrate its human triggers (click/keydown), not the derived submit event.
    const types = ['pointerdown', 'mousedown', 'touchstart', 'click', 'dblclick', 'keydown', 'beforeinput', 'paste', 'cut', 'drop'];
    types.forEach(type => window.addEventListener(type, guard, { capture: true, passive: false }));
    return () => types.forEach(type => window.removeEventListener(type, guard, true));
  }, [runId, host]);
  if (!runId || !host) return null;
  const current = runtime?.view.run.runId === runId ? runtime : null;
  // The existing review dialog already presents the artifact and both decisions.
  // Do not cover it with a second fixed approval notice on compact screens.
  const reviewOpen = host.classList.contains('nova-approval-dialog') ||
    [...host.querySelectorAll<HTMLElement>('[data-testid="prompt-delete-review"]')].some(element => element.dataset.deleteRunId === runId);
  const paused = current?.view.run.pauseRequested || current?.view.run.status === 'PAUSED';
  const awaitingApproval = current?.view.run.status === 'AWAITING_APPROVAL' &&
    (current.view.run.approval === 'PENDING' && !!current.view.proposal || !!current.view.deletion);
  const reviewApproval = () => {
    if (current?.review) { void current.review(); return; }
    const deletion = [...document.querySelectorAll<HTMLElement>('[data-testid="prompt-delete-review"]')]
      .find(element => element.dataset.deleteRunId === runId);
    deletion?.querySelector<HTMLButtonElement>('button')?.focus();
  };
  const stop = () => {
    window.dispatchEvent(new CustomEvent('nova-ui-control', { detail: 'cancel' }));
    if (current) void current.stop();
    else window.dispatchEvent(new CustomEvent('nova-stop-request', { detail: runId }));
  };
  return <>
    {createPortal(<div className="nova-control-frame" data-testid="agent-control-frame" aria-hidden="true" />, document.body)}
    {!reviewOpen && createPortal(<aside className="nova-control-banner" data-awaiting-approval={!!awaitingApproval} data-nova-user-control="true" aria-label="Control del agente">
      {awaitingApproval ? <>
        <div role="alert" data-nova-approval-alert="true"><strong>NOVA necesita tu aprobación</strong><span>{current?.view.deletion ? 'Revisa el prompt antes de confirmar su eliminación.' : 'El borrador está listo. Revisa los cambios antes de guardarlos.'}</span></div>
        <div className="nova-approval-choices"><button type="button" className="primary" disabled={!!current?.busy || !!paused} onClick={reviewApproval}>{current?.view.deletion ? 'Revisar eliminación' : 'Revisar y aprobar'}</button><button type="button" className="secondary" onClick={stop}>No aprobar</button></div>
      </> : <>
      <div role="status"><strong>{current?.disconnected ? 'NOVA · conexión interrumpida' : paused ? 'NOVA tiene el control · en pausa' : 'NOVA tiene el control'}</strong><span>Los comandos están reservados al agente. Puedes detenerlo para continuar.</span></div>
      <button type="button" className="secondary" onClick={stop}>Detener agente</button>
      </>}
    </aside>, host)}
  </>;
}
