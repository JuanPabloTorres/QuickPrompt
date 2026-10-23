import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import App from '../src/app/App';
import { ApplicationServiceProvider } from '../src/components/ServiceContext';
import { createApiClient, type Prompt } from '../src/services/api';
import { createApplicationServices } from '../src/services/application';
import { createEditorDraftSession } from '../src/features/prompts/EditorDraftSession';

const initial: Prompt = { id: '11000000-0000-4000-8000-000000000001', revision: 1, title: 'Origen de la sesión',
  templateText: 'Versión guardada inicial.', description: '', categoryId: 0, isFavorite: false, isArchived: false,
  variableNames: [], defaults: {}, uses: 0, lastUsedAt: null, createdAt: '', updatedAt: '' };
beforeEach(() => { sessionStorage.clear(); localStorage.clear(); });
afterEach(() => { cleanup(); vi.restoreAllMocks(); sessionStorage.clear(); localStorage.clear(); });

it.each(['focus', 'library'])('opens the current revision instead of a stale cached draft through %s', async entry => {
  let current = { ...initial };
  const send = vi.fn(async (path: string, options: RequestInit) => {
    if (path.includes('/prompts?')) return Response.json({ items: [current], total: 1 });
    if (path.endsWith('/prompts/' + initial.id)) return Response.json(current);
    if (path.endsWith('/groups') || path.endsWith('/nova/drafts') || path.endsWith('/categories')) return Response.json([]);
    if (path.endsWith('/templates/inspect')) return Response.json({ variableNames: [] });
    const body = options.body ? JSON.parse(String(options.body)) : {};
    if (path.endsWith('/templates/compile')) return Response.json({ text: body.templateText, missingVariables: [] });
    if (path.endsWith('/nova/editor-drafts')) return Response.json({ id: initial.id, state: 'READY', intent: 'ADAPT_PROMPT', decision: 'ADAPT_EXISTING',
      title: body.title, description: '', templateText: body.templateText, compiledText: body.templateText, variables: [], values: [], missingVariables: [],
      question: '', reason: '', candidates: [], sourceId: initial.id, sourceRevision: body.revision, categoryId: 0, remembered: false, createdAt: '' });
    throw Error('Unexpected path: ' + path);
  });
  const services = createApplicationServices(createApiClient({ send }));
  await act(async () => { render(<ApplicationServiceProvider services={services}><App /></ApplicationServiceProvider>); });
  await screen.findByTestId('library-ready');
  const edit = screen.getByRole('button', { name: 'Editar: ' + initial.title });
  await act(async () => { fireEvent.click(edit); });
  await screen.findByTestId('prompt-editor');
  fireEvent.change(screen.getByLabelText('Contenido'), { target: { value: 'Borrador pendiente de la primera revisión.' } });
  fireEvent.click(screen.getByRole('button', { name: 'Volver' }));
  await screen.findByRole('dialog', { name: '¿Salir del editor?' });
  fireEvent.click(screen.getByRole('button', { name: 'Salir del editor' }));
  await screen.findByTestId('prompt-focus');
  fireEvent.click(screen.getByRole('button', { name: 'Biblioteca' }));
  current = { ...initial, revision: 2, templateText: 'Contenido vigente de la segunda revisión.' };
  await screen.findByTestId('library-ready');
  if (entry === 'focus') {
    const open = screen.getByRole('button', { name: current.title });
    await act(async () => { fireEvent.click(open); });
    await waitFor(() => expect(screen.getByTestId('prompt-focus').dataset.revision).toBe('2'));
    await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Editar' })); });
  } else await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Editar: ' + current.title })); });
  await screen.findByTestId('prompt-editor');
  expect(screen.getByLabelText('Contenido')).toHaveProperty('value', current.templateText);
  expect(screen.getByTestId('prompt-editor').dataset.revision).toBe('2');
});

const draft = () => ({ title: 'Borrador de prueba', templateText: 'Explica <tema>.', description: '', categoryId: 0,
  revision: 1, defaults: { tema: 'C# & .NET 🚀' } });

it('retrieves only a matching source revision without discarding incompatible snapshots', () => {
  const session = createEditorDraftSession();
  session.remember(initial.id, draft());
  expect(session.read({ id: initial.id, revision: 2 })).toBeUndefined();
  expect(session.read(initial)).toEqual(draft());
  expect(session.read({ id: '22000000-0000-4000-8000-000000000002', revision: 1 })).toBeUndefined();
  session.remember(initial.id, { ...draft(), revision: undefined });
  expect(session.read(initial)).toBeUndefined();
});

it('owns detached snapshots including nested defaults on both write and read', () => {
  const session = createEditorDraftSession(), original = draft();
  session.remember(initial.id, original);
  original.title = 'Cambio externo'; original.defaults.tema = 'Alterado';
  const recovered = session.read(initial)!;
  expect(recovered).toEqual(draft());
  recovered.templateText = 'Otro cambio'; recovered.defaults!.tema = 'Otro valor';
  expect(session.read(initial)).toEqual(draft());
});

it('keeps session instances, new drafts and individual removal independent', () => {
  const first = createEditorDraftSession(), second = createEditorDraftSession();
  first.remember(null, { ...draft(), title: 'Nuevo' });
  first.remember(initial.id, draft());
  second.remember(initial.id, { ...draft(), title: 'Segunda sesión' });
  first.forget(initial.id);
  expect(first.read(initial)).toBeUndefined();
  expect(first.read(null)?.title).toBe('Nuevo');
  expect(second.read(initial)?.title).toBe('Segunda sesión');
  expect(second.read(null)).toBeUndefined();
  first.forget(null);
  expect(first.read(null)).toBeUndefined();
});
