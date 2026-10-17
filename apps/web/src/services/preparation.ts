import { z } from 'zod';
import { promptSchema } from './api';
import { semanticSchema } from './semantic';

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
export class PreparationFailure extends Error {
  constructor(message: string, public code: string) { super(message); }
}
export const stageLabels: Record<PreparationState, string> = { RECEIVED: 'Preparando el prompt…', UNDERSTANDING: 'Preparando el prompt…', SEARCHING: 'Buscando en tu biblioteca…', SELECTING_OR_GENERATING: 'Preparando el prompt…', FILLING: 'Preparando el prompt…', VALIDATING: 'Comprobando el resultado…', READY: 'Listo para utilizar', USER_ACTION: 'Esperando tu decisión', COMPLETED: 'Listo para utilizar', NEEDS_INFORMATION: 'Necesito un dato', CANCELLED: 'Detenido', FAILED: 'No se pudo completar', BLOCKED: 'No se pudo completar' };
export async function readPreparation(response: Response, operationId: string, stage: (value: PreparationState) => void, signal: AbortSignal, onResponse?: (text: string) => void): Promise<PreparedDraft> {
  if (!response.ok || !response.body) throw new Error(response.status === 429 ? 'NOVA está ocupada. Espera un momento y reintenta.' : 'No se pudo iniciar la preparación. Tu biblioteca sigue disponible.');
  const reader = response.body.getReader(); const decoder = new TextDecoder('utf-8', { fatal: true }); let buffer = '';
  try {
    while (true) {
      const chunk = await reader.read();
      if (signal.aborted) throw new DOMException('Detenido', 'AbortError');
      buffer += decoder.decode(chunk.value, { stream: !chunk.done });
      if (buffer.length > 1200000) throw new Error('Propuesta demasiado grande.');
      let boundary: number;
      while ((boundary = buffer.indexOf('\n\n')) >= 0) {
        const event = buffer.slice(0, boundary); buffer = buffer.slice(boundary + 2);
        const name = event.split('\n').find(line => line.startsWith('event: '))?.slice(7);
        const raw = event.split('\n').find(line => line.startsWith('data: '))?.slice(6);
        if (!raw) throw new Error('Evento de preparación incompleto.');
        const data: unknown = JSON.parse(raw);
        if (name === 'stage') stage(z.object({ state: stateSchema }).parse(data).state);
        else if (name === 'response') onResponse?.(z.object({ text: z.string().max(64000) }).parse(data).text);
        else if (name === 'error') { const failure = z.object({ message: z.string(), code: z.string().optional() }).parse(data); throw new PreparationFailure(failure.message, failure.code ?? 'FAILED'); }
        else if (name === 'complete') { const result = preparedSchema.parse(data); if (result.id !== operationId) throw new Error('La propuesta pertenece a otra operación.'); return result; }
        else throw new Error('Evento de preparación desconocido.');
      }
      if (chunk.done) throw new Error('La preparación quedó incompleta. Tu trabajo anterior se conserva.');
    }
  } finally { await reader.cancel().catch(() => {}); reader.releaseLock(); }
}
