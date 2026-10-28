import { afterEach, expect, it, vi } from 'vitest';
import { createApiClient, type HttpTransport } from '../src/services/api';
import { createApplicationServices } from '../src/services/application';

const session = '10000000-0000-4000-8000-000000000001';
const record = (runId: string, mode = 'SAME_TAB') => ({ runId, sessionId: session, mode, status: 'CREATED', sequence: 0,
  targetId: null, revision: null, pauseRequested: false, browserOpen: false, approval: 'NONE', effectCommitted: false, errorCode: null });
afterEach(() => { sessionStorage.clear(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

it('composes every connection port over the supplied API without global fetch', async () => {
  const globalFetch = vi.fn(() => { throw Error('Global transport forbidden'); }); vi.stubGlobal('fetch', globalFetch);
  const send = vi.fn<HttpTransport['send']>().mockImplementation(async path => {
    if (path.includes('/prompts?')) return Response.json({ items: [], total: 0 });
    if (path.endsWith('/categories')) return Response.json([{ id: 0, name: 'General' }]);
    if (path.endsWith('/groups') || path.endsWith('/nova/drafts') || path.endsWith('/providers')) return Response.json([]);
    if (path.endsWith('/nova/preferences')) return Response.json({ language: null, format: null, providerId: null });
    if (path.endsWith('/library/export')) return Response.json({ synthetic: true });
    if (path.endsWith('/nova/live/' + session)) return Response.json({ run: record(session) });
    throw Error('Unexpected route ' + path);
  });
  const services = createApplicationServices(createApiClient({ send })); const signal = new AbortController().signal;
  await Promise.all([services.prompts.list({ search: '', filter: 'all', groupId: '', page: 0 }, signal),
    services.templates.categories(signal), services.collections.list(signal), services.archives.export(signal),
    services.preparation.list(signal), services.providers.list(signal), services.preferences.get(signal), services.live.record(session, signal)]);
  expect(send).toHaveBeenCalledTimes(8); expect(globalFetch).not.toHaveBeenCalled();
  expect(send.mock.calls.every(([, options]) => options.signal === signal && new Headers(options.headers).get('X-QuickPrompt-Client') === 'nova-v1')).toBe(true);
});

it('keeps capabilities and release independent even when two compositions receive the same run ID', async () => {
  const runId = '20000000-0000-4000-8000-000000000001';
  const ids = [runId, '30000000-0000-4000-8000-000000000001', '30000000-0000-4000-8000-000000000002',
    runId, '40000000-0000-4000-8000-000000000001', '40000000-0000-4000-8000-000000000002'];
  vi.spyOn(crypto, 'randomUUID').mockImplementation(() => ids.shift()! as ReturnType<Crypto['randomUUID']>);
  const firstSend = vi.fn<HttpTransport['send']>().mockResolvedValue(Response.json(record(runId)));
  const secondSend = vi.fn<HttpTransport['send']>().mockResolvedValue(Response.json(record(runId)));
  const first = createApplicationServices(createApiClient({ send: firstSend }));
  const second = createApplicationServices(createApiClient({ send: secondSend }));
  await first.visibleSession.startVisible('Primera tarea sintética.', session);
  await second.visibleSession.startVisible('Segunda tarea sintética.', session);
  const firstCapability = first.visibleSession.surfaceCapability(runId), secondCapability = second.visibleSession.surfaceCapability(runId);
  expect(firstCapability).toBeTruthy(); expect(secondCapability).toBeTruthy(); expect(firstCapability).not.toBe(secondCapability);
  expect(new Headers(firstSend.mock.calls[0][1].headers).get('X-Nova-Surface')).toBe(firstCapability);
  expect(new Headers(secondSend.mock.calls[0][1].headers).get('X-Nova-Surface')).toBe(secondCapability);
  first.visibleSession.releaseSurface(runId); expect(first.visibleSession.surfaceCapability(runId)).toBeUndefined();
  expect(second.visibleSession.surfaceCapability(runId)).toBe(secondCapability);
  expect(sessionStorage.getItem('nova-live-run')).toBe(runId);
});

it.each([500, 503])('uses its own receipt lookup after HTTP %s and releases an authoritatively absent run', async status => {
  let id = ''; const send = vi.fn<HttpTransport['send']>().mockImplementation(async (_path, options) => {
    if (options.method === 'POST') { id = JSON.parse(options.body as string).runId; return Response.json({ detail: 'Synthetic failure' }, { status }); }
    return Response.json({}, { status: 404 });
  });
  const services = createApplicationServices(createApiClient({ send }));
  await expect(services.visibleSession.startVisible('Solicitud sintética.', session)).rejects.toMatchObject({ status });
  expect(services.visibleSession.surfaceCapability(id)).toBeUndefined(); expect(send).toHaveBeenCalledTimes(2);
  expect(send.mock.calls.filter(([, options]) => options.method === 'POST')).toHaveLength(1);
});

it('preserves uncertain identity in its own registry without retrying a lost POST', async () => {
  let id = ''; const send = vi.fn<HttpTransport['send']>().mockImplementation(async (_path, options) => {
    if (options.method === 'POST') { id = JSON.parse(options.body as string).runId; throw new TypeError('Synthetic lost response'); }
    return Response.json({}, { status: 404 });
  });
  const services = createApplicationServices(createApiClient({ send }));
  await expect(services.visibleSession.startVisible('Solicitud sintética.', session)).rejects.toThrow('La conexión no confirmó el inicio.');
  expect(services.visibleSession.surfaceCapability(id)).toBeTruthy(); expect(sessionStorage.getItem('nova-live-run')).toBe(id);
  expect(send).toHaveBeenCalledTimes(2); expect(send.mock.calls.filter(([, options]) => options.method === 'POST')).toHaveLength(1);
});
