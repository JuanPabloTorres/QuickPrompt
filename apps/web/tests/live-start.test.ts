import { afterEach, expect, it, vi } from 'vitest';
import { ApiError } from '../src/services/api';
import { releaseSurface, startVisible, surfaceCapability } from '../src/services/live';

const ids: string[] = [];
afterEach(() => { ids.splice(0).forEach(releaseSurface); sessionStorage.clear(); vi.unstubAllGlobals(); });
const record = (runId: string, sessionId: string) => ({ runId, sessionId, mode: 'SAME_TAB', status: 'CREATED', sequence: 0,
  targetId: null, revision: null, pauseRequested: false, browserOpen: false, approval: 'NONE', effectCommitted: false, errorCode: null });

it.each([500, 503])('does not publish a phantom run after HTTP %s and authoritative absence', async status => {
  const history = crypto.randomUUID(); sessionStorage.setItem('nova-live-run', history);
  const uncertain = vi.fn(), started = vi.fn();
  window.addEventListener('nova-uncertain-start', uncertain); window.addEventListener('nova-agent-started', started);
  const transport = vi.fn(async (_path: string, options: RequestInit) => {
    if (options.method === 'POST') { ids.push(JSON.parse(options.body as string).runId); return Response.json({ detail: 'Capacidad no disponible.' }, { status }); }
    return Response.json({ detail: 'No existe la ejecución.' }, { status: 404 });
  });
  vi.stubGlobal('fetch', transport);
  try {
    const error = await startVisible('Petición sintética.', crypto.randomUUID()).catch(error => error);
    expect(error).toBeInstanceOf(ApiError); expect(error.status).toBe(status); expect(error.message).toBe('Capacidad no disponible.');
    expect(surfaceCapability(ids[0])).toBeUndefined(); expect(sessionStorage.getItem('nova-live-run')).toBe(history);
    expect(uncertain).not.toHaveBeenCalled(); expect(started).not.toHaveBeenCalled();
    expect(transport).toHaveBeenCalledTimes(2);
  } finally { window.removeEventListener('nova-uncertain-start', uncertain); window.removeEventListener('nova-agent-started', started); }
});

it('retains identity after a lost POST response even if the immediate lookup is absent', async () => {
  const uncertain = vi.fn(); window.addEventListener('nova-uncertain-start', uncertain);
  const transport = vi.fn(async (_path: string, options: RequestInit) => {
    if (options.method === 'POST') { ids.push(JSON.parse(options.body as string).runId); throw new TypeError('Connection lost'); }
    return Response.json({}, { status: 404 });
  }); vi.stubGlobal('fetch', transport);
  try {
    await expect(startVisible('Petición sintética.', crypto.randomUUID())).rejects.toThrow('La conexión no confirmó el inicio.');
    expect(uncertain).toHaveBeenCalledTimes(1); expect(sessionStorage.getItem('nova-live-run')).toBe(ids[0]);
    expect(surfaceCapability(ids[0])).toBeTruthy(); expect(transport).toHaveBeenCalledTimes(2);
  } finally { window.removeEventListener('nova-uncertain-start', uncertain); }
});

it('recovers the existing receipt after a server error without creating another run', async () => {
  const session = crypto.randomUUID(), started = vi.fn(); window.addEventListener('nova-agent-started', started);
  const transport = vi.fn(async (_path: string, options: RequestInit) => {
    if (options.method === 'POST') { ids.push(JSON.parse(options.body as string).runId); return Response.json({}, { status: 503 }); }
    return Response.json({ run: record(ids[0], session) });
  }); vi.stubGlobal('fetch', transport);
  try {
    const run = await startVisible('Petición sintética.', session);
    expect(run.runId).toBe(ids[0]); expect(started).toHaveBeenCalledTimes(1); expect(surfaceCapability(run.runId)).toBeTruthy();
    expect(transport).toHaveBeenCalledTimes(2); expect(transport.mock.calls.filter(([, options]) => options.method === 'POST')).toHaveLength(1);
  } finally { window.removeEventListener('nova-agent-started', started); }
});

it('retains uncertain identity when the receipt service is also unavailable', async () => {
  const transport = vi.fn(async (_path: string, options: RequestInit) => {
    if (options.method === 'POST') ids.push(JSON.parse(options.body as string).runId);
    return Response.json({}, { status: 503 });
  }); vi.stubGlobal('fetch', transport);
  await expect(startVisible('Petición sintética.', crypto.randomUUID())).rejects.toThrow('La conexión no confirmó el inicio.');
  expect(sessionStorage.getItem('nova-live-run')).toBe(ids[0]); expect(surfaceCapability(ids[0])).toBeTruthy(); expect(transport).toHaveBeenCalledTimes(2);
});
