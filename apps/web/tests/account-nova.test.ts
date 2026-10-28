import { afterEach, expect, it, vi } from 'vitest';
import { createAccountNovaServices } from '../src/services/accountNova';
import { createApiClient } from '../src/services/api';
import type { LiveEvents } from '../src/services/live';

const a = '10000000-0000-4000-8000-000000000001';
const b = '10000000-0000-4000-8000-000000000002';
const c = '10000000-0000-4000-8000-000000000003';
const record = (runId: string, sessionId = a) => ({ runId, sessionId, mode: 'SAME_TAB', status: 'CREATED', sequence: 0,
  targetId: null, revision: null, pauseRequested: false, browserOpen: true, approval: 'NONE', effectCommitted: false, errorCode: null });
function events(): LiveEvents { return { addEventListener: vi.fn(), onopen: null, onerror: null, close: vi.fn() }; }
afterEach(() => { sessionStorage.clear(); vi.restoreAllMocks(); });

it('binds native actions, stream and run storage to one account/library/connection without local fallback', async () => {
  const calls: { path: string; options: RequestInit }[] = []; const starts: string[] = [];
  const client = createApiClient({ send: vi.fn(async (path, options) => {
    calls.push({ path, options });
    if (path.endsWith('/sessions/' + a + '/context')) return new Response(null, { status: 204 });
    if (path.endsWith('/nova/live')) { const body = JSON.parse(String(options.body)); starts.push(body.request.runId); return Response.json(record(body.request.runId)); }
    return Response.json(record(starts[0]));
  }) }, '');
  const csrf = vi.fn(async () => 'synthetic-csrf'); const stream = vi.fn(events);
  const services = createAccountNovaServices(client, csrf, { accountId: a, libraryId: b, connectionId: c }, stream);
  const run = await services.visibleSession.startVisible('Abre este prompt.', a, { id: c, revision: 2 }, {}, { bindSource: true, command: { operation: 'OPEN_PROMPT', content: '' } });
  await services.live.control(run.runId, 'pause');
  const approval = { approvalId: c, hash: 'synthetic-proposal', revision: 2, confirmed: true, contentReviewed: true,
    contentReviewHash: 'synthetic-contract', approvedLiteralChangeIds: ['literal-change-a', 'literal-change-b'] };
  await services.live.approve(run.runId, approval);
  services.live.events(run.runId, 9);
  expect(calls.every(call => call.path.startsWith(`/api/accounts/v1/libraries/${b}/nova/`))).toBe(true);
  expect(calls.every(call => call.options.credentials === 'same-origin')).toBe(true);
  expect(calls.every(call => new Headers(call.options.headers).get('X-QP-CSRF') === 'synthetic-csrf')).toBe(true);
  const body = JSON.parse(String(calls[1].options.body));
  expect(body.connectionId).toBe(c); expect(body.accountId).toBeUndefined(); expect(body.request.command.operation).toBe('OPEN_PROMPT');
  const surface = new Headers(calls[1].options.headers).get('X-Nova-Surface'); expect(surface).toHaveLength(72);
  expect(new Headers(calls[2].options.headers).get('X-Nova-Surface')).toBe(surface);
  expect(new Headers(calls[3].options.headers).get('X-Nova-Surface')).toBe(surface);
  expect(JSON.parse(String(calls[3].options.body))).toEqual(approval);
  expect(stream).toHaveBeenCalledWith(`/api/accounts/v1/libraries/${b}/nova/live/${run.runId}/events?after=9`);
  expect(sessionStorage.getItem(services.storageKey)).toBe(run.runId); expect(sessionStorage.getItem('nova-live-run')).toBeNull();
  const other = createAccountNovaServices(client, csrf, { accountId: b, libraryId: b, connectionId: c }, stream);
  expect(other.storageKey).not.toBe(services.storageKey); expect(other.visibleSession.surfaceCapability(run.runId)).toBeUndefined();
  services.visibleSession.releaseSurface(run.runId); expect(services.visibleSession.surfaceCapability(run.runId)).toBeUndefined();
});

it('recovers an uncertain start through the same private receipt without issuing a second start', async () => {
  let id = ''; let starts = 0; const paths: string[] = [];
  const client = createApiClient({ send: async (path, options) => {
    paths.push(path);
    if (options.method === 'POST') { starts++; id = JSON.parse(String(options.body)).request.runId; throw new TypeError('Respuesta perdida'); }
    return Response.json({ run: record(id) });
  } }, '');
  const services = createAccountNovaServices(client, async () => 'synthetic', { accountId: a, libraryId: b, connectionId: c });
  const run = await services.visibleSession.startVisible('Crea un prompt.', a, undefined, undefined,
    { bindSource: false, command: { operation: 'CREATE_DRAFT', content: 'Idea', inputNames: ['tema'] } });
  expect(run.runId).toBe(id); expect(starts).toBe(1); expect(paths[1]).toBe(`/api/accounts/v1/libraries/${b}/nova/live/${id}`);
  expect(services.visibleSession.surfaceCapability(id)).toBeTruthy();
});

it('rejects an invalid scope before any network or browser storage operation', () => {
  const request = vi.fn();
  expect(() => createAccountNovaServices({ request }, async () => 'synthetic', { accountId: 'otro', libraryId: b, connectionId: c })).toThrow();
  expect(request).not.toHaveBeenCalled(); expect(sessionStorage.length).toBe(0);
});

it.each([401, 503])('returns an expired identity to account login without treating a transient error as logout (%s)', async status => {
  const expired = vi.fn();
  const client = createApiClient({ send: async () => Response.json({ title: 'Acceso no disponible' }, { status }) }, '');
  const services = createAccountNovaServices(client, async () => 'synthetic', { accountId: a, libraryId: b, connectionId: c }, undefined, expired);
  await expect(services.live.get(a)).rejects.toMatchObject({ status });
  expect(expired).toHaveBeenCalledTimes(status === 401 ? 1 : 0);
});
