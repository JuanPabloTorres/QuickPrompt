import { cleanup, render, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { SameTabAgent } from '../src/features/nova/SameTabAgent';
import { applyAgentProposal } from '../src/features/nova/AgentUiDriver';
import { startVisible, surfaceCapability } from '../src/services/live';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
const record = (id: string, status = 'CREATED') => ({ runId: id, sessionId: id, mode: 'SAME_TAB', status, sequence: 1, targetId: null, revision: null, pauseRequested: false, browserOpen: false, approval: 'NONE', effectCommitted: false, errorCode: null });

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
  const active = vi.fn(); const view = render(<SameTabAgent runId={first.runId} onActive={active} />);
  await waitFor(() => expect(pending.has(first.runId)).toBe(true));
  const second = await startVisible('Segunda petición', session); view.rerender(<SameTabAgent runId={second.runId} onActive={active} />);
  await waitFor(() => expect(pending.has(second.runId)).toBe(true));
  expect(active.mock.calls.at(-1)).toEqual([true]);
  pending.get(first.runId)!(Response.json({ run: record(first.runId, 'COMPLETED'), action: null }));
  await new Promise(resolve => setTimeout(resolve, 30)); expect(active.mock.calls.at(-1)).toEqual([true]);
  pending.get(second.runId)!(Response.json({ run: record(second.runId, 'COMPLETED'), action: null }));
  await waitFor(() => expect(active.mock.calls.at(-1)).toEqual([false]));
  expect(surfaceCapability(second.runId)).toBeUndefined();
  expect(await applyAgentProposal('Edición manual posterior.', new AbortController().signal)).toBe(false);
});
