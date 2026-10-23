import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { QuickActions } from '../src/features/nova/QuickActions';
import type { Prompt } from '../src/services/api';
import type { LiveCommand } from '../src/services/live';

const source: Prompt = { id: '10000000-0000-4000-8000-000000000001', title: 'Evaluar proyectos', description: 'Evalúa sin inventar datos.', templateText: 'Evalúa <proyecto>. Tono: <tono>.', categoryId: 0, isFavorite: false, isArchived: false, revision: 3, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(), uses: 0, lastUsedAt: null, variableNames: ['proyecto', 'tono'], defaults: {} };
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
function setup(prompt?: Prompt, onRequest = vi.fn<(message: string, source?: Prompt, values?: Record<string, string>, command?: LiveCommand) => Promise<void>>(async () => {})) {
  const onRemove = vi.fn<(prompt: Prompt) => Promise<void>>(async () => {});
  render(<QuickActions source={prompt} onRequest={onRequest} onRemove={onRemove} />);
  return { onRequest, onRemove };
}
function choose(label: string) { fireEvent.click(screen.getByRole('button', { name: label })); return screen.getByRole('dialog', { name: label }); }
function more() { fireEvent.click(screen.getByText('Más acciones')); }
it('shows actual workspace data and blocks a cleared required field instead of using a hidden fallback', async () => {
  const request = vi.fn<(message: string, source?: Prompt, values?: Record<string, string>, command?: LiveCommand) => Promise<void>>(async () => {});
  render(<QuickActions source={source} sourceValues={{ proyecto: 'Proyecto visible', tono: 'Claro' }} onRequest={request} onRemove={async () => {}} />);
  more(); choose('Ejecutar con IA local');
  expect((screen.getByLabelText('proyecto') as HTMLTextAreaElement).value).toBe('Proyecto visible');
  fireEvent.change(screen.getByLabelText('proyecto'), { target: { value: '' } });
  expect(screen.getByRole('button', { name: 'Ejecutar ahora' })).toHaveProperty('disabled', true);
  expect(request).not.toHaveBeenCalled();
  fireEvent.change(screen.getByLabelText('proyecto'), { target: { value: 'Proyecto nuevo' } });
  fireEvent.click(screen.getByRole('button', { name: 'Ejecutar ahora' }));
  await waitFor(() => expect(request).toHaveBeenCalledOnce());
  expect(request.mock.calls[0][2]).toEqual({ proyecto: 'Proyecto nuevo', tono: 'Claro' });
});

it('searches by meaning with the original description and no incidental open prompt or case values', async () => {
  const { onRequest } = setup(source);
  choose('Buscar por significado');
  expect(screen.queryByText('Prompt elegido')).toBeNull();
  expect(screen.queryByLabelText('proyecto')).toBeNull();
  const description = 'Una instrucción para revisar el avance del equipo. Debe permitir analizar riesgos.';
  fireEvent.change(screen.getByLabelText('¿Qué prompt necesitas?'), { target: { value: description } });
  fireEvent.click(screen.getByRole('button', { name: 'Buscar con IA' }));
  await waitFor(() => expect(onRequest).toHaveBeenCalledOnce());
  expect(onRequest.mock.calls[0]).toEqual([description, undefined, {}, { operation: 'SEARCH_PROMPT', content: description }]);
});

it.each(['TEMPLATE', 'RENDERED'] as const)('declares adaptation plus %s copy preparation from an explicit UI choice without authorizing save or clipboard', async copyContent => {
  const { onRequest } = setup(source); more(); choose('Adaptar y preparar copia');
  fireEvent.change(screen.getByLabelText('¿Qué quieres adaptar?'), { target: { value: 'Centrar la instrucción en decisiones.' } });
  fireEvent.click(screen.getByRole('radio', { name: copyContent === 'TEMPLATE' ? 'Plantilla con marcadores' : 'Texto rellenado' }));
  expect(screen.queryByLabelText('proyecto') !== null).toBe(copyContent === 'RENDERED');
  if (copyContent === 'RENDERED') for (const name of source.variableNames) fireEvent.change(screen.getByLabelText(name), { target: { value: 'Dato visible' } });
  fireEvent.click(screen.getByRole('button', { name: 'Adaptar y preparar' }));
  await waitFor(() => expect(onRequest).toHaveBeenCalledOnce());
  expect(onRequest.mock.calls[0][3]).toEqual({ operation: 'PROPOSE_ADAPTATION', content: 'Centrar la instrucción en decisiones.',
    requestedOutcomes: ['PROPOSE_ADAPTATION', 'COPY_PROMPT'], copyContent });
  expect(onRequest.mock.calls[0][0]).toContain('sin copiar ahora, guardar ni ejecutar');
});

it('does not send hidden case values after changing a copy choice back to reusable template', async () => {
  const { onRequest } = setup(source); more(); choose('Adaptar y preparar copia');
  fireEvent.change(screen.getByLabelText('¿Qué quieres adaptar?'), { target: { value: 'Adaptar para decisiones.' } });
  fireEvent.click(screen.getByRole('radio', { name: 'Texto rellenado' }));
  fireEvent.change(screen.getByLabelText('proyecto'), { target: { value: 'Datos anteriores' } });
  fireEvent.click(screen.getByRole('radio', { name: 'Plantilla con marcadores' }));
  fireEvent.click(screen.getByRole('button', { name: 'Adaptar y preparar' }));
  await waitFor(() => expect(onRequest).toHaveBeenCalledOnce());
  expect(onRequest.mock.calls[0][2]).toEqual({});
  expect(onRequest.mock.calls[0][0]).not.toContain('Datos anteriores');
});

it('asks only for the new prompt need and launches once after submitting, never on button selection', async () => {
  let finish!: () => void;
  const onRequest = vi.fn<(message: string, source?: Prompt) => Promise<void>>(() => new Promise<void>(resolve => { finish = resolve; }));
  setup(source, onRequest);
  const dialog = choose('Crear prompt');
  expect(screen.queryByLabelText('¿Qué prompt?')).toBeNull();
  const submit = within(dialog).getByRole('button', { name: 'Crear con NOVA' });
  expect((submit as HTMLButtonElement).disabled).toBe(true);
  expect(onRequest).not.toHaveBeenCalled();
  fireEvent.change(screen.getByLabelText('¿Qué prompt quieres crear?'), { target: { value: 'Una guía para preparar clases de música.' } });
  fireEvent.click(submit); fireEvent.click(submit);
  expect(onRequest).toHaveBeenCalledOnce();
  expect(onRequest.mock.calls[0]).toEqual(['Una guía para preparar clases de música.', undefined, {}, { operation: 'CREATE_DRAFT', content: 'Una guía para preparar clases de música.' }]);
  finish(); await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
});

it.each([
  ['Crear prompt', '¿Qué prompt quieres crear?', 'Crear con NOVA', 'CREATE_DRAFT'],
  ['Actualizar prompt', '¿Qué quieres cambiar?', 'Preparar cambios', 'PROPOSE_ADAPTATION'],
])('keeps literal user title and body authority separate from the typed %s action', async (label, field, submit, operation) => {
  const { onRequest } = setup(source);
  const details = 'Título: «Mi guía: acciones». Cuerpo literal: «Resume <notas>. No inventes datos.»';
  choose(label);
  fireEvent.change(screen.getByLabelText(field), { target: { value: `  ${details}\n` } });
  fireEvent.click(screen.getByRole('button', { name: submit }));
  await waitFor(() => expect(onRequest).toHaveBeenCalledOnce());
  expect(onRequest.mock.calls[0][0]).toBe(details);
  expect(onRequest.mock.calls[0][3]).toEqual({ operation, content: details });
  expect(onRequest.mock.calls[0][1]).toEqual(operation === 'CREATE_DRAFT' ? undefined : source);
});

it('selects a real library identity, preserves change details on failure, and allows a deliberate retry', async () => {
  const other = { ...source, id: '20000000-0000-4000-8000-000000000002', description: 'Otra versión del mismo título.' };
  vi.stubGlobal('fetch', vi.fn(async () => Response.json({ items: [source, other], total: 2 })));
  const onRequest = vi.fn().mockRejectedValueOnce(new Error('Modelo no disponible.')).mockResolvedValue(undefined);
  setup(undefined, onRequest); choose('Actualizar prompt');
  await screen.findByRole('group', { name: 'Elegir prompt' });
  fireEvent.click(screen.getByRole('button', { name: /Otra versión del mismo título/ }));
  fireEvent.change(screen.getByLabelText('¿Qué quieres cambiar?'), { target: { value: 'Añade una tabla de prioridades.' } });
  fireEvent.click(screen.getByRole('button', { name: 'Preparar cambios' }));
  expect((await screen.findByRole('alert')).textContent).toContain('Modelo no disponible.');
  expect((screen.getByLabelText('¿Qué quieres cambiar?') as HTMLTextAreaElement).value).toBe('Añade una tabla de prioridades.');
  expect(onRequest.mock.calls[0][1]).toEqual(other);
  expect(onRequest.mock.calls[0][0]).toBe('Añade una tabla de prioridades.');
  fireEvent.click(screen.getByRole('button', { name: 'Preparar cambios' }));
  await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull()); expect(onRequest).toHaveBeenCalledTimes(2);
});

it('reuses the current selection for read-only opening without asking for a new target or task data', async () => {
  const { onRequest } = setup(source); choose('Buscar y abrir');
  expect(screen.queryByLabelText('¿Qué prompt?')).toBeNull();
  expect(screen.queryByLabelText('proyecto')).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Abrir con NOVA' }));
  await waitFor(() => expect(onRequest).toHaveBeenCalledOnce());
  expect(onRequest.mock.calls[0]).toEqual([expect.stringContaining('sin modificar su contenido ni ejecutar su tarea'), source, {}, { operation: 'OPEN_PROMPT', content: '' }]);
});

it('builds variable inputs from the selected template and preserves multiline Unicode values literally', async () => {
  const { onRequest } = setup(source); more(); choose('Completar variables');
  const submit = screen.getByRole('button', { name: 'Completar con NOVA' });
  expect((submit as HTMLButtonElement).disabled).toBe(true);
  const value = 'Aplicación "Río"\\local\nReact y accesibilidad 李';
  fireEvent.change(screen.getByLabelText('proyecto'), { target: { value } });
  fireEvent.click(submit); await waitFor(() => expect(onRequest).toHaveBeenCalledOnce());
  expect(onRequest.mock.calls[0][0]).toContain(`proyecto: ${value}`);
  expect(onRequest.mock.calls[0][0]).not.toContain('tono:');
  expect(onRequest.mock.calls[0][2]).toEqual({ proyecto: value });
});

it('loads actual registered destinations and separates preparation from explicit local execution', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => Response.json([{ providerId: 'custom', displayName: 'Destino disponible', modesSupported: ['COPY_AND_OPEN'], destinationUrl: 'https://example.invalid/', canSubmit: false, availabilityStatus: 'MANUAL_PASTE' }])));
  const { onRequest } = setup(source); more(); choose('Preparar para una IA');
  await screen.findByRole('option', { name: 'Destino disponible' });
  expect((screen.getByRole('button', { name: 'Preparar con NOVA' }) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.change(screen.getByLabelText('¿En qué IA quieres usarlo?'), { target: { value: 'custom' } });
  for (const name of source.variableNames) fireEvent.change(screen.getByLabelText(name), { target: { value: 'Dato visible' } });
  fireEvent.click(screen.getByRole('button', { name: 'Preparar con NOVA' }));
  await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  expect(onRequest.mock.calls[0][0]).toContain('para Destino disponible, sin ejecutar ni enviar.');
  choose('Ejecutar con IA local');
  for (const name of source.variableNames) fireEvent.change(screen.getByLabelText(name), { target: { value: 'Dato visible' } });
  fireEvent.click(screen.getByRole('button', { name: 'Ejecutar ahora' }));
  await waitFor(() => expect(onRequest).toHaveBeenCalledTimes(2));
  expect(onRequest.mock.calls[1][0]).toContain('Ejecuta ahora');
  expect(onRequest.mock.calls[1][3]).toEqual({ operation: 'EXECUTE_LOCAL', content: '', providerId: 'ollama' });
});

it('asks for deletion review of an explicitly chosen prompt, including archived prompts, without issuing DELETE', async () => {
  const fetchMock = vi.fn(async (path: string) => Response.json({ items: path.includes('archived=true') ? [{ ...source, isArchived: true }] : [], total: path.includes('archived=true') ? 1 : 0 }));
  vi.stubGlobal('fetch', fetchMock);
  const { onRequest, onRemove } = setup(); choose('Eliminar prompt');
  fireEvent.click(screen.getByLabelText('Buscar en el archivo'));
  fireEvent.click(await screen.findByRole('button', { name: /Evaluar proyectos/ }));
  expect(onRemove).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'Revisar eliminación' }));
  await waitFor(() => expect(onRemove).toHaveBeenCalledWith({ ...source, isArchived: true }));
  expect(onRequest).not.toHaveBeenCalled();
  expect(fetchMock.mock.calls.every(([path]) => path.startsWith('/api/v1/prompts?'))).toBe(true);
});

it('cancelling a dialog never launches a task and restores the initiating button focus', async () => {
  const { onRequest, onRemove } = setup(); const dialog = choose('Crear prompt');
  fireEvent.change(screen.getByLabelText('¿Qué prompt quieres crear?'), { target: { value: 'Sin enviar.' } });
  fireEvent.click(within(dialog).getByRole('button', { name: 'Cancelar' }));
  await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  await waitFor(() => expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Crear prompt' })));
  expect(onRequest).not.toHaveBeenCalled(); expect(onRemove).not.toHaveBeenCalled();
});
