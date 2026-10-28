import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import App from '../src/app/App';
import { ApplicationServiceProvider } from '../src/components/ServiceContext';
import { CategorySelector } from '../src/features/prompts/CategorySelector';
import { createApiClient, type HttpTransport, type Prompt } from '../src/services/api';
import { createApplicationServices } from '../src/services/application';

beforeEach(() => { sessionStorage.clear(); localStorage.clear(); });
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

it('connects App, its hook, editor and lazy settings to the supplied transport', async () => {
  const globalFetch = vi.fn(() => { throw Error('Global fetch forbidden'); }); vi.stubGlobal('fetch', globalFetch);
  const prompt: Prompt = { id: '10000000-0000-4000-8000-000000000001', title: 'Prompt de la composición', templateText: 'Explica <tema>.', description: '',
    categoryId: 0, revision: 1, isFavorite: false, isArchived: false, variableNames: ['tema'], defaults: {}, uses: 0, lastUsedAt: null, createdAt: '', updatedAt: '' };
  const send = vi.fn<HttpTransport['send']>().mockImplementation(async path => {
    if (path.includes('/prompts?')) return Response.json({ items: [prompt], total: 1 });
    if (path.endsWith('/prompts/' + prompt.id)) return Response.json(prompt);
    if (path.endsWith('/categories')) return Response.json([{ id: 0, name: 'Categoría compuesta' }]);
    if (path.endsWith('/groups') || path.endsWith('/nova/drafts')) return Response.json([]);
    if (path.endsWith('/templates/inspect')) return Response.json({ variableNames: ['tema'] });
    if (path.endsWith('/templates/compile')) return Response.json({ text: null, missingVariables: ['tema'] });
    if (path.endsWith('/nova/preferences')) return Response.json({ language: null, format: null, providerId: null });
    throw Error('Unexpected route ' + path);
  });
  const services = createApplicationServices(createApiClient({ send }));
  await act(async () => { render(<ApplicationServiceProvider services={services}><App /></ApplicationServiceProvider>); });
  await screen.findByTestId('library-ready');
  await screen.findByRole('button', { name: 'Editar: ' + prompt.title });
  await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Editar: ' + prompt.title })); });
  await waitFor(() => expect((screen.getByRole('textbox', { name: 'Contenido' }) as HTMLTextAreaElement).value).toBe(prompt.templateText));
  await screen.findByRole('option', { name: 'Categoría compuesta' });
  fireEvent.click(screen.getByRole('button', { name: /^Configuración$/ }));
  await screen.findByRole('dialog', { name: /^Configuración$/ });
  await waitFor(() => expect(send.mock.calls.some(([path]) => path.endsWith('/nova/preferences'))).toBe(true));
  expect(send.mock.calls.some(([path]) => path.endsWith('/templates/inspect'))).toBe(true);
  expect(globalFetch).not.toHaveBeenCalled();
});

it('keeps sibling provider trees bound to their own template clients', async () => {
  const first = createApplicationServices(createApiClient({ send: async () => Response.json([{ id: 0, name: 'Primera biblioteca' }]) }));
  const second = createApplicationServices(createApiClient({ send: async () => Response.json([{ id: 0, name: 'Segunda biblioteca' }]) }));
  render(<><section aria-label="Primera"><ApplicationServiceProvider services={first}><CategorySelector value={0} onChange={vi.fn()} disabled={false} /></ApplicationServiceProvider></section>
    <section aria-label="Segunda"><ApplicationServiceProvider services={second}><CategorySelector value={0} onChange={vi.fn()} disabled={false} /></ApplicationServiceProvider></section></>);
  const firstTree = within(screen.getByRole('region', { name: 'Primera' })), secondTree = within(screen.getByRole('region', { name: 'Segunda' }));
  await firstTree.findByRole('option', { name: 'Primera biblioteca' }); await secondTree.findByRole('option', { name: 'Segunda biblioteca' });
  expect(firstTree.queryByRole('option', { name: 'Segunda biblioteca' })).toBeNull(); expect(secondTree.queryByRole('option', { name: 'Primera biblioteca' })).toBeNull();
});
