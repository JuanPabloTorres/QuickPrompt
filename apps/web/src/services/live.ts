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
const reviewSpanSchema = z.object({ start: z.number().int().nonnegative(), length: z.number().int().nonnegative(), text: z.string() });
export const contentReviewSchema = z.object({
  userClarifications: z.array(z.object({ question: z.string().min(1).max(500), answer: z.string().min(1).max(2000) })).max(4).optional(),
  status: z.literal('REQUIRES_HUMAN_CONTENT_REVIEW'), contractHash: z.string(), bodyHash: z.string(), originalRequest: z.string(),
  source: z.object({ promptId: z.string().uuid().nullable(), revision: z.number().int().positive().nullable(), bodyHash: z.string().nullable(), originalText: z.string().nullable(),
    sourceTitle: z.string().nullable().optional(), sourceDescription: z.string().nullable().optional(), sourceCategoryId: z.number().int().nullable().optional(),
    savedBodyHash: z.string().nullable().optional(), savedOriginalText: z.string().nullable().optional(), workingDraftId: z.string().uuid().nullable().optional(), workingBodyHash: z.string().nullable().optional() }),
  ledger: z.array(reviewSpanSchema.extend({ id: z.number().int(), source: z.enum(['REQUEST', 'SOURCE']),
    role: z.enum(['CONTEXT', 'INPUT_DATA']), replacementText: z.null(), changeAuthority: z.array(reviewSpanSchema) })),
  literalChanges: z.object({ entries: z.array(z.object({ id: z.string().min(1), kind: z.enum(['MISSING_SOURCE_CLAUSE', 'REMOVED_SOURCE_URI', 'ADDED_URI']),
    originalText: z.string().nullable(), proposedText: z.string().nullable() })).refine(entries => new Set(entries.map(entry => entry.id)).size === entries.length, 'Los cambios deben tener identidades únicas.') }),
});
export type ContentReview = z.infer<typeof contentReviewSchema>;
export const liveViewSchema = z.object({ sourceChoices: z.array(z.object({ id: z.string().uuid(), title: z.string(), revision: z.number().int().positive() })).optional(), runtime: liveRuntimeSchema.nullable().optional(), deletion: deletionSchema.nullable().optional(), semantic: semanticSchema.nullable().optional(), run: liveRecordSchema, request: z.string().nullable(), question: z.string().nullable(), observation: z.object({ promptId: z.string().uuid().nullable(), title: z.string().nullable(), revision: z.number().nullable(), templateText: z.string().nullable(), draftChanged: z.boolean(), values: z.record(z.string(), z.string()), response: z.string().nullable(), responseComplete: z.boolean() }).nullable(), proposal: z.object({ id: z.string().uuid(), hash: z.string(), promptId: z.string().uuid().nullable(), revision: z.number().nullable(), draft: draftSchema, originalText: z.string(), purpose: z.enum(['SAVE', 'CONTENT_ONLY']).default('SAVE'), contentReview: contentReviewSchema.nullable().optional() }).nullable() });
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
type ApprovalInput = { approvalId: string; hash: string; revision: number | null; confirmed: boolean; contentReviewed?: boolean; contentReviewHash?: string | null; approvedLiteralChangeIds?: string[] };
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
function rememberRunId(id: string, storageKey = 'nova-live-run') { try { sessionStorage.setItem(storageKey, id); } catch { /* The run remains controllable without browser storage. */ } }
function announceAgentStart(id: string, storageKey = 'nova-live-run') { rememberRunId(id, storageKey); window.dispatchEvent(new CustomEvent('nova-agent-started', { detail: id })); }
export type LiveCommand = { operation: 'CREATE_DRAFT' | 'PROPOSE_ADAPTATION' | 'OPEN_PROMPT' | 'SEARCH_PROMPT' | 'FILL_VARIABLES' | 'PREPARE_LOCAL_EXECUTION' | 'PREPARE_PROVIDER' | 'EXECUTE_LOCAL' | 'COPY_TO_CLIPBOARD'; content: string; providerId?: string;
  requestedOutcomes?: Array<LiveCommand['operation'] | 'COPY_PROMPT'>; copyContent?: 'RENDERED' | 'TEMPLATE'; inputNames?: string[] };
export interface VisibleSessionClient {
  startVisible(message: string, sessionId: string, source?: { id: string; revision: number }, values?: Record<string, string>, options?: { bindSource: boolean; command?: LiveCommand }): Promise<LiveRecord>;
  observeOperation(message: string, sessionId: string, source?: { id: string; revision: number }, signal?: AbortSignal, model?: string): Promise<LiveRecord>;
  surfaceCapability(id: string): string | undefined;
  releaseSurface(id: string): boolean;
  clarifyVisible?: (previousId: string, questionSequence: number, answer: string) => Promise<LiveRecord>;
  clarificationCapability?: (id: string) => string | undefined;
  finishSurface?: (id: string, allowClarification: boolean) => void;
  pendingClarification?: (id: string) => Readonly<{ runId: string; previousId: string; questionSequence: number; answer: string }> | undefined;
  retryClarification?: (id: string) => Promise<LiveRecord>;
}
export type VisibleClarificationRequest = (previousId: string, input: { runId: string; questionSequence: number; answer: string; nextSurface: string }, signal: AbortSignal) => Promise<LiveRecord>;
export function createVisibleSessionClient(client: Pick<LiveClient, 'bindContext' | 'start' | 'record' | 'observe'> = live, storageKey = 'nova-live-run', clarificationRequest?: VisibleClarificationRequest): VisibleSessionClient {
  const surfaces = new Map<string, string>();
  const questionSurfaces = new Map<string, string>();
  type PendingClarification = Readonly<{ previousId: string; runId: string; questionSequence: number; answer: string; nextSurface: string }>;
  const pendingClarifications = new Map<string, PendingClarification>();
  const clarificationFlights = new Map<string, Promise<LiveRecord>>();
  function retainSurface(id: string, capability: string, previousId?: string) {
    if (clarificationRequest) {
      while (surfaces.size + questionSurfaces.size >= 3) {
        const retired = [...questionSurfaces.keys()].find(key => key !== previousId && ![...pendingClarifications.values()].some(item => item.previousId === key));
        if (!retired) throw new Error('Esta pestaña ya conserva tres controles pendientes. Detén una ejecución antes de iniciar otra.');
        questionSurfaces.delete(retired);
      }
    }
    surfaces.set(id, capability);
  }
  async function startVisible(message: string, sessionId: string, source?: { id: string; revision: number }, values?: Record<string, string>, options?: { bindSource: boolean; command?: LiveCommand }) {
    if (source) await client.bindContext(sessionId, { promptId: source.id, revision: source.revision, values, allowStaleContext: true });
    const runId = crypto.randomUUID();
    const capability = crypto.randomUUID() + crypto.randomUUID(); retainSurface(runId, capability);
    return admit(runId, () => client.start({ runId, sessionId, message, mode: 'SAME_TAB', ...(options?.bindSource && source ? { sourceId: source.id, sourceRevision: source.revision, values } : {}), ...(options?.command ? { command: options.command } : {}) }, capability, AbortSignal.timeout(10000)));
  }
  async function admit(runId: string, send: () => Promise<LiveRecord>, keepClientError = false) {
    try {
      const run = await send();
      if (run.runId !== runId || run.mode !== 'SAME_TAB') throw new Error('La respuesta no corresponde a esta ejecución visible.');
      announceAgentStart(runId, storageKey); return run;
    } catch (error) {
      if (error instanceof ApiError && error.status < 500) { if (!keepClientError) surfaces.delete(runId); throw error; }
      // An uncertain POST may already have created the run. Recover its receipt, never start another one.
      try { const recovered = await client.record(runId, AbortSignal.timeout(3000)); if (recovered.run.runId === runId && recovered.run.mode === 'SAME_TAB') { announceAgentStart(runId, storageKey); return recovered.run; } }
      catch (recoveryError) {
        // The POST has completed with an HTTP error and the authoritative receipt
        // confirms no run. Do not publish a phantom identity to UI/SSE polling.
        // A lost POST response remains uncertain even if an immediate GET is 404:
        // that request may still be processing, so its identity must be retained.
        if (error instanceof ApiError && recoveryError instanceof ApiError && recoveryError.status === 404) {
          surfaces.delete(runId); throw error;
        }
      }
      rememberRunId(runId, storageKey); window.dispatchEvent(new CustomEvent('nova-uncertain-start', { detail: runId }));
      throw new Error('La conexión no confirmó el inicio. NOVA conservará esta identidad para revisar o detener la ejecución sin repetirla.');
    }
  }
  async function clarifyVisible(previousId: string, questionSequence: number, answer: string) {
    if (!clarificationRequest || !(questionSurfaces.has(previousId) || surfaces.has(previousId))) throw new Error('Esta pestaña no conserva permiso para responder a esa pregunta.');
    const pending = [...pendingClarifications.values()].find(item => item.previousId === previousId);
    if (pending) {
      if (pending.questionSequence !== questionSequence || pending.answer !== answer) throw new Error('La aclaración pendiente no puede editarse ni enviarse con otra identidad. Revisa o detén su ejecución.');
      return retryClarification(pending.runId);
    }
    const runId = crypto.randomUUID(), nextSurface = crypto.randomUUID() + crypto.randomUUID();
    retainSurface(runId, nextSurface, previousId);
    const envelope = Object.freeze({ previousId, runId, questionSequence, answer, nextSurface });
    pendingClarifications.set(runId, envelope);
    try { return await sendClarification(envelope, false); }
    catch (error) { if (!surfaces.has(runId)) pendingClarifications.delete(runId); throw error; }
  }
  async function sendClarification(envelope: PendingClarification, retry: boolean) {
    const existing = clarificationFlights.get(envelope.runId); if (existing) return existing;
    const { previousId, ...input } = envelope;
    const flight = (async () => {
      const run = await admit(input.runId, () => clarificationRequest!(previousId, input, AbortSignal.timeout(10000)), retry);
      pendingClarifications.delete(input.runId); return run;
    })();
    clarificationFlights.set(input.runId, flight);
    try { return await flight; }
    finally { if (clarificationFlights.get(input.runId) === flight) clarificationFlights.delete(input.runId); }
  }
  async function retryClarification(id: string) {
    const envelope = pendingClarifications.get(id);
    if (!envelope || !clarificationRequest || !(questionSurfaces.has(envelope.previousId) || surfaces.has(envelope.previousId)))
      throw new Error('Esta pestaña no conserva la aclaración pendiente ni su permiso original.');
    return sendClarification(envelope, true);
  }
  async function observeOperation(message: string, sessionId: string, source?: { id: string; revision: number }, signal?: AbortSignal, model = 'qwen3:8b') {
    const runId = crypto.randomUUID(); rememberRunId(runId, storageKey);
    return client.observe({ runId, sessionId, message, mode: 'LIVE', sourceId: source?.id, sourceRevision: source?.revision, model }, signal);
  }

  return { startVisible, observeOperation, surfaceCapability: id => surfaces.get(id), releaseSurface: id => {
    pendingClarifications.delete(id);
    for (const [key, value] of pendingClarifications) if (value.previousId === id) pendingClarifications.delete(key);
    clarificationFlights.delete(id);
    const control = surfaces.delete(id), question = questionSurfaces.delete(id); return control || question;
  }, ...(clarificationRequest ? { clarifyVisible, retryClarification, pendingClarification: (id: string) => {
    const value = pendingClarifications.get(id);
    return value ? Object.freeze({ runId: value.runId, previousId: value.previousId, questionSequence: value.questionSequence, answer: value.answer }) : undefined;
  }, clarificationCapability: (id: string) => questionSurfaces.get(id) ?? surfaces.get(id),
    finishSurface: (id: string, allowClarification: boolean) => {
      const capability = surfaces.get(id); surfaces.delete(id);
      if (capability && allowClarification) questionSurfaces.set(id, capability); else questionSurfaces.delete(id);
    } } : {}) };
}
const visibleSession = createVisibleSessionClient();
// Compatibility exports delegate to the same owner as standalone callers.
export const startVisible: VisibleSessionClient['startVisible'] = (...args) => visibleSession.startVisible(...args);
export const observeOperation: VisibleSessionClient['observeOperation'] = (...args) => visibleSession.observeOperation(...args);
export const surfaceCapability: VisibleSessionClient['surfaceCapability'] = id => visibleSession.surfaceCapability(id);
export const releaseSurface: VisibleSessionClient['releaseSurface'] = id => visibleSession.releaseSurface(id);
