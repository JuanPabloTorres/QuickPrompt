import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { AgentControl, blocksUserCommand } from '../src/features/nova/AgentControl';
import { Dialog } from '../src/components/ui/Dialog';
import { useState } from 'react';
import { AgentUiDriver, readUiState } from '../src/features/nova/AgentUiDriver';

afterEach(() => { cleanup(); vi.restoreAllMocks(); });
it('opens activity from the compact control bar without issuing a task command', async () => {
  const open = vi.fn(); window.addEventListener('nova-open-activity', open);
  render(<AgentControl runId="run-a" runtime={null} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Ver actividad de NOVA' }));
  expect(open).toHaveBeenCalledOnce();
  expect(screen.getByRole('button', { name: 'Detener agente' })).toBeTruthy();
  window.removeEventListener('nova-open-activity', open);
});
it('distinguishes the code-owned approval alert from actual workspace errors', () => {
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([{}] as unknown as DOMRectList);
  const root = document.createElement('div'); document.body.append(root);
  root.innerHTML = '<div role="alert" data-nova-approval-alert="true">Necesito tu aprobación</div>';
  expect(readUiState().error).toBeNull();
  root.insertAdjacentHTML('beforeend', '<p role="alert">Error real</p>');
  expect(readUiState().error).toBe('REACT_REPORTED_ERROR');
  root.remove();
});
it('reserves human commands, preserving programmatic actions and only bound human decisions', () => {
  const root = document.createElement('div'); document.body.append(root);
  root.innerHTML = '<input id="workspace"><div data-nova-user-control="true"><button id="stop">Stop</button></div><div class="nova-approval-dialog"><button id="approve">Approve</button></div><div data-testid="prompt-delete-review" data-delete-run-id="run-a"><button id="delete">Delete</button></div>';
  const event = (id: string, isTrusted = true) => ({ target: root.querySelector('#' + id), isTrusted });
  expect(blocksUserCommand(event('workspace'), 'run-a')).toBe(true);
  expect(blocksUserCommand(event('workspace', false), 'run-a')).toBe(false);
  expect(blocksUserCommand(event('stop'), 'run-a')).toBe(false);
  expect(blocksUserCommand(event('approve'), 'run-a')).toBe(false);
  expect(blocksUserCommand(event('delete'), 'run-a')).toBe(false);
  expect(blocksUserCommand(event('delete'), 'run-b')).toBe(true);
  root.remove();
});
it('keeps Stop accessible inside the active dialog and releases the frame when ownership ends', async () => {
  const localStop = vi.fn(), remoteStop = vi.fn();
  window.addEventListener('nova-ui-control', localStop);
  window.addEventListener('nova-stop-request', remoteStop);
  const ui = (runId: string | null) => <><AgentControl runId={runId} runtime={null} /><Dialog open onOpenChange={vi.fn()} title="Destino" description="Revisa el destino"><input aria-label="Contenido" /></Dialog></>;
  const mounted = render(ui('run-a'));
  const button = await screen.findByRole('button', { name: 'Detener agente' });
  await waitFor(() => expect(button.closest('[role="dialog"]')).toBeTruthy());
  expect(screen.getByTestId('agent-control-frame')).toBeTruthy();
  fireEvent.click(button);
  expect(localStop.mock.calls[0][0].detail).toBe('cancel');
  expect(remoteStop.mock.calls[0][0].detail).toBe('run-a');
  mounted.rerender(ui(null));
  expect(screen.queryByTestId('agent-control-frame')).toBeNull();
  expect(screen.queryByRole('button', { name: 'Detener agente' })).toBeNull();
  window.removeEventListener('nova-ui-control', localStop);
  window.removeEventListener('nova-stop-request', remoteStop);
});
it('allows the native form submission derived from an agent click while the lock is active', async () => {
  const saved = vi.fn();
  render(<><AgentControl runId="run-a" runtime={null} /><form onSubmit={event => { event.preventDefault(); saved((event.nativeEvent as SubmitEvent).isTrusted); }}><button type="submit">Aplicar versión aprobada</button></form></>);
  await screen.findByRole('button', { name: 'Detener agente' });
  screen.getByRole('button', { name: 'Aplicar versión aprobada' }).click();
  expect(saved).toHaveBeenCalledOnce();
  // jsdom, like Chromium, emits a trusted submit after a synthetic .click().
  expect(saved).toHaveBeenCalledWith(true);
});
it('completes the approved driver command through a real React form under the command lock', async () => {
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([{}] as unknown as DOMRectList);
  HTMLElement.prototype.scrollIntoView = vi.fn();
  vi.stubGlobal('matchMedia', () => ({ matches: true }));
  const runId = crypto.randomUUID();
  function Workspace() {
    const [saved, setSaved] = useState(false);
    return <><AgentControl runId={runId} runtime={null} />{saved
      ? <section data-testid="prompt-focus" data-prompt-id="prompt-a" data-revision="1" data-template-text="Resumen"><h2 data-testid="focus-title">Resumen</h2><span data-testid="compile-ready" /><div data-testid="compiled-preview">Resumen</div></section>
      : <section data-testid="prompt-editor"><form onSubmit={event => { event.preventDefault(); setSaved(true); }}><input id="prompt-title" defaultValue="Resumen" /><textarea id="prompt-content" defaultValue="Resumen" /><button type="submit" data-nova-control="approval.apply" data-nova-run-id={runId}>Aplicar versión aprobada</button></form><span data-testid="variables-inspected" /><span data-testid="compile-ready" /></section>}</>;
  }
  render(<Workspace />);
  await screen.findByRole('button', { name: 'Detener agente' });
  const driver = new AgentUiDriver(vi.fn()); driver.speed = 'fast';
  const state = await driver.execute({ runId, actionId: crypto.randomUUID(), sequence: 1, tool: 'commit_approved', target: null, arguments: { expectedRevision: 1 }, timestamp: new Date().toISOString(), executionStatus: 'DISPATCHED', verificationStatus: 'PENDING' });
  expect(state.mode).toBe('focus'); expect(state.revision).toBe(1);
  vi.unstubAllGlobals();
});
