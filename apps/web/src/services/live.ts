import { z } from 'zod';
import { semanticSchema } from './semantic';
import * as api from './api';
import { ApiError, emptySchema, post, promptSchema, type ApiClient, type Prompt, type PromptDraft } from './api';

export const liveStatuses = ['CREATED', 'INTERPRETING', 'TOOL_PROPOSED', 'TOOL_APPROVED', 'TOOL_RUNNING', 'UI_UPDATED', 'VERIFYING', 'AWAITING_APPROVAL', 'PAUSED', 'COMPLETED', 'FAILED', 'CANCELLED', 'BLOCKED'] as const;
export const liveRecordSchema = z.object({ runId: z.string().uuid(), sessionId: z.string().uuid(), mode: z.enum(['LIVE', 'BROWSER', 'SAME_TAB']), status: z.enum(liveStatuses), sequence: z.number().int(), targetId: z.string().uuid().nullable(), revision: z.number().nullable(), pauseRequested: z.boolean(), browserOpen: z.boolean(), approval: z.string(), effectCommitted: z.boolean(), errorCode: z.string().nullable() });
export const liveEventSchema = z.object({ runId: z.string().uuid(), eventId: z.string().uuid(), sequence: z.number().int().positive(), timestamp: z.string(), stage: z.string(), actor: z.enum(['MODEL_SELECTED', 'RULE_SELECTED', 'USER_SELECTED', 'SYSTEM_VALIDATION']), toolName: z.string().nullable(), targetId: z.string().uuid().nullable(), status: z.enum(liveStatuses), durationMs: z.number().nullable(), errorCode: z.string().nullable(), safeDisplayMessage: z.string(), actionId: z.string().uuid().nullable().optional(), actionSequence: z.number().nullable().optional() });
const draftSchema = z.object({ title: z.string(), templateText: z.string(), description: z.string(), categoryId: z.number(), revision: z.number().optional() });
export const liveRuntimeSchema = z.object({
  version: z.literal('nova-runtime-1'), runId: z.string().uuid(), sequence: z.number().int().nonnegative(),
  objective: z.string(), contextSource: z.string(), contextResolved: z.boolean(), model: z.string(),
  modelState: z.enum(['ready', 'interpreting', 'generating', 'waiting', 'error']), lastInferenceMs: z.number().nullable(), elapsedMs: z.number(), activeTool: z.string().nullable(),
  tools: z.array(z.object({ id: z.string(), label: z.string(), state: z.enum(['available', 'selected', 'running', 'completed', 'blocked', 'paused', 'unavailable']) })),
  memory: z.object({ sourceBound: z.boolean(), draftActive: z.boolean(), resolvedVariables: z.number().int(), totalVariables: z.number().int(), reusesDraft: z.boolean(), constraints: z.array(z.string()) }),
  flow: z.array(z.object({ id: z.string(), label: z.string(), state: z.enum(['pending', 'active', 'completed', 'stopped', 'paused', 'waiting']) })), recoverableOnly: z.boolean(),
});
export type LiveRuntime = z.infer<typeof liveRuntimeSchema>;
export const deletionSchema = z.object({ id: z.string().uuid(), hash: z.string(), promptId: z.string().uuid(), revision: z.number().int().positive(), title: z.string() });
export const liveViewSchema = z.object({ sourceChoices: z.array(z.object({ id: z.string().uuid(), title: z.string(), revision: z.number().int().positive() })).optional(), runtime: liveRuntimeSchema.nullable().optional(), deletion: deletionSchema.nullable().optional(), semantic: semanticSchema.nullable().optional(), run: liveRecordSchema, request: z.string().nullable(), question: z.string().nullable(), observation: z.object({ promptId: z.string().uuid().nullable(), title: z.string().nullable(), revision: z.number().nullable(), templateText: z.string().nullable(), draftChanged: z.boolean(), values: z.record(z.string(), z.string()), response: z.string().nullable(), responseComplete: z.boolean() }).nullable(), proposal: z.object({ id: z.string().uuid(), hash: z.string(), promptId: z.string().uuid().nullable(), revision: z.number().nullable(), draft: draftSchema, originalText: z.string() }).nullable() });
export type LiveView = z.infer<typeof liveViewSchema>;
export interface UiObservation {
  mode: string; candidates: { id: string; title: string; revision: number }[];
  promptId: string | null; revision: number | null; title: string | null; templateText: string | null;
  values: Record<string, string>; variableNames: string[]; draftChanged: boolean;
  compiledReady: boolean; compiledText: string | null; response: string | null;
  responseComplete: boolean; providerOpen: boolean; providerId: string | null; error: string | null;
  deletionPromptId?: string | null; deletionRevision?: number | null; deletionRunId?: string | null; deletionConfirmationReady?: boolean;
  clipboard?: { operationId: string; promptId: string | null; revision: number | null; text: string; copyContent?: 'RENDERED' | 'TEMPLATE' } | null;
  templatePreviewText?: string | null;
}
const actionSchema = z.object({ runId: z.string().uuid(), actionId: z.string().uuid(), sequence: z.number().int().positive(), tool: z.string(), target: z.string().uuid().nullable(), arguments: z.record(z.string(), z.unknown()), timestamp: z.string(), executionStatus: z.string(), verificationStatus: z.string() });
const pollSchema = z.object({ run: liveRecordSchema, action: actionSchema.nullable() });
export type LiveUiAction = z.infer<typeof actionSchema>;
export type LiveUiReceipt = { actionId: string; sequence: number; executionStatus: string; verificationStatus: string; observation: UiObservation | null; errorCode?: string };
export type LiveControl = 'pause' | 'resume' | 'cancel';
type LiveRecord = z.infer<typeof liveRecordSchema>;
type ApprovalInput = { approvalId: string; hash: string; revision: number | null; confirmed: boolean };
type SourceContext = { promptId: string; revision: number; values?: Record<string, string>; allowStaleContext: boolean };
type ObservedPreparation = { preparedId: string; sourceId: string | null; sourceRevision: number | null; renderedText: string };
type StartLiveInput = { runId: string; sessionId: string; message: string; mode: 'SAME_TAB'; sourceId?: string; sourceRevision?: number; values?: Record<string, string>; command?: LiveCommand };
type ObserveOperationInput = { runId: string; sessionId: string; message: string; mode: 'LIVE'; sourceId?: string; sourceRevision?: number; model: string };
export type LiveEvents = Pick<EventSource, 'addEventListener' | 'close' | 'onopen' | 'onerror'>;
export interface LiveClient {
  start(input: StartLiveInput, capability: string, signal: AbortSignal): Promise<LiveRecord>;
  observe(input: ObserveOperationInput, signal?: AbortSignal): Promise<LiveRecord>;
  get(runId: string, signal?: AbortSignal): Promise<LiveView>;
  record(runId: string, signal?: AbortSignal): Promise<{ run: LiveRecord }>;
  control(runId: string, action: LiveControl, signal?: AbortSignal): Promise<LiveRecord>;
  propose(runId: string, draft: PromptDraft, signal?: AbortSignal): Promise<LiveView>;
  approve(runId: string, input: ApprovalInput, signal?: AbortSignal): Promise<LiveRecord>;
  deletion(runId: string, input: ApprovalInput & { revision: number }, signal?: AbortSignal): Promise<LiveRecord>;
  commit(runId: string, draft: PromptDraft, signal?: AbortSignal): Promise<Prompt>;
  observeUi(runId: string, input: ObservedPreparation, signal?: AbortSignal): Promise<void>;
  bindContext(sessionId: string, input: SourceContext, signal?: AbortSignal): Promise<void>;
  bindDraft(sessionId: string, draftId: string, signal?: AbortSignal): Promise<void>;
  interpret(message: string, sessionId: string, signal?: AbortSignal): Promise<z.infer<typeof semanticSchema>>;
  poll(runId: string, capability: string, visible: boolean, signal: AbortSignal): Promise<z.infer<typeof pollSchema>>;
  progress(runId: string, capability: string, input: Pick<LiveUiReceipt, 'actionId' | 'sequence' | 'executionStatus'>, signal: AbortSignal): Promise<void>;
  receipt(runId: string, capability: string, input: LiveUiReceipt, signal?: AbortSignal): Promise<void>;
  events(runId: string, after: number): LiveEvents;
}
// Wire contracts only. Session/task state, DOM execution, receipt retry and approval
// remain in their existing owners. Capabilities are supplied per authenticated run.
export function createLiveClient(client: Pick<ApiClient, 'request'> = api, eventSource: (url: string) => LiveEvents = url => new EventSource(url)): LiveClient {
  const surface = (capability: string) => ({ 'X-Nova-Surface': capability });
  return {
    start: (input, capability, signal) => client.request('/nova/live', liveRecordSchema, { ...post(input, signal), headers: surface(capability) }),
    observe: (input, signal) => client.request('/nova/live/observe', liveRecordSchema, post(input, signal)),
    get: (id, signal) => client.request(`/nova/live/${id}`, liveViewSchema, { signal }),
    record: (id, signal) => client.request(`/nova/live/${id}`, z.object({ run: liveRecordSchema }), { signal }),
    control: (id, action, signal) => client.request(`/nova/live/${id}/${action}`, liveRecordSchema, post({}, signal)),
    propose: (id, draft, signal) => client.request(`/nova/live/${id}/proposal`, liveViewSchema, post(draft, signal)),
    approve: (id, input, signal) => client.request(`/nova/live/${id}/approve`, liveRecordSchema, post(input, signal)),
    deletion: (id, input, signal) => client.request(`/nova/live/${id}/deletion`, liveRecordSchema, post(input, signal)),
    commit: (id, draft, signal) => client.request(`/nova/live/${id}/commit`, promptSchema, post(draft, signal)),
    observeUi: (id, input, signal) => client.request(`/nova/live/${id}/ui-observed`, emptySchema, post(input, signal)),
    bindContext: (id, input, signal) => client.request(`/nova/sessions/${id}/context`, emptySchema, post(input, signal)),
    bindDraft: (id, draftId, signal) => client.request(`/nova/sessions/${id}/draft-context`, emptySchema, post({ draftId }, signal)),
    interpret: (message, sessionId, signal) => client.request('/nova/semantic/interpret', semanticSchema, post({ message, sessionId }, signal)),
    poll: (id, capability, visible, signal) => client.request(`/nova/live/${id}/actions?visible=${visible}`, pollSchema, { headers: surface(capability), signal }),
    progress: (id, capability, input, signal) => client.request(`/nova/live/${id}/actions/progress`, emptySchema, { ...post(input, signal), headers: surface(capability) }),
    receipt: (id, capability, input, signal) => client.request(`/nova/live/${id}/actions/receipt`, emptySchema, { ...post(input, signal), headers: surface(capability) }),
    events: (id, after) => eventSource(`/api/v1/nova/live/${id}/events?after=${after}`),
  };
}
export const live = createLiveClient();

export type LiveEvent = z.infer<typeof liveEventSchema>;
export const terminalLive = (status: string) => ['COMPLETED', 'FAILED', 'CANCELLED', 'BLOCKED'].includes(status);
export const liveLabels: Record<typeof liveStatuses[number], string> = { CREATED: 'Solicitud recibida', INTERPRETING: 'Decidiendo el siguiente paso', TOOL_PROPOSED: 'Acción propuesta', TOOL_APPROVED: 'Acción validada', TOOL_RUNNING: 'Ejecutando', UI_UPDATED: 'Cambio comprobado en la interfaz', VERIFYING: 'Comprobando el resultado', AWAITING_APPROVAL: 'Propuesta lista para revisar', PAUSED: 'En pausa', COMPLETED: 'Listo para utilizar', FAILED: 'No se pudo completar', CANCELLED: 'Ejecución detenida', BLOCKED: 'Necesito tu atención' };
export const actorLabels = { MODEL_SELECTED: 'Decisión de Qwen', RULE_SELECTED: 'Regla de QuickPrompt', USER_SELECTED: 'Decisión tuya', SYSTEM_VALIDATION: 'Verificación del sistema' };
function rememberRunId(id: string) { try { sessionStorage.setItem('nova-live-run', id); } catch { /* The run remains controllable without browser storage. */ } }
function announceAgentStart(id: string) { rememberRunId(id); window.dispatchEvent(new CustomEvent('nova-agent-started', { detail: id })); }
export type LiveCommand = { operation: 'CREATE_DRAFT' | 'PROPOSE_ADAPTATION' | 'OPEN_PROMPT' | 'SEARCH_PROMPT' | 'FILL_VARIABLES' | 'PREPARE_LOCAL_EXECUTION' | 'PREPARE_PROVIDER' | 'EXECUTE_LOCAL' | 'COPY_TO_CLIPBOARD'; content: string; providerId?: string;
  requestedOutcomes?: Array<LiveCommand['operation'] | 'COPY_PROMPT'>; copyContent?: 'RENDERED' | 'TEMPLATE' };
export interface VisibleSessionClient {
  startVisible(message: string, sessionId: string, source?: { id: string; revision: number }, values?: Record<string, string>, options?: { bindSource: boolean; command?: LiveCommand }): Promise<LiveRecord>;
  observeOperation(message: string, sessionId: string, source?: { id: string; revision: number }, signal?: AbortSignal, model?: string): Promise<LiveRecord>;
  surfaceCapability(id: string): string | undefined;
  releaseSurface(id: string): boolean;
}
export function createVisibleSessionClient(client: Pick<LiveClient, 'bindContext' | 'start' | 'record' | 'observe'> = live): VisibleSessionClient {
  const surfaces = new Map<string, string>();
  async function startVisible(message: string, sessionId: string, source?: { id: string; revision: number }, values?: Record<string, string>, options?: { bindSource: boolean; command?: LiveCommand }) {
  if (source) await client.bindContext(sessionId, { promptId: source.id, revision: source.revision, values, allowStaleContext: true });
  const runId = crypto.randomUUID();
  const capability = crypto.randomUUID() + crypto.randomUUID(); surfaces.set(runId, capability);
  try {
    const run = await client.start({ runId, sessionId, message, mode: 'SAME_TAB', ...(options?.bindSource && source ? { sourceId: source.id, sourceRevision: source.revision, values } : {}), ...(options?.command ? { command: options.command } : {}) }, capability, AbortSignal.timeout(10000)); announceAgentStart(runId); return run;
  } catch (error) {
    if (error instanceof ApiError && error.status < 500) { surfaces.delete(runId); throw error; }
    // An uncertain POST may already have created the run. Recover its receipt, never start another one.
    try { const recovered = await client.record(runId, AbortSignal.timeout(3000)); if (recovered.run.mode === 'SAME_TAB') { announceAgentStart(runId); return recovered.run; } }
    catch (recoveryError) {
      // The POST has completed with an HTTP error and the authoritative receipt
      // confirms no run. Do not publish a phantom identity to UI/SSE polling.
      // A lost POST response remains uncertain even if an immediate GET is 404:
      // that request may still be processing, so its identity must be retained.
      if (error instanceof ApiError && recoveryError instanceof ApiError && recoveryError.status === 404) {
        surfaces.delete(runId); throw error;
      }
    }
    rememberRunId(runId); window.dispatchEvent(new CustomEvent('nova-uncertain-start', { detail: runId }));
    throw new Error('La conexión no confirmó el inicio. NOVA conservará esta identidad para revisar o detener la ejecución sin repetirla.');
  }
}
  async function observeOperation(message: string, sessionId: string, source?: { id: string; revision: number }, signal?: AbortSignal, model = 'qwen3:8b') {
  const runId = crypto.randomUUID(); rememberRunId(runId);
  return client.observe({ runId, sessionId, message, mode: 'LIVE', sourceId: source?.id, sourceRevision: source?.revision, model }, signal);
}

  return { startVisible, observeOperation, surfaceCapability: id => surfaces.get(id), releaseSurface: id => surfaces.delete(id) };
}
const visibleSession = createVisibleSessionClient();
// Compatibility exports delegate to the same owner as standalone callers.
export const startVisible: VisibleSessionClient['startVisible'] = (...args) => visibleSession.startVisible(...args);
export const observeOperation: VisibleSessionClient['observeOperation'] = (...args) => visibleSession.observeOperation(...args);
export const surfaceCapability: VisibleSessionClient['surfaceCapability'] = id => visibleSession.surfaceCapability(id);
export const releaseSurface: VisibleSessionClient['releaseSurface'] = id => visibleSession.releaseSurface(id);
