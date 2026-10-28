import { useState } from 'react';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { beginActivity, confirmActivity, getActivity, progressActivity } from '../src/features/nova/activity';
import { readUiState, type UiAction, type UiState } from '../src/features/nova/AgentUiDriver';
import { SameTabAgent } from '../src/features/nova/SameTabAgent';
import { NovaLive } from '../src/features/nova/NovaLive';
import { startVisible } from '../src/services/live';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });
const action = (tool = 'ui_fill_variable'): UiAction => ({ runId: crypto.randomUUID(), actionId: crypto.randomUUID(), sequence: 1, tool, target: null, arguments: { name: 'tema', value: 'un valor propuesto' }, timestamp: new Date().toISOString(), executionStatus: 'DISPATCHED', verificationStatus: 'PENDING' });
const state = (values: Record<string, string> = {}): UiState => ({ mode: 'focus', candidates: [], promptId: null, revision: null, title: 'Ejemplo', templateText: 'Explica <tema>.', values, variableNames: Object.keys(values), draftChanged: false, compiledReady: false, compiledText: null, response: null, responseComplete: false, providerOpen: false, providerId: null, error: null });

it('records actual observed values only after confirmation, with previous values and idempotent retries', () => {
  const task = action(); beginActivity(task); progressActivity(task, 'checking');
  expect(getActivity(task.runId)[0].fields).toEqual([]);
  confirmActivity(task, state({ tema: 'anterior' }), state({ tema: 'Valor realmente observado 🚀', otro: 'dato existente' }));
  beginActivity(task); confirmActivity(task, state(), state({ tema: 'un reintento incompatible' })); progressActivity(task, 'confirming');
  expect(getActivity(task.runId)).toHaveLength(1);
  expect(getActivity(task.runId)[0]).toMatchObject({ status: 'verified', fields: [{ name: 'tema', value: 'Valor realmente observado 🚀', previous: 'anterior' }] });
});
it('never attributes existing field values to navigation or claims interrupted actions succeeded', () => {
  const navigation = action('ui_open_prompt'); beginActivity(navigation); confirmActivity(navigation, state(), state({ tema: 'Escrito por el usuario' }));
  expect(getActivity(navigation.runId)[0].fields).toEqual([]);
  const failed = action(); beginActivity(failed); progressActivity(failed, 'interrupted', 'UI_VALUE_MISMATCH');
  expect(getActivity(failed.runId)[0]).toMatchObject({ status: 'interrupted', fields: [], errorCode: 'UI_VALUE_MISMATCH' });
});
it('keeps exact generated title and content, bounds history and isolates runs', () => {
  const creation = action('ui_prepare_creation'); beginActivity(creation);
  const after = { ...state(), title: 'Título generado', templateText: 'Texto íntegro <tema>\n'.repeat(100) };
  confirmActivity(creation, { ...state(), title: '', templateText: '' }, after);
  expect(getActivity(creation.runId)[0].fields.map(field => field.value)).toEqual([after.title, after.templateText]);
  for (let sequence = 2; sequence <= 105; sequence++) beginActivity({ ...creation, actionId: crypto.randomUUID(), sequence });
  expect(getActivity(creation.runId)).toHaveLength(100); expect(getActivity(creation.runId)[0].sequence).toBe(6);
  expect(getActivity(crypto.randomUUID())).toEqual([]);
});

it.each(['fill', 'create'])('publishes %s fields only after the accepted real-driver receipt and never repeats writes on delivery retry', async kind => {
  const task = action(kind === 'create' ? 'ui_prepare_creation' : 'ui_fill_variable');
  task.arguments = kind === 'create' ? { title: 'Mi prompt real 🚀', templateText: 'Revisa <proyecto> y organiza sus próximos pasos.\nNo inventes datos.' } : { name: 'tema', value: 'React para proyectos 🚀' };
  const run = { runId: task.runId, sessionId: crypto.randomUUID(), mode: 'SAME_TAB', status: 'TOOL_RUNNING', sequence: 0, targetId: null, revision: null, pauseRequested: false, browserOpen: true, approval: 'NONE', effectCommitted: false, errorCode: null };
  const changed = vi.fn(); let receiptCalls = 0, delivered = false;
  let accept: ((response: Response) => void) | undefined;
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([{}] as unknown as DOMRectList);
  HTMLElement.prototype.scrollIntoView = vi.fn(); vi.stubGlobal('matchMedia', () => ({ matches: true }));
  vi.stubGlobal('EventSource', class extends EventTarget { close() {} });
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/nova/live')) { task.runId = run.runId = JSON.parse(options!.body as string).runId; return Response.json(run); }
    if (path.endsWith('/actions/progress')) return new Promise<Response>((_resolve, reject) => {
      options?.signal?.addEventListener('abort', () => reject(new DOMException('Slow telemetry', 'AbortError')), { once: true });
    });
    if (path.endsWith('/actions/receipt')) {
      receiptCalls++;
      if (receiptCalls === 1) return new Response(null, { status: 503 });
      return new Promise<Response>(resolve => { accept = response => { delivered = true; resolve(response); }; });
    }
    if (path.includes('/actions?')) return Response.json({ run, action: delivered ? null : task });
    return Response.json({ run, request: 'Completa tema con React para proyectos 🚀', question: null, observation: readUiState(), proposal: null });
  }));
  await startVisible('Completa tema.', run.sessionId).then(started => { task.runId = started.runId; });
  function Focus() { const [value, setValue] = useState(''); return <section data-testid="prompt-focus" data-template-text="Explica <tema>."><h1 data-testid="focus-title">Ejemplo</h1><textarea aria-label="tema" data-testid="variable-field" data-variable-name="tema" value={value} onChange={event => { changed(); setValue(event.target.value); }} /><span data-testid="compile-ready" /><p data-testid="compiled-preview">Explica {value}.</p></section>; }
  function Editor() { const [title, setTitle] = useState(''), [body, setBody] = useState(''); return <section data-testid="prompt-editor"><input id="prompt-title" aria-label="Título" value={title} onChange={event => { changed(); setTitle(event.target.value); }} /><textarea id="prompt-content" aria-label="Contenido" value={body} onChange={event => { changed(); setBody(event.target.value); }} /><span data-testid="variables-inspected" /><span data-testid="compile-ready" /></section>; }
  render(<>{kind === 'create' ? <Editor /> : <Focus />}<SameTabAgent runId={task.runId} speed="fast" onActive={vi.fn()} /><NovaLive runId={task.runId} worker={false} onReview={vi.fn()} /></>);
  fireEvent.click(await screen.findByRole('button', { name: 'Actividad de NOVA' }));
  await waitFor(() => expect(accept).toBeDefined(), { timeout: 4000 });
  if (kind === 'create') {
    expect(screen.getByLabelText('Título')).toHaveProperty('value', task.arguments.title);
    expect(screen.getByLabelText('Contenido')).toHaveProperty('value', task.arguments.templateText);
  } else expect(screen.getByLabelText('tema')).toHaveProperty('value', task.arguments.value);
  expect(getActivity(task.runId)[0]).toMatchObject({ status: 'confirming', fields: [] });
  await act(async () => accept!(new Response(null, { status: 204 })));
  await screen.findByText('Comprobado en pantalla');
  const expected = kind === 'create' ? [task.arguments.title, task.arguments.templateText] : [task.arguments.value];
  expect(Array.from(document.querySelectorAll('.activity-fields dd > pre')).map(node => node.textContent)).toEqual(expected);
  expect(getActivity(task.runId)).toHaveLength(1); expect(changed).toHaveBeenCalledTimes(kind === 'create' ? 2 : 1); expect(receiptCalls).toBe(2);
});
