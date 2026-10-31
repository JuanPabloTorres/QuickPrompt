import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { AccountNovaBoundary } from '../src/features/accounts/AccountNovaBoundary';
import { createAccountService, type AccountConnection } from '../src/services/accounts';

const account = '90000000-0000-4000-8000-000000000001';
const library = '90000000-0000-4000-8000-000000000002';
const connectionId = '90000000-0000-4000-8000-000000000003';
const connection: AccountConnection = { id: connectionId, label: 'NOVA sintética', profileId: 'ollama-qwen3-8b-proposals',
  active: true, revoked: false, createdAt: '', expiresAt: '', presence: { status: 'POLLING', observedAt: '2026-10-29T12:00:00Z', expiresAt: '2026-10-29T12:00:20Z' } };
class Events extends EventTarget { onopen = null; onerror = null; close() {} }
beforeEach(() => {
  vi.stubGlobal('EventSource', Events);
  vi.stubGlobal('matchMedia', () => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() }));
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.restoreAllMocks(); sessionStorage.clear(); });

function fixture() {
  const service = createAccountService();
  const ports = service.nova(account, library, connectionId);
  const start = vi.spyOn(ports.visibleSession, 'startVisible').mockRejectedValue(new Error('Synthetic start boundary reached'));
  vi.spyOn(service, 'nova').mockReturnValue(ports);
  const connections = vi.spyOn(service, 'connections').mockResolvedValue([connection]);
  const expired = vi.fn();
  const page = (owner = account) => <AccountNovaBoundary accountId={owner} libraryId={library} service={service} onExpired={expired}>
    {agent => <>{agent.configuration}<button disabled={!agent.services || agent.busy} onClick={() => void agent.begin('Contenido sintético', { operation: 'CREATE_DRAFT', content: 'Contenido sintético' })}>Enviar prueba</button></>}
  </AccountNovaBoundary>;
  const view = render(page());
  return { connections, start, view, page, expired };
}

async function choose() {
  await screen.findByRole('option', { name: /NOVA sintética/ });
  fireEvent.change(screen.getByLabelText('Equipo de NOVA'), { target: { value: connectionId } });
}

it.each(['NOT_OBSERVED', 'STALE', 'STOPPED'] as const)('blocks a new start with %s after a fresh read, without equating consent with availability', async status => {
  const fake = fixture(); await choose();
  fake.connections.mockResolvedValue([{ ...connection, presence: { status, observedAt: null, expiresAt: null } }]);
  fireEvent.click(screen.getByRole('button', { name: 'Enviar prueba' }));
  expect((await screen.findByRole('alert')).textContent).toContain('No hay una señal reciente');
  expect(fake.connections).toHaveBeenCalledTimes(2);
  expect(fake.start).not.toHaveBeenCalled(); expect(fake.expired).not.toHaveBeenCalled();
  expect(screen.getByText(/Una señal reciente no verifica la generación/)).toBeTruthy();
});

it.each(['POLLING', 'WORKING'] as const)('permits only the start boundary after a fresh %s observation', async status => {
  const fake = fixture(); await choose();
  fake.connections.mockResolvedValue([{ ...connection, presence: { ...connection.presence!, status } }]);
  fireEvent.click(screen.getByRole('button', { name: 'Enviar prueba' }));
  await waitFor(() => expect(fake.start).toHaveBeenCalledOnce());
  expect((await screen.findByRole('alert')).textContent).toContain('Synthetic start boundary reached');
});

it('does not assume that an older server without presence metadata observed a runner', async () => {
  const fake = fixture(); await choose();
  fake.connections.mockResolvedValue([{ ...connection, presence: undefined }]);
  fireEvent.click(screen.getByRole('button', { name: 'Enviar prueba' }));
  await screen.findByRole('alert'); expect(fake.start).not.toHaveBeenCalled();
});

it('aborts a delayed preflight when the account changes and never sends the old request', async () => {
  const fake = fixture(); await choose();
  let finish!: (items: AccountConnection[]) => void; let pendingSignal: AbortSignal | undefined;
  fake.connections.mockImplementationOnce(signal => { pendingSignal = signal; return new Promise(resolve => { finish = resolve; }); });
  fireEvent.click(screen.getByRole('button', { name: 'Enviar prueba' }));
  await waitFor(() => expect(finish).toBeTypeOf('function'));
  fake.view.rerender(fake.page('90000000-0000-4000-8000-000000000004'));
  await act(async () => finish([connection]));
  expect(pendingSignal?.aborted).toBe(true); expect(fake.start).not.toHaveBeenCalled();
});
