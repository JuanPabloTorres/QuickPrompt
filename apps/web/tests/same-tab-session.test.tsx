import { act, cleanup, render, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { SameTabAgent } from '../src/features/nova/SameTabAgent';
import { createAgentProposalSession } from '../src/features/nova/AgentProposalSession';
import { AgentUiProvider } from '../src/features/nova/AgentUiContext';
import { releaseSurface, startVisible, surfaceCapability } from '../src/services/live';
import { UiPortalScopeProvider } from '../src/components/ui/UiPortalScope';
import { AgentUiDriver, readUiState } from '../src/features/nova/AgentUiDriver';

const testRuns = new Set<string>();
const trackRun = (event: Event) => testRuns.add((event as CustomEvent<string>).detail);
beforeEach(() => window.addEventListener('nova-agent-started', trackRun));
afterEach(() => { cleanup(); window.removeEventListener('nova-agent-started', trackRun); testRuns.forEach(id => releaseSurface(id)); testRuns.clear(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });
const record = (id: string, status = 'CREATED') => ({ runId: id, sessionId: id, mode: 'SAME_TAB', status, sequence: 1, targetId: null, revision: null, pauseRequested: false, browserOpen: false, approval: 'NONE', effectCommitted: false, errorCode: null });
const startAction = (id: string) => ({ runId: id, actionId: crypto.randomUUID(), sequence: 1, tool: 'start', target: null, arguments: {}, timestamp: new Date().toISOString(), executionStatus: 'DISPATCHED', verificationStatus: 'PENDING' });

it('releases local command ownership on Stop and ignores a late authenticated action', async () => {
  let id = ''; let resolvePoll: (response: Response) => void = () => {};
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    if (path === '/api/v1/nova/live') { id = JSON.parse(options!.body as string).runId; return Response.json(record(id)); }
    if (path.includes('/actions?')) return new Promise<Response>(resolve => { resolvePoll = resolve; });
    throw Error('Late action must never execute or acknowledge: ' + path);
  }));
  await startVisible('Prueba de cancelación', crypto.randomUUID());
  const active = vi.fn();
  render(<><section data-testid="prompt-editor"><input id="prompt-title" defaultValue="Original" /><textarea id="prompt-content" defaultValue="Original." /></section><SameTabAgent runId={id} onActive={active} /></>);
  await waitFor(() => expect(active).toHaveBeenLastCalledWith(true));
  act(() => window.dispatchEvent(new CustomEvent('nova-ui-control', { detail: 'cancel' })));
  expect(active).toHaveBeenLastCalledWith(false);
  expect(surfaceCapability(id)).toBeUndefined();
  await act(async () => resolvePoll(Response.json({ run: record(id, 'TOOL_RUNNING'), action: { runId: id, actionId: crypto.randomUUID(), sequence: 1, tool: 'ui_prepare_creation', target: null, arguments: { title: 'No escribir', templateText: 'No escribir.' }, timestamp: new Date().toISOString(), executionStatus: 'DISPATCHED', verificationStatus: 'PENDING' } })));
  expect(document.getElementById('prompt-title')).toHaveProperty('value', 'Original');
  expect(active).toHaveBeenLastCalledWith(false);
});

it('binds an explicit source revision and exact form values to the run as well as session context', async () => {
  const calls: { path: string; body: Record<string, unknown> }[] = [];
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    const body = JSON.parse(options!.body as string); calls.push({ path, body });
    return path.endsWith('/context') ? new Response(null, { status: 204 }) : Response.json(record(body.runId));
  }));
  const source = { id: crypto.randomUUID(), revision: 4 }, values = { 'proyecto': 'Aplicación React\nDatos "literales" 李' };
  await startVisible('Completa los campos seleccionados.', crypto.randomUUID(), source, values, { bindSource: true });
  expect(calls.find(call => call.path.endsWith('/context'))?.body).toMatchObject({ promptId: source.id, revision: 4, values });
  expect(calls.find(call => call.path.endsWith('/nova/live'))?.body).toMatchObject({ sourceId: source.id, sourceRevision: 4, values });
});

it('keeps ordinary selection as context rather than binding a free-text request to the previously viewed prompt', async () => {
  const calls: { path: string; body: Record<string, unknown> }[] = [];
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    const body = JSON.parse(options!.body as string); calls.push({ path, body });
    return path.endsWith('/context') ? new Response(null, { status: 204 }) : Response.json(record(body.runId));
  }));
  const source = { id: crypto.randomUUID(), revision: 2 };
  await startVisible('Abre otro prompt de mi biblioteca.', crypto.randomUUID(), source, { tema: 'Anterior' });
  expect(calls.find(call => call.path.endsWith('/context'))?.body).toMatchObject({ promptId: source.id, revision: 2 });
  const run = calls.find(call => call.path.endsWith('/nova/live'))!.body;
  expect(run.sourceId).toBeUndefined(); expect(run.sourceRevision).toBeUndefined(); expect(run.values).toBeUndefined();
});

it('discards an old in-flight poll after switching runs and releases the completed document capability', async () => {
  const pending = new Map<string, (response: Response) => void>();
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    if (path === '/api/v1/nova/live') { const body = JSON.parse(options!.body as string); return Response.json(record(body.runId)); }
    if (path.includes('/actions?')) return new Promise<Response>(resolve => pending.set(path.split('/')[5], resolve));
    throw new Error('Unexpected endpoint ' + path);
  }));
  const session = crypto.randomUUID(), first = await startVisible('Primera petición', session);
  const active = vi.fn(); const proposalSession = createAgentProposalSession();
  const agent = (runId: string) => <AgentUiProvider session={proposalSession}><SameTabAgent runId={runId} onActive={active} /></AgentUiProvider>;
  const view = render(agent(first.runId));
  await waitFor(() => expect(pending.has(first.runId)).toBe(true));
  const second = await startVisible('Segunda petición', session); view.rerender(agent(second.runId));
  await waitFor(() => expect(pending.has(second.runId)).toBe(true));
  expect(active.mock.calls.at(-1)).toEqual([true]);
  pending.get(first.runId)!(Response.json({ run: record(first.runId, 'COMPLETED'), action: null }));
  await new Promise(resolve => setTimeout(resolve, 30)); expect(active.mock.calls.at(-1)).toEqual([true]);
  pending.get(second.runId)!(Response.json({ run: record(second.runId, 'COMPLETED'), action: null }));
  await waitFor(() => expect(active.mock.calls.at(-1)).toEqual([false]));
  expect(surfaceCapability(second.runId)).toBeUndefined();
  expect(await proposalSession.apply('Edición manual posterior.', new AbortController().signal)).toBe(false);
});

it('recovers a transient poll failure in a hidden tab without requiring Continue or replaying a write', async () => {
  vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('hidden');
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([{}] as unknown as DOMRectList);
  HTMLElement.prototype.scrollIntoView = vi.fn(); vi.stubGlobal('matchMedia', () => ({ matches: true }));
  let id = '';
  const actionId = crypto.randomUUID(), calls: string[] = [];
  let polls = 0, receipts = 0;
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    calls.push(path);
    if (path === '/api/v1/nova/live') { id = JSON.parse(options!.body as string).runId; return Response.json(record(id)); }
    if (path.endsWith('/actions/progress')) return new Response(null, { status: 204 });
    if (path.endsWith('/actions/receipt')) { receipts++; return new Response(null, { status: 204 }); }
    if (path.includes('/actions?')) {
      if (++polls === 1) throw new TypeError('Synthetic temporary disconnection');
      return Response.json({ run: record(id, receipts ? 'COMPLETED' : 'TOOL_RUNNING'), action: receipts ? null : { runId: id, actionId, sequence: 1, tool: 'ui_prepare_creation', target: null, arguments: { title: 'Primera petición', templateText: 'Una instrucción completa.' }, timestamp: new Date().toISOString(), executionStatus: 'DISPATCHED', verificationStatus: 'PENDING' } });
    }
    throw new Error('Unexpected endpoint ' + path);
  }));
  await startVisible('Crea una instrucción.', crypto.randomUUID());
  const active = vi.fn();
  render(<><section data-testid="prompt-editor" data-prompt-id=""><input id="prompt-title" defaultValue="" /><textarea id="prompt-content" defaultValue="" /><span data-testid="variables-inspected" /><span data-testid="compile-ready" /></section><SameTabAgent runId={id} speed="fast" onActive={active} /></>);
  await waitFor(() => expect(active.mock.calls.at(-1)).toEqual([false]), { timeout: 3000 });
  expect(document.getElementById('prompt-title')).toHaveProperty('value', 'Primera petición');
  expect(document.getElementById('prompt-content')).toHaveProperty('value', 'Una instrucción completa.');
  expect(receipts).toBe(1); expect(calls.some(path => path.endsWith('/pause') || path.endsWith('/resume'))).toBe(false);
});

it('remembers an SSE wake received during an in-flight poll and ignores another run', async () => {
  let id = '', polls = 0; let acceptFirst: (response: Response) => void = () => {};
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    if (path === '/api/v1/nova/live') { id = JSON.parse(options!.body as string).runId; return Response.json(record(id)); }
    if (path.includes('/actions?')) {
      polls++;
      if (polls === 1) return await new Promise<Response>(resolve => { acceptFirst = resolve; });
      return Response.json({ run: record(id), action: null });
    }
    throw Error('Unexpected endpoint');
  }));
  await startVisible('Consulta el prompt.', crypto.randomUUID());
  const rendered = render(<SameTabAgent runId={id} onActive={vi.fn()} />);
  await waitFor(() => expect(polls).toBe(1));
  await act(async () => {
    window.dispatchEvent(new CustomEvent('nova-ui-state-available', { detail: id }));
    acceptFirst(Response.json({ run: record(id), action: null }));
  });
  expect(polls).toBe(2);
  await act(async () => { window.dispatchEvent(new CustomEvent('nova-ui-state-available', { detail: crypto.randomUUID() })); });
  expect(polls).toBe(2); rendered.unmount();
});

it('does not turn a retained user pause into permission to execute after identity verification', async () => {
  let id = '', receipts = 0; let acceptPoll!: (response: Response) => void;
  const execute = vi.spyOn(AgentUiDriver.prototype, 'execute').mockImplementation(async () => readUiState());
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    if (path === '/api/v1/nova/live') { id = JSON.parse(options!.body as string).runId; return Response.json(record(id)); }
    if (path.endsWith('/actions/receipt')) { receipts++; return new Response(null, { status: 204 }); }
    if (path.includes('/actions?')) return new Promise<Response>(resolve => { acceptPoll = resolve; });
    throw Error('Unexpected endpoint');
  }));
  await startVisible('Conserva mi pausa.', crypto.randomUUID());
  const agent = (active: boolean) => <UiPortalScopeProvider value={{ active, dialogHost: null, overlayHost: null, onRetry: vi.fn() }}><SameTabAgent runId={id} onActive={vi.fn()} /></UiPortalScopeProvider>;
  const view = render(agent(true));
  await waitFor(() => expect(acceptPoll).toBeTypeOf('function'));
  const firstPoll = acceptPoll;
  act(() => window.dispatchEvent(new CustomEvent('nova-ui-control', { detail: 'pause' })));
  view.rerender(agent(false));
  await act(async () => acceptPoll(Response.json({ run: record(id), action: startAction(id) })));
  view.rerender(agent(true));
  expect(execute).not.toHaveBeenCalled(); expect(receipts).toBe(0);
  await waitFor(() => expect(acceptPoll).not.toBe(firstPoll));
  act(() => window.dispatchEvent(new CustomEvent('nova-ui-control', { detail: 'resume' })));
  await act(async () => acceptPoll(Response.json({ run: record(id), action: startAction(id) })));
  await waitFor(() => expect(receipts).toBe(1)); expect(execute).toHaveBeenCalledTimes(1);
});

it.each([false, true])('holds a completed UI receipt during verification and drops it if its scope retires (%s)', async retire => {
  let id = '', receipts = 0; let finish!: () => void;
  vi.spyOn(AgentUiDriver.prototype, 'execute').mockImplementation(() => new Promise(resolve => { finish = () => resolve(readUiState()); }));
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    if (path === '/api/v1/nova/live') { id = JSON.parse(options!.body as string).runId; return Response.json(record(id)); }
    if (path.endsWith('/actions/receipt')) { receipts++; return new Response(null, { status: 204 }); }
    if (path.includes('/actions?')) return Response.json({ run: record(id), action: startAction(id) });
    throw Error('Unexpected endpoint');
  }));
  await startVisible('Valida antes del recibo.', crypto.randomUUID());
  const agent = (active: boolean) => <UiPortalScopeProvider value={{ active, dialogHost: null, overlayHost: null, onRetry: vi.fn() }}><SameTabAgent runId={id} onActive={vi.fn()} /></UiPortalScopeProvider>;
  const view = render(agent(true));
  await waitFor(() => expect(finish).toBeTypeOf('function'));
  view.rerender(agent(false));
  await act(async () => { finish(); await new Promise(resolve => setTimeout(resolve, 60)); });
  expect(receipts).toBe(0);
  if (retire) view.unmount(); else view.rerender(agent(true));
  await act(async () => { await new Promise(resolve => setTimeout(resolve, 60)); });
  expect(receipts).toBe(retire ? 0 : 1);
});
