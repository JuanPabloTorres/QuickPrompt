import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { StrictMode } from 'react';
import { AccountPage } from '../src/features/accounts/AccountPage';
import { AccountForm } from '../src/features/accounts/AccountForm';
import { createAccountService, type AccountPrompt, type AccountService, type AccountUser } from '../src/services/accounts';
import { ApiError, createApiClient } from '../src/services/api';
import { AccountInvitations } from '../src/features/accounts/AccountSharing';
import { AccountPromptLibrary } from '../src/features/accounts/AccountPromptLibrary';
import { AccountExecutions, UseAccountPromptDialog } from '../src/features/accounts/AccountExecutions';
import { AccountConnections } from '../src/features/accounts/AccountConnections';
import { PublishCollectionDialog } from '../src/features/accounts/PublishCollectionDialog';

it('reviews collection member revisions and reuses publication controls without inviting anyone implicitly', async () => {
  const accounts = service();
  accounts.prompts = vi.fn(async () => [{ id: idB, libraryId: idA, title: 'Uno revisado', template: 'Explica <tema>.', revision: 4 }]);
  render(<PublishCollectionDialog libraryId={idA} collection={{ id: idA, name: 'Ideas', revision: 8 }} service={accounts} onExpired={vi.fn()} onClose={vi.fn()} />);
  await screen.findByText('Uno revisado · versión 4');
  expect(accounts.publishCollection).not.toHaveBeenCalled(); expect(accounts.invite).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'Publicar versión 8' }));
  await screen.findByLabelText('Correo de la persona invitada');
  expect(accounts.publishCollection).toHaveBeenCalledWith(idA, idA, 8, { [idB]: 4 }, expect.any(AbortSignal));
  expect(accounts.invite).not.toHaveBeenCalled(); expect(accounts.publish).not.toHaveBeenCalled();
});

it('imports a shared collection with one stable operation and removes the snapshot when access is revoked', async () => {
  const accounts = service();
  accounts.invitations = vi.fn(async () => [{ id: idA, publicationId: idB, title: 'Colección compartida', state: 1, sentByMe: false, recipientEmail: null, available: true, isCollection: true }]);
  accounts.readCollectionPublication = vi.fn(async id => ({ id, title: 'Colección compartida', sourceRevision: 3, createdAt: '', items: [{ title: 'Uno', template: '<img src=x onerror=alert(1)>', sourceRevision: 1 }, { title: 'Dos', template: 'Segundo cuerpo.', sourceRevision: 2 }] }));
  vi.mocked(accounts.importCollectionPublication).mockRejectedValueOnce(new ApiError('Sin conexión', 503)).mockRejectedValueOnce(new ApiError('Revocado', 404));
  render(<AccountInvitations libraries={[{ id: idA, ownerId: idA, name: 'Mi destino', createdAt: '' }]} service={accounts} onExpired={vi.fn()} onImported={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Ver versión compartida de Colección compartida' }));
  await screen.findByText('<img src=x onerror=alert(1)>'); expect(document.querySelector('img')).toBeNull();
  expect(screen.getByText('Segundo cuerpo.')).toBeTruthy(); expect(accounts.importCollectionPublication).not.toHaveBeenCalled();
  fireEvent.change(screen.getByLabelText('Importar en mi biblioteca'), { target: { value: idA } });
  fireEvent.click(screen.getByRole('button', { name: 'Importar copia a mi biblioteca' }));
  await within(screen.getByRole('dialog')).findByRole('alert'); const first = vi.mocked(accounts.importCollectionPublication).mock.calls[0];
  fireEvent.click(screen.getByRole('button', { name: 'Importar copia a mi biblioteca' }));
  await waitFor(() => expect(screen.queryByText('Segundo cuerpo.')).toBeNull());
  expect(vi.mocked(accounts.importCollectionPublication).mock.calls[1][2]).toBe(first[2]);
  expect(first[0]).toBe(idB); expect(first[1]).toBe(idA); expect(accounts.importPublication).not.toHaveBeenCalled();
});

it('filters the account library on the server and does not silently assign a newly created prompt to that collection', async () => {
  const accounts = service(); const client = accounts.collections(idA);
  client.list = vi.fn(async () => [{ group: { id: idA, name: 'Ideas', revision: 1 }, templates: 0, instructions: 0 }]);
  accounts.collections = vi.fn(() => client);
  const source = { id: idB, libraryId: idA, title: 'Sin asignar', template: 'Analiza <tema>.', revision: 1 };
  accounts.prompts = vi.fn(async (_library, _signal, filter) => filter?.collectionId ? [] : [source]);
  render(<AccountPromptLibrary library={{ id: idA, ownerId: idA, name: 'Privada', createdAt: '' }} service={accounts} onExpired={vi.fn()} />);
  await screen.findByRole('button', { name: 'Usar Sin asignar' });
  await screen.findByRole('option', { name: 'Ideas' });
  fireEvent.change(screen.getByLabelText('Filtrar por colección'), { target: { value: idA } });
  await screen.findByText('No hay prompts en esta selección.');
  expect(accounts.prompts).toHaveBeenLastCalledWith(idA, expect.any(AbortSignal), { collectionId: idA });
  fireEvent.click(screen.getByRole('button', { name: 'Crear prompt' }));
  fireEvent.change(screen.getByLabelText('Título'), { target: { value: 'Nuevo sin asignar' } });
  fireEvent.change(screen.getByLabelText('Contenido'), { target: { value: 'Explica <tema>.' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Guardar prompt' }).closest('form')!);
  await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  await screen.findByText('No hay prompts en esta selección.');
  expect(client.setMember).not.toHaveBeenCalled();
  expect(screen.queryByRole('button', { name: 'Usar Nuevo sin asignar' })).toBeNull();
});

it('forgets terminal content only after confirmation and retries the same target after a transport failure', async () => {
  const accounts = service();
  const job = { id: idB, libraryId: idA, kind: 'CompilePrompt' as const, status: 'Completed' as const, cancelRequested: false,
    createdAt: '2026-10-26T00:00:00Z', startedAt: null, finishedAt: '2026-10-26T00:00:01Z', errorCode: null };
  vi.mocked(accounts.executions).mockResolvedValue([job]);
  render(<AccountExecutions libraryId={idA} service={accounts} refresh={0} onExpired={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: /Olvidar contenido de/ }));
  expect(accounts.forgetExecution).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'Conservar contenido' }));
  expect(screen.queryByRole('dialog')).toBeNull();
  expect(accounts.forgetExecution).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: /Olvidar contenido de/ }));
  vi.mocked(accounts.forgetExecution).mockRejectedValueOnce(new ApiError('Sin conexión', 503));
  fireEvent.click(screen.getByRole('button', { name: 'Olvidar definitivamente' }));
  await within(screen.getByRole('dialog')).findByRole('alert');
  const erased = { ...job, forgottenAt: '2026-10-26T01:00:00Z' };
  vi.mocked(accounts.forgetExecution).mockResolvedValue(erased);
  vi.mocked(accounts.executions).mockResolvedValue([erased]);
  fireEvent.click(screen.getByRole('button', { name: 'Olvidar definitivamente' }));
  await screen.findByText('Contenido olvidado');
  expect(accounts.forgetExecution).toHaveBeenNthCalledWith(1, idA, idB, expect.any(AbortSignal));
  expect(accounts.forgetExecution).toHaveBeenNthCalledWith(2, idA, idB, expect.any(AbortSignal));
  expect(screen.queryByRole('button', { name: 'Ver resultado' })).toBeNull();
  expect(screen.queryByRole('button', { name: /Olvidar contenido de/ })).toBeNull();
  expect(accounts.compilePrompt).not.toHaveBeenCalled(); expect(accounts.runLocalPrompt).not.toHaveBeenCalled();
});

it('keeps the erasure confirmation open when the returned receipt belongs to a different library', async () => {
  const accounts = service();
  vi.mocked(accounts.executions).mockResolvedValue([{ id: idB, libraryId: idA, kind: 'CompilePrompt', status: 'Failed', cancelRequested: false,
    createdAt: '', startedAt: null, finishedAt: '', errorCode: 'PROCESSING_FAILED' }]);
  vi.mocked(accounts.forgetExecution).mockResolvedValue({ id: idB, libraryId: idB, kind: 'CompilePrompt', status: 'Failed', cancelRequested: false,
    createdAt: '', startedAt: null, finishedAt: '', errorCode: 'PROCESSING_FAILED', forgottenAt: '2026-10-26T01:00:00Z' });
  render(<AccountExecutions libraryId={idA} service={accounts} refresh={0} onExpired={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: /Olvidar contenido de/ }));
  fireEvent.click(screen.getByRole('button', { name: 'Olvidar definitivamente' }));
  await within(screen.getByRole('dialog')).findByText(/El recibo no confirma/);
  expect(screen.queryByText('Contenido olvidado')).toBeNull();
});

it('requires an explicit local target and retains its receipt after a transport failure', async () => {
  const accounts = service();
  accounts.options = vi.fn(async () => ({ ...options, delegation: true }));
  accounts.connections = vi.fn(async () => [
    { id: idB, label: 'Mi equipo QA', profileId: 'ollama-qwen3-8b', createdAt: '', expiresAt: '', active: true, revoked: false },
    { id: idA, label: 'MCP de lectura', profileId: 'mcp-library-read', libraryId: idA, createdAt: '', expiresAt: '', active: true, revoked: false },
    { id: idA, label: 'MCP de importación', profileId: 'mcp-library-import', libraryId: idA, createdAt: '', expiresAt: '', active: true, revoked: false },
    { id: idA, label: 'MCP de creación', profileId: 'mcp-library', libraryId: idA, scopes: ['prompts:read', 'prompts:create'], createdAt: '', expiresAt: '', active: true, revoked: false },
  ]);
  vi.mocked(accounts.runLocalPrompt).mockRejectedValueOnce(new ApiError('Sin conexión', 503));
  const submitted = vi.fn();
  render(<UseAccountPromptDialog prompt={{ id: idA, libraryId: idA, title: 'Prueba local', template: 'Explica <tema>.', revision: 1 }} service={accounts} onExpired={vi.fn()} onSubmitted={submitted} onClose={vi.fn()} />);
  await screen.findByRole('option', { name: 'Qwen3:8b en Mi equipo QA' });
  expect(screen.queryByRole('option', { name: /MCP de lectura/ })).toBeNull();
  expect(screen.queryByRole('option', { name: /MCP de importación/ })).toBeNull();
  expect(screen.queryByRole('option', { name: /MCP de creación/ })).toBeNull();
  expect(accounts.runLocalPrompt).not.toHaveBeenCalled();
  fireEvent.change(await screen.findByLabelText('tema'), { target: { value: 'Una idea sintética' } });
  fireEvent.change(screen.getByLabelText('Cómo usar la instrucción'), { target: { value: idB } });
  fireEvent.click(screen.getByRole('button', { name: 'Ejecutar en mi equipo' }));
  await screen.findByRole('alert');
  const first = vi.mocked(accounts.runLocalPrompt).mock.calls[0];
  fireEvent.click(screen.getByRole('button', { name: 'Ejecutar en mi equipo' }));
  await waitFor(() => expect(submitted).toHaveBeenCalledTimes(1));
  expect(vi.mocked(accounts.runLocalPrompt).mock.calls[1][2]).toBe(first[2]);
  expect(first[3]).toBe(idB); expect(accounts.compilePrompt).not.toHaveBeenCalled();
});

it('reads model response and instruction separately as literal text without running again', async () => {
  const accounts = service();
  accounts.executions = vi.fn(async () => [{ id: idB, libraryId: idA, kind: 'RunLocalPrompt' as const, status: 'Completed' as const, cancelRequested: false, createdAt: '', startedAt: null, finishedAt: null, errorCode: null }]);
  accounts.executionResult = vi.fn(async () => ({ executionId: idB, promptId: idA, promptRevision: 1, title: 'Modelo local', text: 'Instrucción conservada.', response: '<img src=x onerror=alert(1)>', model: 'qwen3:8b' as const, createdAt: '' }));
  render(<AccountExecutions libraryId={idA} service={accounts} refresh={0} onExpired={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Ver resultado' }));
  await screen.findByText('<img src=x onerror=alert(1)>');
  expect(screen.getByText('Instrucción conservada.')).toBeTruthy();
  expect(screen.getByRole('dialog').querySelector('img')).toBeNull();
  expect(accounts.runLocalPrompt).not.toHaveBeenCalled();
});

it('shows only the MCP capabilities granted to each connection', async () => {
  const accounts = service();
  accounts.connections = vi.fn(async () => [
    { id: idA, label: 'MCP sólo lectura', profileId: 'mcp-library', libraryId: idA, scopes: ['prompts:read'], createdAt: '', expiresAt: '', active: true, revoked: false },
    { id: idB, label: 'MCP con colecciones', profileId: 'mcp-library', libraryId: idA, scopes: ['prompts:read', 'collections:import'], createdAt: '', expiresAt: '', active: true, revoked: false },
  ]);
  render(<AccountConnections service={accounts} onExpired={vi.fn()} />);
  const read = (await screen.findByRole('heading', { name: 'MCP sólo lectura' })).closest('li')!;
  const collections = screen.getByRole('heading', { name: 'MCP con colecciones' }).closest('li')!;
  expect(within(read).getByText(/Leer y completar variables/)).toBeTruthy();
  expect(within(read).queryByText(/Leer e importar colecciones aceptadas/)).toBeNull();
  expect(within(collections).getByText(/Leer e importar colecciones aceptadas/)).toBeTruthy();
  expect(within(collections).queryByText(/Guardar prompts nuevos|Importar versiones aceptadas/)).toBeNull();
  expect(accounts.decideDevice).not.toHaveBeenCalled();
  expect(accounts.revokeConnection).not.toHaveBeenCalled();
});

it('reviews a device without approving it and discards consent when its code changes', async () => {
  const accounts = service();
  render(<AccountConnections service={accounts} onExpired={vi.fn()} />);
  await screen.findByText('No tienes equipos autorizados.');
  fireEvent.change(screen.getByLabelText('Código del equipo'), { target: { value: 'synthetic-code' } });
  fireEvent.click(screen.getByRole('button', { name: 'Revisar solicitud' }));
  await screen.findByRole('region', { name: 'Permisos solicitados' });
  expect(accounts.decideDevice).not.toHaveBeenCalled();
  fireEvent.change(screen.getByLabelText('Código del equipo'), { target: { value: 'another-code' } });
  expect(screen.queryByRole('button', { name: 'Aprobar este equipo' })).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Revisar solicitud' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Rechazar solicitud' }));
  await screen.findByText('Solicitud rechazada. No se otorgó acceso al equipo.');
  expect(accounts.decideDevice).toHaveBeenCalledWith('another-code', 'deny', expect.any(AbortSignal));
});

it('distinguishes local proposal permission from prompt execution without granting either', async () => {
  const accounts = service();
  accounts.connections = vi.fn(async () => [
    { id: idA, label: 'Equipo de instrucciones', profileId: 'ollama-qwen3-8b', createdAt: '', expiresAt: '', active: true, revoked: false },
    { id: idB, label: 'Equipo de propuestas', profileId: 'ollama-qwen3-8b-proposals', createdAt: '', expiresAt: '', active: true, revoked: false },
  ]);
  render(<AccountConnections service={accounts} onExpired={vi.fn()} />);
  await screen.findByRole('heading', { name: 'Equipo de propuestas' });
  expect(within(screen.getByRole('heading', { name: 'Equipo de instrucciones' }).closest('li')!).getByText(/Equipo local · ejecutar instrucciones/)).toBeTruthy();
  expect(within(screen.getByRole('heading', { name: 'Equipo de propuestas' }).closest('li')!).getByText(/Equipo local · propuestas de NOVA/)).toBeTruthy();
  expect(accounts.decideDevice).not.toHaveBeenCalled();
  expect(accounts.runLocalPrompt).not.toHaveBeenCalled();
});

it('requires explicit device approval and revocation confirmation', async () => {
  const accounts = service();
  const connection = { id: idB, label: 'Equipo QA', profileId: 'ollama-qwen3-8b', createdAt: '2026-10-26T00:00:00Z', expiresAt: '2026-11-25T00:00:00Z', revoked: false, active: true };
  accounts.decideDevice = vi.fn(async () => { vi.mocked(accounts.connections).mockResolvedValue([connection]); });
  accounts.revokeConnection = vi.fn(async () => { vi.mocked(accounts.connections).mockResolvedValue([{ ...connection, revoked: true, active: false }]); return undefined; });
  render(<AccountConnections service={accounts} onExpired={vi.fn()} />);
  fireEvent.change(screen.getByLabelText('Código del equipo'), { target: { value: 'synthetic-code' } });
  fireEvent.click(screen.getByRole('button', { name: 'Revisar solicitud' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Aprobar este equipo' }));
  await screen.findByText(/Autorización guardada/);
  fireEvent.click(screen.getByRole('button', { name: 'Revocar autorización' }));
  await screen.findByRole('dialog', { name: 'Revocar Equipo QA' });
  fireEvent.click(screen.getByRole('button', { name: 'Conservar autorización' }));
  expect(accounts.revokeConnection).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'Revocar autorización' }));
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar revocación' }));
  await screen.findByText('Autorización revocada.');
  expect(accounts.revokeConnection).toHaveBeenCalledWith(idB, expect.any(AbortSignal));
  expect(screen.queryByRole('button', { name: 'Revocar autorización' })).toBeNull();
});

it('adapts verification form acknowledgments without accepting unrelated errors as denial', async () => {
  const send = vi.fn(async (_path: string, init: RequestInit) => {
    if (_path === '/account/csrf') return Response.json({ token: 'synthetic-csrf' });
    expect(new Headers(init.headers).get('Content-Type')).toBe('application/x-www-form-urlencoded');
    const form = init.body as URLSearchParams;
    expect(form.get('user_code')).toBe('synthetic-code');
    return new Response('error:access_denied\nerror_description:omitted', { status: 400 });
  });
  const accounts = createAccountService(createApiClient({ send }, ''));
  await expect(accounts.decideDevice(' synthetic-code ', 'deny')).resolves.toBeUndefined();
  await expect(accounts.decideDevice(' synthetic-code ', 'approve')).rejects.toBeInstanceOf(ApiError);
});

const idA = '10000000-0000-4000-8000-000000000001';
const idB = '10000000-0000-4000-8000-000000000002';
const options = { email: true, google: true, delegation: false, mailDelivery: 'development-pickup' };
afterEach(() => { cleanup(); window.history.replaceState(null, '', '/'); });
function service(): AccountService {
  return {
    async catalog(libraryId, query, signal) {
      const items = await this.prompts(libraryId, signal, query.collectionId ? { collectionId: query.collectionId } : query.ungrouped ? { ungrouped: true } : undefined);
      return { items, total: items.length, page: query.page, size: query.size };
    },
    actPrompt: vi.fn(async (prompt, action) => ({ ...prompt, id: action === 'duplicate' ? crypto.randomUUID() : prompt.id, revision: prompt.revision + 1 })),
    deletePrompt: vi.fn(async () => undefined),
    recordPromptUse: vi.fn(async () => undefined),
    versions: vi.fn(async () => []),
    exportPrompt: vi.fn(async prompt => ({ format: 'quickprompt-template' as const, formatVersion: 1 as const, prompt: { title: prompt.title, template: prompt.template, description: '', category: 0 } })),
    preferences: { get: vi.fn(async () => ({ language: null, format: null, providerId: null })), save: vi.fn(async preferences => preferences), forget: vi.fn(async () => undefined) },
    nova: vi.fn(() => { throw new Error('Esta prueba no inicia NOVA.'); }),
    inspectEditor: vi.fn(async (_libraryId, input) => ({ variableNames: ['tema'], compiledText: input.values.tema ? `Explica ${input.values.tema}.` : null, missingVariables: input.values.tema ? [] : ['tema'] })),
    csrf: vi.fn(async () => 'synthetic-csrf'), options: vi.fn(async () => options), current: vi.fn(async () => null),
    connections: vi.fn(async () => []), reviewDevice: vi.fn(async () => ({ label: 'Equipo QA', profileId: 'ollama-qwen3-8b', model: 'qwen3:8b', permissions: ['Procesar solicitudes'], expiresInMinutes: 10 })),
    runLocalPrompt: vi.fn(async prompt => ({ id: idB, libraryId: prompt.libraryId, kind: 'RunLocalPrompt' as const, status: 'Queued' as const, cancelRequested: false, createdAt: '2026-10-26T00:00:00Z', startedAt: null, finishedAt: null, errorCode: null })),
    decideDevice: vi.fn(async () => undefined), revokeConnection: vi.fn(async () => undefined),
    login: vi.fn(async () => undefined), register: vi.fn(async () => ({ message: 'Revisa tu correo.' })), recover: vi.fn(async () => ({ message: 'Revisa tu correo.' })),
    confirm: vi.fn(async () => undefined), reset: vi.fn(async () => undefined), logout: vi.fn(async () => undefined),
    libraries: vi.fn(async () => []), createLibrary: vi.fn(async name => ({ id: idA, ownerId: idA, name, createdAt: '' })),
    collections: vi.fn(() => ({ list: vi.fn(async () => []), memberships: vi.fn(async () => []), create: vi.fn(async () => ({ id: idB })),
      rename: vi.fn(async () => ({ id: idB })), remove: vi.fn(async () => undefined), setMember: vi.fn(async () => undefined) })),
    prompts: vi.fn(async () => []), savePrompt: vi.fn(async (libraryId, source, draft) => ({ id: source?.id ?? idB, libraryId, ...draft, revision: (source?.revision ?? 0) + 1 })),
    prompt: vi.fn(async (libraryId, id) => ({ id, libraryId, title: 'Origen', template: 'Explica <tema>.', revision: 1 })),
    collectionPublications: vi.fn(async () => []),
    publishCollection: vi.fn(async () => ({ id: idB, title: 'Colección', sourceRevision: 2, createdAt: '', items: [{ title: 'Uno', template: 'Explica <tema>.', sourceRevision: 1 }] })),
    readCollectionPublication: vi.fn(async id => ({ id, title: 'Colección', sourceRevision: 2, createdAt: '', items: [{ title: 'Uno', template: 'Explica <tema>.', sourceRevision: 1 }] })),
    importCollectionPublication: vi.fn(async (_id, libraryId) => ({ id: idB, libraryId, name: 'Colección', revision: 2, prompts: [{ id: idB, libraryId, title: 'Uno', template: 'Explica <tema>.', revision: 1 }] })),
    publications: vi.fn(async () => []), publish: vi.fn(async prompt => ({ id: idB, title: prompt.title, template: prompt.template, sourceRevision: prompt.revision, createdAt: '2026-10-26T00:00:00Z' })),
    invite: vi.fn(async () => ({ message: 'Solicitud procesada.' })), revokePublication: vi.fn(async () => undefined),
    invitations: vi.fn(async () => []), decideInvitation: vi.fn(async () => undefined),
    readPublication: vi.fn(async id => ({ id, title: 'Compartido', template: 'Explica <tema>.', sourceRevision: 1, createdAt: '2026-10-26T00:00:00Z' })),
    importPublication: vi.fn(async (_id, libraryId) => ({ id: idB, libraryId, title: 'Compartido', template: 'Explica <tema>.', revision: 1 })),
    rememberedContexts: vi.fn(async () => []),
    readContext: vi.fn(async (_libraryId, id) => ({ id, draftId: idB, draftRevision: 1, memoryRevision: 1, sourceId: null, sourceRevision: null,
      title: 'Borrador conservado', templateText: 'Explica <tema>.', values: {}, variableNames: ['tema'], compiledText: null, sourceStale: false })),
    rememberEditor: vi.fn(async (_libraryId, input) => ({ id: input.sessionId, draftId: input.expectedDraftId ?? idB, draftRevision: 1,
      memoryRevision: input.expectedMemoryRevision + 1, sourceId: input.promptId, sourceRevision: input.revision, title: input.title,
      templateText: input.templateText, values: input.values, variableNames: ['tema'], compiledText: null, sourceStale: false })),
    forgetContext: vi.fn(async () => undefined),
    renderPrompt: vi.fn(async prompt => ({ promptId: prompt.id, templateRevision: prompt.revision, text: null, missingVariables: ['tema'] })),
    compilePrompt: vi.fn(async prompt => ({ id: idB, libraryId: prompt.libraryId, kind: 'CompilePrompt' as const, status: 'Queued' as const, cancelRequested: false,
      createdAt: '2026-10-26T00:00:00Z', startedAt: null, finishedAt: null, errorCode: null })),
    executions: vi.fn(async () => []),
    watchExecutions(libraryId, observe, signal) { return this.executions(libraryId, signal).then(observe); },
    executionResult: vi.fn(async (_libraryId, id) => ({ executionId: id, promptId: idB, promptRevision: 1, title: 'Resultado', text: 'Explica RLS.', createdAt: '2026-10-26T00:00:00Z' })),
    cancelExecution: vi.fn(async (libraryId, id) => ({ id, libraryId, kind: 'CompilePrompt' as const, status: 'Queued' as const, cancelRequested: true,
      createdAt: '2026-10-26T00:00:00Z', startedAt: null, finishedAt: null, errorCode: null })),
    forgetExecution: vi.fn(async (libraryId, id) => ({ id, libraryId, kind: 'CompilePrompt' as const, status: 'Completed' as const, cancelRequested: false,
      createdAt: '2026-10-26T00:00:00Z', startedAt: null, finishedAt: '2026-10-26T00:00:01Z', errorCode: null, forgottenAt: '2026-10-26T01:00:00Z' })),
  };
}

it('prepares only on explicit submission, preserves literal values and reuses a receipt after transport failure', async () => {
  const api = service(); const submitted = vi.fn();
  const prompt = { id: idB, libraryId: idA, title: 'Instrucción', template: 'Explica <tema>.', revision: 1 };
  vi.mocked(api.compilePrompt).mockRejectedValueOnce(new ApiError('Sin conexión', 503));
  render(<UseAccountPromptDialog prompt={prompt} service={api} onExpired={vi.fn()} onSubmitted={submitted} onClose={vi.fn()} />);
  const value = await screen.findByLabelText('tema');
  expect(api.compilePrompt).not.toHaveBeenCalled();
  fireEvent.change(value, { target: { value: '<literal>\nUnicode ñ' } });
  fireEvent.click(screen.getByRole('button', { name: 'Preparar resultado' }));
  await screen.findByRole('alert');
  fireEvent.click(screen.getByRole('button', { name: 'Preparar resultado' }));
  await waitFor(() => expect(submitted).toHaveBeenCalledTimes(1));
  expect(vi.mocked(api.compilePrompt).mock.calls[0][1]).toEqual({ tema: '<literal>\nUnicode ñ' });
  expect(vi.mocked(api.compilePrompt).mock.calls[1][2]).toBe(vi.mocked(api.compilePrompt).mock.calls[0][2]);
});

it('discovers editor variables without remembering the draft and keeps the active field during preview refresh', async () => {
  const accounts = service();
  accounts.prompts = vi.fn(async () => [{ id: idB, libraryId: idA, title: 'Conceptos', template: 'Explica <tema>.', revision: 3 }]);
  const expired = vi.fn();
  render(<AccountPromptLibrary library={{ id: idA, ownerId: idA, name: 'Privada', createdAt: '' }} service={accounts} onExpired={expired} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Editar Conceptos' }));
  const value = await screen.findByLabelText('tema'); value.focus();
  fireEvent.change(value, { target: { value: 'RLS <literal>\nñ' } });
  expect(screen.getByLabelText('tema')).toBe(value); expect(document.activeElement).toBe(value);
  expect(accounts.rememberEditor).not.toHaveBeenCalled(); expect(accounts.savePrompt).not.toHaveBeenCalled();
  await waitFor(() => expect(screen.getByLabelText('Vista previa del borrador').textContent).toBe('Explica RLS <literal>\nñ.'));
  expect(accounts.inspectEditor).toHaveBeenLastCalledWith(idA, { promptId: idB, revision: 3, templateText: 'Explica <tema>.', values: { tema: 'RLS <literal>\nñ' } }, expect.any(AbortSignal));
  fireEvent.click(screen.getByRole('button', { name: 'Guardar prompt' }));
  await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  expect(accounts.savePrompt).toHaveBeenCalledWith(idA, expect.objectContaining({ id: idB, revision: 3 }), { title: 'Conceptos', template: 'Explica <tema>.' }, expect.any(AbortSignal));
  expect(accounts.rememberEditor).not.toHaveBeenCalled(); expect(expired).not.toHaveBeenCalled();
});

it('discards old execution snapshots immediately when the library changes', async () => {
  const api = service(); const callbacks = new Map<string, (items: import('../src/services/accounts').AccountExecution[]) => void>();
  const signals: AbortSignal[] = [];
  api.watchExecutions = vi.fn(async (libraryId, observe, signal) => {
    callbacks.set(libraryId, observe); signals.push(signal);
    await new Promise<void>(resolve => signal.addEventListener('abort', () => resolve(), { once: true }));
  });
  const view = render(<AccountExecutions libraryId={idA} service={api} refresh={0} onExpired={vi.fn()} />);
  const job = { id: idB, libraryId: idA, kind: 'CompilePrompt' as const, status: 'Completed' as const, cancelRequested: false, createdAt: '', startedAt: null, finishedAt: null, errorCode: null };
  await act(async () => callbacks.get(idA)!([job]));
  expect(screen.getByRole('button', { name: 'Ver resultado' })).toBeTruthy();
  view.rerender(<AccountExecutions libraryId={idB} service={api} refresh={0} onExpired={vi.fn()} />);
  expect(signals[0].aborted).toBe(true); expect(screen.queryByRole('button', { name: 'Ver resultado' })).toBeNull();
  await act(async () => callbacks.get(idA)!([job]));
  expect(screen.queryByRole('button', { name: 'Ver resultado' })).toBeNull();
  await act(async () => callbacks.get(idB)!([]));
  expect(screen.getByText('Aún no has preparado una instrucción en esta biblioteca.')).toBeTruthy();
});

it('reads a stored result literally without replaying or rendering its HTML', async () => {
  const api = service();
  vi.mocked(api.executions).mockResolvedValue([{ id: idB, libraryId: idA, kind: 'CompilePrompt', status: 'Completed', cancelRequested: false, createdAt: '2026-10-26T00:00:00Z', startedAt: null, finishedAt: '2026-10-26T00:00:01Z', errorCode: null }]);
  vi.mocked(api.executionResult).mockResolvedValue({ executionId: idB, promptId: idB, promptRevision: 1, title: 'Literal', text: '<img src=x onerror=alert(1)>', createdAt: '2026-10-26T00:00:01Z' });
  render(<AccountExecutions libraryId={idA} service={api} refresh={0} onExpired={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Ver resultado' }));
  await screen.findByText('<img src=x onerror=alert(1)>');
  expect(document.querySelector('img')).toBeNull();
  expect(api.compilePrompt).not.toHaveBeenCalled();
  expect(api.renderPrompt).not.toHaveBeenCalled();
});

it('offers an explicit retry if variable discovery fails without creating a preparation', async () => {
  const api = service(); vi.mocked(api.renderPrompt).mockRejectedValueOnce(new ApiError('Desconectado', 503));
  const prompt = { id: idB, libraryId: idA, title: 'Instrucción', template: 'Explica <tema>.', revision: 1 };
  render(<UseAccountPromptDialog prompt={prompt} service={api} onExpired={vi.fn()} onSubmitted={vi.fn()} onClose={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Volver a consultar variables' }));
  await screen.findByLabelText('tema'); expect(api.compilePrompt).not.toHaveBeenCalled();
  expect(api.renderPrompt).toHaveBeenCalledTimes(2);
});

it('requests cancellation of the selected owned preparation without submitting another job', async () => {
  const api = service();
  const queued = { id: idB, libraryId: idA, kind: 'CompilePrompt' as const, status: 'Queued' as const, cancelRequested: false, createdAt: '2026-10-26T00:00:00Z', startedAt: null, finishedAt: null, errorCode: null };
  vi.mocked(api.executions).mockResolvedValue([queued]);
  vi.mocked(api.cancelExecution).mockImplementation(async () => { vi.mocked(api.executions).mockResolvedValue([{ ...queued, cancelRequested: true }]); return { ...queued, cancelRequested: true }; });
  render(<AccountExecutions libraryId={idA} service={api} refresh={0} onExpired={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Detener preparación' }));
  await screen.findByText('Cancelación solicitada…');
  expect(api.cancelExecution).toHaveBeenCalledWith(idA, idB, expect.any(AbortSignal));
  expect(api.compilePrompt).not.toHaveBeenCalled();
});

it('requires a new user request after interruption and does not expose a mismatched result', async () => {
  const api = service();
  const job = { id: idB, libraryId: idA, kind: 'CompilePrompt' as const, status: 'Interrupted' as const, cancelRequested: false, createdAt: '2026-10-26T00:00:00Z', startedAt: null, finishedAt: '2026-10-26T00:00:01Z', errorCode: 'LEASE_EXPIRED' };
  vi.mocked(api.executions).mockResolvedValue([job]);
  const view = render(<AccountExecutions libraryId={idA} service={api} refresh={0} onExpired={vi.fn()} />);
  await screen.findByText('Interrumpida; no se repetirá sola');
  expect(api.compilePrompt).not.toHaveBeenCalled(); expect(screen.queryByRole('button', { name: 'Ver resultado' })).toBeNull();
  vi.mocked(api.executions).mockResolvedValue([{ ...job, status: 'Completed' }]);
  vi.mocked(api.executionResult).mockResolvedValue({ executionId: idA, promptId: idB, promptRevision: 1, title: 'Incorrecto', text: 'No mostrar', createdAt: '2026-10-26T00:00:01Z' });
  view.rerender(<AccountExecutions libraryId={idA} service={api} refresh={1} onExpired={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Ver resultado' }));
  await screen.findByRole('alert'); expect(screen.queryByText('No mostrar')).toBeNull();
});

it('uses fresh CSRF per mutation and keeps account traffic outside the local library API', async () => {
  let token = 0;
  const send = vi.fn(async (path: string, init: RequestInit) => {
    if (path === '/account/csrf') return Response.json({ token: `synthetic-${++token}` });
    expect(new Headers(init.headers).get('X-QP-CSRF')).toBe(`synthetic-${token}`);
    expect(init.credentials).toBe('same-origin');
    if (path === '/account/login') return new Response(null, { status: 204 });
    if (path === '/api/accounts/v1/libraries/') return Response.json({ id: idA, ownerId: idA, name: 'Privada', createdAt: '' });
    throw Error('Unexpected account route.');
  });
  const accounts = createAccountService(createApiClient({ send }, ''));
  await accounts.login('synthetic@example.invalid', 'Existing-Synthetic!79');
  await accounts.createLibrary('Privada');
  expect(token).toBe(2);
  expect(send.mock.calls.map(call => call[0])).toEqual(['/account/csrf', '/account/login', '/account/csrf', '/api/accounts/v1/libraries/']);
});

it('submits Google through a native same-origin form and allows password managers', () => {
  render(<AccountForm service={service()} options={options} csrf="synthetic-token" onLogin={() => {}} />);
  const google = screen.getByRole('button', { name: /Continuar con Google/ }).closest('form')!;
  expect(google.getAttribute('action')).toBe('/account/google');
  expect(google.getAttribute('method')).toBe('post');
  expect(google.querySelector<HTMLInputElement>('[name=__RequestVerificationToken]')?.value).toBe('synthetic-token');
  expect(screen.getByLabelText('Contraseña').getAttribute('autocomplete')).toBe('current-password');
  expect(screen.getByLabelText('Correo electrónico').getAttribute('autocomplete')).toBe('email');
});

it('keeps existing password login available when registration mail is not configured', () => {
  render(<AccountForm service={service()} options={{ ...options, email: false }} csrf="synthetic" onLogin={() => {}} />);
  expect((screen.getByRole('button', { name: 'Entrar' }) as HTMLButtonElement).disabled).toBe(false);
  expect((screen.getByRole('button', { name: 'Crear una cuenta' }) as HTMLButtonElement).disabled).toBe(true);
});

it('does not apply a completed login to an unmounted form', async () => {
  const accounts = service();
  let finish!: () => void;
  accounts.login = vi.fn(() => new Promise<undefined>(resolve => { finish = () => resolve(undefined); }));
  const onLogin = vi.fn();
  const view = render(<AccountForm service={accounts} options={options} csrf="synthetic" onLogin={onLogin} />);
  fireEvent.change(screen.getByLabelText('Correo electrónico'), { target: { value: 'synthetic@example.invalid' } });
  fireEvent.change(screen.getByLabelText('Contraseña'), { target: { value: 'Existing-Synthetic!79' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Entrar' }).closest('form')!);
  view.unmount();
  await act(async () => finish());
  expect(onLogin).not.toHaveBeenCalled();
});

it('retains the confirmation token under StrictMode while clearing it from the visible URL', async () => {
  const accounts = service();
  window.history.replaceState(null, '', `/account/confirm?userId=${idA}&code=synthetic-confirmation`);
  render(<StrictMode><AccountForm service={accounts} options={options} csrf="synthetic" onLogin={() => {}} /></StrictMode>);
  expect(window.location.search).toBe('');
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar correo' }));
  await screen.findByText('Correo confirmado. Ya puedes entrar.');
  expect(accounts.confirm).toHaveBeenCalledTimes(1);
  expect(accounts.confirm).toHaveBeenCalledWith({ userId: idA, code: 'synthetic-confirmation' }, expect.any(AbortSignal));
});

it('clears private library rows on logout and mounts separate state for the next account', async () => {
  const accounts = service();
  let current: AccountUser | null = { id: idA, email: 'a@example.invalid' };
  accounts.current = vi.fn(async () => current);
  accounts.libraries = vi.fn(async () => [{ id: current!.id, ownerId: current!.id, name: current!.id === idA ? 'Sólo A' : 'Sólo B', createdAt: '' }]);
  accounts.logout = vi.fn(async () => { current = null; return undefined; });
  accounts.login = vi.fn(async () => { current = { id: idB, email: 'b@example.invalid' }; return undefined; });
  render(<AccountPage service={accounts} />);
  await screen.findByText('Sólo A');
  fireEvent.click(screen.getByRole('button', { name: /Cerrar sesión/ }));
  await screen.findByRole('button', { name: 'Entrar' });
  expect(screen.queryByText('Sólo A')).toBeNull();
  fireEvent.change(screen.getByLabelText('Correo electrónico'), { target: { value: 'b@example.invalid' } });
  fireEvent.change(screen.getByLabelText('Contraseña'), { target: { value: 'Existing-Synthetic!79' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Entrar' }).closest('form')!);
  await screen.findByText('Sólo B');
  expect(screen.queryByText('Sólo A')).toBeNull();
  await waitFor(() => expect(screen.getByText('b@example.invalid')).toBeTruthy());
});

it('saves the selected prompt revision through PUT without accepting a client actor', async () => {
  const source = { id: idB, libraryId: idA, title: 'Original', template: 'Explica <tema>.', revision: 4 };
  const send = vi.fn(async (path: string, init: RequestInit) => {
    if (path === '/account/csrf') return Response.json({ token: 'synthetic-csrf' });
    expect(path).toBe(`/api/accounts/v1/libraries/${idA}/prompts/${idB}`);
    expect(init.method).toBe('PUT');
    expect(JSON.parse(init.body as string)).toEqual({ title: 'Editado', template: 'Explica <tema> en español.', revision: 4 });
    expect(new Headers(init.headers).get('X-QP-CSRF')).toBe('synthetic-csrf');
    return Response.json({ ...source, title: 'Editado', template: 'Explica <tema> en español.', revision: 5 });
  });
  const accounts = createAccountService(createApiClient({ send }, ''));
  expect((await accounts.savePrompt(idA, source, { title: 'Editado', template: 'Explica <tema> en español.' })).revision).toBe(5);
});

it('does not read pending invitations and requires explicit acceptance before showing content', async () => {
  const accounts = service();
  const invitation = { id: idA, publicationId: idB, title: 'Compartido', state: 0, sentByMe: false, recipientEmail: null, available: false };
  let accepted = false;
  accounts.invitations = vi.fn(async () => [{ ...invitation, state: accepted ? 1 : 0, available: accepted }]);
  accounts.decideInvitation = vi.fn(async () => { accepted = true; return undefined; });
  render(<AccountInvitations libraries={[]} service={accounts} onExpired={vi.fn()} onImported={vi.fn()} />);
  await screen.findByRole('button', { name: 'Aceptar invitación a Compartido' });
  expect(accounts.readPublication).not.toHaveBeenCalled();
  expect(screen.queryByText('Explica <tema>.')).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Aceptar invitación a Compartido' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Ver versión compartida de Compartido' }));
  await screen.findByText('Explica <tema>.');
  expect(accounts.decideInvitation).toHaveBeenCalledWith(idA, 1, expect.any(AbortSignal));
  expect(accounts.readPublication).toHaveBeenCalledWith(idB, expect.any(AbortSignal));
});

it('reuses the import receipt after a transport error instead of creating another copy request', async () => {
  const accounts = service();
  accounts.invitations = vi.fn(async () => [{ id: idA, publicationId: idB, title: 'Compartido', state: 1, sentByMe: false, recipientEmail: null, available: true }]);
  accounts.importPublication = vi.fn().mockRejectedValueOnce(new Error('Conexión interrumpida.')).mockResolvedValueOnce({ id: idB, libraryId: idA, title: 'Compartido', template: 'Explica <tema>.', revision: 1 });
  render(<AccountInvitations libraries={[{ id: idA, ownerId: idA, name: 'Destino', createdAt: '' }]} service={accounts} onExpired={vi.fn()} onImported={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Ver versión compartida de Compartido' }));
  await screen.findByText('Explica <tema>.');
  fireEvent.change(screen.getByLabelText('Importar en mi biblioteca'), { target: { value: idA } });
  fireEvent.click(screen.getByRole('button', { name: 'Importar copia a mi biblioteca' }));
  await screen.findByText(/Conexión interrumpida/);
  fireEvent.click(screen.getByRole('button', { name: 'Importar copia a mi biblioteca' }));
  await screen.findByRole('button', { name: 'Abrir mi copia' });
  const calls = vi.mocked(accounts.importPublication).mock.calls;
  expect(calls).toHaveLength(2);
  expect(calls[0].slice(0, 3)).toEqual(calls[1].slice(0, 3));
});

it('clears a shared preview when a new import is denied after revocation', async () => {
  const accounts = service();
  accounts.invitations = vi.fn(async () => [{ id: idA, publicationId: idB, title: 'Compartido', state: 1, sentByMe: false, recipientEmail: null, available: true }]);
  const { ApiError } = await import('../src/services/api');
  accounts.importPublication = vi.fn(async () => { throw new ApiError('Acceso revocado.', 404); });
  render(<AccountInvitations libraries={[{ id: idA, ownerId: idA, name: 'Destino', createdAt: '' }]} service={accounts} onExpired={vi.fn()} onImported={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Ver versión compartida de Compartido' }));
  await screen.findByText('Explica <tema>.');
  fireEvent.change(screen.getByLabelText('Importar en mi biblioteca'), { target: { value: idA } });
  fireEvent.click(screen.getByRole('button', { name: 'Importar copia a mi biblioteca' }));
  await screen.findByText(/Acceso revocado/);
  expect(screen.queryByText('Explica <tema>.')).toBeNull();
  expect(screen.queryByRole('button', { name: 'Importar copia a mi biblioteca' })).toBeNull();
});

it('discards an old library response when returning to a tab with a different authenticated account', async () => {
  const accounts = service();
  let actor = idA;
  let finish!: () => void;
  let pendingSignal: AbortSignal | undefined;
  accounts.current = vi.fn(async () => ({ id: actor, email: `${actor}@example.invalid` }));
  accounts.libraries = vi.fn(async () => [{ id: actor, ownerId: actor, name: actor === idA ? 'Sólo A' : 'Sólo B', createdAt: '' }]);
  accounts.prompts = vi.fn((_libraryId: string, signal?: AbortSignal) => new Promise<AccountPrompt[]>(resolve => {
    pendingSignal = signal;
    finish = () => resolve([{ id: idB, libraryId: idA, title: 'Respuesta privada tardía de A', template: 'Privado', revision: 1 }]);
  }));
  render(<AccountPage service={accounts} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Abrir biblioteca Sólo A' }));
  await screen.findByText('Cargando prompts…');
  actor = idB;
  fireEvent.focus(window);
  await screen.findByText('Sólo B');
  expect(pendingSignal?.aborted).toBe(true);
  await act(async () => finish());
  expect(screen.queryByText('Respuesta privada tardía de A')).toBeNull();
  expect(screen.queryByText('Sólo A')).toBeNull();
});

it('keeps a manual draft masked after a failed identity check and restores its exact nodes only after a successful retry', async () => {
  const accounts = service(); let online = true;
  accounts.current = vi.fn(async () => { if (!online) throw new TypeError('Sin conexión'); return { id: idA, email: 'a@example.invalid' }; });
  accounts.libraries = vi.fn(async () => [{ id: idA, ownerId: idA, name: 'Privada', createdAt: '' }]);
  render(<AccountPage service={accounts} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Abrir biblioteca Privada' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Crear prompt' }));
  const field = screen.getByRole('textbox', { name: 'Contenido' });
  fireEvent.change(field, { target: { value: 'Borrador privado <tema>.' } });
  online = false; fireEvent.focus(window);
  await screen.findByText('No pudimos confirmar tu acceso. El contenido sigue oculto.');
  expect(field.isConnected).toBe(true);
  expect(screen.queryByRole('textbox', { name: 'Contenido' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Guardar prompt' })).toBeNull();
  expect(accounts.savePrompt).not.toHaveBeenCalled();
  online = true;
  fireEvent.click(within(screen.getByRole('dialog', { name: 'Comprobando sesión' })).getByRole('button', { name: 'Volver a comprobar acceso' }));
  await screen.findByRole('textbox', { name: 'Contenido' });
  expect(screen.getByRole('textbox', { name: 'Contenido' })).toBe(field);
  expect(field).toHaveProperty('value', 'Borrador privado <tema>.');
  expect(accounts.savePrompt).not.toHaveBeenCalled();
});

it('coalesces foreground checks, bounds an unresponsive identity query and ignores its late result', async () => {
  const accounts = service(); let hanging = false; let finish!: () => void;
  const actor = { id: idA, email: 'a@example.invalid' };
  accounts.current = vi.fn(() => hanging ? new Promise<AccountUser>(resolve => { finish = () => resolve(actor); }) : Promise.resolve(actor));
  render(<AccountPage service={accounts} />);
  await screen.findByText(actor.email);
  hanging = true; fireEvent.focus(window); fireEvent.focus(window);
  expect(accounts.current).toHaveBeenCalledTimes(2);
  await screen.findByText('No pudimos confirmar tu acceso. El contenido sigue oculto.', {}, { timeout: 6000 });
  await act(async () => finish());
  expect(screen.queryByRole('heading', { name: 'Mis bibliotecas' })).toBeNull();
  hanging = false; fireEvent.click(screen.getByRole('button', { name: 'Volver a comprobar acceso' }));
  await screen.findByRole('heading', { name: 'Mis bibliotecas' });
}, 10000);

it('keeps an edited draft visible on revision conflict and does not announce a successful save', async () => {
  const accounts = service();
  accounts.prompts = vi.fn(async () => [{ id: idB, libraryId: idA, title: 'Original', template: 'Texto original.', revision: 1 }]);
  const { ApiError } = await import('../src/services/api');
  accounts.savePrompt = vi.fn(async () => { throw new ApiError('El prompt cambió.', 409); });
  render(<AccountPromptLibrary library={{ id: idA, ownerId: idA, name: 'Privada', createdAt: '' }} service={accounts} onExpired={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Editar Original' }));
  fireEvent.change(screen.getByLabelText('Contenido'), { target: { value: 'Mi cambio sin perder.' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Guardar prompt' }).closest('form')!);
  await screen.findByText(/El prompt cambió/);
  expect((screen.getByLabelText('Contenido') as HTMLTextAreaElement).value).toBe('Mi cambio sin perder.');
  expect(screen.queryByText('Prompt guardado en tu biblioteca.')).toBeNull();
});

it('remembers only on explicit action and advances the context revision for value-only changes', async () => {
  const accounts = service();
  accounts.rememberEditor = vi.fn(async (_libraryId, input) => ({ id: input.sessionId, draftId: idB, draftRevision: 1,
    memoryRevision: input.expectedMemoryRevision + 1, sourceId: null, sourceRevision: null, title: input.title, templateText: input.templateText,
    values: input.values, variableNames: ['tema'], compiledText: input.values.tema ? `Explica ${input.values.tema}.` : null, sourceStale: false }));
  render(<AccountPromptLibrary library={{ id: idA, ownerId: idA, name: 'Privada', createdAt: '' }} service={accounts} onExpired={vi.fn()} />);
  fireEvent.click(screen.getByRole('button', { name: 'Crear prompt' }));
  fireEvent.change(screen.getByLabelText('Título'), { target: { value: 'Mi contexto' } });
  fireEvent.change(screen.getByLabelText('Contenido'), { target: { value: 'Explica <tema>.' } });
  expect(accounts.rememberEditor).not.toHaveBeenCalled();
  expect(accounts.savePrompt).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'Conservar borrador' }));
  await screen.findByLabelText('tema');
  fireEvent.change(screen.getByLabelText('tema'), { target: { value: 'RLS' } });
  fireEvent.click(screen.getByRole('button', { name: 'Conservar borrador' }));
  await screen.findByText('Explica RLS.');
  const calls = vi.mocked(accounts.rememberEditor).mock.calls;
  expect(calls[0][1]).toMatchObject({ expectedMemoryRevision: 0, expectedDraftRevision: 0, expectedDraftId: null, confirmed: true });
  expect(calls[1][1]).toMatchObject({ expectedMemoryRevision: 1, expectedDraftRevision: 1, expectedDraftId: idB, confirmed: true, values: { tema: 'RLS' } });
  expect(calls[0][1].sessionId).toBe(calls[1][1].sessionId);
  expect(accounts.savePrompt).not.toHaveBeenCalled();
});

it('restores stale content without allowing an in-place overwrite and offers an explicit new copy', async () => {
  const accounts = service();
  accounts.rememberedContexts = vi.fn(async () => [{ id: idB, title: 'Conservado', updatedAt: '2026-10-26T00:00:00Z', expiresAt: '2026-11-02T00:00:00Z' }]);
  accounts.readContext = vi.fn(async () => ({ id: idB, draftId: idB, draftRevision: 1, memoryRevision: 3, sourceId: idA, sourceRevision: 1,
    title: 'Conservado', templateText: 'Mi contenido anterior <tema>.', values: { tema: 'RLS' }, variableNames: ['tema'], compiledText: 'Mi contenido anterior RLS.', sourceStale: true }));
  accounts.prompt = vi.fn(async () => ({ id: idA, libraryId: idA, title: 'Origen actual', template: 'Origen nuevo.', revision: 2 }));
  render(<AccountPromptLibrary library={{ id: idA, ownerId: idA, name: 'Privada', createdAt: '' }} service={accounts} onExpired={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Recuperar borrador Conservado' }));
  await screen.findByRole('dialog', { name: 'Editar prompt privado' });
  expect((screen.getByLabelText('Contenido') as HTMLTextAreaElement).value).toBe('Mi contenido anterior <tema>.');
  expect((screen.getByRole('button', { name: 'Guardar prompt' }) as HTMLButtonElement).disabled).toBe(true);
  expect(accounts.savePrompt).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'Guardar como nuevo' }));
  await screen.findByText('Prompt guardado en tu biblioteca.');
  expect(accounts.savePrompt).toHaveBeenCalledWith(idA, null, { title: 'Conservado', template: 'Mi contenido anterior <tema>.' }, expect.any(AbortSignal));
});

it('requires forgetting confirmation and leaves the saved prompt intact', async () => {
  const accounts = service();
  accounts.prompts = vi.fn(async () => [{ id: idA, libraryId: idA, title: 'Prompt guardado', template: 'Contenido.', revision: 1 }]);
  accounts.rememberedContexts = vi.fn(async () => [{ id: idB, title: 'Conservado', updatedAt: '2026-10-26T00:00:00Z', expiresAt: '2026-11-02T00:00:00Z' }]);
  render(<AccountPromptLibrary library={{ id: idA, ownerId: idA, name: 'Privada', createdAt: '' }} service={accounts} onExpired={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Olvidar borrador Conservado' }));
  await screen.findByRole('dialog', { name: 'Olvidar borrador conservado' });
  expect(accounts.forgetContext).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'Olvidar definitivamente' }));
  await screen.findByText('Contexto olvidado. Los prompts de tu biblioteca se conservan.');
  expect(accounts.forgetContext).toHaveBeenCalledWith(idA, idB, expect.any(AbortSignal));
  expect(screen.queryByRole('button', { name: 'Recuperar borrador Conservado' })).toBeNull();
  expect(screen.getByRole('button', { name: 'Editar Prompt guardado' })).toBeTruthy();
});
