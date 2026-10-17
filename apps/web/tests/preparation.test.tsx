import { createRef, type ReactElement } from 'react';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { PreparationCanvas } from '../src/features/nova/PreparationCanvas';
import { readPreparation } from '../src/services/preparation';

function renderCanvas(element: ReactElement) { const result = render(element); fireEvent.change(screen.getByLabelText('Modo de NOVA'), { target: { value: 'LIVE' } }); return result; }
const id = '10000000-0000-4000-8000-000000000001';
function draft(operationId = id) { return { id: operationId, state: 'READY', intent: 'CREATE_PROMPT', decision: 'CREATE_NEW', title: 'Explicación sencilla', description: '', templateText: 'Explica con claridad.', compiledText: 'Explica con claridad.', variables: [], values: [], missingVariables: [], question: '', reason: 'No hay coincidencias.', candidates: [], sourceId: null, sourceRevision: null, categoryId: 0, remembered: false, createdAt: new Date().toISOString() }; }
const event = (name: string, value: unknown) => `event: ${name}\ndata: ${JSON.stringify(value)}\n\n`;
function stream(text: string) { const bytes = new TextEncoder().encode(text); return new Response(new ReadableStream({ start(controller) { for (const byte of bytes) controller.enqueue(new Uint8Array([byte])); controller.close(); } })); }
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
it('streams local response only while the request remains active', async () => {
  const response = vi.fn();
  await readPreparation(stream(event('response', { text: 'Respuesta α' }) + event('complete', { ...draft(), localResponse: 'Respuesta α' })), id, vi.fn(), new AbortController().signal, response);
  expect(response).toHaveBeenCalledWith('Respuesta α');
  const aborted = new AbortController(); aborted.abort(); response.mockClear();
  await expect(readPreparation(stream(event('response', { text: 'Tardío' })), id, vi.fn(), aborted.signal, response)).rejects.toThrow('Detenido'); expect(response).not.toHaveBeenCalled();
});
it('passes typed UI context and collection filters without classifying pronouns in React', async () => {
  const calls: { path: string; body?: unknown }[] = [];
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    calls.push({ path, body: options?.body ? JSON.parse(options.body as string) : undefined });
    if (path.endsWith('/nova/drafts')) return new Response('[]');
    if (path.endsWith('/context')) return new Response(null, { status: 204 });
    const body = JSON.parse(options?.body as string) as { operationId: string };
    return stream(event('complete', draft(body.operationId)));
  }));
  const selected = { id, title: 'Anterior', templateText: 'Anterior.', description: '', categoryId: 0, isFavorite: false, isArchived: false, revision: 1, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(), uses: 0, lastUsedAt: null, variableNames: [], defaults: {} };
  const groupId = '20000000-0000-4000-8000-000000000002';
  renderCanvas(<PreparationCanvas sessionId={id} inputRef={createRef()} contextPrompt={selected} retrievalFilter={{ groupId, favorites: true }} onState={vi.fn()} onSaved={vi.fn()} />);
  fireEvent.change(screen.getByLabelText('¿Qué quieres hacer hoy?'), { target: { value: 'Crea una instrucción nueva' } }); fireEvent.click(screen.getByRole('button', { name: 'Preparar prompt' }));
  await screen.findByRole('heading', { name: 'Explicación sencilla' });
  expect(calls.find(call => call.path.endsWith('/context'))?.body).toMatchObject({ promptId: id, revision: 1, allowStaleContext: true });
  expect(calls.find(call => call.path.endsWith('/nova/prepare'))?.body).toMatchObject({ sessionId: id, filter: { groupId, favorites: true } });
});
it('starts a new explicit operation after a known failure and after a completed result', async () => {
  const operations: string[] = [];
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/nova/drafts')) return new Response('[]');
    const input = JSON.parse(options?.body as string) as { operationId: string }; operations.push(input.operationId);
    return operations.length === 1 ? stream(event('error', { code: 'TIMEOUT', message: 'Tiempo agotado.' })) : stream(event('complete', draft(input.operationId)));
  }));
  renderCanvas(<PreparationCanvas sessionId={id} inputRef={createRef()} onState={vi.fn()} onSaved={vi.fn()} />);
  fireEvent.change(screen.getByLabelText('¿Qué quieres hacer hoy?'), { target: { value: 'Crea un prompt sencillo' } }); fireEvent.click(screen.getByRole('button', { name: 'Preparar prompt' })); await screen.findByRole('alert');
  fireEvent.click(screen.getByRole('button', { name: 'Preparar prompt' })); await screen.findByRole('heading', { name: 'Explicación sencilla' });
  fireEvent.click(screen.getByRole('button', { name: 'Cerrar resultado' })); await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  fireEvent.click(screen.getByRole('button', { name: 'Preparar prompt' })); await screen.findByRole('heading', { name: 'Explicación sencilla' });
  expect(operations).toHaveLength(3); expect(new Set(operations).size).toBe(3);
});
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
  renderCanvas(<PreparationCanvas sessionId={id} inputRef={createRef()} onState={vi.fn()} onSaved={vi.fn()} />);
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
    if (path.endsWith('/draft-context')) return new Response(null, { status: 204 });
    const input = JSON.parse(options?.body as string) as { operationId: string };
    if (path.endsWith('/complete')) return Response.json({ ...draft(input.operationId), compiledText: 'Explica SQLite.', templateText: 'Explica <tema>.' });
    return stream(event('complete', { ...draft(input.operationId), state: 'NEEDS_INFORMATION', compiledText: null, variables: [{ name: 'tema', required: true }], missingVariables: ['tema'], question: '¿Puedes aportar tema?', templateText: 'Explica <tema>.' }));
  }); vi.stubGlobal('fetch', fetchMock);
  renderCanvas(<PreparationCanvas sessionId={id} inputRef={createRef()} onState={vi.fn()} onSaved={vi.fn()} />);
  fireEvent.change(screen.getByLabelText('¿Qué quieres hacer hoy?'), { target: { value: 'Prepara una explicación' } }); fireEvent.click(screen.getByRole('button', { name: 'Preparar prompt' }));
  await screen.findByLabelText('tema'); expect((screen.getByRole('button', { name: 'Copiar' }) as HTMLButtonElement).disabled).toBe(true); fireEvent.change(screen.getByLabelText('tema'), { target: { value: 'SQLite' } }); fireEvent.click(screen.getByRole('button', { name: 'Completar prompt' }));
  await waitFor(() => expect((screen.getByRole('button', { name: 'Copiar' }) as HTMLButtonElement).disabled).toBe(false)); expect(fetchMock.mock.calls.filter(([path]) => path.endsWith('/nova/prepare'))).toHaveLength(1);
  fireEvent.click(screen.getByRole('button', { name: 'Volver al borrador anterior' }));
  expect(await screen.findByLabelText('tema')).not.toBeNull(); expect((screen.getByRole('button', { name: 'Copiar' }) as HTMLButtonElement).disabled).toBe(true);
  expect(fetchMock.mock.calls.filter(([path]) => path.endsWith('/draft-context'))).toHaveLength(1);
});
it('a read-only result remains readable while offering optional inputs needed to use it', async () => {
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/nova/drafts')) return new Response('[]');
    const input = JSON.parse(options?.body as string) as { operationId: string };
    return stream(event('complete', { ...draft(input.operationId), state: 'USER_ACTION', compiledText: null, variables: [{ name: 'tema', required: true }], missingVariables: ['tema'], question: '', templateText: 'Explica <tema>.' }));
  }));
  renderCanvas(<PreparationCanvas sessionId={id} inputRef={createRef()} onState={vi.fn()} onSaved={vi.fn()} />);
  fireEvent.change(screen.getByLabelText('¿Qué quieres hacer hoy?'), { target: { value: 'Busca una explicación' } });
  fireEvent.click(screen.getByRole('button', { name: 'Preparar prompt' }));
  expect(await screen.findByLabelText('tema')).not.toBeNull();
  expect(screen.getByText('Para utilizar este prompt, completa sus datos.')).not.toBeNull();
  expect((screen.getByRole('button', { name: 'Copiar' }) as HTMLButtonElement).disabled).toBe(true);
});
it('changed editor context cannot reuse the operation identity of a failed transport request', async () => {
  const operations: string[] = [];
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/nova/drafts')) return new Response('[]');
    const input = JSON.parse(options?.body as string) as { operationId: string }; operations.push(input.operationId);
    return operations.length === 1 ? new Response('', { status: 503 }) : stream(event('complete', draft(input.operationId)));
  }));
  const props = { sessionId: id, inputRef: createRef<HTMLTextAreaElement>(), onState: vi.fn(), onSaved: vi.fn() };
  const rendered = renderCanvas(<PreparationCanvas {...props} contextKey="first editor snapshot" />);
  fireEvent.change(screen.getByLabelText('¿Qué quieres hacer hoy?'), { target: { value: 'Prepara una adaptación' } });
  fireEvent.click(screen.getByRole('button', { name: 'Preparar prompt' })); await screen.findByRole('alert');
  rendered.rerender(<PreparationCanvas {...props} contextKey="changed editor snapshot" />);
  fireEvent.click(screen.getByRole('button', { name: 'Preparar prompt' })); await screen.findByRole('heading', { name: 'Explicación sencilla' });
  expect(operations).toHaveLength(2); expect(operations[0]).not.toBe(operations[1]);
});

it('preserves entered fields when dismissed and does not reopen after completing in the background', async () => {
  let finish!: (response: Response) => void;
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/nova/drafts')) return new Response('[]');
    if (path.endsWith('/complete')) return new Promise<Response>(resolve => { finish = resolve; });
    const input = JSON.parse(options?.body as string) as { operationId: string };
    return stream(event('complete', { ...draft(input.operationId), state: 'NEEDS_INFORMATION', templateText: 'Explica <tema>.', compiledText: null, variables: [{ name: 'tema', required: true }], missingVariables: ['tema'], question: '¿Puedes aportar tema?' }));
  }));
  renderCanvas(<PreparationCanvas sessionId={id} inputRef={createRef()} onState={vi.fn()} onSaved={vi.fn()} />);
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

it('routes a natural navigation request through the unified interpreter before presenting the result', async () => {
  const fetchMock = vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/nova/drafts')) return new Response('[]');
    const input = JSON.parse(options?.body as string) as { operationId: string };
    return stream(event('complete', draft(input.operationId)));
  }); vi.stubGlobal('fetch', fetchMock); const onPrepared = vi.fn(async () => true);
  renderCanvas(<PreparationCanvas sessionId={id} inputRef={createRef()} onState={vi.fn()} onSaved={vi.fn()} onPrepared={onPrepared} />);
  fireEvent.change(screen.getByLabelText('¿Qué quieres hacer hoy?'), { target: { value: 'Ve al prompt arquitectura' } }); fireEvent.click(screen.getByRole('button', { name: 'Preparar prompt' }));
  await waitFor(() => expect(onPrepared).toHaveBeenCalledOnce());
  expect(fetchMock.mock.calls.filter(([path]) => path.endsWith('/nova/prepare'))).toHaveLength(1);
  expect(fetchMock.mock.calls.some(([path]) => path.endsWith('/nova/command'))).toBe(false); expect(screen.queryByRole('dialog')).toBeNull();
});

it('clears the activity indicator before a completed result navigates out of the canvas', async () => {
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/nova/drafts')) return new Response('[]');
    const input = JSON.parse(options?.body as string) as { operationId: string };
    return stream(event('complete', draft(input.operationId)));
  }));
  const onState = vi.fn();
  let rendered: ReturnType<typeof render>;
  const onPrepared = vi.fn(async () => { rendered.unmount(); return true; });
  rendered = renderCanvas(<PreparationCanvas sessionId={id} inputRef={createRef()} onState={onState} onSaved={vi.fn()} onPrepared={onPrepared} />);
  fireEvent.change(screen.getByLabelText('¿Qué quieres hacer hoy?'), { target: { value: 'Prepara una propuesta' } });
  fireEvent.click(screen.getByRole('button', { name: 'Preparar prompt' }));
  await waitFor(() => expect(onPrepared).toHaveBeenCalledOnce());
  expect(onState).toHaveBeenLastCalledWith('idle');
});

it('cancelling the unified request aborts its transport and never applies a late result', async () => {
  let active: AbortSignal | null = null;
  const fetchMock = vi.fn((path: string, options?: RequestInit) => {
    if (path.endsWith('/nova/drafts')) return Promise.resolve(new Response('[]'));
    active = options?.signal ?? null;
    return new Promise<Response>((_resolve, reject) => { active?.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')), { once: true }); });
  }); vi.stubGlobal('fetch', fetchMock); const onPrepared = vi.fn(async () => true);
  renderCanvas(<PreparationCanvas sessionId={id} inputRef={createRef()} onState={vi.fn()} onSaved={vi.fn()} onPrepared={onPrepared} />);
  fireEvent.change(screen.getByLabelText('¿Qué quieres hacer hoy?'), { target: { value: 'Ve al prompt arquitectura' } }); fireEvent.click(screen.getByRole('button', { name: 'Preparar prompt' }));
  await waitFor(() => expect(fetchMock.mock.calls.filter(([path]) => path.endsWith('/nova/prepare'))).toHaveLength(1));
  await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Detener' })); });
  expect(active!.aborted).toBe(true); expect(onPrepared).not.toHaveBeenCalled(); expect(screen.queryByRole('dialog')).toBeNull();
});

it('opens the chosen candidate through the prepared-result handler while its clarification modal is open', async () => {
  const candidate = { id, title: 'Correo para clientes', templateText: 'Redacta una respuesta.', description: '', categoryId: 0, isFavorite: false, isArchived: false, revision: 1, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(), uses: 0, lastUsedAt: null, variableNames: [], defaults: {} };
  const inputs: { sourcePromptId?: string; message: string }[] = [];
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/nova/drafts')) return new Response('[]');
    const input = JSON.parse(options?.body as string); inputs.push(input);
    const result = inputs.length === 1
      ? { ...draft(input.operationId), state: 'NEEDS_INFORMATION', decision: 'ASK_CLARIFICATION', title: 'Elige el origen', question: '¿Cuál quieres usar?', candidates: [{ prompt: candidate, score: 3, reason: 'Opción real' }] }
      : { ...draft(input.operationId), decision: 'REUSE_EXISTING', title: candidate.title, templateText: candidate.templateText, compiledText: candidate.templateText, sourceId: candidate.id, sourceRevision: candidate.revision };
    return stream(event('complete', result));
  }));
  const onPrepared = vi.fn(async (result: { decision: string }) => result.decision !== 'ASK_CLARIFICATION');
  renderCanvas(<PreparationCanvas sessionId={id} inputRef={createRef()} onState={vi.fn()} onSaved={vi.fn()} onPrepared={onPrepared} />);
  fireEvent.change(screen.getByLabelText('¿Qué quieres hacer hoy?'), { target: { value: 'Abre mi prompt de correo.' } }); fireEvent.click(screen.getByRole('button', { name: 'Preparar prompt' }));
  await screen.findByRole('dialog'); fireEvent.click(await screen.findByRole('button', { name: /Correo para clientes/ }));
  await waitFor(() => expect(onPrepared).toHaveBeenCalledTimes(2));
  expect(inputs[1]).toMatchObject({ sourcePromptId: id, message: 'Abre mi prompt de correo.' });
  await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
});
