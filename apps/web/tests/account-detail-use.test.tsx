import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { AccountPromptLibrary } from '../src/features/accounts/AccountPromptLibrary';
import { AccountExecutions } from '../src/features/accounts/AccountExecutions';
import { createAccountService, type AccountExecution, type AccountPrompt } from '../src/services/accounts';
import { createApiClient } from '../src/services/api';

const libraryId = '60000000-0000-4000-8000-000000000001';
const promptId = '60000000-0000-4000-8000-000000000002';
const executionId = '60000000-0000-4000-8000-000000000003';
const library = { id: libraryId, ownerId: libraryId, name: 'Detalle privado', createdAt: '' };
const root = `/api/accounts/v1/libraries/${libraryId}`;
const job: AccountExecution = { id: executionId, libraryId, kind: 'CompilePrompt', status: 'Queued', cancelRequested: false, createdAt: '2026-10-28T00:00:00Z', startedAt: null, finishedAt: null, errorCode: null };
const clipboard = vi.fn<(text: string) => Promise<void>>();
const originalClipboard = Object.getOwnPropertyDescriptor(navigator, 'clipboard');
beforeEach(() => { clipboard.mockReset().mockResolvedValue(undefined); Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: clipboard } }); });
afterEach(() => { cleanup(); vi.restoreAllMocks(); if (originalClipboard) Object.defineProperty(navigator, 'clipboard', originalClipboard); else Reflect.deleteProperty(navigator, 'clipboard'); });

/** Real private client and React composition with HTTP fixtures; not a database or model proof. */
function fixture() {
  let prompt: AccountPrompt = { id: promptId, libraryId, title: 'Idea de software', template: 'Conceptualiza <proyecto>.', revision: 3, variableNames: ['proyecto'], description: 'Plantilla reutilizable.', isFavorite: false, isArchived: false };
  let deleted = false, conflict = false, jobs: AccountExecution[] = [], values: Record<string, string> = {};
  let observe: ((items: AccountExecution[]) => void) | undefined;
  const calls: { path: string; method: string; body?: Record<string, unknown> }[] = [];
  const service = createAccountService(createApiClient({ send: async (path, options) => {
    const method = options?.method ?? 'GET', body = options?.body ? JSON.parse(String(options.body)) as Record<string, unknown> : undefined;
    calls.push({ path, method, body });
    if (path === '/account/csrf') return Response.json({ token: 'csrf' });
    if (path === '/account/options') return Response.json({ email: true, google: false, delegation: false, mailDelivery: 'unconfigured' });
    if (path.includes('/prompts/catalog?')) return Response.json({ items: deleted ? [] : [prompt], total: deleted ? 0 : 1, page: 0, size: 12 });
    if (path.endsWith('/groups') || path.endsWith('/groups/memberships') || path.endsWith('/publications')) return Response.json([]);
    if (path.endsWith('/memory/editor/inspect')) {
      const input = body?.values as Record<string, string>;
      return Response.json({ variableNames: ['proyecto'], missingVariables: input.proyecto?.trim() ? [] : ['proyecto'], compiledText: input.proyecto?.trim() ? `Conceptualiza ${input.proyecto}.` : null });
    }
    if (path === `${root}/prompts/${promptId}/render`) return Response.json({ promptId, templateRevision: prompt.revision, text: null, missingVariables: ['proyecto'] });
    if (path === `${root}/prompts/${promptId}/compile`) {
      if (conflict) return Response.json({ detail: 'La revisión cambió.' }, { status: 409 });
      values = { ...body?.values as Record<string, string> }; jobs = [job]; return Response.json(job);
    }
    if (path === `${root}/executions/${executionId}/result`) return Response.json({ executionId, promptId, promptRevision: 3, title: prompt.title,
      text: `Conceptualiza ${values.proyecto}.`, createdAt: job.createdAt, usage: { values, operationId: executionId } });
    if (path === `${root}/prompts/${promptId}/use`) return new Response(null, { status: 204 });
    if (path === `${root}/prompts/${promptId}` && method === 'GET') return Response.json(prompt);
    if (path === `${root}/prompts/${promptId}` && method === 'DELETE') { deleted = true; return new Response(null, { status: 204 }); }
    if (/\/(favorite|archive|restore|duplicate)$/.test(path)) {
      if (conflict) return Response.json({ detail: 'La revisión cambió.' }, { status: 409 });
      prompt = { ...prompt, revision: prompt.revision + 1, ...(path.endsWith('/favorite') ? { isFavorite: !prompt.isFavorite } : {}),
        ...(path.endsWith('/archive') ? { isArchived: true } : {}), ...(path.endsWith('/restore') ? { isArchived: false } : {}) };
      if (path.endsWith('/duplicate')) prompt = { ...prompt, id: '60000000-0000-4000-8000-000000000004', title: 'Copia de idea' };
      return Response.json(prompt);
    }
    throw new Error(`Unexpected detail-use fixture request: ${method} ${path}`);
  } }, ''));
  service.watchExecutions = vi.fn(async (_id, next) => { observe = next; next(jobs); });
  service.rememberedContexts = vi.fn(async () => []);
  return { service, calls, conflict: () => { conflict = true; }, complete: () => { jobs = [{ ...job, status: 'Completed', finishedAt: job.createdAt }]; observe?.(jobs); } };
}

async function detail(fake: ReturnType<typeof fixture>, text = 'Árbol\n  proyecto literal') {
  render(<AccountPromptLibrary library={library} service={fake.service} onExpired={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Idea de software' }));
  const focus = await screen.findByRole('region', { name: 'Detalle de Idea de software' });
  const field = await within(focus).findByLabelText('proyecto');
  fireEvent.change(field, { target: { value: text } });
  // A server-owned inspection is debounced. Keep a bounded integration deadline
  // under the full two-worker suite without changing the product's timers.
  await waitFor(() => expect(within(focus).getByRole('button', { name: 'Usar prompt' })).toHaveProperty('disabled', false), { timeout: 5000 });
  return { focus, field };
}

it('keeps the exact detail values across preparation cancellation and registers only after copying its completed result', async () => {
  const fake = fixture(), text = 'Árbol\n  proyecto literal'; const { field } = await detail(fake, text);
  fireEvent.click(screen.getByRole('button', { name: 'Usar prompt' }));
  const firstDialog = screen.getByRole('dialog');
  expect((await within(firstDialog).findByLabelText('proyecto') as HTMLTextAreaElement).value).toBe(text);
  fireEvent.click(within(firstDialog).getByRole('button', { name: 'Cerrar' }));
  expect(screen.getByLabelText('proyecto')).toBe(field); expect((field as HTMLTextAreaElement).value).toBe(text);
  fireEvent.click(screen.getByRole('button', { name: 'Usar prompt' }));
  fireEvent.click(await within(screen.getByRole('dialog')).findByRole('button', { name: 'Preparar resultado' }));
  await screen.findByText('En cola');
  const submitted = fake.calls.find(call => call.path.endsWith('/compile'))!;
  expect(submitted.body).toMatchObject({ revision: 3, values: { proyecto: text } });
  expect(fake.calls.some(call => call.path.endsWith('/use'))).toBe(false); expect(clipboard).not.toHaveBeenCalled();
  await act(async () => fake.complete());
  fireEvent.click(await screen.findByRole('button', { name: 'Copiar instrucción guardada' }));
  await screen.findByText('Instrucción copiada. Uso confirmado para esta preparación.');
  expect(clipboard).toHaveBeenCalledWith(`Conceptualiza ${text}.`);
  expect(fake.calls.find(call => call.path.endsWith('/use'))?.body).toEqual({ revision: 3, values: { proyecto: text }, operationId: executionId, expectedText: `Conceptualiza ${text}.` });
  fireEvent.click(screen.getByRole('button', { name: 'Cerrar diálogo' }));
  fireEvent.click(screen.getByRole('button', { name: 'Actualizar preparaciones' }));
  await waitFor(() => expect(fake.service.watchExecutions).toHaveBeenCalledTimes(3));
  expect(screen.queryByRole('dialog')).toBeNull();
}, 15000);

it('preserves entered values through favorite and archive revisions, using the existing mutation client', async () => {
  const fake = fixture(); const { field } = await detail(fake);
  fireEvent.click(screen.getByRole('button', { name: 'Favorito' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Favorito' }).getAttribute('aria-pressed')).toBe('true'));
  expect(screen.getByLabelText('proyecto')).toBe(field);
  expect((field as HTMLTextAreaElement).value).toBe('Árbol\n  proyecto literal');
  fireEvent.click(screen.getByRole('button', { name: 'Archivar' }));
  await screen.findByText('Este prompt está archivado. Recupéralo para volver a utilizarlo.');
  expect(screen.getByRole('button', { name: 'Usar prompt' })).toHaveProperty('disabled', true);
  fireEvent.click(screen.getByRole('button', { name: 'Recuperar' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Usar prompt' })).toHaveProperty('disabled', false), { timeout: 5000 });
  expect(fake.calls.filter(call => /\/(favorite|archive|restore)$/.test(call.path)).map(call => call.body)).toEqual([{ revision: 3 }, { revision: 4 }, { revision: 5 }]);
  expect(screen.getByLabelText('proyecto')).toBe(field);
}, 15000);

it('shows revision failures in the visible detail and keeps its original revision and values', async () => {
  const fake = fixture(); const { field, focus } = await detail(fake); fake.conflict();
  fireEvent.click(screen.getByRole('button', { name: 'Favorito' }));
  expect((await screen.findByRole('alert')).textContent).toContain('Tus datos permanecen en el detalle');
  expect(focus.getAttribute('data-revision')).toBe('3'); expect((field as HTMLTextAreaElement).value).toContain('Árbol');
  expect(fake.calls.filter(call => call.path.endsWith('/favorite'))).toHaveLength(1);
});

it('reuses collection, sharing and irreversible deletion dialogs from detail, and clears focus only after confirmed deletion', async () => {
  const fake = fixture(); await detail(fake);
  fireEvent.click(screen.getByRole('button', { name: 'Colecciones' }));
  await screen.findByRole('dialog', { name: 'Tus colecciones' });
  fireEvent.click(screen.getByRole('button', { name: 'Listo' }));
  fireEvent.click(screen.getByRole('button', { name: 'Compartir plantilla' }));
  await screen.findByRole('dialog', { name: 'Compartir Idea de software' });
  fireEvent.click(screen.getByRole('button', { name: 'Cerrar diálogo' }));
  fireEvent.click(screen.getByRole('button', { name: 'Eliminar prompt' }));
  expect(screen.getByRole('button', { name: 'Eliminar definitivamente' })).toHaveProperty('disabled', true);
  fireEvent.click(screen.getByRole('button', { name: 'Conservar prompt' }));
  expect(fake.calls.filter(call => call.method === 'DELETE')).toHaveLength(0);
  expect(screen.getByTestId('prompt-focus')).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Eliminar prompt' }));
  fireEvent.click(screen.getByLabelText('Confirmo eliminar este prompt y sus versiones.'));
  fireEvent.click(screen.getByRole('button', { name: 'Eliminar definitivamente' }));
  await screen.findByText('Guarda tu primera instrucción.');
  expect(screen.queryByTestId('prompt-focus')).toBeNull();
  expect(fake.calls.find(call => call.method === 'DELETE')?.body).toEqual({ revision: 3, confirmed: true });
}, 15000);

it('keeps a rejected preparation and its values open without requesting use or discarding detail', async () => {
  const fake = fixture(); await detail(fake); fake.conflict();
  fireEvent.click(screen.getByRole('button', { name: 'Usar prompt' }));
  const dialog = screen.getByRole('dialog');
  fireEvent.click(await within(dialog).findByRole('button', { name: 'Preparar resultado' }));
  await within(dialog).findByRole('alert');
  expect((within(dialog).getByLabelText('proyecto') as HTMLTextAreaElement).value).toContain('Árbol');
  expect(fake.calls.filter(call => call.path.endsWith('/compile'))).toHaveLength(1);
  expect(fake.calls.some(call => call.path.endsWith('/use'))).toBe(false);
  fireEvent.click(within(dialog).getByRole('button', { name: 'Cerrar' }));
  expect(screen.getByTestId('prompt-focus').getAttribute('data-revision')).toBe('3');
});

it('opens a requested completion once without reopening it after dismissal and replay', async () => {
  const fake = fixture(), shown = vi.fn();
  render(<AccountExecutions libraryId={libraryId} service={fake.service} refresh={0} requestedId={executionId} onRequestedShown={shown} onExpired={vi.fn()} />);
  await act(async () => fake.complete());
  await screen.findByRole('dialog'); expect(shown).toHaveBeenCalledOnce();
  fireEvent.click(screen.getByRole('button', { name: 'Cerrar diálogo' }));
  await act(async () => fake.complete());
  expect(screen.queryByRole('dialog')).toBeNull(); expect(shown).toHaveBeenCalledOnce();
});

it('rejects a foreign-library queue snapshot before opening a requested result', async () => {
  const fake = fixture(), shown = vi.fn();
  fake.service.watchExecutions = vi.fn(async (_id, next) => { next([{ ...job, libraryId: '60000000-0000-4000-8000-000000000099', status: 'Completed' }]); });
  render(<AccountExecutions libraryId={libraryId} service={fake.service} refresh={0} requestedId={executionId} onRequestedShown={shown} onExpired={vi.fn()} />);
  expect((await screen.findByRole('alert')).textContent).toContain('La respuesta no corresponde a esta biblioteca');
  expect(screen.queryByRole('dialog')).toBeNull(); expect(shown).not.toHaveBeenCalled();
  expect(fake.calls.some(call => call.path.endsWith('/result'))).toBe(false);
});

it('duplicates through the parent and exposes one visible native edit, use and home control in detail', async () => {
  const fake = fixture(); await detail(fake);
  for (const id of ['prompt.edit', 'prompt.use', 'library.home']) expect(Array.from(document.querySelectorAll(`[data-nova-control="${id}"]`)).filter(node => !node.closest('[hidden]'))).toHaveLength(1);
  fireEvent.click(screen.getByRole('button', { name: 'Duplicar' }));
  await screen.findByRole('heading', { name: 'Copia de idea', level: 1 });
  expect(fake.calls.find(call => call.path.endsWith('/duplicate'))?.body).toEqual({ revision: 3 });
});
