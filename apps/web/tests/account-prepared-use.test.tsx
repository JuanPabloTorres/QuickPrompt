import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { AccountExecutions, UseAccountPromptDialog } from '../src/features/accounts/AccountExecutions';
import { createAccountService, type AccountExecution, type AccountExecutionResult } from '../src/services/accounts';
import { createApiClient } from '../src/services/api';
import { UiPortalScopeProvider } from '../src/components/ui/UiPortalScope';

const libraryId = '50000000-0000-4000-8000-000000000001';
const promptId = '50000000-0000-4000-8000-000000000002';
const executionId = '50000000-0000-4000-8000-000000000003';
const otherLibrary = '50000000-0000-4000-8000-000000000004';
const result: AccountExecutionResult = { executionId, promptId, promptRevision: 3, title: 'Uso revisado', text: 'Explica árbol\n  literal.', createdAt: '2026-10-28T00:00:00Z',
  usage: { operationId: executionId, values: { tema: 'árbol\n  literal' } } };
const job: AccountExecution = { id: executionId, libraryId, kind: 'CompilePrompt', status: 'Completed', cancelRequested: false, createdAt: result.createdAt,
  startedAt: result.createdAt, finishedAt: result.createdAt, errorCode: null };
const clipboard = vi.fn<(text: string) => Promise<void>>();
const originalClipboard = Object.getOwnPropertyDescriptor(navigator, 'clipboard');
beforeEach(() => { clipboard.mockReset().mockResolvedValue(undefined); Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: clipboard } }); });
afterEach(() => { cleanup(); vi.restoreAllMocks(); if (originalClipboard) Object.defineProperty(navigator, 'clipboard', originalClipboard); else Reflect.deleteProperty(navigator, 'clipboard'); });

function fixture(value: AccountExecutionResult = result) {
  let nextStatus = 204;
  let csrf = 0;
  const writes: { body: Record<string, unknown>; token: string | null }[] = [];
  const reads: string[] = [];
  const service = createAccountService(createApiClient({ send: async (path, options) => {
    if (path === '/account/csrf') return Response.json({ token: `csrf-${++csrf}` });
    if (path.endsWith(`/executions/${executionId}/result`)) { reads.push(path); return Response.json(value); }
    if (path.endsWith(`/prompts/${promptId}/use`)) {
      writes.push({ body: JSON.parse(String(options?.body)), token: new Headers(options?.headers).get('X-QP-CSRF') });
      return nextStatus === 204 ? new Response(null, { status: 204 }) : Response.json({ detail: nextStatus === 409 ? 'La revisión cambió.' : 'No se pudo confirmar el recibo.' }, { status: nextStatus });
    }
    throw new Error(`Unexpected prepared-use request: ${path}`);
  } }, ''));
  service.watchExecutions = vi.fn(async (_id, observe) => { observe([job]); });
  return { service, writes, reads, fail: (status: number) => { nextStatus = status; } };
}

async function openResult(fake: ReturnType<typeof fixture>, onCatalogChanged = vi.fn()) {
  const mounted = render(<AccountExecutions libraryId={libraryId} service={fake.service} refresh={0} onExpired={vi.fn()} onCatalogChanged={onCatalogChanged} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Ver resultado' }));
  await screen.findByRole('button', { name: 'Copiar instrucción guardada' });
  return mounted;
}

it('registers only after successful copying, with the exact reviewed snapshot and one stable preparation UUID after reopening', async () => {
  const fake = fixture(); const changed = vi.fn();
  await openResult(fake, changed);
  expect(fake.writes).toEqual([]); expect(clipboard).not.toHaveBeenCalled();
  clipboard.mockImplementation(async text => { expect(text).toBe(result.text); expect(fake.writes).toEqual([]); });
  fireEvent.click(screen.getByRole('button', { name: 'Copiar instrucción guardada' }));
  await screen.findByText('Instrucción copiada. Uso confirmado para esta preparación.');
  expect(fake.writes[0]).toEqual({ token: 'csrf-1', body: { revision: 3, values: { tema: 'árbol\n  literal' }, operationId: executionId, expectedText: result.text } });
  expect(changed).toHaveBeenCalledOnce();
  fireEvent.click(screen.getByRole('button', { name: 'Cerrar diálogo' }));
  fireEvent.click(screen.getByRole('button', { name: 'Ver resultado' }));
  await screen.findByRole('button', { name: 'Copiar instrucción guardada' });
  clipboard.mockResolvedValue(undefined);
  fireEvent.click(screen.getByRole('button', { name: 'Copiar instrucción guardada' }));
  await waitFor(() => expect(fake.writes).toHaveLength(2));
  expect(fake.writes[1].body).toEqual(fake.writes[0].body);
  expect(fake.writes[1].token).toBe('csrf-2');
});

it('does not send a use receipt when the real clipboard promise fails', async () => {
  const fake = fixture(); await openResult(fake);
  clipboard.mockRejectedValueOnce(new Error('Portapapeles no disponible.'));
  fireEvent.click(screen.getByRole('button', { name: 'Copiar instrucción guardada' }));
  await screen.findByRole('alert');
  expect(fake.writes).toEqual([]);
  expect(screen.queryByRole('button', { name: 'Reintentar sólo el registro de uso' })).toBeNull();
  expect(screen.queryByText('Instrucción copiada. Uso confirmado para esta preparación.')).toBeNull();
});

it('retries only the receipt after a lost acknowledgement without copying again or changing its operation', async () => {
  const fake = fixture(); fake.fail(503); await openResult(fake);
  fireEvent.click(screen.getByRole('button', { name: 'Copiar instrucción guardada' }));
  await screen.findByRole('alert');
  expect(clipboard).toHaveBeenCalledOnce(); expect(fake.writes).toHaveLength(1);
  fake.fail(204);
  fireEvent.click(screen.getByRole('button', { name: 'Reintentar sólo el registro de uso' }));
  await screen.findByText('Instrucción copiada. Uso confirmado para esta preparación.');
  expect(clipboard).toHaveBeenCalledOnce();
  expect(fake.writes).toHaveLength(2); expect(fake.writes[1].body).toEqual(fake.writes[0].body);
  expect(fake.writes[1].token).not.toBe(fake.writes[0].token);
});

it.each([409, 404])('retains the copied artifact after revision or availability conflict %s without forcing another write', async status => {
  const fake = fixture(); fake.fail(status); const changed = vi.fn(); await openResult(fake, changed);
  fireEvent.click(screen.getByRole('button', { name: 'Copiar instrucción guardada' }));
  await screen.findByText(/La revisión ya no coincide/);
  expect(screen.getByRole('alert').textContent).toContain('La instrucción ya se copió; su uso no está confirmado.');
  expect(screen.queryByRole('button', { name: 'Reintentar sólo el registro de uso' })).toBeNull();
  expect(fake.writes).toHaveLength(1); expect(clipboard).toHaveBeenCalledOnce(); expect(changed).not.toHaveBeenCalled();
  expect(document.querySelector('.history-text')?.textContent).toBe(result.text);
});

it('copies legacy snapshots and model responses without inventing a prepared-use receipt', async () => {
  const fake = fixture({ ...result, usage: null, response: 'Respuesta guardada.', model: 'qwen3:8b' }); await openResult(fake);
  fireEvent.click(screen.getByRole('button', { name: 'Copiar respuesta guardada' }));
  await screen.findByText('Respuesta copiada.'); expect(fake.writes).toEqual([]);
  fireEvent.click(screen.getByRole('button', { name: 'Copiar instrucción guardada' }));
  await screen.findByText('Instrucción copiada. Este resultado anterior no permite verificar un registro de uso.');
  expect(fake.writes).toEqual([]);
});

it('rejects a foreign use operation before showing its snapshot or writing to the clipboard', async () => {
  const fake = fixture({ ...result, usage: { values: {}, operationId: otherLibrary } });
  render(<AccountExecutions libraryId={libraryId} service={fake.service} refresh={0} onExpired={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Ver resultado' }));
  await screen.findByRole('alert');
  expect(screen.queryByRole('button', { name: 'Copiar instrucción guardada' })).toBeNull();
  expect(clipboard).not.toHaveBeenCalled(); expect(fake.writes).toEqual([]);
});

it('cancels a pending copy receipt when the private context is retired', async () => {
  const fake = fixture(); let copied!: () => void;
  clipboard.mockImplementation(() => new Promise<void>(resolve => { copied = resolve; }));
  const mounted = await openResult(fake);
  fireEvent.click(screen.getByRole('button', { name: 'Copiar instrucción guardada' }));
  mounted.rerender(<AccountExecutions libraryId={otherLibrary} service={fake.service} refresh={0} onExpired={vi.fn()} />);
  await act(async () => copied());
  expect(fake.writes).toEqual([]);
  expect(screen.queryByRole('dialog')).toBeNull();
});

it('does not acknowledge a copy while identity verification suspends private controls', async () => {
  const fake = fixture(); let copied!: () => void;
  clipboard.mockImplementation(() => new Promise<void>(resolve => { copied = resolve; }));
  const expired = vi.fn(), retry = vi.fn();
  const view = (active: boolean) => <UiPortalScopeProvider value={{ active, dialogHost: null, overlayHost: null, onRetry: retry }}><AccountExecutions libraryId={libraryId} service={fake.service} refresh={0} onExpired={expired} /></UiPortalScopeProvider>;
  const mounted = render(view(true));
  fireEvent.click(await screen.findByRole('button', { name: 'Ver resultado' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Copiar instrucción guardada' }));
  mounted.rerender(view(false));
  await act(async () => copied());
  expect(fake.writes).toEqual([]);
  expect(screen.queryByRole('button', { name: 'Copiar instrucción guardada' })).toBeNull();
  expect(screen.getByRole('button', { name: 'Volver a comprobar acceso' })).toBeTruthy();
});

it('refreshes catalog metadata after a real model completes but never posts a use from a read or compilation event', async () => {
  const fake = fixture(); const changed = vi.fn(); let observe!: (jobs: AccountExecution[]) => void;
  fake.service.watchExecutions = vi.fn(async (_id, next) => { observe = next; next([job]); });
  render(<AccountExecutions libraryId={libraryId} service={fake.service} refresh={0} onExpired={vi.fn()} onCatalogChanged={changed} />);
  await screen.findByRole('button', { name: 'Ver resultado' });
  expect(changed).not.toHaveBeenCalled();
  await act(async () => observe([{ ...job, kind: 'RunLocalPrompt' }]));
  expect(changed).toHaveBeenCalledOnce();
  await act(async () => observe([{ ...job, kind: 'RunLocalPrompt' }]));
  expect(changed).toHaveBeenCalledOnce(); expect(fake.writes).toEqual([]);
});

it('accepts a preparation submission without recording use before a copy or successful model execution', async () => {
  const fake = fixture();
  fake.service.options = vi.fn(async () => ({ email: true, google: false, delegation: false, mailDelivery: 'unconfigured' }));
  fake.service.renderPrompt = vi.fn(async () => ({ promptId, templateRevision: 3, text: null, missingVariables: [] }));
  fake.service.compilePrompt = vi.fn(async () => ({ ...job, status: 'Queued' as const }));
  render(<UseAccountPromptDialog prompt={{ id: promptId, libraryId, revision: 3, title: 'Sin variables', template: 'Texto literal.' }} service={fake.service} onExpired={vi.fn()} onSubmitted={vi.fn()} onClose={vi.fn()} />);
  await screen.findByText('Esta instrucción no necesita variables.');
  fireEvent.click(screen.getByRole('button', { name: 'Preparar resultado' }));
  await waitFor(() => expect(fake.service.compilePrompt).toHaveBeenCalledOnce());
  expect(clipboard).not.toHaveBeenCalled(); expect(fake.writes).toEqual([]);
});
