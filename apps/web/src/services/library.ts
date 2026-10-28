import { z } from 'zod';
import * as api from './api';
import type { ApiClient, Prompt, PromptDraft } from './api';

export type LibraryFilter = 'all' | 'favorites' | 'recent' | 'archived';
export type PromptAction = 'favorite' | 'archive' | 'restore' | 'duplicate';
export interface LibraryQuery { search: string; filter: LibraryFilter; groupId: string; page: number }
type Page = z.infer<typeof api.pageSchema>;
type Compiled = z.infer<typeof api.compileSchema>;
type Inspection = z.infer<typeof api.inspectSchema>;
type Identity = Pick<Prompt, 'id' | 'revision'>;
type Values = Record<string, string>;
const versionsSchema = z.array(z.object({ revision: z.number().int().positive(), createdAt: z.string(), snapshot: z.object({ title: z.string(), description: z.string(), template: z.string(), variables: z.record(z.string(), z.string()) }) }));
export const packageSchema = z.object({ format: z.literal('quickprompt-template'), formatVersion: z.literal(1), prompt: z.object({ title: z.string(), template: z.string(), description: z.string(), category: z.union([z.string(), z.number().int()]) }).strict() }).strict();
const renderedSchema = z.object({ text: z.string().nullable(), missingVariables: z.array(z.string()) });
export type PromptVersion = z.infer<typeof versionsSchema>[number];
export type SharedPromptPacket = z.infer<typeof packageSchema>;
const importPreviewSchema = z.object({ newTemplates: z.number(), duplicates: z.number(), conflicts: z.number(), items: z.array(z.object({ title: z.string(), status: z.string() })) });
const restorePreviewSchema = z.object({ preview: importPreviewSchema, expectedSnapshot: z.string() });
const importedSchema = z.object({ imported: z.number() });
const backupSchema = z.object({ fileName: z.string(), integrity: z.literal('ok') });
export type ImportPreview = z.infer<typeof importPreviewSchema>;
export interface LibraryArchiveClient {
  export(signal?: AbortSignal): Promise<unknown>;
  inspect(data: unknown, signal?: AbortSignal): Promise<ImportPreview>;
  inspectRestore(data: unknown, signal?: AbortSignal): Promise<z.infer<typeof restorePreviewSchema>>;
  import(input: { data: unknown; confirmed: boolean }, signal?: AbortSignal): Promise<z.infer<typeof importedSchema>>;
  restore(input: { data: unknown; confirmed: boolean; expectedSnapshot: string }, signal?: AbortSignal): Promise<z.infer<typeof importedSchema>>;
  backup(signal?: AbortSignal): Promise<z.infer<typeof backupSchema>>;
  convertLegacy(source: Blob, signal?: AbortSignal): Promise<unknown>;
}
export interface PromptLibraryClient {
  list(query: LibraryQuery, signal?: AbortSignal): Promise<Page>;
  get(id: string, signal?: AbortSignal): Promise<Prompt>;
  create(draft: PromptDraft, signal?: AbortSignal): Promise<Prompt>;
  update(id: string, draft: PromptDraft, signal?: AbortSignal): Promise<Prompt>;
  act(prompt: Identity, action: PromptAction, signal?: AbortSignal): Promise<Prompt>;
  remove(prompt: Identity, confirmed: boolean, signal?: AbortSignal): Promise<void>;
  compile(prompt: Identity, values: Values, signal?: AbortSignal): Promise<Compiled>;
  recordUse(prompt: Identity, values: Values, operationId: string, expectedText: string, signal?: AbortSignal): Promise<void>;
  versions(id: string, signal?: AbortSignal): Promise<PromptVersion[]>;
  restoreVersion(prompt: Identity, revision: number, confirmed: boolean, signal?: AbortSignal): Promise<Prompt>;
  share(prompt: Identity, signal?: AbortSignal): Promise<SharedPromptPacket>;
}
const toggleSchema = z.object({ templateText: z.string(), variableNames: z.array(z.string()), start: z.number(), end: z.number(), action: z.enum(['created', 'removed']), name: z.string(), originalText: z.string().nullable() });
const renameSchema = z.object({ templateText: z.string(), variableNames: z.array(z.string()), defaults: z.record(z.string(), z.string()), start: z.number(), end: z.number(), previousName: z.string(), name: z.string() });
export interface TemplateClient {
  categories(signal?: AbortSignal): Promise<{ id: number; name: string }[]>;
  inspect(templateText: string, signal?: AbortSignal): Promise<Inspection>;
  compile(templateText: string, values: Values, signal?: AbortSignal): Promise<z.infer<typeof renderedSchema>>;
  toggle(input: { templateText: string; start: number; end: number; originalText?: string }, signal?: AbortSignal): Promise<z.infer<typeof toggleSchema>>;
  rename(input: { templateText: string; start: number; end: number; name: string; defaults: Values }, signal?: AbortSignal): Promise<z.infer<typeof renameSchema>>;
}
export interface CollectionClient {
  list(signal?: AbortSignal): Promise<api.Group[]>;
  memberships(promptId: string, signal?: AbortSignal): Promise<string[]>;
  setMember(prompt: Identity, groupId: string, member: boolean, signal?: AbortSignal): Promise<void>;
  create(name: string, signal?: AbortSignal): Promise<{ id: string }>;
  rename(group: { id: string; revision: number }, name: string, signal?: AbortSignal): Promise<{ id: string }>;
  remove(group: { id: string; revision: number }, confirmed: boolean, signal?: AbortSignal): Promise<void>;
}

// Connection contracts only. Business validation, revision conflicts and transactions
// remain in PromptLibrary/EditorTools; UI state and cancellation remain with callers.
export function createLibraryServices(client: Pick<ApiClient, 'request'> = api): { prompts: PromptLibraryClient; templates: TemplateClient; collections: CollectionClient; archives: LibraryArchiveClient } {
  const prompts: PromptLibraryClient = {
    list({ search, filter, groupId, page }, signal) {
      const query = new URLSearchParams({ search, page: String(page), size: '12', sort: filter === 'recent' ? 'recent' : 'updated' });
      if (filter !== 'all') query.set(filter, 'true');
      if (groupId) query.set('groupId', groupId);
      return client.request(`/prompts?${query}`, api.pageSchema, { signal });
    },
    get: (id, signal) => client.request(`/prompts/${id}`, api.promptSchema, { signal }),
    create: (draft, signal) => client.request('/prompts', api.promptSchema, api.post(draft, signal)),
    update: (id, draft, signal) => client.request(`/prompts/${id}`, api.promptSchema, { method: 'PUT', body: JSON.stringify(draft), signal }),
    act: (prompt, action, signal) => client.request(`/prompts/${prompt.id}/${action}`, api.promptSchema, api.post({ revision: prompt.revision }, signal)),
    remove: (prompt, confirmed, signal) => client.request(`/prompts/${prompt.id}`, api.emptySchema, { method: 'DELETE', body: JSON.stringify({ revision: prompt.revision, confirmed }), signal }),
    compile: (prompt, values, signal) => client.request(`/prompts/${prompt.id}/compile`, api.compileSchema, api.post({ revision: prompt.revision, values }, signal)),
    recordUse: (prompt, values, operationId, expectedText, signal) => client.request(`/prompts/${prompt.id}/usage`, api.emptySchema, api.post({ revision: prompt.revision, values, operationId, expectedText }, signal)),
    versions: (id, signal) => client.request(`/prompts/${id}/versions`, versionsSchema, { signal }),
    restoreVersion: (prompt, revision, confirmed, signal) => client.request(`/prompts/${prompt.id}/versions/${revision}/restore`, api.promptSchema, api.post({ revision: prompt.revision, confirmed }, signal)),
    share: (prompt, signal) => client.request(`/prompts/${prompt.id}/share?revision=${prompt.revision}`, packageSchema, { signal }),
  };
  const templates: TemplateClient = {
    categories: signal => client.request('/categories', z.array(z.object({ id: z.number().int().nonnegative(), name: z.string().min(1) })), { signal }),
    inspect: (templateText, signal) => client.request('/templates/inspect', api.inspectSchema, api.post({ templateText }, signal)),
    compile: (templateText, values, signal) => client.request('/templates/compile', renderedSchema, api.post({ templateText, values }, signal)),
    toggle: (input, signal) => client.request('/templates/toggle-variable', toggleSchema, api.post(input, signal)),
    rename: (input, signal) => client.request('/templates/rename-variable', renameSchema, api.post(input, signal)),
  };
  const collections: CollectionClient = {
    list: signal => client.request('/groups', api.groupsSchema, { signal }),
    memberships: (promptId, signal) => client.request(`/prompts/${promptId}/groups`, z.array(z.string().uuid()), { signal }),
    setMember: (prompt, groupId, member, signal) => client.request(`/prompts/${prompt.id}/groups`, api.emptySchema, api.post({ groupId, revision: prompt.revision, member }, signal)),
    create: (name, signal) => client.request('/groups', z.object({ id: z.string() }), api.post({ name }, signal)),
    rename: (group, name, signal) => client.request(`/groups/${group.id}`, z.object({ id: z.string() }), { method: 'PUT', body: JSON.stringify({ name, revision: group.revision }), signal }),
    remove: (group, confirmed, signal) => client.request(`/groups/${group.id}`, api.emptySchema, { method: 'DELETE', body: JSON.stringify({ revision: group.revision, confirmed }), signal }),
  };
  const archives: LibraryArchiveClient = {
    export: signal => client.request('/library/export', z.unknown(), { signal }),
    inspect: (data, signal) => client.request('/library/import/preview', importPreviewSchema, api.post(data, signal)),
    inspectRestore: (data, signal) => client.request('/library/restore/preview', restorePreviewSchema, api.post(data, signal)),
    import: (input, signal) => client.request('/library/import', importedSchema, api.post(input, signal)),
    restore: (input, signal) => client.request('/library/restore', importedSchema, api.post(input, signal)),
    backup: signal => client.request('/library/backup', backupSchema, api.post({}, signal)),
    convertLegacy: (source, signal) => client.request('/library/legacy/convert', z.unknown(), { method: 'POST', headers: { 'Content-Type': 'application/octet-stream' }, body: source, signal }),
  };
  return { prompts, templates, collections, archives };
}
export const { prompts, templates, collections, archives } = createLibraryServices();
