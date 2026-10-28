import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { AccountNovaActions } from '../src/features/accounts/AccountNovaActions';
import { AccountNovaBoundary, type AccountAgentBinding } from '../src/features/accounts/AccountNovaBoundary';
import { createAccountService } from '../src/services/accounts';
import { createApiClient } from '../src/services/api';
import type { LiveCommand } from '../src/services/live';

const account = '70000000-0000-4000-8000-000000000001';
const libraryId = '70000000-0000-4000-8000-000000000002';
const connectionId = '70000000-0000-4000-8000-000000000003';
const request = 'Crea una plantilla con las variables nombre proyecto, propósito y tipo.';
const source = { id: '70000000-0000-4000-8000-000000000004', libraryId, title: 'Origen', template: 'Hola <tema>.', revision: 4 };
class Events extends EventTarget { onopen = null; onerror = null; close() {} }
beforeEach(() => { vi.stubGlobal('EventSource', Events); vi.stubGlobal('matchMedia', () => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() })); });
afterEach(() => { cleanup(); vi.unstubAllGlobals(); sessionStorage.clear(); });

function actionFixture() {
  const service = createAccountService(createApiClient({ send: async () => { throw new Error('No HTTP expected for action selection'); } }, ''));
  const begin = vi.fn<AccountAgentBinding['begin']>(async () => {});
  const agent: AccountAgentBinding = { configuration: null, services: service.nova(account, libraryId, connectionId), runId: null, runtime: null, controlled: false, busy: false, begin };
  render(<AccountNovaActions agent={agent} prompts={[source]} unavailable={false} onSourceChange={vi.fn()} />);
  return begin;
}

it('omits inputNames when untouched even if the request mentions names, so the server may ask', async () => {
  const begin = actionFixture();
  expect(screen.queryByLabelText('Variables de la plantilla')).toBeNull();
  fireEvent.change(screen.getByLabelText('Qué prompt necesitas'), { target: { value: request } });
  fireEvent.click(screen.getByRole('button', { name: 'Preparar con NOVA' }));
  await waitFor(() => expect(begin).toHaveBeenCalledOnce());
  expect(begin.mock.calls[0][1]).toEqual({ operation: 'CREATE_DRAFT', content: request });
  expect(Object.hasOwn(begin.mock.calls[0][1], 'inputNames')).toBe(false);
});

it.each(['CREATE_DRAFT', 'PROPOSE_ADAPTATION'] as const)('preserves an explicitly chosen empty list for %s', async action => {
  const begin = actionFixture();
  fireEvent.change(screen.getByLabelText('Acción de NOVA'), { target: { value: action } });
  if (action !== 'CREATE_DRAFT') fireEvent.change(screen.getByLabelText('Prompt de origen'), { target: { value: source.id } });
  fireEvent.change(screen.getByLabelText(action === 'CREATE_DRAFT' ? 'Qué prompt necesitas' : 'Cambios solicitados'), { target: { value: request } });
  fireEvent.click(screen.getByLabelText('Definir las variables ahora'));
  fireEvent.click(screen.getByRole('button', { name: 'Preparar con NOVA' }));
  await waitFor(() => expect(begin).toHaveBeenCalledOnce());
  expect(begin.mock.calls[0][1]).toEqual({ operation: action, content: request, inputNames: [] });
});

it('sends explicit Unicode names and restores omission when the person disables their definition', async () => {
  const begin = actionFixture();
  fireEvent.change(screen.getByLabelText('Qué prompt necesitas'), { target: { value: request } });
  fireEvent.click(screen.getByLabelText('Definir las variables ahora'));
  fireEvent.change(screen.getByLabelText('Variables de la plantilla'), { target: { value: 'nombre proyecto\npropósito\ntipo' } });
  fireEvent.click(screen.getByRole('button', { name: 'Preparar con NOVA' }));
  await waitFor(() => expect(begin).toHaveBeenCalledOnce());
  expect(begin.mock.calls[0][1].inputNames).toEqual(['nombre proyecto', 'propósito', 'tipo']);
  fireEvent.click(screen.getByLabelText('Definir las variables ahora'));
  fireEvent.click(screen.getByRole('button', { name: 'Preparar con NOVA' }));
  await waitFor(() => expect(begin).toHaveBeenCalledTimes(2));
  expect(Object.hasOwn(begin.mock.calls[1][1], 'inputNames')).toBe(false);
});

it('composes the real account ASK dialog and restarts only after explicit names, retaining the original command and source', async () => {
  let id = '', starts = 0;
  const sent: { message: string; command: LiveCommand; sourceId?: string; sourceRevision?: number }[] = [];
  const record = () => ({ runId: id, sessionId: account, mode: 'SAME_TAB', status: starts === 1 ? 'BLOCKED' : 'COMPLETED', sequence: 0,
    targetId: source.id, revision: source.revision, pauseRequested: false, browserOpen: false, approval: 'NONE', effectCommitted: false, errorCode: starts === 1 ? 'INPUT_FIELDS_NEED_CONFIRMATION' : null });
  const service = createAccountService(createApiClient({ send: async (path, options) => {
    if (path === '/account/csrf') return Response.json({ token: 'csrf' });
    if (path === '/account/connections/') return Response.json([{ id: connectionId, profileId: 'ollama-qwen3-8b-proposals', label: 'Equipo QA', active: true, revoked: false, createdAt: '', expiresAt: '2030-01-01' }]);
    if (path.includes('/nova/sessions/')) return new Response(null, { status: 204 });
    if (path.endsWith('/nova/live')) {
      const body = JSON.parse(String(options?.body)); sent.push(body.request); starts++; id = body.request.runId; return Response.json(record());
    }
    if (path.endsWith('/actions?after=0')) return Response.json({ run: record(), action: null });
    if (path.includes('/nova/live/')) return Response.json({ run: record(), request, observation: null, question: starts === 1 ? 'Indica los nombres de las variables.' : null, proposal: null });
    throw new Error('Unexpected account ASK fixture endpoint: ' + path);
  } }, ''));
  render(<AccountNovaBoundary accountId={account} libraryId={libraryId} service={service} onExpired={vi.fn()}>
    {agent => <>{agent.configuration}<AccountNovaActions agent={agent} prompts={[source]} source={source} unavailable={false} onSourceChange={vi.fn()} /></>}
  </AccountNovaBoundary>);
  fireEvent.change(await screen.findByLabelText('Equipo de NOVA'), { target: { value: connectionId } });
  fireEvent.change(await screen.findByLabelText('Acción de NOVA'), { target: { value: 'PROPOSE_ADAPTATION' } });
  fireEvent.change(screen.getByLabelText('Cambios solicitados'), { target: { value: request } });
  fireEvent.click(screen.getByRole('button', { name: 'Preparar con NOVA' }));
  const dialog = await screen.findByRole('dialog', { name: 'Define los campos del prompt' });
  expect(starts).toBe(1); expect(Object.hasOwn(sent[0].command, 'inputNames')).toBe(false);
  fireEvent.change(within(dialog).getByLabelText('Variables a agregar'), { target: { value: 'nombre proyecto\npropósito\ntipo' } });
  fireEvent.click(within(dialog).getByRole('button', { name: 'Continuar con estos campos' }));
  await waitFor(() => expect(starts).toBe(2));
  expect(sent[1]).toMatchObject({ message: request, sourceId: source.id, sourceRevision: 4, command: { operation: 'PROPOSE_ADAPTATION', content: request, inputNames: ['nombre proyecto', 'propósito', 'tipo'] } });
  await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  expect(Array.from({ length: sessionStorage.length }, (_, index) => sessionStorage.getItem(sessionStorage.key(index)!)).every(value => value !== null && !value.includes(request))).toBe(true);
});
