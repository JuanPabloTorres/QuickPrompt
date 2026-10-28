import { act, cleanup, fireEvent, render, renderHook, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import App from '../src/app/App';
import { AgentUiDriver, type UiAction } from '../src/features/nova/AgentUiDriver';
import { ApplicationServiceProvider } from '../src/components/ServiceContext';
import { createApiClient, type HttpTransport } from '../src/services/api';
import { createApplicationServices } from '../src/services/application';
import { usePromptDeletion } from '../src/features/prompts/usePromptDeletion';

const prompt = { id: crypto.randomUUID(), title: 'Registro de auditoría sintético', templateText: 'Explica <tema>.', description: '', categoryId: 0, revision: 3, isFavorite: false, isArchived: false, variableNames: ['tema'], defaults: {}, uses: 0, lastUsedAt: null, createdAt: '', updatedAt: '' };
const runId = crypto.randomUUID(), approvalId = crypto.randomUUID();
const run = { runId, sessionId: crypto.randomUUID(), mode: 'SAME_TAB', status: 'AWAITING_APPROVAL', sequence: 4, targetId: prompt.id, revision: 3, pauseRequested: false, browserOpen: true, approval: 'PREPARING', effectCommitted: false, errorCode: null };
class Events extends EventTarget { close() {} }
beforeEach(() => { localStorage.clear(); sessionStorage.clear(); vi.stubGlobal('EventSource', Events); });
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); localStorage.clear(); sessionStorage.clear(); });
function setup() {
  const calls: { path: string; method: string; body?: Record<string, unknown> }[] = [];
  let deleted = false;
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    const body = options?.body ? JSON.parse(String(options.body)) : undefined;
    calls.push({ path, method: options?.method ?? 'GET', body });
    if (path.endsWith('/groups') || path.endsWith('/nova/drafts')) return Response.json([]);
    if (path.endsWith('/nova/preferences')) return Response.json({ language: null, format: null, providerId: null });
    if (path.includes('/prompts?')) return Response.json({ items: deleted ? [] : [prompt], total: deleted ? 0 : 1 });
    if (path.endsWith('/prompts/' + prompt.id)) return Response.json(prompt);
    if (path.endsWith(`/nova/live/${runId}`)) return Response.json({ run, request: null, question: null, observation: null, proposal: null,
      deletion: { id: approvalId, hash: 'domain-bound-hash', promptId: prompt.id, revision: 3, title: prompt.title } });
    if (path.endsWith(`/nova/live/${runId}/deletion`)) {
      deleted = body.confirmed === true;
      return Response.json({ ...run, effectCommitted: deleted, approval: deleted ? 'CONSUMED' : 'REJECTED', status: deleted ? 'UI_UPDATED' : 'CANCELLED' });
    }
    throw new Error('Unexpected endpoint: ' + path);
  }));
  render(<App />); return calls;
}
async function openReview() {
  await screen.findByTestId('library-ready');
  let reviewed!: Promise<void>;
  act(() => { reviewed = new Promise<void>((resolve, reject) => window.dispatchEvent(new CustomEvent('nova-review-deletion', { detail: { runId, promptId: prompt.id, revision: 3, resolve, reject } }))); });
  await act(async () => { await reviewed; });
  return screen.findByRole('dialog', { name: '¿Eliminar este prompt?' });
}
function enable() { act(() => { window.dispatchEvent(new CustomEvent('nova-enable-deletion', { detail: { runId, promptId: prompt.id, revision: 3 } })); }); }

it.each(['Cancelar', 'Cerrar diálogo', 'Escape'])('rejects a NOVA deletion through %s without opening the record or using DELETE', async close => {
  const calls = setup(); const dialog = await openReview();
  expect(screen.getByRole('button', { name: 'Eliminar definitivamente' })).toHaveProperty('disabled', true);
  if (close === 'Escape') fireEvent.keyDown(dialog, { key: 'Escape' });
  else fireEvent.click(screen.getByRole('button', { name: close }));
  await waitFor(() => expect(screen.queryByRole('dialog', { name: '¿Eliminar este prompt?' })).toBeNull());
  expect(screen.queryByTestId('prompt-focus')).toBeNull();
  expect(await screen.findByRole('button', { name: prompt.title })).toBeTruthy();
  const decisions = calls.filter(call => call.path.endsWith('/deletion'));
  expect(decisions).toHaveLength(1); expect(decisions[0].body).toEqual({ approvalId, hash: 'domain-bound-hash', revision: 3, confirmed: false });
  expect(calls.some(call => call.method === 'DELETE')).toBe(false);
});
it('enables only the bound review and sends the human decision to the guarded domain endpoint', async () => {
  const calls = setup(); await openReview();
  act(() => { window.dispatchEvent(new CustomEvent('nova-enable-deletion', { detail: { runId: crypto.randomUUID(), promptId: prompt.id, revision: 3 } })); });
  expect(screen.getByRole('button', { name: 'Eliminar definitivamente' })).toHaveProperty('disabled', true);
  enable(); fireEvent.click(screen.getByRole('button', { name: 'Eliminar definitivamente' }));
  await waitFor(() => expect(screen.queryByRole('dialog', { name: '¿Eliminar este prompt?' })).toBeNull());
  await waitFor(() => expect(screen.queryByRole('button', { name: prompt.title })).toBeNull());
  expect(calls.filter(call => call.path.endsWith('/deletion'))[0].body).toEqual({ approvalId, hash: 'domain-bound-hash', revision: 3, confirmed: true });
  expect(calls.some(call => call.method === 'DELETE')).toBe(false);
});
it('aborting a run closes its pending review without a persistence request', async () => {
  const calls = setup(); await openReview();
  act(() => { window.dispatchEvent(new Event('nova-abort-ui-operation')); });
  await waitFor(() => expect(screen.queryByRole('dialog', { name: '¿Eliminar este prompt?' })).toBeNull());
  expect(calls.some(call => call.path.endsWith('/deletion') || call.method === 'DELETE')).toBe(false);
});
it('the same-tab driver captures native deletion identity without navigating or committing', async () => {
  const calls = setup();
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([{}] as unknown as DOMRectList);
  const driver = new AgentUiDriver(vi.fn()); driver.speed = 'fast';
  const action = (tool: string): UiAction => ({ runId, tool, actionId: crypto.randomUUID(), sequence: 1, target: prompt.id, arguments: { promptId: prompt.id, revision: 3 }, timestamp: '', executionStatus: 'DISPATCHED', verificationStatus: 'PENDING' });
  await screen.findByTestId('library-ready');
  let state: Awaited<ReturnType<typeof driver.execute>> | undefined;
  await act(async () => { state = await driver.execute(action('ui_review_deletion')); });
  expect(state?.mode).toBe('discover'); expect(state?.deletionRunId).toBe(runId); expect(state?.deletionPromptId).toBe(prompt.id); expect(state?.deletionConfirmationReady).toBe(false);
  await act(async () => { state = await driver.execute(action('ui_enable_deletion_confirmation')); });
  expect(state?.deletionConfirmationReady).toBe(true);
  expect(calls.some(call => call.path.endsWith('/deletion') || call.method === 'DELETE')).toBe(false);
  act(() => { driver.stop(); });
});

function connection(title: string, pending?: Promise<Response>) {
  const item = { ...prompt, title };
  const send = vi.fn<HttpTransport['send']>().mockImplementation(async path => {
    if (path.endsWith('/groups') || path.endsWith('/nova/drafts')) return Response.json([]);
    if (path.includes('/prompts?')) return Response.json({ items: [item], total: 1 });
    if (path.endsWith('/prompts/' + item.id)) return Response.json(item);
    if (path.endsWith(`/nova/live/${runId}`)) return pending ?? Response.json({ run, request: null, question: null, observation: null, proposal: null,
      deletion: { id: approvalId, hash: 'domain-bound-hash', promptId: item.id, revision: 3, title: item.title } });
    throw Error('Unexpected route: ' + path);
  });
  return { item, send, services: createApplicationServices(createApiClient({ send })) };
}
function requestReview() {
  let pending!: Promise<void>;
  act(() => { pending = new Promise<void>((resolve, reject) => window.dispatchEvent(new CustomEvent('nova-review-deletion', {
    detail: { runId, promptId: prompt.id, revision: 3, resolve, reject },
  }))); });
  return pending.then(() => 'OPENED', (error: Error) => error.message);
}

it('binds deletion review to the currently supplied connection after service replacement', async () => {
  const first = connection('Primera conexión'), second = connection('Segunda conexión');
  const tree = (services: typeof first.services) => <ApplicationServiceProvider services={services}><App /></ApplicationServiceProvider>;
  const view = render(tree(first.services));
  await screen.findByRole('button', { name: first.item.title });
  view.rerender(tree(second.services));
  await screen.findByRole('button', { name: second.item.title });
  let outcome = '';
  await act(async () => { outcome = await requestReview(); });
  expect(outcome).toBe('OPENED');
  expect(second.send.mock.calls.some(([path]) => path.endsWith(`/nova/live/${runId}`))).toBe(true);
  expect(screen.getByRole('dialog', { name: '¿Eliminar este prompt?' }).textContent).toContain(second.item.title);
  expect(first.send.mock.calls.some(([path]) => path.endsWith(`/nova/live/${runId}`))).toBe(false);
});

it('never opens a pending review from the replaced connection when its response arrives late', async () => {
  let release!: (value: Response) => void;
  const first = connection('Conexión retirada', new Promise(resolve => { release = resolve; })), second = connection('Conexión vigente');
  const tree = (services: typeof first.services) => <ApplicationServiceProvider services={services}><App /></ApplicationServiceProvider>;
  const view = render(tree(first.services));
  await screen.findByRole('button', { name: first.item.title });
  const pending = requestReview();
  await waitFor(() => expect(first.send.mock.calls.some(([path]) => path.endsWith(`/nova/live/${runId}`))).toBe(true));
  view.rerender(tree(second.services));
  await screen.findByRole('button', { name: second.item.title });
  let outcome = '';
  await act(async () => {
    release(Response.json({ run, request: null, question: null, observation: null, proposal: null,
      deletion: { id: approvalId, hash: 'domain-bound-hash', promptId: prompt.id, revision: 3, title: first.item.title } }));
    outcome = await pending;
  });
  expect(outcome).not.toBe('OPENED');
  expect(screen.queryByRole('dialog', { name: '¿Eliminar este prompt?' })).toBeNull();
  expect(screen.getByRole('button', { name: second.item.title })).toBeTruthy();
  expect(first.send.mock.calls.some(([path]) => path.endsWith('/prompts/' + prompt.id))).toBe(false);
});

it('exposes a detached review view while the owner retains the domain-bound approval identity', async () => {
  const client = connection('Revisión del propietario');
  const onDeleted = vi.fn();
  const { result } = renderHook(() => usePromptDeletion(client.services.prompts, client.services.live, {
    saving: false, setSaving: vi.fn(), setError: vi.fn(), ensureAvailable: async () => {}, onDeleted,
  }));
  await act(async () => { expect(await requestReview()).toBe('OPENED'); });
  expect(result.current.review).toEqual({ runId });
  Object.assign(result.current.review!, { runId: 'mutated-presentation-view' });
  enable();
  client.send.mockResolvedValueOnce(Response.json({ ...run, effectCommitted: true, approval: 'CONSUMED', status: 'UI_UPDATED' }));
  await act(async () => { await result.current.confirm(); });
  const [path, options] = client.send.mock.calls.at(-1)!;
  expect(path).toMatch(new RegExp(`/nova/live/${runId}/deletion$`));
  expect(JSON.parse(String(options.body))).toEqual({ approvalId, hash: 'domain-bound-hash', revision: 3, confirmed: true });
  expect(onDeleted).toHaveBeenCalledOnce();
  expect(result.current.target).toBeNull();
});
