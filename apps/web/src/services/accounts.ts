import { z } from 'zod';
import { createAccountNovaServices } from './accountNova';
import { ApiError, createApiClient, emptySchema, groupsSchema, post, type ApiClient } from './api';
import { packageSchema, type CollectionClient } from './library';
import { createExecutionWatcher } from './accountExecutionStream';
import { preferencesSchema, type PreferenceClient } from './preferences';

const userSchema = z.object({ id: z.string().uuid(), email: z.string().nullable() });
const accountVersionSchema = z.object({ libraryId: z.string().uuid(), promptId: z.string().uuid(), revision: z.number().int().positive(), title: z.string(), template: z.string(), createdAt: z.string() });
export type AccountPromptVersion = z.infer<typeof accountVersionSchema>;
const optionsSchema = z.object({ email: z.boolean(), google: z.boolean(), delegation: z.boolean().optional().default(false), mailDelivery: z.string() });
const connectionSchema = z.object({ id: z.string().uuid(), label: z.string(), profileId: z.string(), createdAt: z.string(), expiresAt: z.string(), revoked: z.boolean(), active: z.boolean(), libraryId: z.string().uuid().nullable().optional(), scopes: z.array(z.string()).nullable().optional() });
const deviceReviewSchema = z.object({ label: z.string(), profileId: z.string(), model: z.string(), permissions: z.array(z.string()), expiresInMinutes: z.number() });
export type AccountConnection = z.infer<typeof connectionSchema>;
export type DeviceReview = z.infer<typeof deviceReviewSchema>;
const librarySchema = z.object({ id: z.string().uuid(), name: z.string(), ownerId: z.string().uuid(), createdAt: z.string() });
const noticeSchema = z.object({ message: z.string() });
const accountPromptSchema = z.object({ id: z.string().uuid(), libraryId: z.string().uuid(), title: z.string(), template: z.string(), revision: z.number().int().positive(),
  description: z.string().optional(), categoryId: z.number().int().optional(), isFavorite: z.boolean().optional(), isArchived: z.boolean().optional(),
  uses: z.number().int().nonnegative().optional(), lastUsedAt: z.string().nullable().optional(), createdAt: z.string().nullable().optional(), updatedAt: z.string().nullable().optional(), variableNames: z.array(z.string()).optional() });
const catalogSchema = z.object({ items: z.array(accountPromptSchema), total: z.number().int().nonnegative(), page: z.number().int().nonnegative(), size: z.number().int().positive() });
export type AccountCatalogQuery = { search: string; filter: 'all' | 'favorites' | 'recent' | 'archived'; collectionId?: string; ungrouped?: boolean; page: number; size: number; sort: 'updated' | 'title' | 'recent' };
export type AccountPromptAction = 'favorite' | 'archive' | 'restore' | 'duplicate';
const publicationSchema = z.object({ id: z.string().uuid(), title: z.string(), template: z.string(), sourceRevision: z.number().int().positive(), createdAt: z.string() });
const ownedPublicationSchema = publicationSchema.omit({ template: true }).extend({ libraryId: z.string().uuid(), promptId: z.string().uuid(), revokedAt: z.string().nullable() });
const collectionPublicationSchema = publicationSchema.omit({ template: true }).extend({ items: z.array(z.object({ title: z.string(), template: z.string(), sourceRevision: z.number().int().positive() })).min(1).max(200) });
const ownedCollectionPublicationSchema = ownedPublicationSchema.omit({ promptId: true }).extend({ collectionId: z.string().uuid() });
const importedCollectionSchema = z.object({ id: z.string().uuid(), libraryId: z.string().uuid(), name: z.string(), revision: z.number().int().positive(), prompts: z.array(accountPromptSchema).min(1).max(200) });
const invitationSchema = z.object({ id: z.string().uuid(), publicationId: z.string().uuid(), title: z.string(), state: z.number().int().min(0).max(3), sentByMe: z.boolean(), recipientEmail: z.string().nullable(), available: z.boolean(), isCollection: z.boolean().optional() });
const rememberedContextSchema = z.object({ id: z.string().uuid(), title: z.string(), updatedAt: z.string(), expiresAt: z.string() });
const editorInspectionSchema = z.object({ variableNames: z.array(z.string()), compiledText: z.string().nullable(), missingVariables: z.array(z.string()) });
const renderingSchema = z.object({ promptId: z.string().uuid(), templateRevision: z.number().int().positive(), text: z.string().nullable(), missingVariables: z.array(z.string()) });
const executionSchema = z.object({ id: z.string().uuid(), libraryId: z.string().uuid(), kind: z.enum(['CompilePrompt', 'RunLocalPrompt']),
  status: z.enum(['Queued', 'Running', 'Completed', 'Failed', 'Cancelled', 'Interrupted']), cancelRequested: z.boolean(),
  createdAt: z.string(), startedAt: z.string().nullable(), finishedAt: z.string().nullable(), errorCode: z.string().nullable(), forgottenAt: z.string().nullable().optional() });
const preparedUsageSchema = z.object({ values: z.record(z.string().max(80), z.string().max(8000)).refine(values => Object.keys(values).length <= 60), operationId: z.string().uuid() });
const executionResultSchema = z.object({ executionId: z.string().uuid(), promptId: z.string().uuid(), promptRevision: z.number().int().positive(), title: z.string(), text: z.string(), createdAt: z.string(),
  response: z.string().min(1).max(64000).nullable().optional(), model: z.literal('qwen3:8b').nullable().optional(), usage: preparedUsageSchema.nullable().optional() })
  .refine(value => (value.response == null) === (value.model == null), 'La respuesta y su modelo deben corresponder.')
  .refine(value => value.usage == null || value.usage.operationId === value.executionId, 'El uso no corresponde a esta preparación.');
const savedEditorContextSchema = z.object({ id: z.string().uuid(), draftId: z.string().uuid(), draftRevision: z.number().int().positive(), memoryRevision: z.number().int().positive(),
  sourceId: z.string().uuid().nullable(), sourceRevision: z.number().int().nullable(), title: z.string(), templateText: z.string(),
  values: z.record(z.string(), z.string()), variableNames: z.array(z.string()), compiledText: z.string().nullable(), sourceStale: z.boolean() });
export type AccountUser = z.infer<typeof userSchema>;
export type AccountOptions = z.infer<typeof optionsSchema>;
export type AccountLibrary = z.infer<typeof librarySchema>;
export type AccountFormMode = 'login' | 'register' | 'recover' | 'confirm' | 'reset';
export type AccountToken = { userId: string; code: string };
export type AccountPrompt = z.infer<typeof accountPromptSchema>;
export type AccountPublication = z.infer<typeof ownedPublicationSchema>;
export type SharedPrompt = z.infer<typeof publicationSchema>;
export type SharedCollection = z.infer<typeof collectionPublicationSchema>;
export type AccountInvitation = z.infer<typeof invitationSchema>;
export type AccountPromptDraft = Pick<AccountPrompt, 'title' | 'template'>;
export type RememberedContext = z.infer<typeof rememberedContextSchema>;
export type AccountExecution = z.infer<typeof executionSchema>;
export type AccountExecutionResult = z.infer<typeof executionResultSchema>;
export type SavedEditorContext = z.infer<typeof savedEditorContextSchema>;
export type AccountEditorInspection = z.infer<typeof editorInspectionSchema>;
export type InspectAccountEditor = { promptId: string | null; revision: number | null; templateText: string; values: Record<string, string> };
export type RememberEditorInput = { sessionId: string; expectedDraftRevision: number; expectedDraftId: string | null; expectedMemoryRevision: number; confirmed: true;
  promptId: string | null; revision: number | null; title: string; templateText: string; values: Record<string, string> };
const sharingPath = '/api/accounts/v1/sharing';

/** Cookie/CSRF protocol belongs to this capability, never to a page or an LLM command. */
export function createAccountService(client: ApiClient = createApiClient(undefined, '')) {
  const csrf = async (signal?: AbortSignal) => (await client.request('/account/csrf', z.object({ token: z.string() }), { signal, credentials: 'same-origin' })).token;
  async function mutate<T>(path: string, schema: z.ZodType<T>, body: unknown, signal?: AbortSignal, method = 'POST') {
    const token = await csrf(signal);
    return client.request(path, schema, { ...post(body, signal), method, credentials: 'same-origin', headers: { 'X-QP-CSRF': token } });
  }
  async function verification(code: string, action: string, signal?: AbortSignal): Promise<RequestInit> {
    return { method: 'POST', credentials: 'same-origin', signal,
      body: new URLSearchParams({ user_code: code.trim(), action }),
      headers: { 'X-QP-CSRF': await csrf(signal), 'Content-Type': 'application/x-www-form-urlencoded' } };
  }
  return {
    preferences: {
      get: signal => client.request('/account/preferences/', preferencesSchema, { signal }),
      save: (preferences, confirmed, signal) => mutate('/account/preferences/', preferencesSchema, { preferences, confirmed }, signal),
      forget: signal => mutate('/account/preferences/', emptySchema, {}, signal, 'DELETE'),
    } satisfies PreferenceClient,
    nova: (accountId: string, libraryId: string, connectionId: string, onExpired?: () => void) => createAccountNovaServices(client, csrf, { accountId, libraryId, connectionId }, undefined, onExpired),
    csrf,
    connections: (signal?: AbortSignal) => client.request('/account/connections/', z.array(connectionSchema), { signal }),
    reviewDevice: async (code: string, signal?: AbortSignal) => client.request('/connect/verify', deviceReviewSchema, await verification(code, 'review', signal)),
    async decideDevice(code: string, action: 'approve' | 'deny', signal?: AbortSignal) {
      const response = await client.open('/connect/verify', await verification(code, action, signal));
      if (response.ok) return;
      // SDK verification acknowledgments are local text responses. Only the explicit
      // access_denied outcome acknowledges a denial; expired/invalid codes remain errors.
      if (action === 'deny' && response.status === 400 && /^error:access_denied\r?$/m.test(await response.text())) return;
      throw new ApiError('No se pudo confirmar la decisión. Revisa de nuevo el código y el estado de las conexiones.', response.status);
    },
    revokeConnection: (id: string, signal?: AbortSignal) => mutate(`/account/connections/${encodeURIComponent(id)}/revoke`, emptySchema, {}, signal),
    options: (signal?: AbortSignal) => client.request('/account/options', optionsSchema, { signal }),
    async current(signal?: AbortSignal): Promise<AccountUser | null> {
      try { return await client.request('/account/me', userSchema, { signal, credentials: 'same-origin' }); }
      catch (error) { if (error instanceof ApiError && error.status === 401) return null; throw error; }
    },
    login: (email: string, password: string, signal?: AbortSignal) => mutate('/account/login', emptySchema, { email, password }, signal),
    register: (email: string, password: string, signal?: AbortSignal) => mutate('/account/register', noticeSchema, { email, password }, signal),
    recover: (email: string, signal?: AbortSignal) => mutate('/account/recover', noticeSchema, { email }, signal),
    confirm: (token: AccountToken, signal?: AbortSignal) => mutate('/account/confirm', emptySchema, token, signal),
    reset: (token: AccountToken, password: string, signal?: AbortSignal) => mutate('/account/reset', emptySchema, { ...token, password }, signal),
    logout: (signal?: AbortSignal) => mutate('/account/logout', emptySchema, {}, signal),
    libraries: (signal?: AbortSignal) => client.request('/api/accounts/v1/libraries/', z.array(librarySchema), { signal }),
    createLibrary: (name: string, signal?: AbortSignal) => mutate('/api/accounts/v1/libraries/', librarySchema, { name }, signal),
    collections(libraryId: string): CollectionClient {
      const root = `/api/accounts/v1/libraries/${encodeURIComponent(libraryId)}`;
      const identity = z.object({ id: z.string().uuid() });
      return {
        list: signal => client.request(`${root}/groups`, groupsSchema, { signal }),
        memberships: (promptId, signal) => client.request(`${root}/prompts/${encodeURIComponent(promptId)}/groups`, z.array(z.string().uuid()), { signal }),
        setMember: (prompt, groupId, member, signal) => mutate(`${root}/prompts/${encodeURIComponent(prompt.id)}/groups`, emptySchema, { groupId, revision: prompt.revision, member }, signal),
        create: (name, signal) => mutate(`${root}/groups`, identity, { name, revision: 0 }, signal),
        rename: (group, name, signal) => mutate(`${root}/groups/${encodeURIComponent(group.id)}`, identity, { name, revision: group.revision }, signal, 'PUT'),
        remove: (group, confirmed, signal) => mutate(`${root}/groups/${encodeURIComponent(group.id)}`, emptySchema, { confirmed, revision: group.revision }, signal, 'DELETE'),
      };
    },
    async catalog(libraryId: string, input: AccountCatalogQuery, signal?: AbortSignal) {
      const query = new URLSearchParams({ search: input.search, filter: input.filter, page: String(input.page), size: String(input.size), sort: input.sort });
      if (input.collectionId) query.set('collectionId', input.collectionId);
      if (input.ungrouped) query.set('ungrouped', 'true');
      const result = await client.request(`/api/accounts/v1/libraries/${encodeURIComponent(libraryId)}/prompts/catalog?${query}`, catalogSchema, { signal });
      if (result.items.some(prompt => prompt.libraryId !== libraryId) || new Set(result.items.map(prompt => prompt.id)).size !== result.items.length
        || result.items.length > input.size || result.page !== input.page || result.size !== input.size)
        throw new Error('El catálogo no corresponde a esta biblioteca y página.');
      return result;
    },
    async actPrompt(prompt: AccountPrompt, action: AccountPromptAction, signal?: AbortSignal) {
      const result = await mutate(`/api/accounts/v1/libraries/${encodeURIComponent(prompt.libraryId)}/prompts/${encodeURIComponent(prompt.id)}/${action}`,
        accountPromptSchema, { revision: prompt.revision }, signal);
      if (result.libraryId !== prompt.libraryId || (action === 'duplicate' ? result.id === prompt.id : result.id !== prompt.id))
        throw new Error('La respuesta no corresponde al prompt solicitado.');
      return result;
    },
    deletePrompt: (prompt: AccountPrompt, confirmed: boolean, signal?: AbortSignal) => mutate(`/api/accounts/v1/libraries/${encodeURIComponent(prompt.libraryId)}/prompts/${encodeURIComponent(prompt.id)}`,
      emptySchema, { revision: prompt.revision, confirmed }, signal, 'DELETE'),
    prompts: (libraryId: string, signal?: AbortSignal, filter?: { collectionId?: string; ungrouped?: boolean }) => {
      const query = new URLSearchParams(); if (filter?.collectionId) query.set('collectionId', filter.collectionId); if (filter?.ungrouped) query.set('ungrouped', 'true');
      return client.request(`/api/accounts/v1/libraries/${encodeURIComponent(libraryId)}/prompts${query.size ? `?${query}` : ''}`, z.array(accountPromptSchema), { signal });
    },
    prompt: (libraryId: string, id: string, signal?: AbortSignal) => client.request(`/api/accounts/v1/libraries/${encodeURIComponent(libraryId)}/prompts/${encodeURIComponent(id)}`, accountPromptSchema, { signal }),
    versions: (prompt: AccountPrompt, signal?: AbortSignal) => client.request(`/api/accounts/v1/libraries/${encodeURIComponent(prompt.libraryId)}/prompts/${encodeURIComponent(prompt.id)}/versions`, z.array(accountVersionSchema), { signal }),
    exportPrompt: (prompt: AccountPrompt, signal?: AbortSignal) => client.request(`/api/accounts/v1/libraries/${encodeURIComponent(prompt.libraryId)}/prompts/${encodeURIComponent(prompt.id)}/share?revision=${prompt.revision}`, packageSchema, { signal }),
    renderPrompt: (prompt: AccountPrompt, values: Record<string, string>, signal?: AbortSignal) => mutate(`/api/accounts/v1/libraries/${encodeURIComponent(prompt.libraryId)}/prompts/${encodeURIComponent(prompt.id)}/render`, renderingSchema, { revision: prompt.revision, values }, signal),
    compilePrompt: (prompt: AccountPrompt, values: Record<string, string>, operationId: string, signal?: AbortSignal) => mutate(`/api/accounts/v1/libraries/${encodeURIComponent(prompt.libraryId)}/prompts/${encodeURIComponent(prompt.id)}/compile`, executionSchema, { operationId, revision: prompt.revision, values }, signal),
    runLocalPrompt: (prompt: AccountPrompt, values: Record<string, string>, operationId: string, connectionId: string, signal?: AbortSignal) => mutate(`/api/accounts/v1/libraries/${encodeURIComponent(prompt.libraryId)}/prompts/${encodeURIComponent(prompt.id)}/run-local`, executionSchema, { operationId, connectionId, revision: prompt.revision, values }, signal),
    executions: (libraryId: string, signal?: AbortSignal) => client.request(`/api/accounts/v1/libraries/${encodeURIComponent(libraryId)}/executions`, z.array(executionSchema), { signal }),
    watchExecutions: createExecutionWatcher(client, z.array(executionSchema)),
    executionResult: (libraryId: string, id: string, signal?: AbortSignal) => client.request(`/api/accounts/v1/libraries/${encodeURIComponent(libraryId)}/executions/${encodeURIComponent(id)}/result`, executionResultSchema, { signal }),
    recordPromptUse(libraryId: string, result: AccountExecutionResult, signal?: AbortSignal) {
      if (!result.usage || result.usage.operationId !== result.executionId || result.text.length > 64000)
        throw new Error('Esta preparación no contiene un snapshot de uso verificable.');
      return mutate(`/api/accounts/v1/libraries/${encodeURIComponent(libraryId)}/prompts/${encodeURIComponent(result.promptId)}/use`, emptySchema,
        { revision: result.promptRevision, values: result.usage.values, operationId: result.usage.operationId, expectedText: result.text }, signal);
    },
    cancelExecution: (libraryId: string, id: string, signal?: AbortSignal) => mutate(`/api/accounts/v1/libraries/${encodeURIComponent(libraryId)}/executions/${encodeURIComponent(id)}/cancel`, executionSchema, {}, signal),
    forgetExecution: (libraryId: string, id: string, signal?: AbortSignal) => mutate(`/api/accounts/v1/libraries/${encodeURIComponent(libraryId)}/executions/${encodeURIComponent(id)}/content`, executionSchema, {}, signal, 'DELETE'),
    savePrompt: (libraryId: string, source: AccountPrompt | null, draft: AccountPromptDraft, signal?: AbortSignal) =>
      mutate(`/api/accounts/v1/libraries/${encodeURIComponent(libraryId)}/prompts${source ? '/' + encodeURIComponent(source.id) : ''}`, accountPromptSchema,
        { ...draft, revision: source?.revision ?? 0 }, signal, source ? 'PUT' : 'POST'),
    publications: (libraryId: string, promptId: string, signal?: AbortSignal) => client.request(`${sharingPath}/publications?${new URLSearchParams({ libraryId, promptId })}`, z.array(ownedPublicationSchema), { signal }),
    collectionPublications: (libraryId: string, collectionId: string, signal?: AbortSignal) => client.request(`${sharingPath}/collections?${new URLSearchParams({ libraryId, collectionId })}`, z.array(ownedCollectionPublicationSchema), { signal }),
    publishCollection: (libraryId: string, collectionId: string, revision: number, promptRevisions: Record<string, number>, signal?: AbortSignal) => mutate(`${sharingPath}/collections`, collectionPublicationSchema, { libraryId, collectionId, revision, promptRevisions }, signal),
    readCollectionPublication: (id: string, signal?: AbortSignal) => client.request(`${sharingPath}/collections/${encodeURIComponent(id)}`, collectionPublicationSchema, { signal }),
    importCollectionPublication: (id: string, libraryId: string, operationId: string, signal?: AbortSignal) => mutate(`${sharingPath}/collections/${encodeURIComponent(id)}/import`, importedCollectionSchema, { libraryId, operationId }, signal),
    publish: (prompt: AccountPrompt, signal?: AbortSignal) => mutate(`${sharingPath}/publications`, publicationSchema,
      { libraryId: prompt.libraryId, promptId: prompt.id, revision: prompt.revision }, signal),
    invite: (publicationId: string, email: string, signal?: AbortSignal) => mutate(`${sharingPath}/publications/${encodeURIComponent(publicationId)}/invite`, noticeSchema, { email }, signal),
    revokePublication: (publicationId: string, signal?: AbortSignal) => mutate(`${sharingPath}/publications/${encodeURIComponent(publicationId)}/revoke`, emptySchema, {}, signal),
    invitations: (signal?: AbortSignal) => client.request(`${sharingPath}/invitations`, z.array(invitationSchema), { signal }),
    decideInvitation: (id: string, state: 1 | 2 | 3, signal?: AbortSignal) => mutate(`${sharingPath}/invitations/${encodeURIComponent(id)}/decision`, emptySchema, { state }, signal),
    readPublication: (id: string, signal?: AbortSignal) => client.request(`${sharingPath}/publications/${encodeURIComponent(id)}`, publicationSchema, { signal }),
    importPublication: (id: string, libraryId: string, operationId: string, signal?: AbortSignal) => mutate(`${sharingPath}/publications/${encodeURIComponent(id)}/import`, accountPromptSchema, { libraryId, operationId }, signal),
    rememberedContexts: (libraryId: string, signal?: AbortSignal) => client.request(`/api/accounts/v1/libraries/${encodeURIComponent(libraryId)}/memory/sessions`, z.array(rememberedContextSchema), { signal }),
    readContext: (libraryId: string, id: string, signal?: AbortSignal) => client.request(`/api/accounts/v1/libraries/${encodeURIComponent(libraryId)}/memory/sessions/${encodeURIComponent(id)}`, savedEditorContextSchema, { signal }),
    rememberEditor: (libraryId: string, input: RememberEditorInput, signal?: AbortSignal) => mutate(`/api/accounts/v1/libraries/${encodeURIComponent(libraryId)}/memory/editor`, savedEditorContextSchema, input, signal),
    inspectEditor: (libraryId: string, input: InspectAccountEditor, signal?: AbortSignal) => mutate(`/api/accounts/v1/libraries/${encodeURIComponent(libraryId)}/memory/editor/inspect`, editorInspectionSchema, input, signal),
    forgetContext: (libraryId: string, id: string, signal?: AbortSignal) => mutate(`/api/accounts/v1/libraries/${encodeURIComponent(libraryId)}/memory/sessions/${encodeURIComponent(id)}`, emptySchema, {}, signal, 'DELETE'),
  };
}
export type AccountService = ReturnType<typeof createAccountService>;
