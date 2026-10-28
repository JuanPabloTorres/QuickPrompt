import { z } from 'zod';
import type { ApiClient } from './api';
import { ApiError } from './api';
import { createLiveClient, createVisibleSessionClient, type LiveEvents, type VisibleSessionClient } from './live';

export type AccountNovaScope = Readonly<{ accountId: string; libraryId: string; connectionId: string }>;

/** Binds transport/storage once in trusted UI composition. No local API fallback or actor field in requests. */
export function createAccountNovaServices(client: Pick<ApiClient, 'request'>, csrf: (signal?: AbortSignal) => Promise<string>,
  selected: AccountNovaScope, eventSource: (url: string) => LiveEvents = url => new EventSource(url), onExpired?: () => void) {
  const uuid = z.string().uuid();
  const scope = Object.freeze({ accountId: uuid.parse(selected.accountId), libraryId: uuid.parse(selected.libraryId), connectionId: uuid.parse(selected.connectionId) });
  const prefix = `/api/accounts/v1/libraries/${scope.libraryId}`;
  const storageKey = `quickprompt:${scope.accountId}:${scope.libraryId}:${scope.connectionId}:nova-run`;
  let visibleSession: VisibleSessionClient;
  const scoped: Pick<ApiClient, 'request'> = {
    async request<T>(path: string, schema: z.ZodType<T>, options: RequestInit = {}): Promise<T> {
      if (!path.startsWith('/nova/') || path.includes('..') || path.includes('\\') || path.includes('#')) throw new Error('Ruta NOVA inválida.');
      const headers = new Headers(options.headers);
      const segments = path.split('?')[0].split('/');
      const id = segments[2] === 'live' ? segments[3] : undefined;
      const capability = id ? visibleSession.surfaceCapability(uuid.parse(id)) : undefined;
      if (capability) headers.set('X-Nova-Surface', capability);
      if ((options.method ?? 'GET').toUpperCase() !== 'GET') headers.set('X-QP-CSRF', await csrf(options.signal ?? undefined));
      const body = path === '/nova/live' ? JSON.stringify({ connectionId: scope.connectionId, request: JSON.parse(String(options.body)) }) : options.body;
      try { return await client.request(prefix + path, schema, { ...options, headers, body, credentials: 'same-origin' }); }
      catch (failure) { if (failure instanceof ApiError && failure.status === 401) onExpired?.(); throw failure; }
    },
  };
  const live = createLiveClient(scoped, url => {
    if (!url.startsWith('/api/v1/nova/live/') || url.includes('..') || url.includes('\\')) throw new Error('Ruta de actividad NOVA inválida.');
    return eventSource(prefix + url.slice('/api/v1'.length));
  });
  visibleSession = createVisibleSessionClient(live, storageKey);
  return Object.freeze({ live, visibleSession, storageKey, scope });
}
