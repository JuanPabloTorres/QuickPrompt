import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import App from '../src/app/App';
import { ApplicationServiceProvider } from '../src/components/ServiceContext';
import { createApiClient, type HttpTransport } from '../src/services/api';
import { createApplicationServices } from '../src/services/application';
import type { LiveEvents } from '../src/services/live';

beforeEach(() => {
  sessionStorage.clear(); localStorage.clear();
  vi.stubGlobal('matchMedia', () => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() }));
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([{}] as unknown as DOMRectList);
  HTMLElement.prototype.scrollIntoView = vi.fn();
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

it.each(['brief', 'question'] as const)('adopts an admitted local %s continuation in App and mounts the new SameTab worker through exact DOM receipts', async branch => {
  let original = '', next = '', session = '', step = 0;
  const body = 'Explica <tema>. No inventes datos.';
  const description = 'Guía reutilizable para explicar un tema con claridad.';
  const calls: { path: string; body?: Record<string, unknown>; surface: string | null }[] = [];
  const actions = ['start', 'ui_open_create_prompt', 'ui_prepare_creation'];
  const actionIds = actions.map(() => crypto.randomUUID());
  const record = (id: string) => ({ runId: id, sessionId: session, mode: 'SAME_TAB', status: id === original ? 'BLOCKED' : step >= 3 ? 'AWAITING_APPROVAL' : 'TOOL_RUNNING',
    sequence: id === original ? 12 : step + 1, targetId: null, revision: null, pauseRequested: false, browserOpen: false, approval: id !== original && step >= 3 ? 'PENDING' : 'NONE',
    effectCommitted: false, errorCode: id === original ? 'NEEDS_CLARIFICATION' : null });
  const review = { basisHash: 'synthetic-basis', originalRequest: 'Crea un prompt para explicar temas.', source: null, inputs: ['tema'],
    proposed: { title: 'Explicación', description, task: body, steps: [], constraints: [], outputRequirements: [], references: [] } };
  const contentReview = { status: 'REQUIRES_HUMAN_CONTENT_REVIEW', contractHash: 'confirmed-contract', bodyHash: 'exact-body', originalRequest: review.originalRequest,
    source: { promptId: null, revision: null, bodyHash: null, originalText: null }, ledger: [], literalChanges: { entries: [] } };
  const send = vi.fn<HttpTransport['send']>().mockImplementation(async (path, options = {}) => {
    const input = options.body ? JSON.parse(String(options.body)) : undefined;
    calls.push({ path, body: input, surface: new Headers(options.headers).get('X-Nova-Surface') });
    if (path.includes('/prompts?')) return Response.json({ items: [], total: 0 });
    if (path.endsWith('/categories')) return Response.json([{ id: 0, name: 'General' }]);
    if (path.endsWith('/groups') || path.endsWith('/nova/drafts')) return Response.json([]);
    if (path.endsWith('/templates/inspect')) return Response.json({ variableNames: input.templateText.includes('<tema>') ? ['tema'] : [] });
    if (path.endsWith('/templates/compile')) return Response.json(input.templateText.includes('<tema>')
      ? { text: null, missingVariables: ['tema'] } : { text: input.templateText, missingVariables: [] });
    if (path.endsWith('/nova/live')) { original = input.runId; session = input.sessionId; return Response.json(record(original)); }
    if (path.endsWith('/clarification')) { next = input.runId; return Response.json(record(next)); }
    if (path.endsWith('/actions/progress')) return new Response(null, { status: 204 });
    if (path.endsWith('/actions/receipt')) { step++; return new Response(null, { status: 204 }); }
    if (path.includes('/actions?')) {
      const id = path.includes(original) ? original : next;
      const action = id === next && step < actions.length ? { runId: next, actionId: actionIds[step], sequence: step + 1,
        tool: actions[step], target: null, arguments: step === 2 ? { title: 'Explicación', description, templateText: body } : {},
        timestamp: new Date().toISOString(), executionStatus: 'DISPATCHED', verificationStatus: 'PENDING' } : null;
      return Response.json({ run: record(id), action });
    }
    if (path.includes('/nova/live/')) {
      const id = path.includes(original) ? original : next;
      return Response.json({ run: record(id), request: review.originalRequest, question: id === original ? 'Confirma la tarea.' : null,
        brief: id === original && branch === 'brief' ? review : null, observation: null, proposal: id === next && step >= 3 ? {
          id: crypto.randomUUID(), hash: 'approval-fixture', promptId: null, revision: null,
          draft: { title: 'Explicación', description, templateText: body, categoryId: 0 }, originalText: '', purpose: 'CONTENT_ONLY', contentReview
        } : null });
    }
    throw Error('Unexpected route ' + path);
  });
  const services = createApplicationServices(createApiClient({ send }), () => {
    const stream = new EventTarget() as EventTarget & LiveEvents;
    stream.onopen = null; stream.onerror = null; stream.close = vi.fn(); return stream;
  });
  await act(async () => { render(<ApplicationServiceProvider services={services}><App /></ApplicationServiceProvider>); });
  await screen.findByTestId('library-ready');
  fireEvent.click(screen.getByRole('button', { name: 'Abrir asistente NOVA' }));
  fireEvent.click(within(screen.getByRole('region', { name: 'Panel de NOVA' })).getByRole('button', { name: 'Crear prompt' }));
  fireEvent.change(screen.getByLabelText('¿Qué prompt quieres crear?'), { target: { value: review.originalRequest } });
  fireEvent.click(screen.getByLabelText('Definir las variables ahora'));
  fireEvent.change(screen.getByLabelText('Variables de la plantilla'), { target: { value: 'tema' } });
  fireEvent.click(screen.getByRole('button', { name: 'Crear con NOVA' }));
  if (branch === 'brief') {
    await screen.findByRole('dialog', { name: 'Confirma la tarea del futuro asistente' });
    fireEvent.click(screen.getByRole('checkbox')); fireEvent.click(screen.getByRole('button', { name: 'Confirmar tarea y componer plantilla' }));
  } else {
    await screen.findByRole('dialog', { name: 'Aclara el contenido del prompt' });
    fireEvent.change(screen.getByLabelText('Tu respuesta'), { target: { value: 'Quiero explicar el tema sin inventar datos.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Responder y continuar' }));
  }
  await waitFor(() => expect(step, JSON.stringify(calls.map(call => ({ path: call.path, receipt: call.body?.verificationStatus })))).toBe(3), { timeout: 10000 });
  expect(screen.getByLabelText('Contenido')).toHaveProperty('value', body);
  expect(screen.getByLabelText('Descripción')).toHaveProperty('value', description);
  expect(calls.filter(call => call.path.endsWith('/clarification'))).toHaveLength(1);
  const continuation = calls.find(call => call.path.endsWith('/clarification'))!;
  expect(continuation.surface).toBe(calls.find(call => call.path.endsWith('/nova/live'))!.surface);
  expect(continuation.body).toMatchObject({ questionSequence: 12, answer: branch === 'brief' ? '' : 'Quiero explicar el tema sin inventar datos.' });
  expect(Object.hasOwn(continuation.body!, 'brief')).toBe(branch === 'brief');
  const receipts = calls.filter(call => call.path.endsWith('/actions/receipt'));
  expect(receipts).toHaveLength(3); expect(receipts.every(call => call.path.includes(next))).toBe(true);
  expect(receipts.at(-1)?.body?.observation).toMatchObject({ templateText: body, title: 'Explicación', description });
  expect(sessionStorage.getItem('nova-live-run')).toBe(next);
  expect(calls.some(call => /\/(approve|commit)$/.test(call.path))).toBe(false);
}, 15000);
