import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import App from '../src/app/App';
import { PromptStream } from '../src/features/prompts/PromptStream';
import type { Prompt } from '../src/services/api';

const prompt: Prompt = { id: '55efc1e9-f51d-424e-9e3a-cf543fdf0ceb', title: 'Revisión de proyectos', templateText: 'Revisa <proyecto>.', description: 'Revisión práctica', categoryId: 0, revision: 1, isFavorite: false, isArchived: false, variableNames: ['proyecto'], defaults: {}, uses: 0, lastUsedAt: null, createdAt: '', updatedAt: '' };
class Events extends EventTarget { close() {} }
beforeEach(() => { localStorage.clear(); sessionStorage.clear(); vi.stubGlobal('EventSource', Events); });
afterEach(() => { cleanup(); vi.unstubAllGlobals(); localStorage.clear(); sessionStorage.clear(); });
async function loadedPrompt() {
  // Wait for the actual item, not the API-ready marker that can precede entry animation.
  // A cheap DOM lookup also yields to fetch/render before accessible-role assertions.
  await screen.findByTestId('library-prompt');
  return screen.getByRole('button', { name: prompt.title });
}
function setup(openAgent = true) {
  const calls: { path: string; body?: Record<string, unknown>; method: string }[] = [];
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    const body = options?.body ? JSON.parse(options.body as string) : undefined; calls.push({ path, body, method: options?.method ?? 'GET' });
    if (path.endsWith('/groups') || path.endsWith('/nova/drafts')) return Response.json([]);
    if (path.endsWith('/nova/preferences')) return Response.json({ language: null, format: null, providerId: null });
    if (path.includes('/prompts?')) return Response.json({ items: [prompt], total: 1 });
    if (path.endsWith('/prompts/' + prompt.id)) return Response.json(prompt);
    if (path.endsWith('/templates/inspect')) return Response.json({ variableNames: ['proyecto'] });
    if (path.endsWith('/templates/compile')) return Response.json({ text: null, missingVariables: ['proyecto'] });
    if (path.endsWith('/nova/editor-drafts')) return Response.json({ id: prompt.id, state: 'READY', intent: 'ADAPT_PROMPT', decision: 'ADAPT_EXISTING', title: body!.title, description: '', templateText: body!.templateText, compiledText: null, variables: [{ name: 'proyecto', required: true }], values: [], missingVariables: ['proyecto'], question: '', reason: 'Editor actual', candidates: [], sourceId: prompt.id, sourceRevision: prompt.revision, categoryId: 0, remembered: false, createdAt: new Date().toISOString() });
    if (path.endsWith('/context')) return new Response(null, { status: 204 });
    if (path.endsWith('/nova/live')) return Response.json({ runId: body!.runId, sessionId: body!.sessionId, mode: 'SAME_TAB', status: 'COMPLETED', sequence: 1, targetId: null, revision: null, pauseRequested: false, browserOpen: false, approval: 'NONE', effectCommitted: false, errorCode: null });
    if (path.includes('/nova/live/')) {
      const run = { runId: path.split('/')[5], sessionId: prompt.id, mode: 'SAME_TAB', status: 'COMPLETED', sequence: 1, targetId: null, revision: null, pauseRequested: false, browserOpen: false, approval: 'NONE', effectCommitted: false, errorCode: null };
      return Response.json(path.includes('/actions?') ? { run, action: null } : { run, request: null, question: null, observation: null, proposal: null });
    }
    throw Error('Unexpected endpoint: ' + path);
  }));
  render(<App />);
  if (openAgent) fireEvent.click(screen.getByRole('button', { name: 'Abrir asistente NOVA' }));
  return calls;
}
it('opens one action workbench, restores keyboard focus and never sends unclassified text', async () => {
  const calls = setup(false);
  const launcher = screen.getByRole('button', { name: 'Abrir asistente NOVA' });
  expect(launcher.getAttribute('aria-expanded')).toBe('false');
  fireEvent.click(launcher);
  const entry = screen.getByRole('button', { name: 'Crear prompt' });
  await waitFor(() => expect(document.activeElement).toBe(entry));
  expect(screen.queryByRole('textbox', { name: '¿Qué quieres hacer hoy?' })).toBeNull();
  expect(screen.getByRole('button', { name: 'Buscar por significado' })).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Cerrar panel de NOVA' }));
  fireEvent.click(await loadedPrompt()); await screen.findByTestId('prompt-focus');
  fireEvent.keyDown(document, { key: 'k', ctrlKey: true });
  await waitFor(() => expect(document.activeElement).toBe(entry));
  expect(screen.getAllByTestId('nova-action-workbench')).toHaveLength(1);
  expect(screen.getByTestId('prompt-focus')).toBeTruthy();
  act(() => { window.dispatchEvent(new CustomEvent('nova-agent-status', { detail: 'TOOL_RUNNING' })); });
  fireEvent.keyDown(entry, { key: 'Escape' });
  expect(launcher.getAttribute('data-working')).toBe('true'); expect(document.activeElement).toBe(launcher);
  act(() => { window.dispatchEvent(new CustomEvent('nova-agent-status', { detail: 'COMPLETED' })); });
  expect(launcher.getAttribute('data-working')).toBe('false');
  expect(calls.some(call => call.path.endsWith('/nova/live') || call.path.endsWith('/interpret'))).toBe(false);
});
it('keeps explicit actions across navigation and submits the chosen operation with its exact source', async () => {
  const calls = setup();
  fireEvent.click(await loadedPrompt()); await screen.findByTestId('prompt-focus');
  fireEvent.keyDown(document, { key: 'k', ctrlKey: true });
  expect(screen.getAllByTestId('nova-action-workbench')).toHaveLength(1);
  fireEvent.click(screen.getByRole('button', { name: 'Actualizar prompt' }));
  fireEvent.change(screen.getByLabelText('¿Qué quieres cambiar?'), { target: { value: 'Haz la instrucción más clara.' } });
  fireEvent.click(screen.getByRole('button', { name: 'Preparar cambios' }));
  await waitFor(() => expect(calls.some(call => call.path.endsWith('/nova/live'))).toBe(true));
  expect(calls.find(call => call.path.endsWith('/nova/live'))?.body).toMatchObject({ sourceId: prompt.id, sourceRevision: 1,
    message: 'Haz la instrucción más clara.', command: { operation: 'PROPOSE_ADAPTATION', content: 'Haz la instrucción más clara.' } });
  expect(calls.some(call => call.path.endsWith('/interpret'))).toBe(false);
  expect(screen.getByTestId('prompt-focus')).toBeTruthy();
});
it.each(['cards', 'rows'])('cancels deletion in %s without opening the prompt or changing the library context', async view => {
  const calls = setup(); await loadedPrompt();
  fireEvent.click(screen.getByRole('button', { name: view === 'rows' ? 'Ver filas' : 'Ver tarjetas' }));
  const workbench = screen.getByTestId('nova-action-workbench');
  for (const close of ['Cancelar', 'Cerrar diálogo', 'Escape']) {
    fireEvent.click(screen.getByRole('button', { name: 'Eliminar: ' + prompt.title }));
    const dialog = await screen.findByRole('dialog', { name: '¿Eliminar este prompt?' });
    expect(screen.queryByTestId('prompt-focus')).toBeNull();
    if (close === 'Escape') fireEvent.keyDown(dialog, { key: 'Escape' });
    else fireEvent.click(within(dialog).getByRole('button', { name: close }));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: '¿Eliminar este prompt?' })).toBeNull());
    expect(screen.getByRole('list', { name: 'Prompts guardados' }).dataset.view).toBe(view);
    expect(screen.queryByTestId('prompt-focus')).toBeNull();
    expect(screen.getByTestId('nova-action-workbench')).toBe(workbench);
  }
  expect(calls.some(call => call.method === 'DELETE')).toBe(false);
});
it('keeps preparation settings inside the header configuration and persists the selected library view', async () => {
  setup(); await loadedPrompt();
  expect(screen.queryByText('Opciones de preparación')).toBeNull(); expect(screen.queryByLabelText('Presentación del resultado')).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Ver filas' }));
  expect(screen.getByRole('list', { name: 'Prompts guardados' }).dataset.view).toBe('rows');
  fireEvent.click(screen.getByRole('button', { name: 'Configuración' }));
  await screen.findByRole('dialog', { name: 'Configuración' });
  expect(screen.getByLabelText('Presentación del resultado')).toBeTruthy();
  fireEvent.change(screen.getByLabelText('Presentación del resultado'), { target: { value: 'LIVE' } });
  await waitFor(() => expect(JSON.parse(localStorage.getItem('nova-interface-v1')!)).toMatchObject({ view: 'rows', executionMode: 'LIVE' }));
  fireEvent.click(screen.getByRole('button', { name: 'Cerrar diálogo' }));
  await waitFor(() => expect(screen.queryByLabelText('Presentación del resultado')).toBeNull());
});
it('sends the current unsaved editor snapshot before starting NOVA and stays in that editor', async () => {
  const calls = setup();
  await loadedPrompt(); fireEvent.click(screen.getByRole('button', { name: 'Editar: ' + prompt.title }));
  await screen.findByTestId('prompt-editor');
  fireEvent.change(screen.getByLabelText('Título'), { target: { value: 'Mi borrador actual' } });
  fireEvent.change(screen.getByLabelText('Contenido'), { target: { value: 'Mi revisión de <proyecto>, todavía sin guardar.' } });
  fireEvent.click(screen.getByRole('button', { name: 'Actualizar prompt' }));
  fireEvent.change(screen.getByLabelText('¿Qué quieres cambiar?'), { target: { value: 'Revisa este borrador.' } });
  fireEvent.click(screen.getByRole('button', { name: 'Preparar cambios' }));
  await waitFor(() => expect(calls.some(call => call.path.endsWith('/nova/live'))).toBe(true));
  const snapshot = calls.findIndex(call => call.path.endsWith('/nova/editor-drafts'));
  const start = calls.findIndex(call => call.path.endsWith('/nova/live'));
  expect(snapshot).toBeGreaterThan(-1); expect(snapshot).toBeLessThan(start);
  expect(calls[snapshot].body).toMatchObject({ promptId: prompt.id, revision: prompt.revision, title: 'Mi borrador actual', templateText: 'Mi revisión de <proyecto>, todavía sin guardar.' });
  expect(screen.getByTestId('prompt-editor')).toBeTruthy(); expect(screen.getByLabelText('Contenido')).toHaveProperty('value', 'Mi revisión de <proyecto>, todavía sin guardar.');
});
it('routes icon actions to the exact prompt without opening it, in cards and rows', async () => {
  const open = vi.fn(), favorite = vi.fn(), edit = vi.fn(), duplicate = vi.fn(), remove = vi.fn();
  const props = { items: [prompt], open, onFavorite: favorite, onEdit: edit, onDuplicate: duplicate, onDelete: remove };
  const rendered = render(<PromptStream {...props} view="cards" />);
  for (const [name, callback] of [['Añadir a favoritos:', favorite], ['Editar:', edit], ['Duplicar:', duplicate], ['Eliminar:', remove]] as const) {
    fireEvent.click(screen.getByRole('button', { name: `${name} ${prompt.title}` })); expect(callback).toHaveBeenCalledWith(prompt);
  }
  expect(open).not.toHaveBeenCalled();
  rendered.rerender(<PromptStream {...props} view="rows" />);
  fireEvent.click(screen.getByRole('button', { name: prompt.title })); expect(open).toHaveBeenCalledWith(prompt);
});
