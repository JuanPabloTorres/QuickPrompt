import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { companionPresenceLabel } from '../src/services/companionPresence';
import { createAccountService, type AccountConnection } from '../src/services/accounts';
import { AccountConnections } from '../src/features/accounts/AccountConnections';

const now = Date.parse('2026-10-29T12:00:00Z');
const connection: AccountConnection = { id: '90000000-0000-4000-8000-000000000003', label: 'Equipo sintético',
  profileId: 'ollama-qwen3-8b-proposals', active: true, revoked: false, createdAt: '', expiresAt: '',
  presence: { status: 'POLLING', observedAt: new Date(now).toISOString(), expiresAt: new Date(now + 5000).toISOString() } };

beforeEach(() => { vi.useFakeTimers(); vi.setSystemTime(now); });
afterEach(() => { cleanup(); vi.useRealTimers(); vi.restoreAllMocks(); });

it('does not present a cached polling signal as current after its expiry', () => {
  expect(companionPresenceLabel(connection, now + 5001)).toBe('Sin señal reciente');
});

it('expires a quiet connections page without polling, revoking consent or changing the configured device', async () => {
  const service = createAccountService(); const load = vi.spyOn(service, 'connections').mockResolvedValue([connection]);
  const revoke = vi.spyOn(service, 'revokeConnection');
  await act(async () => { render(<AccountConnections service={service} onExpired={vi.fn()} />); });
  expect(screen.getByText(/Consultando trabajos/)).toBeTruthy();
  await act(async () => { vi.advanceTimersByTime(5001); });
  expect(screen.getByText(/Sin señal reciente/)).toBeTruthy();
  expect(screen.getByText(/Autorización vigente/)).toBeTruthy();
  expect(load).toHaveBeenCalledOnce(); expect(revoke).not.toHaveBeenCalled();
  expect(screen.getByRole('heading', { name: 'Equipo sintético' })).toBeTruthy();
});

it('accepts a new observed snapshot only after an explicit refresh and expires it again', async () => {
  const service = createAccountService(); const load = vi.spyOn(service, 'connections').mockResolvedValue([connection]);
  await act(async () => { render(<AccountConnections service={service} onExpired={vi.fn()} />); });
  await act(async () => { vi.advanceTimersByTime(5001); });
  load.mockResolvedValue([{ ...connection, presence: { status: 'WORKING', observedAt: new Date(Date.now()).toISOString(), expiresAt: new Date(Date.now() + 20_000).toISOString() } }]);
  await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Actualizar conexiones' })); });
  expect(screen.getByText(/Procesando un trabajo/)).toBeTruthy(); expect(load).toHaveBeenCalledTimes(2);
  await act(async () => { vi.advanceTimersByTime(20_001); });
  expect(screen.getByText(/Sin señal reciente/)).toBeTruthy(); expect(load).toHaveBeenCalledTimes(2);
});

it('bounds a cached positive label even if the local wall clock moves backwards', async () => {
  const service = createAccountService(); vi.spyOn(service, 'connections').mockResolvedValue([{ ...connection,
    presence: { ...connection.presence!, expiresAt: new Date(now + 3600_000).toISOString() } }]);
  await act(async () => { render(<AccountConnections service={service} onExpired={vi.fn()} />); });
  vi.setSystemTime(now - 60_000);
  await act(async () => { vi.advanceTimersByTime(20_001); });
  expect(screen.getByText(/Sin señal reciente/)).toBeTruthy();
});

it('clears its local timer and discards a connection response after closing the page', async () => {
  const service = createAccountService(); let resolve!: (items: AccountConnection[]) => void; let signal: AbortSignal | undefined;
  vi.spyOn(service, 'connections').mockImplementation(value => { signal = value; return new Promise(complete => { resolve = complete; }); });
  const page = render(<AccountConnections service={service} onExpired={vi.fn()} />);
  page.unmount(); await act(async () => resolve([connection]));
  expect(signal?.aborted).toBe(true); expect(vi.getTimerCount()).toBe(0);
});

it('does not overwrite a manually refreshed stopped observation with a late initial response', async () => {
  const service = createAccountService(); let old!: (items: AccountConnection[]) => void;
  vi.spyOn(service, 'connections').mockImplementationOnce(() => new Promise(resolve => { old = resolve; }))
    .mockResolvedValue([{ ...connection, presence: { status: 'STOPPED', observedAt: new Date(now).toISOString(), expiresAt: new Date(now).toISOString() } }]);
  await act(async () => { render(<AccountConnections service={service} onExpired={vi.fn()} />); });
  await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Actualizar conexiones' })); });
  expect(screen.getByText(/Componente detenido/)).toBeTruthy();
  await act(async () => old([connection]));
  expect(screen.getByText(/Componente detenido/)).toBeTruthy(); expect(screen.queryByText(/Consultando trabajos/)).toBeNull();
});
