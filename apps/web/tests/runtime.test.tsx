import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { NovaLive } from '../src/features/nova/NovaLive';
import { AgentRuntimePanel } from '../src/features/nova/AgentRuntimePanel';
import { liveViewSchema, type LiveRuntime, type LiveView } from '../src/services/live';
import { AgentDock } from '../src/features/nova/AgentDock';
import { domAnimation, LazyMotion } from 'motion/react';

const id = '10000000-0000-4000-8000-000000000001';
const secondId = '10000000-0000-4000-8000-000000000002';
function runtime(runId = id): LiveRuntime {
  return { version: 'nova-runtime-1', runId, sequence: 1, objective: 'Crear un prompt', contextSource: 'Petición actual', contextResolved: true,
    model: 'qwen3:8b', modelState: 'generating', lastInferenceMs: 1430, elapsedMs: 4000, activeTool: 'ui_prepare_creation',
    tools: [{ id: 'ui_prepare_creation', label: 'Generar el borrador', state: 'running' }, { id: 'ask_clarification', label: 'Pedir aclaración', state: 'available' }],
    memory: { sourceBound: false, draftActive: true, resolvedVariables: 1, totalVariables: 2, reusesDraft: true, constraints: ['Escrituras con aprobación'] },
    flow: [{ id: 'interpret', label: 'Interpretar', state: 'completed' }, { id: 'execute', label: 'Ejecutar', state: 'active' }], recoverableOnly: false };
}
function view(runId = id): LiveView {
  return liveViewSchema.parse({ run: { runId, sessionId: id, mode: 'BROWSER', status: 'TOOL_RUNNING', sequence: 1, targetId: null, revision: null,
    pauseRequested: false, browserOpen: true, approval: 'NONE', effectCommitted: false, errorCode: null }, runtime: runtime(runId),
    request: 'PRIVATE_REQUEST', question: null, observation: null, proposal: null, decisions: [{ arguments: { hidden: 'PRIVATE_REASONING' } }] });
}
class Events extends EventTarget {
  static items: Events[] = [];
  onopen: (() => void) | null = null; onerror: (() => void) | null = null;
  close = vi.fn();
  constructor() { super(); Events.items.push(this); }
  send(runId: string, sequence: number, status = 'UI_UPDATED') { this.dispatchEvent(new MessageEvent('activity', { data: JSON.stringify({ runId, eventId: crypto.randomUUID(), sequence,
    timestamp: new Date().toISOString(), stage: 'Observation', actor: 'SYSTEM_VALIDATION', toolName: 'ui_prepare_creation', targetId: null,
    status, durationMs: 100, errorCode: null, safeDisplayMessage: 'PRIVATE_MESSAGE' }) })); }
}
afterEach(() => { cleanup(); vi.unstubAllGlobals(); Events.items = []; });
it('renders only public runtime facts and uses explicit textual states', () => {
  render(<AgentRuntimePanel view={view()} status="Écriture en cours" controls={<button>Pausar</button>} disconnected={false} />);
  const panel = screen.getByRole('region', { name: 'Runtime de NOVA' });
  expect(panel.dataset.activity).toBe('working');
  expect(panel.querySelector('.runtime-model')?.getAttribute('data-active')).toBe('true');
  expect(panel.querySelector('.runtime-tools')?.getAttribute('data-active')).toBe('true');
  expect(within(panel).getByText('qwen3:8b')).toBeTruthy(); expect(within(panel).getByText('1/2 variables resueltas')).toBeTruthy();
  expect(panel.textContent).not.toMatch(/PRIVATE_|ui_prepare_creation|ask_clarification/);
  expect(within(panel).getByText('Última etapa de IA · 1.4 s')).toBeTruthy();
  fireEvent.click(within(panel).getByText('Ver herramientas (2)'));
  expect(within(panel).getByText('Disponible')).toBeTruthy();
  expect(within(panel).getByRole('list', { name: 'Flujo observado de NOVA' }).querySelector('[aria-current="step"]')?.textContent).toContain('Ejecutar');
});
it('rejects a summary for another run or sequence and keeps legacy controls usable', () => {
  const current = view(); current.runtime!.runId = secondId;
  const mounted = render(<AgentRuntimePanel view={current} status="Ejecutando" controls={<button>Detener NOVA</button>} disconnected={false} />);
  expect(screen.queryByTestId('agent-runtime')).toBeNull(); expect(screen.getByRole('button', { name: 'Detener NOVA' })).toBeTruthy();
  current.runtime!.runId = id; current.runtime!.sequence = 0;
  mounted.rerender(<AgentRuntimePanel view={current} status="Ejecutando" controls={null} disconnected={false} />);
  expect(screen.queryByTestId('agent-runtime')).toBeNull();
});
it('shows unconfirmed activity when SSE disconnects and restores live facts on recovery', async () => {
  vi.stubGlobal('EventSource', Events); vi.stubGlobal('fetch', vi.fn(async () => Response.json(view())));
  render(<NovaLive runId={id} worker={false} onReview={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Actividad de NOVA' }));
  expect(await screen.findByTestId('agent-runtime')).toBeTruthy();
  act(() => Events.items[0].onerror?.());
  expect(screen.getByTestId('agent-runtime').dataset.connected).toBe('false'); expect(screen.getByText('Estado sin confirmar')).toBeTruthy();
  expect(screen.getByTestId('agent-runtime').dataset.activity).toBe('reconnecting');
  expect(screen.getByText('Reconectando · estado sin confirmar')).toBeTruthy();
  expect(screen.getByTestId('agent-runtime').querySelector('[data-active="true"]')).toBeNull();
  act(() => Events.items[0].onopen?.()); expect(screen.getByTestId('agent-runtime').dataset.connected).toBe('true');
});
it.each(['PAUSED', 'AWAITING_APPROVAL', 'COMPLETED', 'FAILED', 'CANCELLED'] as const)('stops working animation for %s', status => {
  const current = view(); current.run.status = status;
  render(<AgentRuntimePanel view={current} status="Estado observado" controls={null} disconnected={false} />);
  const panel = screen.getByTestId('agent-runtime');
  expect(panel.dataset.activity).not.toBe('working');
  expect(panel.querySelector('[data-active="true"]')).toBeNull();
});
it('keeps a confirmed terminal result after normal SSE closure', async () => {
  let current = view();
  vi.stubGlobal('EventSource', Events); vi.stubGlobal('fetch', vi.fn(async () => Response.json(current)));
  render(<NovaLive runId={id} worker={false} onReview={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Actividad de NOVA' }));
  current = view(); current.run.status = 'COMPLETED'; current.run.browserOpen = false;
  current.runtime!.activeTool = null; current.runtime!.modelState = 'ready';
  act(() => Events.items[0].send(id, 1, 'COMPLETED'));
  await screen.findByRole('heading', { name: 'Tarea completada y comprobada' });
  act(() => Events.items[0].onerror?.());
  expect(screen.getByTestId('agent-runtime').dataset.activity).toBe('idle');
  expect(screen.getByTestId('agent-runtime').dataset.connected).toBe('true');
  expect(screen.queryByText('Estado sin confirmar')).toBeNull();
});
it('updates the active tool from same-run events and ignores foreign or repeated receipts', async () => {
  let current = view();
  const transport = vi.fn(async () => Response.json(current));
  vi.stubGlobal('EventSource', Events); vi.stubGlobal('fetch', transport);
  render(<NovaLive runId={id} worker={false} onReview={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Actividad de NOVA' }));
  act(() => Events.items[0].send(secondId, 1)); expect(transport).toHaveBeenCalledTimes(1);
  current = view(); current.runtime!.activeTool = null; current.runtime!.tools[0].state = 'completed';
  act(() => { Events.items[0].send(id, 1); Events.items[0].send(id, 1); });
  await screen.findByText('Ninguna en ejecución'); expect(transport).toHaveBeenCalledTimes(2);
});
it('cleans old subscriptions and ignores a late control response after switching runs', async () => {
  let finishPause: ((response: Response) => void) | undefined;
  vi.stubGlobal('EventSource', Events);
  vi.stubGlobal('fetch', vi.fn(async (path: string) => path.endsWith('/pause') ? new Promise<Response>(resolve => { finishPause = resolve; }) : Response.json(view(path.includes(secondId) ? secondId : id))));
  const mounted = render(<NovaLive runId={id} worker={false} onReview={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Actividad de NOVA' })); fireEvent.click(screen.getByRole('button', { name: 'Pausar' }));
  await waitFor(() => expect(finishPause).toBeDefined());
  mounted.rerender(<NovaLive runId={secondId} worker={false} onReview={vi.fn()} />);
  await waitFor(() => expect(screen.getByTestId('nova-live').dataset.runId).toBe(secondId));
  expect(Events.items[0].close).toHaveBeenCalled();
  await act(async () => { finishPause!(Response.json({ ...view().run, status: 'PAUSED', pauseRequested: true })); });
  expect(screen.getByTestId('nova-live').dataset.runId).toBe(secondId);
});
it('reopens hidden runtime details from the compact control after the assistant closes', async () => {
  vi.stubGlobal('EventSource', Events); vi.stubGlobal('fetch', vi.fn(async () => Response.json(view())));
  render(<LazyMotion features={domAnimation}><NovaLive runId={id} worker={false} onReview={vi.fn()} /><AgentDock context="Biblioteca" actions={null}>{null}</AgentDock></LazyMotion>);
  fireEvent.click(await screen.findByRole('button', { name: 'Actividad de NOVA' }));
  expect(await screen.findByTestId('agent-runtime')).toBeTruthy();
  act(() => screen.getByRole('button', { name: 'Pausar' }).focus());
  expect(screen.getByTestId('nova-dock').dataset.expanded).toBe('true');
  fireEvent.click(screen.getByRole('button', { name: 'Cerrar asistente NOVA' }));
  expect(screen.queryByTestId('agent-runtime')).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Actividad de NOVA' }));
  expect(await screen.findByTestId('agent-runtime')).toBeTruthy();
});
