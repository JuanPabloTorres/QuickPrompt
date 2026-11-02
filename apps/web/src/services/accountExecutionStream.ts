import { z } from 'zod';
import { ApiError, type ApiClient } from './api';
import type { AccountExecution } from './accounts';
import { readServerEvents } from './serverEvents';

/** Snapshot protocol only. Transport failure never resubmits work or silently reconnects. */
export function createExecutionWatcher(client: ApiClient, itemsSchema: z.ZodType<AccountExecution[]>) {
  const frameSchema = z.strictObject({ libraryId: z.string().uuid(), signal: z.enum(['snapshot', 'heartbeat', 'complete', 'renew', 'access-lost', 'unavailable']), items: itemsSchema.nullable() });
  return async (libraryId: string, observe: (jobs: AccountExecution[]) => void, signal: AbortSignal): Promise<void> => {
    // 40 explicit server renewals cover the fixed queue/run deadlines without an unbounded loop.
    for (let connection = 0; connection < 40; connection++) {
      signal.throwIfAborted();
      const response = await client.open(`/api/accounts/v1/libraries/${encodeURIComponent(libraryId)}/executions/events`, { signal, credentials: 'same-origin', headers: { Accept: 'text/event-stream' } });
      if (!response.ok) { await response.body?.cancel(); throw new ApiError('No se pudo seguir la ejecución. Actualiza las preparaciones para reintentar.', response.status); }
      if (!response.headers.get('Content-Type')?.toLowerCase().startsWith('text/event-stream')) {
        await response.body?.cancel();
        throw new Error('El servidor no entregó eventos de ejecución.');
      }
      let snapshot: AccountExecution[] | undefined;
      let renew = false;
      for await (const event of readServerEvents(response, signal, 128000)) {
        if (event.type !== 'execution-state') throw new Error('Evento de ejecución desconocido.');
        const frame = frameSchema.parse(JSON.parse(event.data));
        if (frame.libraryId !== libraryId || frame.items?.some(item => item.libraryId !== libraryId)) throw new Error('El estado no corresponde a esta biblioteca.');
        if (frame.signal === 'access-lost') throw new ApiError('Tu sesión o permiso dejó de estar vigente. Vuelve a entrar.', 401);
        if (frame.signal === 'unavailable') throw new ApiError('El seguimiento se interrumpió. Actualiza las preparaciones para reintentar.', 503);
        if (frame.signal === 'snapshot') {
          if (frame.items === null || frame.items.length > 100 || new Set(frame.items.map(item => item.id)).size !== frame.items.length) throw new Error('Snapshot de ejecuciones inválido.');
          snapshot = frame.items;
          if (!signal.aborted) observe(snapshot);
        } else {
          if (!snapshot || frame.items !== null) throw new Error('Falta un snapshot válido para el seguimiento.');
          const active = snapshot.some(item => item.status === 'Queued' || item.status === 'Running');
          if (frame.signal === 'complete') {
            if (active) throw new Error('El seguimiento terminó con trabajo todavía activo.');
            return;
          }
          if (frame.signal === 'renew') {
            if (!active) throw new Error('Renovación de seguimiento sin trabajo activo.');
            renew = true; break;
          }
        }
      }
      if (!renew) throw new Error('Se perdió el seguimiento. Actualiza las preparaciones para reintentar.');
    }
    throw new Error('El seguimiento alcanzó su límite. Actualiza las preparaciones para consultar su estado.');
  };
}
