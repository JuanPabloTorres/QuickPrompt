import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { NovaLive } from '../src/features/nova/NovaLive';
import { EditorPreparation } from '../src/features/prompts/EditorPreparation';
import { Dialog } from '../src/components/ui/Dialog';
import { startVisible } from '../src/services/live';
import { AgentDock } from '../src/features/nova/AgentDock';
import { domAnimation, LazyMotion } from 'motion/react';

const id = '10000000-0000-4000-8000-000000000001';
const source = { id, title: 'Ejemplo', templateText: 'Explica <tema>.', description: '', categoryId: 0, isFavorite: false, isArchived: false, revision: 1, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(), uses: 0, lastUsedAt: null, variableNames: ['tema'], defaults: {} };
class Events extends EventTarget {
  static items: Events[] = []; closed = false; onopen: (() => void) | null = null; onerror: (() => void) | null = null;
  constructor(public url: string) { super(); Events.items.push(this); }
  close() { this.closed = true; }
  send(sequence: number, stage = 'Execution', overrides: Record<string, unknown> = {}) { this.dispatchEvent(new MessageEvent('activity', { data: JSON.stringify({ runId: id, eventId: `20000000-0000-4000-8000-${String(sequence).padStart(12, '0')}`, sequence, timestamp: new Date().toISOString(), stage, actor: 'MODEL_SELECTED', toolName: 'ui_open_prompt', targetId: null, status: 'TOOL_PROPOSED', durationMs: 10, errorCode: null, safeDisplayMessage: `Acción observada ${sequence}`, ...overrides }) })); }
}
function view(status = 'CREATED', sequence = 0) { return { run: { runId: id, sessionId: id, mode: 'BROWSER', status, sequence, targetId: null, revision: null, pauseRequested: status === 'PAUSED', browserOpen: true, approval: 'NONE', effectCommitted: false, errorCode: null }, request: 'Abre el prompt de ejemplo.', question: null, observation: null, proposal: null }; }
afterEach(() => { cleanup(); vi.useRealTimers(); vi.unstubAllGlobals(); Events.items = []; });
function setup() {
  let current = view(); const calls: string[] = [];
  vi.stubGlobal('EventSource', Events); vi.stubGlobal('fetch', vi.fn(async (path: string) => {
    calls.push(path);
    if (path.endsWith('/pause')) { current = view('PAUSED', 1); return Response.json(current.run); }
    if (path.endsWith('/resume')) { current = view('INTERPRETING', 2); return Response.json(current.run); }
    if (path.endsWith('/cancel')) { current = view('CANCELLED', 3); return Response.json(current.run); }
    return Response.json(current);
  }));
  render(<NovaLive runId={id} worker={false} onReview={vi.fn()} />); return calls;
}
async function openActivity() { fireEvent.click(await screen.findByRole('button', { name: 'Actividad de NOVA' })); }
it('publishes run-bound approval independently of activity visibility for the editor owner', async () => {
  const current = { ...view('TOOL_RUNNING'), run: { ...view('TOOL_RUNNING').run, mode: 'SAME_TAB', approval: 'APPROVED' } };
  vi.stubGlobal('EventSource', Events); vi.stubGlobal('fetch', vi.fn(async () => Response.json(current)));
  const changed = vi.fn();
  render(<NovaLive runId={id} worker onApprovalChange={changed} onReview={vi.fn()} />);
  await waitFor(() => expect(changed).toHaveBeenLastCalledWith(id));
  act(() => { window.dispatchEvent(new Event('nova-close-activity')); window.dispatchEvent(new CustomEvent('nova-assistant-visibility', { detail: false })); });
  expect(changed).toHaveBeenLastCalledWith(id);
  expect(screen.queryByRole('button', { name: 'Aplicar versión aprobada' })).toBeNull();
});
it('refreshes the editor proposal before reviewing from the NOVA button', async () => {
  const original = { id, hash: 'old', promptId: null, revision: null, draft: { title: 'Viejo', templateText: 'Viejo.', description: '', categoryId: 0 }, originalText: '' };
  let proposal = original;
  vi.stubGlobal('EventSource', Events); vi.stubGlobal('fetch', vi.fn(async () => Response.json({ ...view('AWAITING_APPROVAL'), run: { ...view('AWAITING_APPROVAL').run, approval: 'PENDING' }, proposal })));
  const synchronize = vi.fn(async () => { proposal = { ...original, hash: 'edited', draft: { ...original.draft, title: 'Corregido', templateText: 'Contenido editado.' } }; });
  render(<NovaLive runId={id} worker onPrepareReview={synchronize} onReview={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Revisar y guardar copia' }));
  const dialog = await screen.findByRole('dialog', { name: 'Guardar copia separada' });
  expect(synchronize).toHaveBeenCalledOnce(); expect(within(dialog).getByText('Contenido editado.')).toBeTruthy(); expect(within(dialog).queryByText('Viejo.')).toBeNull();
});
it('offers identity-bound choices for a blocked search without executing a candidate automatically', async () => {
  const choose = vi.fn(async () => {}), candidate = { id, revision: 4, title: 'Revisar código' };
  vi.stubGlobal('EventSource', Events); vi.stubGlobal('fetch', vi.fn(async () => Response.json({ ...view('BLOCKED'), question: '¿Cuál quieres usar?', sourceChoices: [candidate] })));
  render(<NovaLive runId={id} worker={false} onReview={vi.fn()} onChooseSource={choose} />);
  await act(async () => { await Promise.resolve(); window.dispatchEvent(new Event('nova-open-activity')); });
  expect(choose).not.toHaveBeenCalled(); fireEvent.click(await screen.findByRole('button', { name: candidate.title }));
  await waitFor(() => expect(choose).toHaveBeenCalledWith(candidate));
});
it.each([true, false])('describes the actual persistence effect before approving (copy=%s)', async savingCopy => {
  const approval = { id, hash: 'reviewed-content-hash', promptId: savingCopy ? null : id, revision: savingCopy ? null : 1,
    draft: { title: 'Propuesta', templateText: 'Explica <tema> con claridad.', description: '', categoryId: 0 }, originalText: source.templateText };
  const current = { ...view('AWAITING_APPROVAL'), run: { ...view('AWAITING_APPROVAL').run, approval: 'PENDING' }, proposal: approval };
  const approved: unknown[] = [];
  vi.stubGlobal('EventSource', Events);
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/approve')) { approved.push(JSON.parse(options!.body as string)); return Response.json(current.run); }
    return Response.json(current);
  }));
  render(<NovaLive runId={id} worker={false} onReview={vi.fn()} />);
  const review = await screen.findByRole('button', { name: savingCopy ? 'Revisar y guardar copia' : 'Revisar y guardar versión' });
  expect(screen.getByRole('button', { name: 'Actividad de NOVA' }).getAttribute('aria-expanded')).toBe('false');
  fireEvent.click(review);
  const dialog = await screen.findByRole('dialog', { name: savingCopy ? 'Guardar copia separada' : 'Confirmar nueva versión' });
  expect(within(dialog).getByText(savingCopy ? 'Se creará un prompt separado. El original conservará su contenido y versión.' : 'Se guardará una nueva versión del mismo prompt. Se conservarán las versiones anteriores.')).toBeTruthy();
  expect(approved).toEqual([]);
  fireEvent.click(within(dialog).getByRole('button', { name: savingCopy ? 'Confirmar guardado de esta copia' : 'Confirmar guardado de esta versión' }));
  await waitFor(() => expect(approved).toEqual([{ approvalId: approval.id, hash: approval.hash, revision: approval.revision, confirmed: true }]));
});
it('announces only a confirmed agent start in a corner toast for five seconds without stealing focus', async () => {
  vi.stubGlobal('fetch', vi.fn(async (_path: string, options: RequestInit) => Response.json({ ...view().run, runId: JSON.parse(options.body as string).runId, mode: 'SAME_TAB' })));
  render(<><input aria-label="Mi petición" /><NovaLive runId={null} worker={false} onReview={vi.fn()} /></>);
  const field = screen.getByLabelText('Mi petición'); field.focus();
  expect(screen.queryByTestId('nova-start-toast')).toBeNull();
  vi.useFakeTimers();
  await act(async () => { await startVisible('Crea una instrucción.', crypto.randomUUID()); });
  expect(screen.getByTestId('nova-start-toast')).toBeTruthy(); expect(document.activeElement).toBe(field);
  act(() => { vi.advanceTimersByTime(4999); }); expect(screen.queryByTestId('nova-start-toast')).toBeTruthy();
  act(() => { vi.advanceTimersByTime(1); }); expect(screen.queryByTestId('nova-start-toast')).toBeNull();
  await act(async () => { await startVisible('Otra instrucción.', crypto.randomUUID()); });
  fireEvent.click(screen.getByRole('button', { name: 'Cerrar aviso de NOVA' })); expect(screen.queryByTestId('nova-start-toast')).toBeNull();
});
it('keeps a terminal question in recoverable history instead of leaving it over the workspace', async () => {
  const question = '¿Qué prompt quieres actualizar?';
  vi.stubGlobal('EventSource', Events); vi.stubGlobal('fetch', vi.fn(async () => Response.json({ ...view('BLOCKED'), question })));
  render(<LazyMotion features={domAnimation}><NovaLive runId={id} worker={false} notice="Aviso operativo anterior" onReview={vi.fn()} /><AgentDock context="Biblioteca" hasActivity actions={null}>{null}</AgentDock></LazyMotion>);
  await waitFor(() => expect(Events.items.length).toBeGreaterThan(0));
  expect(screen.queryByText(question)).toBeNull(); expect(screen.queryByText('Aviso operativo anterior')).toBeNull();
  expect(document.querySelector('.nova-outcome')).toBeNull(); expect(screen.queryByTestId('nova-start-toast')).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Abrir asistente NOVA' }));
  fireEvent.click(screen.getByRole('button', { name: 'Ver última actividad de NOVA' }));
  expect(await screen.findByText(question)).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Cerrar actividad' }));
  expect(screen.queryByText(question)).toBeNull();
});
it('uncovers an obscured workspace control without taking its focus or pausing the agent', async () => {
  const control = vi.fn(); window.addEventListener('nova-ui-control', control);
  try {
    render(<LazyMotion features={domAnimation}><input aria-label="Campo de trabajo" /><AgentDock context="Biblioteca" actions={null}><textarea aria-label="Petición" /></AgentDock></LazyMotion>);
    fireEvent.click(screen.getByRole('button', { name: 'Abrir asistente NOVA' }));
    expect(screen.getByRole('button', { name: 'Cerrar asistente NOVA' }).getAttribute('aria-expanded')).toBe('true');
    const field = screen.getByRole('textbox', { name: 'Campo de trabajo' });
    vi.spyOn(document.querySelector<HTMLElement>('.nova-dock-panel')!, 'getBoundingClientRect').mockReturnValue(new DOMRect(600, 300, 400, 400));
    vi.spyOn(field, 'getBoundingClientRect').mockReturnValue(new DOMRect(650, 400, 100, 50));
    act(() => field.focus());
    expect(screen.getByRole('button', { name: 'Abrir asistente NOVA' }).getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(field); expect(control).not.toHaveBeenCalled();
  } finally { window.removeEventListener('nova-ui-control', control); }
});
it('uncovers a field that was already focused before a non-focusing panel reveal', async () => {
  render(<LazyMotion features={domAnimation}><input aria-label="Campo activo" /><AgentDock context="Biblioteca" actions={null}>{null}</AgentDock></LazyMotion>);
  const field = screen.getByRole('textbox', { name: 'Campo activo' });
  vi.spyOn(document.querySelector<HTMLElement>('.nova-dock-panel')!, 'getBoundingClientRect').mockReturnValue(new DOMRect(600, 300, 400, 400));
  vi.spyOn(field, 'getBoundingClientRect').mockReturnValue(new DOMRect(650, 400, 100, 50));
  field.focus();
  act(() => window.dispatchEvent(new CustomEvent('nova-open-assistant', { detail: { focus: false } })));
  await waitFor(() => expect(screen.getByTestId('nova-dock').getAttribute('data-expanded')).toBe('false'));
  expect(document.activeElement).toBe(field);
});
it('removes the working toast as soon as a fast task has finished', async () => {
  let current = view();
  vi.stubGlobal('EventSource', Events); vi.stubGlobal('fetch', vi.fn(async () => Response.json(current)));
  render(<NovaLive runId={id} worker={false} onReview={vi.fn()} />);
  await waitFor(() => expect(Events.items.length).toBeGreaterThan(0));
  act(() => window.dispatchEvent(new Event('nova-agent-started')));
  expect(screen.getByTestId('nova-start-toast')).toBeTruthy();
  current = view('COMPLETED', 1);
  act(() => Events.items[0].send(1, 'Verification', { status: 'COMPLETED' }));
  await waitFor(() => expect(screen.queryByTestId('nova-start-toast')).toBeNull());
});
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
  render(<NovaLive runId={id} worker={false} onReview={vi.fn()} />); await openActivity();
  fireEvent.click(screen.getByRole('button', { name: 'Continuar' })); await waitFor(() => expect(accept).toBeDefined()); expect(controls).not.toContain('resume');
  accept!(Response.json(view('INTERPRETING', 2).run)); await waitFor(() => expect(controls).toContain('resume')); window.removeEventListener('nova-ui-control', listener);
});
it('offers stop instead of resume when a reloaded same-tab document lost ownership', async () => {
  vi.stubGlobal('EventSource', Events); vi.stubGlobal('fetch', vi.fn(async () => Response.json({ ...view('PAUSED', 1), run: { ...view('PAUSED', 1).run, mode: 'SAME_TAB' } })));
  render(<NovaLive runId={id} worker={false} onReview={vi.fn()} />); await openActivity();
  expect(screen.queryByRole('button', { name: 'Continuar' })).toBeNull(); expect(screen.getByRole('button', { name: 'Detener NOVA' })).toBeTruthy();
});
it('deduplicates replayed events and reconnects a gap from the last confirmed sequence', async () => {
  const calls = setup(); await openActivity(); const first = Events.items[0];
  await act(async () => { first.send(1); first.send(1); });
  await waitFor(() => expect(calls.filter(path => path === `/api/v1/nova/live/${id}`)).toHaveLength(2));
  await act(async () => { first.send(3); }); expect(first.closed).toBe(true); expect(Events.items[1].url).toContain('after=1');
  await act(async () => { Events.items[1].send(2); Events.items[1].send(3); });
  await waitFor(() => expect(calls.filter(path => path === `/api/v1/nova/live/${id}`)).toHaveLength(3));
});
it('coalesces an event burst into one in-flight snapshot without losing the terminal result', async () => {
  let resolve: ((response: Response) => void) | undefined;
  vi.stubGlobal('EventSource', Events);
  const fetcher = vi.fn(() => new Promise<Response>(complete => { resolve = complete; })); vi.stubGlobal('fetch', fetcher);
  render(<NovaLive runId={id} worker={false} onReview={vi.fn()} />);
  act(() => { for (let sequence = 1; sequence <= 100; sequence++) Events.items[0].send(sequence); });
  await new Promise(complete => setTimeout(complete, 180)); expect(fetcher).toHaveBeenCalledOnce();
  await act(async () => resolve!(Response.json({ ...view('COMPLETED', 100), run: { ...view('COMPLETED', 100).run, browserOpen: false } })));
  expect(Events.items[0].closed).toBe(true); expect(fetcher).toHaveBeenCalledOnce();
});
it('a disconnected activity observer never starts another run or invents completion', async () => {
  const calls = setup(); await openActivity();
  await act(async () => { Events.items[0].onerror?.(); }); await screen.findByText(/Conexión interrumpida/);
  await waitFor(() => expect(calls.filter(path => path === `/api/v1/nova/live/${id}`).length).toBeGreaterThan(1)); expect(calls.every(path => path.endsWith(id))).toBe(true);
});
it('stays absent without a task or after a completed task', async () => {
  vi.stubGlobal('EventSource', Events); vi.stubGlobal('fetch', vi.fn(async () => Response.json(view('COMPLETED', 1))));
  const mounted = render(<NovaLive runId={null} worker={false} onReview={vi.fn()} />);
  expect(screen.queryByRole('button', { name: 'Actividad de NOVA' })).toBeNull();
  mounted.rerender(<NovaLive runId={id} worker={false} onReview={vi.fn()} />);
  await waitFor(() => expect(fetch).toHaveBeenCalled()); expect(screen.queryByTestId('nova-live')).toBeNull();
});
it('identifies generation and input extraction instead of using the same interpretation label throughout', async () => {
  setup(); await openActivity();
  await act(async () => Events.items[0].send(1, 'Generation', { toolName: null, status: 'INTERPRETING' }));
  expect(screen.getByRole('heading', { name: 'Redactando el contenido del prompt' })).toBeTruthy();
  await act(async () => Events.items[0].send(2, 'InputExtraction', { toolName: null, status: 'INTERPRETING' }));
  expect(screen.getByRole('heading', { name: 'Relacionando tus datos con los campos' })).toBeTruthy();
});
it('opens completed history explicitly without restarting or exposing obsolete execution controls', async () => {
  vi.stubGlobal('EventSource', Events); vi.stubGlobal('fetch', vi.fn(async () => Response.json(view('COMPLETED', 1))));
  render(<NovaLive runId={id} worker={false} onReview={vi.fn()} />);
  await waitFor(() => expect(fetch).toHaveBeenCalled());
  act(() => { window.dispatchEvent(new Event('nova-open-activity')); });
  expect(await screen.findByRole('heading', { name: 'Tarea completada y comprobada' })).toBeTruthy();
  expect(screen.queryByRole('button', { name: 'Detener NOVA' })).toBeNull();
  expect(screen.getByRole('button', { name: 'Descargar actividad' })).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Cerrar actividad' })); expect(screen.queryByTestId('nova-live')).toBeNull();
});
it('keeps activity stable on pointerdown inside the assistant and hides its details when the launcher closes', async () => {
  vi.stubGlobal('EventSource', Events); const fetcher = vi.fn(async () => Response.json(view())); vi.stubGlobal('fetch', fetcher);
  render(<LazyMotion features={domAnimation}><NovaLive runId={id} worker={false} onReview={vi.fn()} /><AgentDock context="Biblioteca" hasActivity actions={<button>Crear</button>}><div className="nova-input"><textarea aria-label="Mi petición" /></div></AgentDock></LazyMotion>);
  fireEvent.click(screen.getByRole('button', { name: 'Abrir asistente NOVA' })); await openActivity();
  fireEvent.keyDown(screen.getByRole('button', { name: 'Ver última actividad de NOVA' }), { key: 'Escape' });
  expect(screen.queryByRole('button', { name: 'Pausar' })).toBeNull();
  expect(screen.getByTestId('nova-dock').getAttribute('data-expanded')).toBe('true');
  expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Actividad de NOVA' }));
  await openActivity();
  const close = screen.getByRole('button', { name: 'Cerrar panel de NOVA' });
  fireEvent.pointerDown(close);
  expect(screen.getByRole('button', { name: 'Pausar' })).toBeTruthy();
  fireEvent.click(close);
  expect(screen.queryByRole('button', { name: 'Pausar' })).toBeNull(); expect(document.querySelector('.nova-live-popover')).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Abrir asistente NOVA' }));
  expect(screen.getByRole('button', { name: 'Pausar' })).toBeTruthy();
  expect(fetcher.mock.calls).toHaveLength(1);
});
it.each(['COMPLETED', 'FAILED', 'BLOCKED', 'CANCELLED'])('reopens task choices without covering them with %s history and preserves explicit history access', async status => {
  vi.stubGlobal('EventSource', Events);
  const fetcher = vi.fn(async () => Response.json(view(status, 1))); vi.stubGlobal('fetch', fetcher);
  render(<LazyMotion features={domAnimation}><NovaLive runId={id} worker={false} onReview={vi.fn()} /><AgentDock context="Biblioteca" hasActivity actions={<button>Crear otra tarea</button>}>{null}</AgentDock></LazyMotion>);
  await waitFor(() => expect(fetcher).toHaveBeenCalledOnce());
  fireEvent.click(screen.getByRole('button', { name: 'Abrir asistente NOVA' }));
  fireEvent.click(screen.getByRole('button', { name: 'Ver última actividad de NOVA' }));
  expect(document.querySelector('.nova-live-popover')).not.toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Cerrar panel de NOVA' }));
  fireEvent.click(screen.getByRole('button', { name: 'Abrir asistente NOVA' }));
  expect(document.querySelector('.nova-live-popover')).toBeNull();
  expect(screen.getByRole('button', { name: 'Crear otra tarea' })).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Ver última actividad de NOVA' }));
  expect(document.querySelector('.nova-live-popover')).not.toBeNull();
  expect(fetcher).toHaveBeenCalledOnce();
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
it('manual editor preparation sends the same observed identity to its real proposal endpoint', async () => {
  const observe = vi.fn(async () => id), changed = vi.fn(), calls: { path: string; headers?: HeadersInit }[] = [];
  vi.stubGlobal('fetch', vi.fn(async (path: string, options: RequestInit) => { calls.push({ path, headers: options.headers }); return Response.json({ id, templateId: id, baseRevision: 1, text: 'Resume <tema>.', reasons: ['Cambio'], changes: ['Cambio'], warnings: [], variablesPreserved: ['tema'], model: 'qwen3:8b' }); }));
  render(<EditorPreparation source={source} draft={source} values={{}} onChange={changed} onValues={vi.fn()} onUse={vi.fn()} onObserve={observe} />);
  fireEvent.change(screen.getByLabelText('Cambio que necesitas'), { target: { value: 'Hazlo más breve.' } }); fireEvent.click(screen.getByRole('button', { name: 'Preparar cambio en el editor' }));
  await waitFor(() => expect(changed).toHaveBeenCalled()); expect(observe).toHaveBeenCalledWith('Hazlo más breve.', source, expect.any(AbortSignal)); expect(new Headers(calls.find(call => call.path.endsWith('/nova/propose'))?.headers).get('X-Nova-Live-Run')).toBe(id);
});
