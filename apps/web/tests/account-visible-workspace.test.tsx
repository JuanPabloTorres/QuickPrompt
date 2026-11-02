import { StrictMode } from 'react';
import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { AccountPromptLibrary } from '../src/features/accounts/AccountPromptLibrary';
import { AccountPage } from '../src/features/accounts/AccountPage';
import { createAccountService } from '../src/services/accounts';
import { createApiClient } from '../src/services/api';
import { getActivity } from '../src/features/nova/activity';
import type { LiveCommand } from '../src/services/live';

const account = '10000000-0000-4000-8000-000000000001';
const libraryId = '10000000-0000-4000-8000-000000000002';
const connectionId = '10000000-0000-4000-8000-000000000003';
const sourceId = '10000000-0000-4000-8000-000000000004';
const createdId = '10000000-0000-4000-8000-000000000005';
const library = { id: libraryId, ownerId: account, name: 'Privada QA', createdAt: '' };
const initial = { id: sourceId, libraryId, title: 'Origen', template: 'Explica <tema>.', revision: 1 };
class Events extends EventTarget {
  static items: Events[] = [];
  onopen: (() => void) | null = null; onerror: (() => void) | null = null; closed = false;
  constructor(public url: string) { super(); Events.items.push(this); }
  close() { this.closed = true; }
}
beforeEach(() => {
  vi.stubGlobal('EventSource', Events);
  vi.stubGlobal('matchMedia', () => ({ matches: true, media: '(prefers-reduced-motion: reduce)', addEventListener: vi.fn(), removeEventListener: vi.fn(), addListener: vi.fn(), removeListener: vi.fn() }));
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockImplementation(function (this: HTMLElement) {
    return (this.closest('[hidden]') ? [] : [{ top: 0, left: 0, width: 100, height: 30 }]) as unknown as DOMRectList;
  });
  HTMLElement.prototype.scrollIntoView = vi.fn();
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); sessionStorage.clear(); Events.items = []; });

/** HTTP fixture only. The test operates the real React workspace, native driver and scoped clients; no model proof. */
function backend(kind: 'create' | 'update' | 'read' = 'create', recovered = false, lostStart = false, lostCommit = false, offPageSource = false) {
  const root = `/api/accounts/v1/libraries/${libraryId}`;
  const calls: { path: string; method: string; body: Record<string, unknown> | null }[] = [];
  const receipts: Record<string, unknown>[] = [];
  const ports: ReturnType<ReturnType<typeof createAccountService>['nova']>[] = [];
  const saved: typeof initial[] = [];
  const otherPrompts = offPageSource ? Array.from({ length: 12 }, (_, index) => ({ ...initial, id: `40000000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`, title: `Otro prompt ${index + 1}` })) : [];
  const promptSnapshot = () => Array.from(new Map([...otherPrompts, initial, ...saved].map(item => [item.id, item])).values());
  const proposed = { title: kind === 'create' ? 'Creado con NOVA' : 'Origen actualizado', templateText: kind === 'update' ? 'Explica <tema> con un ejemplo.' : 'Explica <tema>.', description: 'Descripción propuesta', categoryId: 0 };
  const contentReview = { status: 'REQUIRES_HUMAN_CONTENT_REVIEW', contractHash: 'synthetic-review-contract', bodyHash: 'synthetic-body', originalRequest: 'Solicitud sintética',
    source: { promptId: kind === 'update' ? sourceId : null, revision: kind === 'update' ? 1 : null, bodyHash: kind === 'update' ? 'synthetic-source' : null, originalText: kind === 'update' ? initial.template : null },
    ledger: [{ id: 1, source: 'REQUEST', start: 0, length: 19, text: 'Solicitud sintética', role: 'CONTEXT', replacementText: null, changeAuthority: [] }], literalChanges: { entries: [] } };
  const proposal = { id: crypto.randomUUID(), hash: 'synthetic-exact-artifact', promptId: kind === 'update' ? sourceId : null, revision: kind === 'update' ? 1 : null, draft: proposed, originalText: kind === 'update' ? initial.template : '', contentReview };
  let id = recovered ? crypto.randomUUID() : '';
  let unavailableReceipt = lostStart;
  let index = 0, sequence = 0, approved = false, status = recovered ? 'AWAITING_APPROVAL' : 'TOOL_RUNNING';
  const record = () => ({ runId: id, sessionId: account, mode: 'SAME_TAB', status, sequence, targetId: saved[0]?.id ?? (kind === 'create' ? null : sourceId),
    revision: saved[0]?.revision ?? (kind === 'create' ? null : 1), pauseRequested: false, browserOpen: !['COMPLETED', 'CANCELLED'].includes(status), approval: saved.length ? 'CONSUMED' : approved ? 'APPROVED' : status === 'AWAITING_APPROVAL' ? 'PENDING' : 'NONE', effectCommitted: saved.length > 0, errorCode: null });
  const view = () => ({ run: record(), request: 'Solicitud sintética', observation: null, question: null, proposal: status === 'AWAITING_APPROVAL' || approved ? proposal : null });
  const tools = kind === 'read' ? ['start', 'ui_open_prompt', 'ui_verify_prompt_state']
    : kind === 'update' ? ['start', 'ui_open_prompt', 'ui_open_editor', 'ui_prepare_update', 'prepare_review', 'commit_approved']
      : ['start', 'ui_open_create_prompt', 'ui_prepare_creation', 'prepare_review', 'commit_approved'];
  const actionIds = tools.map(() => crypto.randomUUID());
  const action = () => ({ runId: id, actionId: actionIds[index], sequence: index + 1, tool: tools[index], target: kind === 'update' && ['ui_open_prompt', 'ui_open_editor', 'ui_prepare_update'].includes(tools[index]) || kind === 'read' && index > 0 ? sourceId : null,
    arguments: tools[index] === 'start' ? { editRequest: 'Cambios', localExecutionAllowed: false } : tools[index] === 'commit_approved' ? { expectedRevision: kind === 'create' ? 1 : 2 } : { ...proposed },
    timestamp: new Date().toISOString(), executionStatus: 'DISPATCHED', verificationStatus: 'PENDING' });
  function emit() {
    const event = { runId: id, eventId: crypto.randomUUID(), sequence: ++sequence, timestamp: new Date().toISOString(), stage: status === 'COMPLETED' ? 'Browser' : 'Execution',
      actor: 'SYSTEM_VALIDATION', toolName: null, targetId: null, status, durationMs: 0, errorCode: null, safeDisplayMessage: 'Estado del fixture HTTP' };
    Events.items.filter(item => !item.closed && item.url.includes(id)).forEach(item => item.dispatchEvent(new MessageEvent('activity', { data: JSON.stringify(event) })));
  }
  const client = createApiClient({ send: async (path, options) => {
    const method = options.method ?? 'GET'; const body = options.body ? JSON.parse(String(options.body)) as Record<string, unknown> : null;
    calls.push({ path, method, body });
    if (path === '/account/csrf') return Response.json({ token: 'synthetic-csrf' });
    // This HTTP fixture represents an authenticated worker currently polling, independently of consent.
    if (path === '/account/connections/') return Response.json([{ id: connectionId, profileId: 'ollama-qwen3-8b-proposals', label: 'Equipo QA', active: true, revoked: false, createdAt: '', expiresAt: '2030-01-01', presence: { status: 'POLLING', observedAt: '2026-10-29T12:00:00Z', expiresAt: '2026-10-29T12:00:20Z' } }]);
    if (!path.startsWith(root)) throw new Error('Unexpected local or foreign API: ' + path);
    if (method === 'POST') expect(new Headers(options.headers).get('X-QP-CSRF')).toBe('synthetic-csrf');
    if (path.endsWith('/groups') || path.endsWith('/memory/sessions')) return Response.json([]);
    if (path.endsWith('/executions/events')) return new Response(`event: execution-state\ndata: ${JSON.stringify({ libraryId, signal: 'snapshot', items: [] })}\n\nevent: execution-state\ndata: ${JSON.stringify({ libraryId, signal: 'complete', items: null })}\n\n`, { headers: { 'Content-Type': 'text/event-stream' } });
    if (path.includes('/prompts/catalog?') && method === 'GET') return Response.json({ items: promptSnapshot().slice(0, 12), total: promptSnapshot().length, page: 0, size: 12 });
    if (path.endsWith('/prompts') && method === 'GET') return Response.json(promptSnapshot());
    if (path.endsWith('/prompts/' + sourceId)) return Response.json(saved.find(item => item.id === sourceId) ?? initial);
    if (path.endsWith('/prompts/' + createdId)) return Response.json(saved[0]);
    if (path.endsWith('/memory/editor/inspect')) {
      const text = String(body!.templateText); const values = body!.values as Record<string, string>;
      const names = [initial.template, proposed.templateText].includes(text) ? ['tema'] : [];
      return Response.json({ variableNames: names, missingVariables: names.filter(name => !values[name]?.trim()), compiledText: names.length && !values.tema?.trim() ? null : text.replace('<tema>', values.tema ?? '') });
    }
    if (path.includes('/nova/sessions/')) return new Response(null, { status: 204 });
    if (path.endsWith('/nova/live') && method === 'POST') {
      expect(body!.connectionId).toBe(connectionId); id = String((body!.request as Record<string, unknown>).runId);
      if (lostStart) { lostStart = false; throw new TypeError('Respuesta perdida después de admitir la ejecución.'); }
      return Response.json(record());
    }
    if (path.includes('/nova/live/')) {
      const tail = path.split('/nova/live/')[1].split('/').slice(1).join('/').split('?')[0];
      if (!tail) { if (unavailableReceipt) { unavailableReceipt = false; return Response.json({ title: 'Temporal' }, { status: 503 }); } return Response.json(view()); }
      if (tail === 'actions' && method === 'GET') return Response.json({ run: record(), action: status === 'AWAITING_APPROVAL' || ['COMPLETED', 'CANCELLED'].includes(status) ? null : action() });
      if (tail === 'cancel') { status = 'CANCELLED'; emit(); return Response.json(record()); }
      expect(new Headers(options.headers).get('X-Nova-Surface')).toBeTruthy();
      if (tail === 'approve') {
        expect(body).toEqual({ approvalId: proposal.id, hash: proposal.hash, revision: proposal.revision, confirmed: true, contentReviewed: true, contentReviewHash: contentReview.contractHash, approvedLiteralChangeIds: [] }); approved = true; status = 'TOOL_RUNNING'; emit(); return Response.json(record());
      }
      if (tail === 'commit') {
        expect(approved).toBe(true); expect(body).toEqual(proposed); expect(saved).toHaveLength(0);
        const prompt = { id: kind === 'create' ? createdId : sourceId, libraryId, title: proposed.title, template: proposed.templateText, revision: kind === 'create' ? 1 : 2 }; saved.push(prompt);
        if (lostCommit) { lostCommit = false; throw new TypeError('Guardado confirmado, respuesta perdida.'); }
        return Response.json({ ...prompt, templateText: prompt.template, description: proposed.description, categoryId: 0, isFavorite: false, isArchived: false,
          createdAt: '', updatedAt: '', uses: 0, lastUsedAt: null, variableNames: ['tema'], defaults: {} });
      }
      if (tail === 'actions/progress') return new Response(null, { status: 204 });
      if (tail === 'actions/receipt') {
        expect(body!.verificationStatus).toBe('VERIFIED'); receipts.push(body!); index++;
        if (tools[index - 1] === 'prepare_review') status = 'AWAITING_APPROVAL';
        if (index === tools.length) status = 'COMPLETED'; emit(); return new Response(null, { status: 204 });
      }
    }
    throw new Error('Unexpected owning endpoint: ' + path);
  } }, '');
  const service = createAccountService(client); const nova = service.nova;
  service.nova = (...args) => { const result = nova(...args); ports.push(result); return result; };
  if (recovered) sessionStorage.setItem(`quickprompt:${account}:${libraryId}:${connectionId}:nova-run`, id);
  return { service, calls, receipts, ports, saved, id: () => id, proposed };
}
async function chooseDevice() {
  const launcher = screen.queryByRole('button', { name: 'Abrir asistente NOVA' });
  if (launcher) fireEvent.click(launcher);
  expect(await screen.findByRole('option', { name: /^Equipo QA ·/ })).toHaveProperty('value', connectionId);
  fireEvent.change(screen.getByLabelText('Equipo de NOVA'), { target: { value: connectionId } });
  await screen.findByRole('heading', { name: 'Trabaja con NOVA' });
}
async function launch(kind: 'create' | 'update' | 'read') {
  fireEvent.change(screen.getByLabelText('Acción de NOVA'), { target: { value: kind === 'create' ? 'CREATE_DRAFT' : kind === 'update' ? 'PROPOSE_ADAPTATION' : 'OPEN_PROMPT' } });
  if (kind !== 'create') fireEvent.change(screen.getByLabelText('Prompt de origen'), { target: { value: sourceId } });
  if (kind !== 'read') {
    fireEvent.change(screen.getByLabelText(kind === 'create' ? 'Qué prompt necesitas' : 'Cambios solicitados'), { target: { value: 'Explica conceptos con claridad.' } });
    fireEvent.click(screen.getByLabelText('Definir las variables ahora'));
    fireEvent.change(screen.getByLabelText(kind === 'create' ? 'Variables de la plantilla' : 'Variables a agregar'), { target: { value: kind === 'create' ? 'tema' : '' } });
  }
  fireEvent.click(screen.getByRole('button', { name: kind === 'read' ? 'Abrir con NOVA' : 'Preparar con NOVA' }));
}

it.each(['create', 'update'] as const)('operates the real private React editor and commits exactly the approved artifact (%s)', async kind => {
  const fake = backend(kind); const expired = vi.fn();
  const mounted = render(<StrictMode><AccountPromptLibrary library={library} agentAccountId={account} service={fake.service} onExpired={expired} /></StrictMode>);
  await chooseDevice(); await launch(kind);
  await screen.findByText('NOVA necesita tu aprobación', {}, { timeout: 12000 });
  expect(screen.queryByRole('button', { name: 'Abrir asistente NOVA' })).toBeNull();
  expect(screen.getByTestId('nova-dock').hasAttribute('hidden')).toBe(true);
  expect(screen.getAllByRole('button', { name: 'No aprobar' })).toHaveLength(1);
  expect(screen.queryByRole('button', { name: 'Detener agente' })).toBeNull();
  expect(fake.saved).toEqual([]); expect(screen.getByLabelText('Contenido')).toHaveProperty('value', fake.proposed.templateText);
  expect(screen.getByLabelText('Descripción')).toHaveProperty('value', fake.proposed.description);
  fireEvent.click(screen.getByRole('button', { name: 'Revisar y aprobar' }));
  const dialog = await screen.findByRole('dialog', { name: kind === 'create' ? 'Guardar copia separada' : 'Confirmar nueva versión' });
  expect(within(dialog).getByText(fake.proposed.templateText)).toBeTruthy();
  expect(within(dialog).getByText(fake.proposed.description)).toBeTruthy();
  fireEvent.click(within(dialog).getByRole('button', { name: kind === 'create' ? 'Aprobar contenido y guardar esta copia' : 'Aprobar contenido y guardar esta versión' }));
  await waitFor(() => expect(fake.saved).toHaveLength(1), { timeout: 12000 });
  await waitFor(() => expect(fake.receipts).toHaveLength(kind === 'create' ? 5 : 6), { timeout: 12000 });
  expect(screen.getByTestId('prompt-focus').dataset.promptId).toBe(kind === 'create' ? createdId : sourceId);
  expect(fake.saved[0].revision).toBe(kind === 'create' ? 1 : 2);
  const start = fake.calls.find(call => call.path.endsWith('/nova/live'))!.body!.request as { command: LiveCommand };
  expect(start.command.operation).toBe(kind === 'create' ? 'CREATE_DRAFT' : 'PROPOSE_ADAPTATION');
  expect(start.command.inputNames).toEqual(kind === 'create' ? ['tema'] : []);
  expect(fake.calls.filter(call => call.path.endsWith('/commit'))).toHaveLength(1); expect(expired).not.toHaveBeenCalled();
  expect(fake.calls.find(call => call.path.endsWith('/commit'))!.body).toMatchObject({ description: fake.proposed.description });
  expect(getActivity(fake.id()).some(item => item.status === 'verified')).toBe(true);
  mounted.unmount();
  expect(fake.ports.at(-1)!.visibleSession.surfaceCapability(fake.id())).toBeUndefined(); expect(getActivity(fake.id())).toEqual([]);
}, 20000);

it('opens the selected private source through its actual title control and never proposes or saves', async () => {
  const fake = backend('read'); render(<AccountPromptLibrary library={library} agentAccountId={account} service={fake.service} onExpired={vi.fn()} />);
  await chooseDevice(); await launch('read');
  await waitFor(() => expect(fake.receipts).toHaveLength(3), { timeout: 12000 });
  expect(screen.getByTestId('prompt-focus').dataset.promptId).toBe(sourceId); expect(screen.getByTestId('template-preview').textContent).toBe(initial.template);
  expect(fake.calls.some(call => /\/(approve|commit)$/.test(call.path))).toBe(false); expect(fake.saved).toEqual([]);
}, 15000);

it('rejects through the shared agent control and retains the visible draft without a persistence request', async () => {
  const fake = backend(); render(<AccountPromptLibrary library={library} agentAccountId={account} service={fake.service} onExpired={vi.fn()} />);
  await chooseDevice(); await launch('create'); await screen.findByText('NOVA necesita tu aprobación', {}, { timeout: 12000 });
  fireEvent.click(screen.getByRole('button', { name: 'No aprobar' }));
  await waitFor(() => expect(screen.queryByTestId('agent-control-frame')).toBeNull());
  expect(screen.getByLabelText('Contenido')).toHaveProperty('value', fake.proposed.templateText);
  expect(fake.calls.some(call => /\/(approve|commit)$/.test(call.path))).toBe(false); expect(fake.saved).toEqual([]);
}, 15000);

it('recovers metadata without restoring UI control or approval and offers owner cancellation', async () => {
  const fake = backend('create', true); render(<AccountPromptLibrary library={library} agentAccountId={account} service={fake.service} onExpired={vi.fn()} />);
  await chooseDevice();
  fireEvent.click(await screen.findByRole('button', { name: 'Detener ejecución guardada' }));
  await waitFor(() => expect(fake.calls.some(call => call.path.endsWith('/cancel'))).toBe(true));
  expect(fake.calls.some(call => /\/actions\?/.test(call.path))).toBe(false);
  expect(fake.calls.some(call => /\/(approve|commit)$/.test(call.path))).toBe(false);
  expect(screen.queryByTestId('agent-control-frame')).toBeNull(); expect(fake.saved).toEqual([]);
});

it('opens a NOVA source beyond the catalog page through its visible selected-source control', async () => {
  const fake = backend('read', false, false, false, true);
  render(<AccountPromptLibrary library={library} agentAccountId={account} service={fake.service} onExpired={vi.fn()} />);
  await screen.findByRole('button', { name: 'Otro prompt 1' });
  expect(screen.queryByRole('button', { name: 'Origen' })).toBeNull();
  await chooseDevice();
  fireEvent.change(screen.getByLabelText('Acción de NOVA'), { target: { value: 'OPEN_PROMPT' } });
  fireEvent.change(screen.getByLabelText('Prompt de origen'), { target: { value: sourceId } });
  const sourceControl = await screen.findByRole('button', { name: 'Origen' });
  expect(sourceControl.closest('.nova-dock')).toBeNull();
  expect(document.querySelectorAll(`[data-testid="library-prompt"][data-prompt-id="${sourceId}"]`)).toHaveLength(1);
  await launch('read');
  await waitFor(() => expect(fake.receipts).toHaveLength(3), { timeout: 12000 });
  expect(screen.getByTestId('prompt-focus').getAttribute('data-prompt-id')).toBe(sourceId);
  expect(fake.calls.some(call => /\/(approve|commit)$/.test(call.path))).toBe(false);
  expect(fake.saved).toEqual([]);
}, 20000);

it('opens an icon-only NOVA dock and retains the exact action values and connection when closed', async () => {
  const fake = backend();
  render(<AccountPromptLibrary library={library} agentAccountId={account} service={fake.service} onExpired={vi.fn()} />);
  const launcher = screen.getByRole('button', { name: 'Abrir asistente NOVA' });
  expect(launcher.textContent).toBe('');
  expect(launcher.getAttribute('aria-expanded')).toBe('false');
  expect(screen.queryByRole('combobox', { name: 'Equipo de NOVA' })).toBeNull();
  await chooseDevice();
  const details = screen.getByLabelText('Qué prompt necesitas');
  fireEvent.change(details, { target: { value: 'Conservar este borrador sin iniciar una ejecución.' } });
  fireEvent.click(screen.getByLabelText('Definir las variables ahora'));
  fireEvent.change(screen.getByLabelText('Variables de la plantilla'), { target: { value: 'tema\nnivel' } });
  fireEvent.click(screen.getByRole('button', { name: 'Cerrar panel de NOVA' }));
  expect(screen.queryByRole('textbox', { name: 'Qué prompt necesitas' })).toBeNull();
  expect(details.isConnected).toBe(true);
  fireEvent.click(screen.getByRole('button', { name: 'Abrir asistente NOVA' }));
  expect(screen.getByLabelText('Qué prompt necesitas')).toBe(details);
  expect(details).toHaveProperty('value', 'Conservar este borrador sin iniciar una ejecución.');
  expect(screen.getByLabelText('Variables de la plantilla')).toHaveProperty('value', 'tema\nnivel');
  expect(screen.getByLabelText('Equipo de NOVA')).toHaveProperty('value', connectionId);
  expect(fake.calls.filter(call => call.path.endsWith('/nova/live') && call.method === 'POST')).toEqual([]);
});

it('masks the dock while verifying identity and restores its mounted draft only for the same account', async () => {
  const fake = backend(); let confirmIdentity!: () => void; let checking = false;
  const user = { id: account, email: 'qa-dock@example.invalid' };
  fake.service.current = vi.fn(() => checking ? new Promise<typeof user>(resolve => { confirmIdentity = () => resolve(user); }) : Promise.resolve(user));
  fake.service.options = vi.fn(async () => ({ email: true, google: false, delegation: true, mailDelivery: 'development-pickup' }));
  fake.service.libraries = vi.fn(async () => [library]);
  render(<AccountPage service={fake.service} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Abrir biblioteca Privada QA' }));
  await chooseDevice();
  const details = screen.getByLabelText('Qué prompt necesitas');
  fireEvent.change(details, { target: { value: 'Borrador privado dentro de NOVA.' } });
  checking = true; fireEvent.focus(window);
  expect(screen.queryByRole('button', { name: 'Abrir asistente NOVA' })).toBeNull();
  expect(screen.queryByRole('textbox', { name: 'Qué prompt necesitas' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Detener agente' })).toBeNull();
  expect(details.isConnected).toBe(true);
  await act(async () => { checking = false; confirmIdentity(); });
  fireEvent.click(await screen.findByRole('button', { name: 'Abrir asistente NOVA' }));
  expect(screen.getByLabelText('Qué prompt necesitas')).toBe(details);
  expect(details).toHaveProperty('value', 'Borrador privado dentro de NOVA.');
  expect(fake.calls.filter(call => call.path.endsWith('/nova/live') && call.method === 'POST')).toEqual([]);
});

it('waits for an authoritative view after an uncertain start and drives the same run without repeating its POST', async () => {
  const fake = backend('create', false, true); render(<AccountPromptLibrary library={library} agentAccountId={account} service={fake.service} onExpired={vi.fn()} />);
  await chooseDevice(); await launch('create');
  await screen.findByText('NOVA necesita tu aprobación', {}, { timeout: 12000 });
  expect(fake.calls.filter(call => call.path.endsWith('/nova/live') && call.method === 'POST')).toHaveLength(1);
  expect(fake.receipts).toHaveLength(4); expect(fake.saved).toEqual([]);
  expect(screen.getByLabelText('Contenido')).toHaveProperty('value', fake.proposed.templateText);
  fireEvent.click(screen.getByRole('button', { name: 'No aprobar' }));
  await waitFor(() => expect(screen.queryByTestId('agent-control-frame')).toBeNull());
}, 15000);

it('shows the committed version after a lost write response by reading its receipt, without another save', async () => {
  const fake = backend('create', false, false, true); render(<AccountPromptLibrary library={library} agentAccountId={account} service={fake.service} onExpired={vi.fn()} />);
  await chooseDevice(); await launch('create'); await screen.findByText('NOVA necesita tu aprobación', {}, { timeout: 12000 });
  fireEvent.click(screen.getByRole('button', { name: 'Revisar y aprobar' }));
  const dialog = await screen.findByRole('dialog', { name: 'Guardar copia separada' });
  fireEvent.click(within(dialog).getByRole('button', { name: 'Aprobar contenido y guardar esta copia' }));
  await waitFor(() => expect(fake.receipts).toHaveLength(5), { timeout: 12000 });
  expect(fake.saved).toHaveLength(1); expect(fake.calls.filter(call => call.path.endsWith('/commit'))).toHaveLength(1);
  expect(screen.getByTestId('prompt-focus').dataset.promptId).toBe(createdId);
  expect(screen.getByTestId('template-preview').textContent).toBe(fake.proposed.templateText);
  expect(fake.calls.some(call => call.path.endsWith('/prompts/' + createdId) && call.method === 'GET')).toBe(true);
}, 20000);

it('revalidates the same account on focus without losing the active draft, original surface or approval review', async () => {
  const fake = backend(); let confirmIdentity!: () => void; let checking = false;
  const user = { id: account, email: 'qa-visible@example.invalid' };
  fake.service.current = vi.fn(() => checking ? new Promise<typeof user>(resolve => { confirmIdentity = () => resolve(user); }) : Promise.resolve(user));
  fake.service.options = vi.fn(async () => ({ email: true, google: false, delegation: true, mailDelivery: 'development-pickup' }));
  fake.service.libraries = vi.fn(async () => [library]);
  render(<AccountPage service={fake.service} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Abrir biblioteca Privada QA' }));
  await chooseDevice(); await launch('create'); await screen.findByText('NOVA necesita tu aprobación', {}, { timeout: 12000 });
  const editor = screen.getByLabelText('Contenido'); const ports = fake.ports.at(-1)!;
  const surface = ports.visibleSession.surfaceCapability(fake.id());
  checking = true; fireEvent.focus(window);
  await waitFor(() => expect(confirmIdentity).toBeTypeOf('function'));
  expect(editor.isConnected).toBe(true);
  expect(screen.queryByRole('textbox', { name: 'Contenido' })).toBeNull();
  await act(async () => { checking = false; confirmIdentity(); });
  await screen.findByText('NOVA necesita tu aprobación');
  expect(screen.getByLabelText('Contenido')).toBe(editor);
  expect(ports.visibleSession.surfaceCapability(fake.id())).toBe(surface);
  expect(fake.calls.filter(call => call.path.endsWith('/nova/live'))).toHaveLength(1);
  fireEvent.click(screen.getByRole('button', { name: 'Revisar y aprobar' }));
  const dialog = await screen.findByRole('dialog', { name: 'Guardar copia separada' });
  fireEvent.click(within(dialog).getByRole('button', { name: 'Aprobar contenido y guardar esta copia' }));
  await waitFor(() => expect(fake.saved).toHaveLength(1), { timeout: 12000 });
  await waitFor(() => expect(fake.receipts).toHaveLength(5), { timeout: 12000 });
}, 20000);

it('masks an open approval review and retires all old controls when the account changes', async () => {
  const fake = backend(); let actor = account; let confirmIdentity!: () => void; let checking = false;
  const other = '20000000-0000-4000-8000-000000000001';
  const identity = () => ({ id: actor, email: actor === account ? 'a@example.invalid' : 'b@example.invalid' });
  fake.service.current = vi.fn(() => checking ? new Promise<ReturnType<typeof identity>>(resolve => { confirmIdentity = () => resolve(identity()); }) : Promise.resolve(identity()));
  fake.service.options = vi.fn(async () => ({ email: true, google: false, delegation: true, mailDelivery: 'development-pickup' }));
  fake.service.libraries = vi.fn(async () => actor === account ? [library] : []);
  render(<AccountPage service={fake.service} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Abrir biblioteca Privada QA' }));
  await chooseDevice(); await launch('create'); await screen.findByText('NOVA necesita tu aprobación', {}, { timeout: 12000 });
  fireEvent.click(screen.getByRole('button', { name: 'Revisar y aprobar' }));
  const oldReview = await screen.findByRole('dialog', { name: 'Guardar copia separada' });
  const ports = fake.ports.at(-1)!; const id = fake.id();
  checking = true; fireEvent.focus(window);
  await waitFor(() => expect(confirmIdentity).toBeTypeOf('function'));
  expect(oldReview.isConnected).toBe(true);
  expect(screen.queryByRole('button', { name: 'Aprobar contenido y guardar esta copia' })).toBeNull();
  expect(screen.queryByRole('textbox', { name: 'Contenido' })).toBeNull();
  expect(screen.queryByRole('complementary', { name: 'Control del agente' })).toBeNull();
  await act(async () => { actor = other; checking = false; confirmIdentity(); });
  await screen.findByText('b@example.invalid');
  expect(oldReview.isConnected).toBe(false);
  expect(ports.visibleSession.surfaceCapability(id)).toBeUndefined();
  expect(getActivity(id)).toEqual([]);
  expect(fake.calls.some(call => /\/(approve|commit)$/.test(call.path))).toBe(false);
  expect(fake.saved).toEqual([]);
}, 20000);
