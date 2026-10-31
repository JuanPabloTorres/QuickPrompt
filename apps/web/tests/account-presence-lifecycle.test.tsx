import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { useEffect } from 'react';
import { AccountNovaBoundary } from '../src/features/accounts/AccountNovaBoundary';
import { createAccountService } from '../src/services/accounts';
import { createApiClient } from '../src/services/api';
import { useAgentRuntimeServices } from '../src/components/ServiceContext';

// Only the DOM execution driver is a fixture. Scoped clients, activity, controls and boundary lifecycle remain real.
vi.mock('../src/features/nova/SameTabAgent', () => ({ SameTabAgent: ({ runId, onActive }: { runId: string | null; onActive: (active: boolean) => void }) => {
  const { visibleSession } = useAgentRuntimeServices(); const owns = !!runId && !!visibleSession.surfaceCapability(runId);
  useEffect(() => { onActive(owns); return () => onActive(false); }, [owns, onActive]);
  return <span data-testid="fixture-driver">{owns ? runId : 'read-only'}</span>;
} }));

const account = '91000000-0000-4000-8000-000000000001';
const library = '91000000-0000-4000-8000-000000000002';
const connection = '91000000-0000-4000-8000-000000000003';
const foreign = '92000000-0000-4000-8000-000000000001';
const now = Date.parse('2026-10-29T12:00:00Z');
class Events extends EventTarget {
  static items: Events[] = []; onopen: (() => void) | null = null; onerror: (() => void) | null = null; closed = false;
  constructor(public url: string) { super(); Events.items.push(this); }
  close() { this.closed = true; }
  send(runId: string, sequence: number, status = 'INTERPRETING') {
    this.dispatchEvent(new MessageEvent('activity', { data: JSON.stringify({ runId, sequence, status, eventId: crypto.randomUUID(), timestamp: new Date().toISOString(),
      stage: 'Execution', actor: 'SYSTEM_VALIDATION', toolName: null, targetId: null, durationMs: 0, errorCode: null, safeDisplayMessage: 'Actividad sintética' }) }));
  }
}
beforeEach(() => {
  vi.useFakeTimers(); vi.setSystemTime(now); vi.stubGlobal('EventSource', Events);
  vi.stubGlobal('matchMedia', () => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() }));
});
afterEach(() => { cleanup(); vi.useRealTimers(); vi.restoreAllMocks(); vi.unstubAllGlobals(); sessionStorage.clear(); Events.items = []; });

async function settle() { await act(async () => { for (let count = 0; count < 8; count++) await Promise.resolve(); }); }
function fixture() {
  const prefix = `/api/accounts/v1/libraries/${library}/nova`;
  const calls: { path: string; method: string }[] = [];
  let runId = ''; let sequence = 0; let status = 'INTERPRETING';
  const record = () => ({ runId, sessionId: account, mode: 'SAME_TAB', status, sequence, targetId: null, revision: null,
    pauseRequested: status === 'PAUSED', browserOpen: status !== 'CANCELLED', approval: 'NONE', effectCommitted: false, errorCode: null });
  const service = createAccountService(createApiClient({ send: async (path, options) => {
    const method = options.method ?? 'GET'; calls.push({ path, method });
    if (path === '/account/csrf') return Response.json({ token: 'synthetic' });
    if (path === '/account/connections/') return Response.json([{ id: connection, profileId: 'ollama-qwen3-8b-proposals', label: 'NOVA de prueba',
      active: true, revoked: false, createdAt: '', expiresAt: '2030-01-01', presence: { status: 'POLLING', observedAt: new Date(now).toISOString(), expiresAt: new Date(now + 5000).toISOString() } }]);
    if (path.startsWith(prefix + '/sessions/') && path.endsWith('/context')) return new Response(null, { status: 204 });
    if (path === prefix + '/live' && method === 'POST') { runId = JSON.parse(String(options.body)).request.runId; return Response.json(record()); }
    if (path === prefix + `/live/${runId}/cancel` && method === 'POST') { status = 'CANCELLED'; sequence++; return Response.json(record()); }
    if (path === prefix + `/live/${runId}` && method === 'GET') return Response.json({ run: record(), request: 'Solicitud sintética', question: null, observation: null, proposal: null });
    throw new Error('Unexpected local, foreign or effect API: ' + path);
  } }, ''));
  const created: ReturnType<typeof service.nova>[] = []; const original = service.nova;
  vi.spyOn(service, 'nova').mockImplementation((...args) => { const ports = original(...args); created.push(ports); return ports; });
  const expired = vi.fn();
  const page = () => <AccountNovaBoundary accountId={account} libraryId={library} service={service} onExpired={expired}>
    {agent => <>{agent.configuration}<output data-testid="current-run">{agent.runId}</output>
      <button disabled={!agent.services || agent.busy} onClick={() => void agent.begin('Solicitud sintética', { operation: 'CREATE_DRAFT', content: 'Contenido sintético' })}>Iniciar prueba</button></>}
  </AccountNovaBoundary>;
  return { service, created, calls, page, id: () => runId, paused: () => { status = 'PAUSED'; sequence++; } };
}
async function choose() {
  await settle(); fireEvent.change(screen.getByLabelText('Equipo de NOVA'), { target: { value: connection } }); await settle();
}
async function begin() { fireEvent.click(screen.getByRole('button', { name: 'Iniciar prueba' })); await settle(); }

it('expires only the label while preserving the same active run, surface and stop command', async () => {
  const fake = fixture(); render(fake.page()); await choose(); await begin();
  const id = fake.id(); const ports = fake.created[0]; const capability = ports.visibleSession.surfaceCapability(id);
  const driver = screen.getByTestId('fixture-driver'); expect(driver.textContent).toBe(id); expect(capability).toBeTruthy();
  await act(async () => { vi.advanceTimersByTime(5001); }); await settle();
  expect(screen.getByRole('option', { name: /Sin señal reciente/ })).toBeTruthy();
  expect(screen.getByTestId('current-run').textContent).toBe(id); expect(screen.getByTestId('fixture-driver')).toBe(driver);
  expect(fake.created).toHaveLength(1); expect(ports.visibleSession.surfaceCapability(id)).toBe(capability);
  expect(fake.calls.filter(call => call.path === '/account/connections/')).toHaveLength(2);
  fireEvent.click(screen.getByRole('button', { name: 'Detener agente' })); await settle();
  expect(fake.calls.filter(call => call.path.endsWith('/cancel') && call.method === 'POST')).toHaveLength(1);
  expect(fake.calls.filter(call => call.path.endsWith('/nova/live') && call.method === 'POST')).toHaveLength(1);
});

it('does not replay a start or lose ownership when activity disconnects and reconnects with duplicate or foreign events', async () => {
  const fake = fixture(); render(fake.page()); await choose(); await begin();
  const id = fake.id(); const ports = fake.created[0]; const capability = ports.visibleSession.surfaceCapability(id); const stream = Events.items[0];
  await act(async () => stream.onerror?.()); await settle();
  expect(screen.getByText('NOVA · sin conexión')).toBeTruthy();
  await act(async () => { stream.onopen?.(); stream.send(id, 1); stream.send(id, 1); stream.send(foreign, 2); stream.send(id, 2); vi.advanceTimersByTime(121); }); await settle();
  expect(screen.queryByText('NOVA · sin conexión')).toBeNull(); expect(screen.getByTestId('current-run').textContent).toBe(id);
  expect(ports.visibleSession.surfaceCapability(id)).toBe(capability);
  expect(fake.calls.filter(call => call.path.endsWith('/nova/live') && call.method === 'POST')).toHaveLength(1);
  expect(fake.calls.some(call => call.path.includes(foreign))).toBe(false);
});

it('closing releases this tab capability and metadata recovery never restores it to the next document', async () => {
  const fake = fixture(); const first = render(fake.page()); await choose(); await begin();
  const id = fake.id(); const old = fake.created[0]; const stream = Events.items[0]; fake.paused();
  first.unmount(); expect(stream.closed).toBe(true); expect(old.visibleSession.surfaceCapability(id)).toBeUndefined();
  render(fake.page()); await choose();
  expect(screen.getByTestId('current-run').textContent).toBe(id); expect(fake.created).toHaveLength(2);
  expect(fake.created[1].visibleSession.surfaceCapability(id)).toBeUndefined(); expect(screen.getByTestId('fixture-driver').textContent).toBe('read-only');
  expect(screen.queryByRole('button', { name: 'Continuar' })).toBeNull();
  await act(async () => stream.send(id, 50, 'COMPLETED')); await settle();
  expect(fake.created[1].visibleSession.surfaceCapability(id)).toBeUndefined();
  fireEvent.click(screen.getByRole('button', { name: 'Detener ejecución guardada' })); await settle();
  expect(fake.calls.filter(call => call.path.endsWith('/cancel') && call.method === 'POST')).toHaveLength(1);
  expect(fake.calls.filter(call => call.path.endsWith('/nova/live') && call.method === 'POST')).toHaveLength(1);
});
