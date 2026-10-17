import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { NovaLive } from '../src/features/nova/NovaLive';
import { NovaAssist } from '../src/features/nova/NovaAssist';
import { EditorPreparation } from '../src/features/prompts/EditorPreparation';
import { Dialog } from '../src/components/ui/Dialog';

const id = '10000000-0000-4000-8000-000000000001';
const source = { id, title: 'Ejemplo', templateText: 'Explica <tema>.', description: '', categoryId: 0, isFavorite: false, isArchived: false, revision: 1, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(), uses: 0, lastUsedAt: null, variableNames: ['tema'], defaults: {} };
class Events extends EventTarget {
  static items: Events[] = []; closed = false; onopen: (() => void) | null = null; onerror: (() => void) | null = null;
  constructor(public url: string) { super(); Events.items.push(this); }
  close() { this.closed = true; }
  send(sequence: number, stage = 'Execution') { this.dispatchEvent(new MessageEvent('activity', { data: JSON.stringify({ runId: id, eventId: `20000000-0000-4000-8000-${String(sequence).padStart(12, '0')}`, sequence, timestamp: new Date().toISOString(), stage, actor: 'MODEL_SELECTED', toolName: 'ui_open_prompt', targetId: null, status: 'TOOL_PROPOSED', durationMs: 10, errorCode: null, safeDisplayMessage: `Acción observada ${sequence}` }) })); }
}
function view(status = 'CREATED', sequence = 0) { return { run: { runId: id, sessionId: id, mode: 'BROWSER', status, sequence, targetId: null, revision: null, pauseRequested: status === 'PAUSED', browserOpen: true, approval: 'NONE', effectCommitted: false, errorCode: null }, request: 'Abre el prompt de ejemplo.', question: null, observation: null, proposal: null }; }
afterEach(() => { cleanup(); vi.unstubAllGlobals(); Events.items = []; });
function setup() {
  let current = view(); const calls: string[] = [];
  vi.stubGlobal('EventSource', Events); vi.stubGlobal('fetch', vi.fn(async (path: string) => {
    calls.push(path);
    if (path.endsWith('/pause')) { current = view('PAUSED', 1); return Response.json(current.run); }
    if (path.endsWith('/resume')) { current = view('INTERPRETING', 2); return Response.json(current.run); }
    if (path.endsWith('/cancel')) { current = view('CANCELLED', 3); return Response.json(current.run); }
    return Response.json(current);
  }));
  render(<NovaLive runId={id} worker={false} onCommit={async () => {}} onReview={vi.fn()} />); return calls;
}
async function openActivity() { fireEvent.click(await screen.findByRole('button', { name: 'Actividad de NOVA' })); }
it('shows collapsed activity only during a task and controls actual pause, resume and cancellation', async () => {
  const calls = setup(); const toggle = await screen.findByRole('button', { name: 'Actividad de NOVA' });
  expect(toggle.getAttribute('aria-expanded')).toBe('false'); expect(screen.queryByRole('button', { name: 'Pausar' })).toBeNull();
  fireEvent.click(toggle); expect(toggle.getAttribute('aria-expanded')).toBe('true');
  fireEvent.click(screen.getByRole('button', { name: 'Pausar' })); await screen.findByRole('heading', { name: 'En pausa' }); expect(calls).toContain(`/api/v1/nova/live/${id}/pause`);
  fireEvent.click(screen.getByRole('button', { name: 'Continuar' })); await waitFor(() => expect(screen.queryByRole('button', { name: 'Pausar' })).toBeTruthy());
  fireEvent.click(screen.getByRole('button', { name: 'Detener NOVA' })); await waitFor(() => expect(screen.queryByRole('button', { name: 'Actividad de NOVA' })).toBeNull()); expect(calls).toContain(`/api/v1/nova/live/${id}/cancel`);
});
it('keeps functional step counts without rendering raw messages, tools, identifiers or provenance', async () => {
  setup(); await openActivity(); const stream = Events.items.at(-1)!;
  act(() => { for (let sequence = 1; sequence <= 125; sequence++) stream.send(sequence, 'Decision'); });
  expect(screen.getByText(/Paso 125/)).toBeTruthy(); expect(screen.queryByText(/Acción observada/)).toBeNull();
  expect(screen.queryByText('ui_open_prompt')).toBeNull(); expect(screen.queryByText(id)).toBeNull(); expect(screen.queryByText('Ver actividad y procedencia')).toBeNull();
  expect(within(screen.getByRole('list', { name: 'Actividad reciente de NOVA' })).getAllByRole('listitem')).toHaveLength(1);
  cleanup(); act(() => stream.send(126, 'Decision')); expect(stream.closed).toBe(true);
});
it('does not release the local pause until the backend accepts resume', async () => {
  const current = view('PAUSED', 1); let accept: ((response: Response) => void) | undefined;
  const controls: string[] = []; const listener = (event: Event) => controls.push((event as CustomEvent<string>).detail); window.addEventListener('nova-ui-control', listener);
  vi.stubGlobal('EventSource', Events); vi.stubGlobal('fetch', vi.fn(async (path: string) => path.endsWith('/resume') ? new Promise<Response>(resolve => { accept = resolve; }) : Response.json(current)));
  render(<NovaLive runId={id} worker={false} onCommit={async () => {}} onReview={vi.fn()} />); await openActivity();
  fireEvent.click(screen.getByRole('button', { name: 'Continuar' })); await waitFor(() => expect(accept).toBeDefined()); expect(controls).not.toContain('resume');
  accept!(Response.json(view('INTERPRETING', 2).run)); await waitFor(() => expect(controls).toContain('resume')); window.removeEventListener('nova-ui-control', listener);
});
it('offers stop instead of resume when a reloaded same-tab document lost ownership', async () => {
  vi.stubGlobal('EventSource', Events); vi.stubGlobal('fetch', vi.fn(async () => Response.json({ ...view('PAUSED', 1), run: { ...view('PAUSED', 1).run, mode: 'SAME_TAB' } })));
  render(<NovaLive runId={id} worker={false} onCommit={async () => {}} onReview={vi.fn()} />); await openActivity();
  expect(screen.queryByRole('button', { name: 'Continuar' })).toBeNull(); expect(screen.getByRole('button', { name: 'Detener NOVA' })).toBeTruthy();
});
it('deduplicates replayed events and reconnects a gap from the last confirmed sequence', async () => {
  const calls = setup(); await openActivity(); const first = Events.items[0];
  await act(async () => { first.send(1); first.send(1); });
  expect(calls.filter(path => path === `/api/v1/nova/live/${id}`)).toHaveLength(2);
  await act(async () => { first.send(3); }); expect(first.closed).toBe(true); expect(Events.items[1].url).toContain('after=1');
  await act(async () => { Events.items[1].send(2); Events.items[1].send(3); });
  expect(calls.filter(path => path === `/api/v1/nova/live/${id}`)).toHaveLength(4);
});
it('a disconnected activity observer never starts another run or invents completion', async () => {
  const calls = setup(); await openActivity();
  await act(async () => { Events.items[0].onerror?.(); }); await screen.findByText(/Conexión interrumpida/);
  await waitFor(() => expect(calls.filter(path => path === `/api/v1/nova/live/${id}`).length).toBeGreaterThan(1)); expect(calls.every(path => path.endsWith(id))).toBe(true);
});
it('stays absent without a task or after a completed task', async () => {
  vi.stubGlobal('EventSource', Events); vi.stubGlobal('fetch', vi.fn(async () => Response.json(view('COMPLETED', 1))));
  const mounted = render(<NovaLive runId={null} worker={false} onCommit={async () => {}} onReview={vi.fn()} />);
  expect(screen.queryByRole('button', { name: 'Actividad de NOVA' })).toBeNull();
  mounted.rerender(<NovaLive runId={id} worker={false} onCommit={async () => {}} onReview={vi.fn()} />);
  await waitFor(() => expect(fetch).toHaveBeenCalled()); expect(screen.queryByTestId('nova-live')).toBeNull();
});
it('closes with Escape, restores its trigger and stays inside an open dialog focus scope', async () => {
  setup(); await openActivity(); const toggle = screen.getByRole('button', { name: 'Actividad de NOVA' });
  fireEvent.keyDown(screen.getByRole('button', { name: 'Pausar' }), { key: 'Escape' });
  expect(toggle.getAttribute('aria-expanded')).toBe('false'); expect(document.activeElement).toBe(toggle);
  const modal = render(<Dialog open onOpenChange={vi.fn()} title="Elegir destino" description="Prepara tu prompt"><button>Usar destino</button></Dialog>);
  await waitFor(() => expect(screen.getByRole('dialog').contains(screen.getByTestId('nova-live'))).toBe(true));
  await openActivity(); expect(within(screen.getByRole('dialog')).getByRole('button', { name: 'Detener NOVA' })).toBeTruthy();
  modal.unmount(); await waitFor(() => expect(document.body.contains(screen.getByTestId('nova-live'))).toBe(true));
});
it('manual variable suggestions bind their model request to the observed run', async () => {
  const observed = vi.fn(), calls: { path: string; headers?: HeadersInit }[] = [];
  vi.stubGlobal('fetch', vi.fn(async (path: string, options: RequestInit) => { calls.push({ path, headers: options.headers }); return Response.json(path.endsWith('/observe') ? view().run : { tema: 'matemáticas' }); }));
  render(<NovaAssist message="tema: matemáticas" source={source} sessionId={id} onLiveRun={observed} onOpen={vi.fn()} onDraft={vi.fn()} />);
  fireEvent.click(screen.getByRole('button', { name: 'Sugerir valores' })); await screen.findByText('matemáticas');
  expect(observed).toHaveBeenCalledWith(id); expect(calls.find(call => call.path.endsWith('/nova/variables'))?.headers).toMatchObject({ 'X-Nova-Live-Run': id });
});
it('manual editor preparation sends the same observed identity to its real proposal endpoint', async () => {
  const observe = vi.fn(async () => id), changed = vi.fn(), calls: { path: string; headers?: HeadersInit }[] = [];
  vi.stubGlobal('fetch', vi.fn(async (path: string, options: RequestInit) => { calls.push({ path, headers: options.headers }); return Response.json({ id, templateId: id, baseRevision: 1, text: 'Resume <tema>.', reasons: ['Cambio'], changes: ['Cambio'], warnings: [], variablesPreserved: ['tema'], model: 'qwen3:8b' }); }));
  render(<EditorPreparation source={source} draft={source} values={{}} onChange={changed} onValues={vi.fn()} onUse={vi.fn()} onObserve={observe} />);
  fireEvent.change(screen.getByLabelText('Cambio que necesitas'), { target: { value: 'Hazlo más breve.' } }); fireEvent.click(screen.getByRole('button', { name: 'Preparar cambio en el editor' }));
  await waitFor(() => expect(changed).toHaveBeenCalled()); expect(observe).toHaveBeenCalledWith('Hazlo más breve.', source, expect.any(AbortSignal)); expect(calls.find(call => call.path.endsWith('/nova/propose'))?.headers).toMatchObject({ 'X-Nova-Live-Run': id });
});
