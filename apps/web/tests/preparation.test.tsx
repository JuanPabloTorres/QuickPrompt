import { createRef } from 'react';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { PreparationCanvas } from '../src/features/nova/PreparationCanvas';
import { readPreparation } from '../src/services/preparation';

const id = '10000000-0000-4000-8000-000000000001';
function draft(operationId = id) { return { id: operationId, state: 'READY', intent: 'CREATE_PROMPT', decision: 'CREATE_NEW', title: 'Explicación sencilla', description: '', templateText: 'Explica con claridad.', compiledText: 'Explica con claridad.', variables: [], values: [], missingVariables: [], question: '', reason: 'No hay coincidencias.', candidates: [], sourceId: null, sourceRevision: null, categoryId: 0, remembered: false, createdAt: new Date().toISOString() }; }
const event = (name: string, value: unknown) => `event: ${name}\ndata: ${JSON.stringify(value)}\n\n`;
function stream(text: string) { const bytes = new TextEncoder().encode(text); return new Response(new ReadableStream({ start(controller) { for (const byte of bytes) controller.enqueue(new Uint8Array([byte])); controller.close(); } })); }
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
it('accepts split UTF-8 stages and only an operation-bound final contract', async () => {
  const value = { ...draft(), title: 'α 李' }; const stage = vi.fn();
  expect((await readPreparation(stream(event('stage', { state: 'SEARCHING' }) + event('complete', value)), id, stage, new AbortController().signal)).title).toBe('α 李');
  expect(stage).toHaveBeenCalledWith('SEARCHING');
  await expect(readPreparation(stream(event('complete', draft('20000000-0000-4000-8000-000000000002'))), id, vi.fn(), new AbortController().signal)).rejects.toThrow('otra operación');
});
it('rejects partial, unknown and failed preparation without returning a usable draft', async () => {
  for (const text of [event('stage', { state: 'FILLING' }), event('tool', {}), event('error', { message: 'Falló la herramienta' }), event('complete', { id })])
    await expect(readPreparation(stream(text), id, vi.fn(), new AbortController().signal)).rejects.toThrow();
});
it('aborted preparation cannot apply late completion or stage updates', async () => {
  const controller = new AbortController(); controller.abort(); const stage = vi.fn();
  await expect(readPreparation(stream(event('complete', draft())), id, stage, controller.signal)).rejects.toThrow('Detenido'); expect(stage).not.toHaveBeenCalled();
});
it('prepares from one intention without a manual editor and preserves the previous result on a tool error', async () => {
  let requests = 0;
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/nova/drafts')) return new Response('[]');
    const input = JSON.parse(options?.body as string) as { operationId: string };
    return ++requests === 1 ? stream(event('complete', draft(input.operationId))) : stream(event('error', { message: 'Herramienta no disponible' }));
  }));
  render(<PreparationCanvas inputRef={createRef()} onSearch={vi.fn()} onState={vi.fn()} onSaved={vi.fn()} />);
  fireEvent.change(screen.getByLabelText('¿Qué quieres hacer hoy?'), { target: { value: 'Necesito explicar conceptos' } }); fireEvent.click(screen.getByRole('button', { name: 'Preparar prompt' }));
  await screen.findByRole('heading', { name: 'Explicación sencilla' }); expect(screen.queryByLabelText('Título')).toBeNull(); expect((screen.getByRole('button', { name: 'Guardar' }) as HTMLButtonElement).disabled).toBe(false);
  fireEvent.click(screen.getByRole('button', { name: 'Cerrar resultado' }));
  await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  fireEvent.change(screen.getByLabelText('¿Qué quieres hacer hoy?'), { target: { value: 'Otro intento' } }); fireEvent.click(screen.getByRole('button', { name: 'Preparar prompt' }));
  await screen.findByRole('alert'); expect(screen.queryByRole('dialog')).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Ver resultado de NOVA' }));
  expect(screen.getByRole('heading', { name: 'Explicación sencilla' })).not.toBeNull(); expect((screen.getByRole('button', { name: 'Copiar' }) as HTMLButtonElement).disabled).toBe(false);
});
it('only asks for genuinely missing fields and compiles supplied values without calling a model', async () => {
  const fetchMock = vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/nova/drafts')) return new Response('[]');
    const input = JSON.parse(options?.body as string) as { operationId: string };
    if (path.endsWith('/complete')) return Response.json({ ...draft(input.operationId), compiledText: 'Explica SQLite.', templateText: 'Explica <tema>.' });
    return stream(event('complete', { ...draft(input.operationId), state: 'NEEDS_INFORMATION', compiledText: null, variables: [{ name: 'tema', required: true }], missingVariables: ['tema'], question: '¿Puedes aportar tema?', templateText: 'Explica <tema>.' }));
  }); vi.stubGlobal('fetch', fetchMock);
  render(<PreparationCanvas inputRef={createRef()} onSearch={vi.fn()} onState={vi.fn()} onSaved={vi.fn()} />);
  fireEvent.change(screen.getByLabelText('¿Qué quieres hacer hoy?'), { target: { value: 'Prepara una explicación' } }); fireEvent.click(screen.getByRole('button', { name: 'Preparar prompt' }));
  await screen.findByLabelText('tema'); expect((screen.getByRole('button', { name: 'Copiar' }) as HTMLButtonElement).disabled).toBe(true); fireEvent.change(screen.getByLabelText('tema'), { target: { value: 'SQLite' } }); fireEvent.click(screen.getByRole('button', { name: 'Completar prompt' }));
  await waitFor(() => expect((screen.getByRole('button', { name: 'Copiar' }) as HTMLButtonElement).disabled).toBe(false)); expect(fetchMock.mock.calls.filter(([path]) => path.endsWith('/nova/prepare'))).toHaveLength(1);
  fireEvent.click(screen.getByRole('button', { name: 'Volver al borrador anterior' }));
  expect((screen.getByRole('button', { name: 'Copiar' }) as HTMLButtonElement).disabled).toBe(true); expect(screen.getByLabelText('tema')).not.toBeNull();
});

it('preserves entered fields when dismissed and does not reopen after completing in the background', async () => {
  let finish!: (response: Response) => void;
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/nova/drafts')) return new Response('[]');
    if (path.endsWith('/complete')) return new Promise<Response>(resolve => { finish = resolve; });
    const input = JSON.parse(options?.body as string) as { operationId: string };
    return stream(event('complete', { ...draft(input.operationId), state: 'NEEDS_INFORMATION', templateText: 'Explica <tema>.', compiledText: null, variables: [{ name: 'tema', required: true }], missingVariables: ['tema'], question: '¿Puedes aportar tema?' }));
  }));
  render(<PreparationCanvas inputRef={createRef()} onSearch={vi.fn()} onState={vi.fn()} onSaved={vi.fn()} />);
  fireEvent.change(screen.getByLabelText('¿Qué quieres hacer hoy?'), { target: { value: 'Una explicación' } }); fireEvent.click(screen.getByRole('button', { name: 'Preparar prompt' }));
  fireEvent.change(await screen.findByLabelText('tema'), { target: { value: 'SQLite' } });
  fireEvent.click(screen.getByRole('button', { name: 'Cerrar diálogo' }));
  await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  expect(screen.queryByText('Explica <tema>.')).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Ver resultado de NOVA' }));
  expect((screen.getByLabelText('tema') as HTMLTextAreaElement).value).toBe('SQLite');
  fireEvent.click(screen.getByRole('button', { name: 'Completar prompt' }));
  fireEvent.click(screen.getByRole('button', { name: 'Cerrar resultado' }));
  await act(async () => { finish(Response.json({ ...draft(), compiledText: 'Explica SQLite.' })); });
  await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  fireEvent.click(screen.getByRole('button', { name: 'Ver resultado de NOVA' }));
  expect(screen.getByText('Explica SQLite.')).not.toBeNull();
  expect((screen.getByRole('button', { name: 'Copiar' }) as HTMLButtonElement).disabled).toBe(false);
});

it('dispatches handled local commands without generating a new prompt', async () => {
  const fetchMock = vi.fn(async () => new Response('[]')); vi.stubGlobal('fetch', fetchMock);
  const onCommand = vi.fn(async () => true);
  render(<PreparationCanvas inputRef={createRef()} onSearch={vi.fn()} onState={vi.fn()} onSaved={vi.fn()} onCommand={onCommand} />);
  fireEvent.change(screen.getByLabelText('¿Qué quieres hacer hoy?'), { target: { value: 'Ve al prompt arquitectura' } }); fireEvent.click(screen.getByRole('button', { name: 'Preparar prompt' }));
  await waitFor(() => expect(onCommand).toHaveBeenCalledOnce());
  await waitFor(() => expect((screen.getByLabelText('¿Qué quieres hacer hoy?') as HTMLTextAreaElement).disabled).toBe(false));
  expect(fetchMock.mock.calls).toHaveLength(1); expect(screen.queryByRole('dialog')).toBeNull();
});

it('cancelling command resolution cannot fall through into prompt generation', async () => {
  const fetchMock = vi.fn(async () => new Response('[]')); vi.stubGlobal('fetch', fetchMock);
  const onCommand = vi.fn((_message: string, signal: AbortSignal) => new Promise<boolean>(resolve => { signal.addEventListener('abort', () => resolve(false), { once: true }); }));
  render(<PreparationCanvas inputRef={createRef()} onSearch={vi.fn()} onState={vi.fn()} onSaved={vi.fn()} onCommand={onCommand} />);
  fireEvent.change(screen.getByLabelText('¿Qué quieres hacer hoy?'), { target: { value: 'Ve al prompt arquitectura' } }); fireEvent.click(screen.getByRole('button', { name: 'Preparar prompt' }));
  await waitFor(() => expect(onCommand).toHaveBeenCalledOnce());
  await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Detener' })); });
  expect(fetchMock.mock.calls).toHaveLength(1); expect(screen.queryByRole('dialog')).toBeNull();
});
