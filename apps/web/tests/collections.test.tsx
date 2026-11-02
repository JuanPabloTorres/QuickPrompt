import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { GroupSelector } from '../src/features/groups/GroupSelector';
import type { Group, Prompt } from '../src/services/api';
import { createApiClient } from '../src/services/api';
import { createAccountService } from '../src/services/accounts';
import { AccountCollectionDialog } from '../src/features/accounts/AccountCollectionDialog';

const groupId = '10000000-0000-4000-8000-000000000001';
const prompt: Prompt = { id: '10000000-0000-4000-8000-000000000002', revision: 1, title: 'Notas', templateText: 'Resume.', description: '', categoryId: 0, isFavorite: false, isArchived: false, variableNames: [], defaults: {}, uses: 0, lastUsedAt: null, createdAt: '', updatedAt: '' };
const group: Group = { group: { id: groupId, name: 'Ideas', revision: 1 }, templates: 0, instructions: 0 };
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('uses the same manager with authenticated library routes and CSRF without local fallback', async () => {
  const libraryId = '10000000-0000-4000-8000-000000000003';
  const root = `/api/accounts/v1/libraries/${libraryId}`;
  let groups: Group[] = []; let memberships: string[] = [];
  const send = vi.fn(async (path: string, init: RequestInit) => {
    if (path === '/account/csrf') return Response.json({ token: 'synthetic-csrf' });
    if (init.method === 'POST') {
      expect(new Headers(init.headers).get('X-QP-CSRF')).toBe('synthetic-csrf');
      if (path === `${root}/groups`) { expect(JSON.parse(init.body as string)).toEqual({ name: 'Ideas', revision: 0 }); groups = [group]; return Response.json({ id: groupId }); }
      if (path === `${root}/prompts/${prompt.id}/groups`) {
        expect(JSON.parse(init.body as string)).toEqual({ groupId, revision: 1, member: true }); memberships = [groupId];
        groups = [{ ...group, group: { ...group.group, revision: 2 }, templates: 1 }]; return new Response(null, { status: 204 });
      }
    }
    if (path === `${root}/groups`) return Response.json(groups);
    if (path === `${root}/prompts/${prompt.id}/groups`) return Response.json(memberships);
    throw Error(`Unexpected request: ${path}`);
  });
  const service = createAccountService(createApiClient({ send }, ''));
  const props = { libraryId, service, onExpired: vi.fn(), onUpdated: vi.fn(), onClose: vi.fn() };
  const library = render(<AccountCollectionDialog {...props} />);
  await screen.findByText('Aún no tienes colecciones.');
  fireEvent.change(screen.getByLabelText('Nueva colección'), { target: { value: 'Ideas' } });
  fireEvent.click(screen.getByRole('button', { name: 'Crear colección' }));
  await screen.findByLabelText('Nombre de colección Ideas'); library.unmount();
  const details = render(<AccountCollectionDialog {...props} prompt={{ id: prompt.id, libraryId, title: prompt.title, revision: 1, template: prompt.templateText }} />);
  const check = await screen.findByRole('checkbox', { name: 'Asignar Ideas' });
  await waitFor(() => expect((check as HTMLInputElement).disabled).toBe(false)); fireEvent.click(check);
  await waitFor(() => expect(memberships).toEqual([groupId])); details.unmount();
  render(<AccountCollectionDialog {...props} prompt={{ id: prompt.id, libraryId, title: prompt.title, revision: 1, template: prompt.templateText }} />);
  await waitFor(() => expect((screen.getByRole('checkbox', { name: 'Asignar Ideas' }) as HTMLInputElement).checked).toBe(true));
  expect(send.mock.calls.every(([path]) => path === '/account/csrf' || path.startsWith(root))).toBe(true);
});

it('returns an expired account to login when the shared collection manager receives a 401', async () => {
  const service = createAccountService(createApiClient({ send: async () => Response.json({ detail: 'Sesión caducada' }, { status: 401 }) }, ''));
  const expired = vi.fn();
  render(<AccountCollectionDialog libraryId={groupId} service={service} onExpired={expired} onUpdated={vi.fn()} onClose={vi.fn()} />);
  await waitFor(() => expect(expired).toHaveBeenCalledTimes(1));
  expect((screen.getByRole('button', { name: 'Crear colección' }) as HTMLButtonElement).disabled).toBe(true);
});

it('shows a collection created from Home in prompt details and retains its assignment on reopen', async () => {
  let groups: Group[] = []; let memberships: string[] = [];
  const transport = vi.fn(async (path: string, options?: RequestInit) => {
    if (path === '/api/v1/groups' && options?.method === 'POST') { groups = [group]; return Response.json({ id: groupId }); }
    if (path === '/api/v1/groups') return Response.json(groups);
    if (path === `/api/v1/prompts/${prompt.id}/groups` && options?.method === 'POST') {
      const body = JSON.parse(options.body as string);
      expect(body).toEqual({ groupId, revision: 1, member: true }); memberships = [groupId];
      return new Response(null, { status: 204 });
    }
    if (path === `/api/v1/prompts/${prompt.id}/groups`) return Response.json(memberships);
    throw Error(`Unexpected request: ${path}`);
  });
  vi.stubGlobal('fetch', transport);
  const home = render(<GroupSelector onClose={vi.fn()} onUpdated={vi.fn()} />);
  await screen.findByText('Aún no tienes colecciones.');
  fireEvent.change(screen.getByLabelText('Nueva colección'), { target: { value: 'Ideas' } });
  fireEvent.click(screen.getByRole('button', { name: 'Crear colección' }));
  await screen.findByLabelText('Nombre de colección Ideas'); home.unmount();
  const details = render(<GroupSelector prompt={prompt} onClose={vi.fn()} onUpdated={vi.fn()} />);
  const checkbox = await screen.findByRole('checkbox', { name: 'Asignar Ideas' });
  await waitFor(() => expect((checkbox as HTMLInputElement).disabled).toBe(false));
  fireEvent.click(checkbox);
  await waitFor(() => expect(memberships).toEqual([groupId])); details.unmount();
  render(<GroupSelector prompt={prompt} onClose={vi.fn()} onUpdated={vi.fn()} />);
  await waitFor(() => expect((screen.getByRole('checkbox', { name: 'Asignar Ideas' }) as HTMLInputElement).checked).toBe(true));
});

it('keeps the catalog visible when memberships fail and retries before allowing an assignment', async () => {
  let failing = true;
  const transport = vi.fn(async (path: string, _options?: RequestInit) => path === '/api/v1/groups' ? Response.json([group]) : failing ? Response.json({ detail: 'Temporalmente no disponible' }, { status: 503 }) : Response.json([groupId]));
  vi.stubGlobal('fetch', transport);
  render(<GroupSelector prompt={prompt} onClose={vi.fn()} onUpdated={vi.fn()} />);
  await screen.findByRole('alert');
  const checkbox = screen.getByRole('checkbox', { name: 'Asignar Ideas' }) as HTMLInputElement;
  expect(checkbox.disabled).toBe(true);
  expect(screen.queryByText('Aún no tienes colecciones.')).toBeNull();
  fireEvent.click(checkbox);
  expect(transport.mock.calls.every(call => !call[1]?.method || call[1].method === 'GET')).toBe(true);
  failing = false; fireEvent.click(screen.getByRole('button', { name: 'Reintentar carga' }));
  await waitFor(() => expect(checkbox.disabled).toBe(false)); expect(checkbox.checked).toBe(true);
  expect(screen.queryByRole('alert')).toBeNull();
});

it('distinguishes a pending or failed catalog from an empty collection list', async () => {
  let finish!: (value: Response) => void;
  vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(resolve => { finish = resolve; })));
  render(<GroupSelector onClose={vi.fn()} onUpdated={vi.fn()} />);
  expect(screen.getByRole('status').textContent).toBe('Cargando colecciones…');
  expect(screen.queryByText('Aún no tienes colecciones.')).toBeNull();
  await act(async () => { finish(Response.json({ detail: 'Sin conexión' }, { status: 503 })); });
  expect(screen.getByRole('alert').textContent).toContain('Sin conexión');
  expect(screen.queryByText('Aún no tienes colecciones.')).toBeNull();
});
