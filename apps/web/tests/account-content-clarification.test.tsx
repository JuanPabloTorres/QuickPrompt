import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { AccountNovaBoundary } from '../src/features/accounts/AccountNovaBoundary';
import { AccountNovaActions } from '../src/features/accounts/AccountNovaActions';
import { UiPortalScopeProvider } from '../src/components/ui/UiPortalScope';
import { createAccountService } from '../src/services/accounts';
import { createApiClient } from '../src/services/api';

const actor = '80000000-0000-4000-8000-000000000001';
const library = '80000000-0000-4000-8000-000000000002';
const connection = '80000000-0000-4000-8000-000000000003';
const source = { id: '80000000-0000-4000-8000-000000000004', libraryId: library, title: 'Conceptualización', template: 'Analiza <tipo>.', revision: 4 };
const request = 'Mejora este prompt para conceptualizar ideas de software.';
const question = '¿Debe conceptualizar software común o sólo AI?';
const answer = 'Software común y AI, incluidos Web, App y Spatial 3D.\nConservar la idea sin resolver un caso.';
class Events extends EventTarget { onopen = null; onerror = null; close() {} }
beforeEach(() => { vi.stubGlobal('EventSource', Events); vi.stubGlobal('matchMedia', () => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() })); });
afterEach(() => { cleanup(); vi.unstubAllGlobals(); sessionStorage.clear(); });

/** Real account transport, surface owner and React boundary with HTTP fixtures, never a model or database. */
function fixture(recovered = false) {
  let id = recovered ? crypto.randomUUID() : '', session = actor, status = recovered ? 'BLOCKED' : 'CREATED', reject = false, lostReply: 'before' | 'after' | undefined, missingRun = '', continuation: Promise<void> | undefined;
  const calls: { path: string; method: string; body?: Record<string, unknown>; surface: string | null; csrf: string | null }[] = [];
  const record = () => ({ runId: id, sessionId: session, mode: 'SAME_TAB', status, sequence: 12, targetId: source.id, revision: 4,
    browserOpen: false, pauseRequested: false, approval: 'NONE', effectCommitted: false, errorCode: status === 'BLOCKED' ? 'NEEDS_CLARIFICATION' : null });
  const service = createAccountService(createApiClient({ send: async (path, options) => {
    const body = options?.body ? JSON.parse(String(options.body)) as Record<string, unknown> : undefined;
    const headers = new Headers(options?.headers);
    calls.push({ path, method: options?.method ?? 'GET', body, surface: headers.get('X-Nova-Surface'), csrf: headers.get('X-QP-CSRF') });
    if (path === '/account/csrf') return Response.json({ token: 'csrf' });
    if (path === '/account/connections/') return Response.json([{ id: connection, profileId: 'ollama-qwen3-8b-proposals', label: 'Equipo QA', active: true, revoked: false, createdAt: '', expiresAt: '2030-01-01' }]);
    if (path.includes('/nova/sessions/')) return new Response(null, { status: 204 });
    if (path.endsWith('/nova/live')) {
      const input = body!.request as { runId: string; sessionId: string }; id = input.runId; session = input.sessionId; status = 'BLOCKED'; return Response.json(record());
    }
    if (path.endsWith('/clarification')) {
      await continuation;
      if (lostReply === 'before') { missingRun = String(body!.runId); throw new TypeError('Lost admission response'); }
      if (reject) return Response.json({ detail: 'La revisión del origen cambió.' }, { status: 409 });
      id = String(body!.runId); status = 'COMPLETED';
      if (lostReply === 'after') throw new TypeError('Lost committed admission response');
      return Response.json(record());
    }
    if (missingRun && path.includes('/nova/live/' + missingRun)) return Response.json({}, { status: 404 });
    if (path.includes('/actions?')) return Response.json({ run: record(), action: null });
    if (path.includes('/nova/live/')) return Response.json({ run: record(), request, question: status === 'BLOCKED' ? question : null, observation: null, proposal: null });
    throw new Error('Unexpected clarification fixture request: ' + path);
  } }, ''));
  const ports: ReturnType<typeof service.nova>[] = [];
  const nova = service.nova;
  service.nova = (...arguments_) => { const value = nova(...arguments_); ports.push(value); return value; };
  if (recovered) sessionStorage.setItem(`quickprompt:${actor}:${library}:${connection}:nova-run`, id);
  const changed = vi.fn(), expired = vi.fn();
  function ui(account = actor, active = true, libraryId = library) {
    return <UiPortalScopeProvider value={{ active, dialogHost: null, overlayHost: null, onRetry: vi.fn() }}><AccountNovaBoundary accountId={account} libraryId={libraryId} service={service} onExpired={expired}>
      {agent => <>{agent.configuration}<AccountNovaActions agent={agent} prompts={[source]} source={source} unavailable={false} onSourceChange={changed} /></>}
    </AccountNovaBoundary></UiPortalScopeProvider>;
  }
  return { service, calls, ports, ui, reject: () => { reject = true; }, loseReply: (admitted = false) => { lostReply = admitted ? 'after' : 'before'; }, restoreReply: () => { lostReply = undefined; missingRun = ''; }, holdReply: () => {
    let complete!: () => void; continuation = new Promise<void>(resolve => { complete = resolve; }); return complete;
  } };
}

async function chooseAndAsk(fake: ReturnType<typeof fixture>) {
  const mounted = render(fake.ui());
  await screen.findByRole('option', { name: 'Equipo QA · Qwen3:8b' });
  fireEvent.change(screen.getByLabelText('Equipo de NOVA'), { target: { value: connection } });
  fireEvent.change(await screen.findByLabelText('Acción de NOVA'), { target: { value: 'PROPOSE_ADAPTATION' } });
  fireEvent.change(screen.getByLabelText('Cambios solicitados'), { target: { value: request } });
  fireEvent.click(screen.getByLabelText('Definir las variables ahora'));
  fireEvent.change(screen.getByLabelText('Variables a agregar'), { target: { value: 'audiencia\nrestricciones' } });
  fireEvent.click(screen.getByRole('button', { name: 'Preparar con NOVA' }));
  await screen.findByRole('dialog', { name: 'Aclara el contenido del prompt' });
  return mounted;
}

it('sends only an explicit typed reply linked to the actual question and old surface, keeping the original command separate', async () => {
  const fake = fixture(); await chooseAndAsk(fake);
  expect(fake.calls.filter(call => call.path.endsWith('/clarification'))).toHaveLength(0);
  fireEvent.change(screen.getByLabelText('Tu respuesta'), { target: { value: answer } });
  fireEvent.click(screen.getByRole('button', { name: 'Responder y continuar' }));
  await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  const first = fake.calls.find(call => call.path.endsWith('/nova/live'))!;
  expect(first.body!.request).toMatchObject({ message: request, sourceId: source.id, sourceRevision: 4, command: { operation: 'PROPOSE_ADAPTATION', content: request, inputNames: ['audiencia', 'restricciones'] } });
  const reply = fake.calls.find(call => call.path.endsWith('/clarification'))!;
  expect(reply.body).toEqual({ runId: expect.any(String), questionSequence: 12, answer, nextSurface: expect.any(String), connectionId: connection });
  expect(reply.surface).toBe(first.surface); expect(reply.csrf).toBe('csrf');
  expect(Object.hasOwn(reply.body!, 'command')).toBe(false); expect(Object.hasOwn(reply.body!, 'message')).toBe(false);
  expect(fake.calls.filter(call => call.path.endsWith('/nova/live'))).toHaveLength(1);
  expect(fake.calls.filter(call => call.path.endsWith('/clarification'))).toHaveLength(1);
  expect(fake.calls.some(call => /\/(approve|commit)$/.test(call.path))).toBe(false);
  expect(Array.from({ length: sessionStorage.length }, (_, index) => sessionStorage.getItem(sessionStorage.key(index)!)).some(value => value?.includes(answer))).toBe(false);
});

it('defers and reopens the same question with its RAM answer without sending a reply, approval or persistence request', async () => {
  const fake = fixture(); await chooseAndAsk(fake);
  fireEvent.change(screen.getByLabelText('Tu respuesta'), { target: { value: answer } });
  fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Ahora no' }));
  expect(screen.queryByRole('dialog')).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Responder a NOVA' }));
  expect(screen.getByLabelText('Tu respuesta')).toHaveProperty('value', answer);
  expect(fake.calls.filter(call => call.path.endsWith('/clarification'))).toHaveLength(0);
  expect(fake.calls.some(call => /\/(approve|commit)$/.test(call.path))).toBe(false);
});

it('keeps an already submitted continuation visible until admission resolves and never repeats the POST on dismissal or double submit', async () => {
  const fake = fixture(); await chooseAndAsk(fake); const complete = fake.holdReply();
  fireEvent.change(screen.getByLabelText('Tu respuesta'), { target: { value: answer } });
  fireEvent.click(screen.getByRole('button', { name: 'Responder y continuar' }));
  await waitFor(() => expect(fake.calls.filter(call => call.path.endsWith('/clarification'))).toHaveLength(1));
  expect(screen.getByRole('button', { name: 'Ahora no' })).toHaveProperty('disabled', true);
  fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Escape' });
  fireEvent.click(screen.getByRole('button', { name: 'Cerrar diálogo' }));
  fireEvent.submit(screen.getByLabelText('Tu respuesta').closest('form')!);
  expect(screen.getByRole('dialog')).toBeTruthy();
  expect(fake.calls.filter(call => call.path.endsWith('/clarification'))).toHaveLength(1);
  complete(); await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
});

it('retains the literal reply after a revision conflict without automatically retrying or forcing a different source', async () => {
  const fake = fixture(); await chooseAndAsk(fake); fake.reject();
  fireEvent.change(screen.getByLabelText('Tu respuesta'), { target: { value: answer } });
  fireEvent.click(screen.getByRole('button', { name: 'Responder y continuar' }));
  expect((await within(screen.getByRole('dialog')).findByRole('alert')).textContent).toContain('Tu respuesta permanece aquí');
  expect(screen.getByLabelText('Tu respuesta')).toHaveProperty('value', answer);
  expect(fake.calls.filter(call => call.path.endsWith('/clarification'))).toHaveLength(1);
  expect(fake.calls.some(call => /\/(approve|commit)$/.test(call.path))).toBe(false);
});

it('retries only on explicit confirmation with the exact same RAM envelope, UUID, surface and answer after uncertain absence', async () => {
  const fake = fixture(); await chooseAndAsk(fake); fake.loseReply();
  fireEvent.change(screen.getByLabelText('Tu respuesta'), { target: { value: answer } });
  fireEvent.click(screen.getByRole('button', { name: 'Responder y continuar' }));
  await screen.findByRole('button', { name: 'Detener solicitud sin confirmar' });
  const reply = fake.calls.find(call => call.path.endsWith('/clarification'))!;
  expect(sessionStorage.getItem(`quickprompt:${actor}:${library}:${connection}:nova-run`)).toBe(reply.body!.runId);
  expect(fake.calls.filter(call => call.path.endsWith('/clarification'))).toHaveLength(1);
  expect(fake.calls.filter(call => call.path.endsWith('/nova/live'))).toHaveLength(1);
  expect(fake.calls.some(call => /\/(approve|commit)$/.test(call.path))).toBe(false);
  expect(screen.queryByRole('button', { name: 'Responder y continuar' })).toBeNull();
  const ports = fake.ports[0], pending = ports.visibleSession.pendingClarification!(String(reply.body!.runId))!;
  expect(Object.isFrozen(pending)).toBe(true); expect(Reflect.set(pending, 'answer', 'Alteración')).toBe(false);
  await expect(ports.visibleSession.clarifyVisible!(pending.previousId, pending.questionSequence, 'Respuesta distinta')).rejects.toThrow('no puede editarse');
  expect(fake.calls.filter(call => call.path.endsWith('/clarification'))).toHaveLength(1);
  fireEvent.click(screen.getByText('Revisar el encargo y tu respuesta'));
  expect(screen.getByText(answer, { normalizer: value => value })).toBeTruthy();
  const csrfReads = fake.calls.filter(call => call.path === '/account/csrf').length;
  fake.restoreReply(); fireEvent.click(screen.getByRole('button', { name: 'Reenviar esta misma aclaración' }));
  await waitFor(() => expect(screen.queryByRole('button', { name: 'Reenviar esta misma aclaración' })).toBeNull());
  const retries = fake.calls.filter(call => call.path.endsWith('/clarification'));
  expect(retries).toHaveLength(2); expect(retries[1]).toEqual(retries[0]);
  expect(fake.calls.filter(call => call.path === '/account/csrf').length).toBe(csrfReads + 1);
  expect(ports.visibleSession.pendingClarification!(pending.runId)).toBeUndefined();
});

it('recovers a lost admitted reply through its existing receipt with no resend or second inference request', async () => {
  const fake = fixture(); await chooseAndAsk(fake); fake.loseReply(true);
  fireEvent.change(screen.getByLabelText('Tu respuesta'), { target: { value: answer } });
  fireEvent.click(screen.getByRole('button', { name: 'Responder y continuar' }));
  await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  expect(fake.calls.filter(call => call.path.endsWith('/clarification'))).toHaveLength(1);
  expect(screen.queryByRole('button', { name: 'Reenviar esta misma aclaración' })).toBeNull();
  expect(fake.calls.some(call => /\/(approve|commit)$/.test(call.path))).toBe(false);
});

it('keeps an uncertain retry conflict visible without changing identity or automatically editing the answer', async () => {
  const fake = fixture(); await chooseAndAsk(fake); fake.loseReply();
  fireEvent.change(screen.getByLabelText('Tu respuesta'), { target: { value: answer } });
  fireEvent.click(screen.getByRole('button', { name: 'Responder y continuar' }));
  const retry = await screen.findByRole('button', { name: 'Reenviar esta misma aclaración' });
  fake.restoreReply(); fake.reject(); fireEvent.click(retry);
  await waitFor(() => expect(fake.calls.filter(call => call.path.endsWith('/clarification'))).toHaveLength(2));
  expect((await screen.findByText('La revisión del origen cambió.')).getAttribute('role')).toBe('alert');
  const retries = fake.calls.filter(call => call.path.endsWith('/clarification'));
  expect(retries[0].body).toEqual(retries[1].body); expect(screen.queryByRole('textbox', { name: 'Tu respuesta' })).toBeNull();
  expect(screen.getByRole('button', { name: 'Reenviar esta misma aclaración' })).toBeTruthy();
});

it.each(['account', 'library'])('masks pending reply data during verification and never resends it after the %s changes or scope unmounts', async scopeChange => {
  const fake = fixture(); const mounted = await chooseAndAsk(fake); fake.loseReply();
  fireEvent.change(screen.getByLabelText('Tu respuesta'), { target: { value: answer } });
  fireEvent.click(screen.getByRole('button', { name: 'Responder y continuar' }));
  const retry = await screen.findByRole('button', { name: 'Reenviar esta misma aclaración' });
  const newId = String(fake.calls.find(call => call.path.endsWith('/clarification'))!.body!.runId);
  mounted.rerender(fake.ui(actor, false)); fireEvent.click(retry);
  expect(screen.queryByRole('region', { name: 'Aclaración pendiente de confirmación' })).toBeNull();
  expect(fake.calls.filter(call => call.path.endsWith('/clarification'))).toHaveLength(1);
  mounted.rerender(scopeChange === 'account' ? fake.ui('80000000-0000-4000-8000-000000000099') : fake.ui(actor, true, '80000000-0000-4000-8000-000000000098')); fireEvent.click(retry);
  expect(fake.calls.filter(call => call.path.endsWith('/clarification'))).toHaveLength(1);
  expect(fake.ports[0].visibleSession.pendingClarification!(newId)).toBeUndefined();
  mounted.unmount();
});

it('masks the question during identity verification and retires it after a changed account without an automatic continuation', async () => {
  const fake = fixture(); const mounted = await chooseAndAsk(fake);
  const field = screen.getByLabelText('Tu respuesta'); fireEvent.change(field, { target: { value: answer } });
  mounted.rerender(fake.ui(actor, false));
  expect(screen.getByRole('dialog', { name: 'Comprobando sesión' })).toBeTruthy();
  expect(screen.queryByRole('textbox', { name: 'Tu respuesta' })).toBeNull();
  fireEvent.submit(field.closest('form')!);
  expect(fake.calls.filter(call => call.path.endsWith('/clarification'))).toHaveLength(0);
  mounted.rerender(fake.ui());
  expect(screen.getByLabelText('Tu respuesta')).toBe(field); expect(field).toHaveProperty('value', answer);
  mounted.rerender(fake.ui('80000000-0000-4000-8000-000000000099'));
  await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  expect(fake.calls.filter(call => call.path.endsWith('/clarification'))).toHaveLength(0);
});

it('retires active control at the terminal question and keeps only its separate RAM clarification permission', async () => {
  const fake = fixture(); const mounted = await chooseAndAsk(fake);
  const first = fake.calls.find(call => call.path.endsWith('/nova/live'))!.body!.request as { runId: string };
  const ports = fake.ports[0];
  await waitFor(() => expect(ports.visibleSession.surfaceCapability(first.runId)).toBeUndefined());
  expect(ports.visibleSession.clarificationCapability?.(first.runId)).toBeTruthy();
  mounted.unmount();
  expect(ports.visibleSession.clarificationCapability?.(first.runId)).toBeUndefined();
});

it('never offers content continuation from a recovered run ID without RAM request and surface permission', async () => {
  const fake = fixture(true); render(fake.ui());
  await screen.findByRole('option', { name: 'Equipo QA · Qwen3:8b' });
  fireEvent.change(screen.getByLabelText('Equipo de NOVA'), { target: { value: connection } });
  await screen.findByRole('button', { name: 'Ver última actividad de NOVA' });
  expect(screen.queryByRole('dialog')).toBeNull();
  expect(fake.calls.filter(call => call.path.endsWith('/nova/live'))).toHaveLength(0);
  const ports = fake.service.nova(actor, library, connection);
  await expect(ports.visibleSession.clarifyVisible!(crypto.randomUUID(), 12, answer)).rejects.toThrow('no conserva permiso');
  expect(fake.calls.filter(call => call.path.endsWith('/clarification'))).toHaveLength(0);
});
