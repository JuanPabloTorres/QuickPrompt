import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { AccountPromptLibrary } from '../src/features/accounts/AccountPromptLibrary';
import { createAccountService, type AccountCatalogQuery, type AccountPrompt } from '../src/services/accounts';
import { createApiClient } from '../src/services/api';

const libraryId = '20000000-0000-4000-8000-000000000001';
const otherLibrary = '20000000-0000-4000-8000-000000000002';
const library = { id: libraryId, ownerId: libraryId, name: 'Catálogo privado', createdAt: '' };
const root = `/api/accounts/v1/libraries/${libraryId}`;
const prompt = (index: number): AccountPrompt => ({ id: `30000000-0000-4000-8000-${String(index).padStart(12, '0')}`, libraryId,
  title: `Prompt ${index}`, template: `Explica <tema> ${index}.`, revision: 1, description: `Ejemplo ${index}`, categoryId: 0,
  isFavorite: false, isArchived: false, uses: 0, lastUsedAt: null, createdAt: '', updatedAt: '', variableNames: ['tema'] });
const query: AccountCatalogQuery = { search: '', filter: 'all', page: 0, size: 12, sort: 'updated' };
afterEach(cleanup);

/** HTTP replies exercise the real client and React consumers; matching is not a backend proof. */
function fixture(count = 1) {
  let items = Array.from({ length: count }, (_, index) => prompt(index + 1));
  let deletionFailure = 0;
  let csrf = 0;
  const calls: { path: string; method: string; body?: Record<string, unknown>; csrf: string | null }[] = [];
  const send = vi.fn(async (path: string, options?: RequestInit) => {
    const method = options?.method ?? 'GET';
    const body = options?.body ? JSON.parse(String(options.body)) as Record<string, unknown> : undefined;
    calls.push({ path, method, body, csrf: new Headers(options?.headers).get('X-QP-CSRF') });
    if (path === '/account/csrf') return Response.json({ token: `csrf-${++csrf}` });
    if (path.startsWith(`${root}/prompts/catalog?`)) {
      const url = new URL(path, 'https://fixture.invalid');
      const filter = url.searchParams.get('filter');
      const selected = items.filter(item => filter === 'archived' ? item.isArchived : !item.isArchived)
        .filter(item => filter !== 'favorites' || item.isFavorite)
        .filter(item => !url.searchParams.get('search') || item.title.includes(url.searchParams.get('search')!));
      const page = Number(url.searchParams.get('page')), size = Number(url.searchParams.get('size'));
      return Response.json({ items: selected.slice(page * size, (page + 1) * size), total: selected.length, page, size });
    }
    if (path === `${root}/groups` || path === `${root}/nova/editor-contexts` || path === `${root}/executions`) return Response.json([]);
    const match = path.match(/\/prompts\/([^/]+)(?:\/(favorite|archive|restore|duplicate))?$/);
    if (match) {
      const source = items.find(item => item.id === match[1]);
      if (!source) return Response.json({ detail: 'No disponible.' }, { status: 404 });
      if (method === 'GET') return Response.json(source);
      if (method === 'DELETE') {
        if (deletionFailure) return Response.json({ detail: deletionFailure === 409 ? 'La revisión cambió o hay trabajo activo.' : 'Conexión no disponible.' }, { status: deletionFailure });
        items = items.filter(item => item.id !== source.id); return new Response(null, { status: 204 });
      }
      const changed = { ...source, revision: source.revision + 1 };
      if (match[2] === 'favorite') changed.isFavorite = !source.isFavorite;
      if (match[2] === 'archive' || match[2] === 'restore') changed.isArchived = match[2] === 'archive';
      if (match[2] === 'duplicate') { changed.id = prompt(99).id; changed.title = 'Copia de Prompt 1'; }
      items = [changed, ...items.filter(item => item.id !== changed.id)];
      return Response.json(changed);
    }
    throw new Error(`Unexpected catalog fixture request: ${path}`);
  });
  const service = createAccountService(createApiClient({ send }, ''));
  service.watchExecutions = vi.fn(async (_id, observe) => { observe([]); });
  service.rememberedContexts = vi.fn(async () => []);
  return { service, calls, failDeletion: (status: number) => { deletionFailure = status; } };
}

it('serializes the private catalog and each action with revision and fresh CSRF, without a client actor', async () => {
  const fake = fixture(); const source = prompt(1);
  const result = await fake.service.catalog(libraryId, { ...query, search: 'Prompt 1', collectionId: otherLibrary, ungrouped: true, sort: 'title' });
  expect(result.items[0]).toMatchObject({ variableNames: ['tema'], description: 'Ejemplo 1' });
  const parameters = new URL(fake.calls[0].path, 'https://fixture.invalid').searchParams;
  expect(Object.fromEntries(parameters)).toEqual({ search: 'Prompt 1', filter: 'all', page: '0', size: '12', sort: 'title', collectionId: otherLibrary, ungrouped: 'true' });
  await fake.service.actPrompt(source, 'favorite');
  await fake.service.actPrompt(source, 'duplicate');
  await fake.service.deletePrompt(source, true);
  const writes = fake.calls.filter(call => call.method !== 'GET');
  expect(writes.map(call => call.csrf)).toEqual(['csrf-1', 'csrf-2', 'csrf-3']);
  expect(writes.map(call => call.body)).toEqual([{ revision: 1 }, { revision: 1 }, { revision: 1, confirmed: true }]);
  expect(writes[2].method).toBe('DELETE');
  expect(fake.calls.every(call => call.path.startsWith('/account/') || call.path.startsWith(root))).toBe(true);
});

it('rejects catalog content from another library and an action result for another resource', async () => {
  const foreign = { ...prompt(1), libraryId: otherLibrary };
  const client = createAccountService(createApiClient({ send: async path => path === '/account/csrf' ? Response.json({ token: 'csrf' }) :
    path.includes('/catalog?') ? Response.json({ items: [foreign], total: 1, page: 0, size: 12 }) : Response.json(foreign) }, ''));
  await expect(client.catalog(libraryId, query)).rejects.toThrow('El catálogo no corresponde');
  await expect(client.actPrompt(prompt(1), 'favorite')).rejects.toThrow('La respuesta no corresponde');
  const duplicated = createAccountService(createApiClient({ send: async () => Response.json({ items: [prompt(1), prompt(1)], total: 2, page: 0, size: 12 }) }, ''));
  await expect(duplicated.catalog(libraryId, query)).rejects.toThrow('El catálogo no corresponde');
});

it('queries beyond the first twelve items, resets paging for search and switches shared row/card presentation', async () => {
  const fake = fixture(13);
  render(<AccountPromptLibrary library={library} service={fake.service} onExpired={vi.fn()} />);
  await screen.findByRole('button', { name: 'Prompt 1' });
  expect(screen.queryByRole('button', { name: 'Prompt 13' })).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Página siguiente' }));
  await screen.findByRole('button', { name: 'Prompt 13' });
  expect(new URL(fake.calls.filter(call => call.path.includes('/catalog?')).at(-1)!.path, 'https://fixture.invalid').searchParams.get('page')).toBe('1');
  fireEvent.change(screen.getByLabelText('Buscar en la biblioteca'), { target: { value: 'Prompt 2' } });
  await screen.findByRole('button', { name: 'Prompt 2' });
  const last = new URL(fake.calls.filter(call => call.path.includes('/catalog?')).at(-1)!.path, 'https://fixture.invalid');
  expect(last.searchParams.get('page')).toBe('0'); expect(last.searchParams.get('search')).toBe('Prompt 2');
  fireEvent.click(screen.getByRole('button', { name: 'Ver filas' }));
  expect(screen.getByRole('list', { name: 'Prompts guardados' }).getAttribute('data-view')).toBe('rows');
  fireEvent.click(screen.getByRole('button', { name: 'Ver tarjetas' }));
  expect(screen.getByRole('list', { name: 'Prompts guardados' }).getAttribute('data-view')).toBe('cards');
});

it('updates favorites, archives, restores and duplicates through the real revision-bound action client', async () => {
  const fake = fixture();
  render(<AccountPromptLibrary library={library} service={fake.service} onExpired={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Añadir a favoritos: Prompt 1' }));
  await screen.findByRole('button', { name: 'Quitar de favoritos: Prompt 1' });
  fireEvent.click(screen.getByRole('button', { name: 'Archivar: Prompt 1' }));
  await screen.findByText('Guarda tu primera instrucción.');
  fireEvent.click(screen.getByRole('button', { name: 'Archivo' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Restaurar: Prompt 1' }));
  await screen.findByText('No hay prompts en esta selección.');
  fireEvent.click(screen.getByRole('button', { name: 'Todos' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Duplicar: Prompt 1' }));
  await screen.findByRole('button', { name: 'Copia de Prompt 1' });
  expect(fake.calls.filter(call => call.method === 'POST').map(call => call.body)).toEqual([{ revision: 1 }, { revision: 2 }, { revision: 3 }, { revision: 4 }]);
});

it('deletes only after explicit confirmation and cancellation preserves the original prompt', async () => {
  const fake = fixture();
  render(<AccountPromptLibrary library={library} service={fake.service} onExpired={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Eliminar: Prompt 1' }));
  expect(screen.getByRole('button', { name: 'Eliminar definitivamente' })).toHaveProperty('disabled', true);
  fireEvent.click(screen.getByRole('button', { name: 'Conservar prompt' }));
  expect(fake.calls.filter(call => call.method === 'DELETE')).toHaveLength(0);
  fireEvent.click(screen.getByRole('button', { name: 'Eliminar: Prompt 1' }));
  fireEvent.click(screen.getByLabelText('Confirmo eliminar este prompt y sus versiones.'));
  fireEvent.click(screen.getByRole('button', { name: 'Eliminar definitivamente' }));
  await screen.findByText('Guarda tu primera instrucción.');
  expect(fake.calls.filter(call => call.method === 'DELETE').map(call => call.body)).toEqual([{ revision: 1, confirmed: true }]);
});

it.each([409, 503])('preserves the original resource after deletion failure %s without automatic retry or success', async status => {
  const fake = fixture(); fake.failDeletion(status);
  render(<AccountPromptLibrary library={library} service={fake.service} onExpired={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Eliminar: Prompt 1' }));
  fireEvent.click(screen.getByLabelText('Confirmo eliminar este prompt y sus versiones.'));
  fireEvent.click(screen.getByRole('button', { name: 'Eliminar definitivamente' }));
  const dialog = screen.getByRole('dialog');
  expect((await within(dialog).findByRole('alert')).textContent).toContain('No se ha confirmado la eliminación.');
  expect(document.querySelector('[data-testid="library-prompt"]')?.getAttribute('data-revision')).toBe('1');
  expect(fake.calls.filter(call => call.method === 'DELETE')).toHaveLength(1);
  if (status === 409) expect(within(dialog).queryByRole('button', { name: 'Eliminar definitivamente' })).toBeNull();
  fireEvent.click(within(dialog).getByRole('button', { name: 'Conservar prompt' }));
  expect(screen.getByRole('button', { name: 'Prompt 1' })).toBeTruthy();
});

it('discards a late catalog response when the library context changes', async () => {
  let finish!: (value: Response) => void;
  const old = new Promise<Response>(resolve => { finish = resolve; });
  const client = createAccountService(createApiClient({ send: async path => {
    if (path.includes(`/libraries/${libraryId}/prompts/catalog?`)) return old;
    if (path.includes('/prompts/catalog?')) return Response.json({ items: [{ ...prompt(2), libraryId: otherLibrary }], total: 1, page: 0, size: 12 });
    return Response.json([]);
  } }, ''));
  client.watchExecutions = vi.fn(async (_id, observe) => { observe([]); }); client.rememberedContexts = vi.fn(async () => []);
  const expired = vi.fn();
  const mounted = render(<AccountPromptLibrary library={library} service={client} onExpired={expired} />);
  mounted.rerender(<AccountPromptLibrary library={{ ...library, id: otherLibrary }} service={client} onExpired={expired} />);
  await screen.findByRole('button', { name: 'Prompt 2' });
  await act(async () => finish(Response.json({ items: [prompt(1)], total: 1, page: 0, size: 12 })));
  expect(screen.queryByRole('button', { name: 'Prompt 1' })).toBeNull();
  expect(screen.getByRole('button', { name: 'Prompt 2' })).toBeTruthy();
});

it('retires deletion confirmation and its late result when changing the private library', async () => {
  let finish!: (value: Response) => void;
  const pending = new Promise<Response>(resolve => { finish = resolve; });
  const sent: { path: string; signal?: AbortSignal | null }[] = [];
  const client = createAccountService(createApiClient({ send: async (path, options) => {
    if (path === '/account/csrf') return Response.json({ token: 'csrf' });
    if (options?.method === 'DELETE') { sent.push({ path, signal: options.signal }); return pending; }
    if (path.includes('/prompts/catalog?')) {
      const item = path.includes(`/libraries/${libraryId}/`) ? prompt(1) : { ...prompt(2), libraryId: otherLibrary };
      return Response.json({ items: [item], total: 1, page: 0, size: 12 });
    }
    return Response.json([]);
  } }, ''));
  client.watchExecutions = vi.fn(async (_id, observe) => { observe([]); }); client.rememberedContexts = vi.fn(async () => []);
  const expired = vi.fn();
  const mounted = render(<AccountPromptLibrary library={library} service={client} onExpired={expired} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Eliminar: Prompt 1' }));
  fireEvent.click(screen.getByLabelText('Confirmo eliminar este prompt y sus versiones.'));
  fireEvent.click(screen.getByRole('button', { name: 'Eliminar definitivamente' }));
  await waitFor(() => expect(sent).toHaveLength(1));
  mounted.rerender(<AccountPromptLibrary library={{ ...library, id: otherLibrary }} service={client} onExpired={expired} />);
  await screen.findByRole('button', { name: 'Prompt 2' });
  expect(sent[0].signal?.aborted).toBe(true);
  await act(async () => finish(new Response(null, { status: 204 })));
  expect(screen.queryByRole('dialog')).toBeNull();
  expect(screen.getByRole('button', { name: 'Prompt 2' })).toBeTruthy();
  expect(sent[0].path).toBe(`${root}/prompts/${prompt(1).id}`);
});
