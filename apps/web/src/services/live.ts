import { z } from 'zod';
import { semanticSchema } from './semantic';
import { ApiError, emptySchema, post, request } from './api';

export const liveStatuses = ['CREATED', 'INTERPRETING', 'TOOL_PROPOSED', 'TOOL_APPROVED', 'TOOL_RUNNING', 'UI_UPDATED', 'VERIFYING', 'AWAITING_APPROVAL', 'PAUSED', 'COMPLETED', 'FAILED', 'CANCELLED', 'BLOCKED'] as const;
export const liveRecordSchema = z.object({ runId: z.string().uuid(), sessionId: z.string().uuid(), mode: z.enum(['LIVE', 'BROWSER', 'SAME_TAB']), status: z.enum(liveStatuses), sequence: z.number().int(), targetId: z.string().uuid().nullable(), revision: z.number().nullable(), pauseRequested: z.boolean(), browserOpen: z.boolean(), approval: z.string(), effectCommitted: z.boolean(), errorCode: z.string().nullable() });
export const liveEventSchema = z.object({ runId: z.string().uuid(), eventId: z.string().uuid(), sequence: z.number().int().positive(), timestamp: z.string(), stage: z.string(), actor: z.enum(['MODEL_SELECTED', 'RULE_SELECTED', 'USER_SELECTED', 'SYSTEM_VALIDATION']), toolName: z.string().nullable(), targetId: z.string().uuid().nullable(), status: z.enum(liveStatuses), durationMs: z.number().nullable(), errorCode: z.string().nullable(), safeDisplayMessage: z.string() });
const draftSchema = z.object({ title: z.string(), templateText: z.string(), description: z.string(), categoryId: z.number(), revision: z.number().optional() });
export const liveViewSchema = z.object({ semantic: semanticSchema.nullable().optional(), run: liveRecordSchema, request: z.string().nullable(), question: z.string().nullable(), observation: z.object({ promptId: z.string().uuid().nullable(), title: z.string().nullable(), revision: z.number().nullable(), templateText: z.string().nullable(), draftChanged: z.boolean(), values: z.record(z.string(), z.string()), response: z.string().nullable(), responseComplete: z.boolean() }).nullable(), proposal: z.object({ id: z.string().uuid(), hash: z.string(), promptId: z.string().uuid().nullable(), revision: z.number().nullable(), draft: draftSchema, originalText: z.string() }).nullable() });
export type LiveView = z.infer<typeof liveViewSchema>;
export type LiveEvent = z.infer<typeof liveEventSchema>;
export const terminalLive = (status: string) => ['COMPLETED', 'FAILED', 'CANCELLED', 'BLOCKED'].includes(status);
export const liveLabels: Record<typeof liveStatuses[number], string> = { CREATED: 'Solicitud recibida', INTERPRETING: 'Decidiendo el siguiente paso', TOOL_PROPOSED: 'Acción propuesta', TOOL_APPROVED: 'Acción validada', TOOL_RUNNING: 'Ejecutando', UI_UPDATED: 'Cambio comprobado en la interfaz', VERIFYING: 'Comprobando el resultado', AWAITING_APPROVAL: 'Propuesta lista para revisar', PAUSED: 'En pausa', COMPLETED: 'Listo para utilizar', FAILED: 'No se pudo completar', CANCELLED: 'Ejecución detenida', BLOCKED: 'Necesito tu atención' };
export const actorLabels = { MODEL_SELECTED: 'Decisión de Qwen', RULE_SELECTED: 'Regla de QuickPrompt', USER_SELECTED: 'Decisión tuya', SYSTEM_VALIDATION: 'Verificación del sistema' };
function rememberRunId(id: string) { try { sessionStorage.setItem('nova-live-run', id); } catch { /* The run remains controllable without browser storage. */ } }
const surfaces = new Map<string, string>();
export const surfaceCapability = (id: string) => surfaces.get(id);
export const releaseSurface = (id: string) => surfaces.delete(id);
export async function startVisible(message: string, sessionId: string, source?: { id: string; revision: number }, values?: Record<string, string>, options?: { bindSource: boolean }) {
  if (source) await request(`/nova/sessions/${sessionId}/context`, emptySchema, post({ promptId: source.id, revision: source.revision, values, allowStaleContext: true }));
  const runId = crypto.randomUUID();
  const capability = crypto.randomUUID() + crypto.randomUUID(); surfaces.set(runId, capability);
  try {
    const run = await request('/nova/live', liveRecordSchema, { ...post({ runId, sessionId, message, mode: 'SAME_TAB', ...(options?.bindSource && source ? { sourceId: source.id, sourceRevision: source.revision, values } : {}) }, AbortSignal.timeout(10000)), headers: { 'X-Nova-Surface': capability } }); rememberRunId(runId); return run;
  } catch (error) {
    if (error instanceof ApiError && error.status < 500) { surfaces.delete(runId); throw error; }
    // An uncertain POST may already have created the run. Recover its receipt, never start another one.
    try { const recovered = await request(`/nova/live/${runId}`, z.object({ run: liveRecordSchema }), { signal: AbortSignal.timeout(3000) }); if (recovered.run.mode === 'SAME_TAB') { rememberRunId(runId); return recovered.run; } } catch { /* Keep the known ID for explicit recovery below. */ }
    rememberRunId(runId); window.dispatchEvent(new CustomEvent('nova-uncertain-start', { detail: runId }));
    throw new Error('La conexión no confirmó el inicio. NOVA conservará esta identidad para revisar o detener la ejecución sin repetirla.');
  }
}
export async function observeOperation(message: string, sessionId: string, source?: { id: string; revision: number }, signal?: AbortSignal, model = 'qwen3:8b') {
  const runId = crypto.randomUUID(); rememberRunId(runId);
  return request('/nova/live/observe', liveRecordSchema, post({ runId, sessionId, message, mode: 'LIVE', sourceId: source?.id, sourceRevision: source?.revision, model }, signal));
}
