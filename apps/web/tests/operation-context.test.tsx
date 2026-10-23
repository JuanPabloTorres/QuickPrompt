import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { PromptFocus } from '../src/features/prompts/PromptFocus';
import { ProviderPicker } from '../src/features/providers/ProviderPicker';
import { InlinePromptEditor } from '../src/features/prompts/InlinePromptEditor';
import type { Prompt, PromptDraft } from '../src/services/api';

const prompt: Prompt = { id: '55efc1e9-f51d-424e-9e3a-cf543fdf0ceb', title: 'Contexto', templateText: 'Explica <tema>.', description: '', revision: 1, categoryId: 0, isFavorite: false, isArchived: false, variableNames: ['tema'], defaults: {}, uses: 0, lastUsedAt: null, createdAt: '', updatedAt: '' };
const initialValues = { tema: 'anterior' };
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });
function deferred() { let resolve!: (response: Response) => void; const promise = new Promise<Response>(done => { resolve = done; }); return { promise, resolve }; }

it.each(['values', 'revision', 'identity', 'unmount'])('discards a delayed copy after changing %s', async change => {
  const pending = deferred(), clipboard = vi.fn(), onUsed = vi.fn(), notify = vi.fn();
  vi.stubGlobal('navigator', { clipboard: { writeText: clipboard } });
  const fetch = vi.fn((_path: string) => pending.promise); vi.stubGlobal('fetch', fetch);
  const props = { prompt, values: initialValues, onValues: vi.fn(), onBack: vi.fn(), onEdit: vi.fn(), onAction: vi.fn(), onDelete: vi.fn(), onUsed, notify };
  const view = render(<PromptFocus {...props} />);
  fireEvent.click(screen.getByRole('button', { name: 'Copiar prompt' }));
  expect(fetch).toHaveBeenCalled();
  if (change === 'unmount') view.unmount();
  else view.rerender(<PromptFocus {...props} values={change === 'values' ? { tema: 'nuevo' } : initialValues} prompt={change === 'revision' ? { ...prompt, revision: 2 } : change === 'identity' ? { ...prompt, id: '268ab432-770c-44ee-b6b8-6b7e52b0f517' } : prompt} />);
  await act(async () => { pending.resolve(Response.json({ text: 'Explica anterior.', templateRevision: 1 })); await pending.promise; });
  expect(clipboard).not.toHaveBeenCalled(); expect(onUsed).not.toHaveBeenCalled(); expect(notify).not.toHaveBeenCalled();
  expect(fetch.mock.calls.some(call => String(call[0]).endsWith('/usage'))).toBe(false);
  if (change !== 'unmount') expect(screen.getByRole('button', { name: 'Copiar prompt' })).toHaveProperty('disabled', false);
});

it.each(['values', 'unmount'])('does not copy or navigate a stale external transfer after %s', async change => {
  const pending = deferred(), clipboard = vi.fn(), replace = vi.fn(), close = vi.fn(), onUsed = vi.fn();
  vi.stubGlobal('navigator', { clipboard: { writeText: clipboard } });
  vi.spyOn(window, 'open').mockReturnValue({ opener: null, close, location: { replace } } as unknown as Window);
  vi.stubGlobal('fetch', vi.fn(async (path: string) => {
    if (path.endsWith('/providers')) return Response.json([{ providerId: 'claude', displayName: 'Claude', destinationUrl: 'https://claude.ai/new', modesSupported: ['COPY_AND_OPEN'], canSubmit: false, availabilityStatus: 'AVAILABLE' }]);
    if (path.endsWith('/compile')) return Response.json({ text: 'Explica anterior.', templateRevision: 1 });
    if (path.endsWith('/models')) return Response.json({ models: [] });
    if (path.endsWith('/transfers/prepare')) return pending.promise;
    throw Error('Unexpected request: ' + path);
  }));
  const props = { prompt, values: initialValues, onClose: vi.fn(), notify: vi.fn(), onUsed };
  const view = render(<ProviderPicker {...props} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Claude' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Copiar y abrir' })).toHaveProperty('disabled', false));
  fireEvent.click(screen.getByRole('button', { name: 'Copiar y abrir' }));
  if (change === 'unmount') view.unmount(); else view.rerender(<ProviderPicker {...props} values={{ tema: 'nuevo' }} />);
  await act(async () => { pending.resolve(Response.json({ text: 'Explica anterior.', templateRevision: 1, destinationUrl: 'https://claude.ai/new', displayName: 'Claude', mode: 'COPY_AND_OPEN' })); await pending.promise; });
  expect(clipboard).not.toHaveBeenCalled(); expect(replace).not.toHaveBeenCalled(); expect(onUsed).not.toHaveBeenCalled(); expect(close).toHaveBeenCalled();
});

it('a late cancelled execution cannot publish output or release a newer execution', async () => {
  const oldExecution = deferred(), newExecution = deferred(), onUsed = vi.fn();
  const executions: string[] = [], usage: unknown[] = [];
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/providers')) return Response.json([{ providerId: 'ollama', displayName: 'Local', destinationUrl: null, modesSupported: ['LOCAL_EXECUTION'], canSubmit: false, availabilityStatus: 'AVAILABLE' }]);
    if (path.endsWith('/compile')) return Response.json({ text: 'Explica ' + JSON.parse(options!.body as string).values.tema + '.', templateRevision: 1 });
    if (path.endsWith('/models')) return Response.json({ models: ['qwen3:8b'] });
    if (path.endsWith('/usage')) { usage.push(JSON.parse(options!.body as string)); return new Response(null, { status: 204 }); }
    if (path.endsWith('/execute')) { executions.push(JSON.parse(options!.body as string).expectedText); return executions.length === 1 ? oldExecution.promise : newExecution.promise; }
    throw Error('Unexpected request: ' + path);
  }));
  const props = { prompt, values: initialValues, onClose: vi.fn(), notify: vi.fn(), onUsed };
  const view = render(<ProviderPicker {...props} />);
  const execute = await screen.findByRole('button', { name: 'Ejecutar en este equipo' });
  await waitFor(() => expect(execute).toHaveProperty('disabled', false));
  fireEvent.click(execute);
  view.rerender(<ProviderPicker {...props} values={{ tema: 'vigente' }} />);
  await waitFor(() => expect(screen.getByRole('button', { name: 'Ejecutar en este equipo' })).toHaveProperty('disabled', false));
  fireEvent.click(screen.getByRole('button', { name: 'Ejecutar en este equipo' }));
  const completed = (text: string) => new Response(`event: complete\ndata: ${JSON.stringify({ text, revision: 1 })}\n\n`);
  await act(async () => { oldExecution.resolve(completed('SALIDA OBSOLETA')); await oldExecution.promise; });
  expect(screen.queryByText('SALIDA OBSOLETA')).toBeNull(); expect(screen.getByRole('button', { name: 'Detener' })).toBeTruthy(); expect(usage).toEqual([]);
  await act(async () => { newExecution.resolve(completed('SALIDA VIGENTE')); await newExecution.promise; });
  await waitFor(() => expect(onUsed).toHaveBeenCalledOnce());
  expect(screen.getByText('SALIDA VIGENTE')).toBeTruthy();
  expect(executions).toEqual(['Explica anterior.', 'Explica vigente.']);
  expect(usage).toEqual([expect.objectContaining({ revision: 1, values: { tema: 'vigente' }, expectedText: 'Explica vigente.' })]);
});

it('does not overwrite a newer editor title with a delayed variable transformation', async () => {
  const pending = deferred(), onChange = vi.fn();
  const fetch = vi.fn(async (path: string) => {
    if (path.endsWith('/inspect')) return Response.json({ variableNames: [] });
    if (path.endsWith('/categories')) return Response.json([{ id: 0, name: 'General' }]);
    if (path.endsWith('/toggle-variable')) return pending.promise;
    throw Error('Unexpected request: ' + path);
  });
  vi.stubGlobal('fetch', fetch);
  const draft: PromptDraft = { title: 'Anterior', templateText: 'Explica tema.', description: '', categoryId: 0 };
  const props = { draft, onChange, onSave: vi.fn(), onBack: vi.fn(), saving: false, error: '', editing: false };
  const view = render(<InlinePromptEditor {...props} />);
  const field = screen.getByLabelText('Contenido') as HTMLTextAreaElement;
  field.setSelectionRange(8, 12); fireEvent.select(field);
  fireEvent.click(screen.getByRole('button', { name: 'Crear / quitar variable' }));
  expect(fetch).toHaveBeenCalledWith('/api/v1/templates/toggle-variable', expect.anything());
  view.rerender(<InlinePromptEditor {...props} draft={{ ...draft, title: 'Título vigente' }} />);
  await act(async () => { pending.resolve(Response.json({ templateText: 'Explica <tema>.', variableNames: ['tema'], start: 8, end: 14, action: 'created', name: 'tema', originalText: 'tema' })); await pending.promise; });
  await waitFor(() => expect(field.disabled).toBe(false));
  expect(onChange).not.toHaveBeenCalled();
  expect(screen.getByLabelText('Título')).toHaveProperty('value', 'Título vigente');
  expect(field).toHaveProperty('value', 'Explica tema.'); expect(field.disabled).toBe(false);
});
