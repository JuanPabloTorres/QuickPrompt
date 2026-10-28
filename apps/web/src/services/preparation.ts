import { z } from 'zod';
import { readServerEvents } from './serverEvents';
import { promptSchema, emptySchema, openApiResponse, post, request, type ApiClient, type Prompt } from './api';
import { semanticSchema } from './semantic';

export const proposalSchema = z.object({ text: z.string(), reasons: z.array(z.string()), changes: z.array(z.string()), warnings: z.array(z.string()), templateId: z.string().uuid().nullable(), baseRevision: z.number(), model: z.string() });
const stateSchema = z.enum(['RECEIVED', 'UNDERSTANDING', 'SEARCHING', 'SELECTING_OR_GENERATING', 'FILLING', 'VALIDATING', 'READY', 'USER_ACTION', 'COMPLETED', 'NEEDS_INFORMATION', 'CANCELLED', 'FAILED', 'BLOCKED']);
export const preparedSchema = z.object({
  id: z.string().uuid(), state: stateSchema, intent: z.enum(['FIND_PROMPT', 'CREATE_PROMPT', 'ADAPT_PROMPT', 'COMPLETE_PROMPT', 'IMPROVE_PROMPT', 'SAVE_PROMPT', 'USE_PROMPT', 'RUN_LOCAL', 'ORGANIZE_PROMPT', 'UNKNOWN']),
  decision: z.enum(['REUSE_EXISTING', 'ADAPT_EXISTING', 'CREATE_NEW', 'ASK_CLARIFICATION']), title: z.string(), description: z.string(), templateText: z.string(), compiledText: z.string().nullable(),
  variables: z.array(z.object({ name: z.string(), required: z.boolean() })), values: z.array(z.object({ name: z.string(), value: z.string(), valueSource: z.string(), validationStatus: z.literal('VALIDATED') })),
  missingVariables: z.array(z.string()), question: z.string(), reason: z.string(), candidates: z.array(z.object({ prompt: promptSchema, score: z.number(), reason: z.string() })),
  sourceId: z.string().uuid().nullable(), sourceRevision: z.number().nullable(), categoryId: z.number(), remembered: z.boolean(), createdAt: z.string(),
  sessionId: z.string().uuid().nullable().optional(), localResponse: z.string().nullable().optional(), providerId: z.string().nullable().optional(),
  semantic: semanticSchema.nullable().optional(),
});
export type PreparedDraft = z.infer<typeof preparedSchema>;
export type PreparationState = z.infer<typeof stateSchema>;
export type NovaState = 'idle' | 'listening' | 'thinking' | 'searching' | 'suggesting' | 'ready' | 'error' | 'offline';

export interface PreparationRequest {
  operationId: string; sessionId: string; message: string; rememberDraft: boolean;
  sourcePromptId?: string; sourceRevision?: number; filter?: { groupId?: string; favorites?: boolean };
}
export interface EditorDraftInput {
  promptId: string | null; revision: number | null; title: string; templateText: string;
  values: Record<string, string>; sessionId: string;
}
export interface PreparationClient {
  propose(input: { intent: 'improve'; context: string; promptId: string; revision: number; model: string }, runId: string | undefined, signal: AbortSignal): Promise<z.infer<typeof proposalSchema>>;
  list(signal?: AbortSignal): Promise<PreparedDraft[]>;
  prepare(input: PreparationRequest, signal: AbortSignal): Promise<Response>;
  editorDraft(input: EditorDraftInput, signal?: AbortSignal): Promise<PreparedDraft>;
  complete(id: string, operationId: string, values: Record<string, string>, signal?: AbortSignal): Promise<PreparedDraft>;
  save(id: string, confirmed: boolean, updateSource: boolean, signal?: AbortSignal): Promise<Prompt>;
  forget(id: string, signal?: AbortSignal): Promise<void>;
  execute(id: string, input: { operationId: string; confirmed: boolean; expectedText: string | null; model: string }, runId: string | null, signal: AbortSignal): Promise<Response>;
}
// Streams stay unread until readPreparation/readLocalStream validate their protocol.
// IDs, confirmation and approval remain explicit; connection substitution cannot
// grant an effect or create a new retry/operation identity.
export function createPreparationClient(client: ApiClient = { request, open: openApiResponse }): PreparationClient {
  return {
    propose: (input, runId, signal) => client.request('/nova/propose', proposalSchema, { ...post(input, signal), headers: runId ? { 'X-Nova-Live-Run': runId } : undefined }),
    list: signal => client.request('/nova/drafts', z.array(preparedSchema), { signal }),
    prepare: (input, signal) => client.open('/nova/prepare', post(input, signal)),
    editorDraft: (input, signal) => client.request('/nova/editor-drafts', preparedSchema, post(input, signal)),
    complete: (id, operationId, values, signal) => client.request(`/nova/drafts/${id}/complete`, preparedSchema, post({ operationId, values }, signal)),
    save: (id, confirmed, updateSource, signal) => client.request(`/nova/drafts/${id}/save`, promptSchema, post({ confirmed, updateSource }, signal)),
    forget: (id, signal) => client.request(`/nova/drafts/${id}`, emptySchema, { method: 'DELETE', signal }),
    execute: (id, input, runId, signal) => client.open(`/nova/drafts/${id}/execute`, { ...post(input, signal), headers: runId ? { 'X-Nova-Live-Run': runId } : {} }),
  };
}
export const preparation = createPreparationClient();

export class PreparationFailure extends Error {
  constructor(message: string, public code: string) { super(message); }
}
export const stageLabels: Record<PreparationState, string> = { RECEIVED: 'Preparando el prompt…', UNDERSTANDING: 'Preparando el prompt…', SEARCHING: 'Buscando en tu biblioteca…', SELECTING_OR_GENERATING: 'Preparando el prompt…', FILLING: 'Preparando el prompt…', VALIDATING: 'Comprobando el resultado…', READY: 'Listo para utilizar', USER_ACTION: 'Esperando tu decisión', COMPLETED: 'Listo para utilizar', NEEDS_INFORMATION: 'Necesito un dato', CANCELLED: 'Detenido', FAILED: 'No se pudo completar', BLOCKED: 'No se pudo completar' };
export async function readPreparation(response: Response, operationId: string, stage: (value: PreparationState) => void, signal: AbortSignal, onResponse?: (text: string) => void): Promise<PreparedDraft> {
  if (!response.ok || !response.body) throw new Error(response.status === 429 ? 'NOVA está ocupada. Espera un momento y reintenta.' : 'No se pudo iniciar la preparación. Tu biblioteca sigue disponible.');
  try {
    for await (const event of readServerEvents(response, signal)) {
      const data: unknown = JSON.parse(event.data);
      if (event.type === 'stage') stage(z.object({ state: stateSchema }).parse(data).state);
      else if (event.type === 'response') onResponse?.(z.object({ text: z.string().max(64000) }).parse(data).text);
      else if (event.type === 'error') { const failure = z.object({ message: z.string(), code: z.string().optional() }).parse(data); throw new PreparationFailure(failure.message, failure.code ?? 'FAILED'); }
      else if (event.type === 'complete') { const result = preparedSchema.parse(data); if (result.id !== operationId) throw new Error('La propuesta pertenece a otra operación.'); return result; }
      else throw new Error('Evento de preparación desconocido.');
    }
    throw new Error('La preparación quedó incompleta. Tu trabajo anterior se conserva.');
  } catch (error) {
    if (signal.aborted) throw new DOMException('Detenido', 'AbortError');
    throw error;
  }
}
