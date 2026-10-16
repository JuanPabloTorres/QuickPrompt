import { z } from 'zod';

const progressSchema = z.object({ text: z.string().max(64000) });
const completeSchema = progressSchema.extend({ revision: z.number().int().positive() });
// Only a complete, revision-bound result may be copied or acknowledged as used.
export async function readLocalStream(response: Response, revision: number, progress: (text: string) => void, signal: AbortSignal): Promise<string> {
  if (!response.ok || !response.body) throw new Error('No se pudo iniciar la ejecución local. Revisa el modelo y vuelve a intentarlo.');
  const reader = response.body.getReader(); const decoder = new TextDecoder('utf-8', { fatal: true }); let buffer = '';
  try {
    while (true) {
      const chunk = await reader.read();
      if (signal.aborted) throw new DOMException('Operación cancelada', 'AbortError');
      buffer += decoder.decode(chunk.value, { stream: !chunk.done });
      if (buffer.length > 1000000) throw new Error('Respuesta demasiado grande.');
      let boundary: number;
      while ((boundary = buffer.indexOf('\n\n')) >= 0) {
        const event = buffer.slice(0, boundary); buffer = buffer.slice(boundary + 2);
        const name = event.split('\n').find(line => line.startsWith('event: '))?.slice(7);
        const raw = event.split('\n').find(line => line.startsWith('data: '))?.slice(6);
        if (!raw) throw new Error('Evento local incompleto.');
        const data: unknown = JSON.parse(raw);
        if (name === 'error') throw new Error(z.object({ message: z.string() }).parse(data).message);
        if (name === 'progress') progress(progressSchema.parse(data).text);
        else if (name === 'complete') {
          const result = completeSchema.parse(data);
          if (result.revision !== revision) throw new Error('La respuesta pertenece a otra revisión.');
          return result.text;
        } else throw new Error('Evento local desconocido.');
      }
      if (chunk.done) throw new Error('Respuesta incompleta. Puedes reintentar; no se guardó el resultado.');
    }
  } finally { await reader.cancel().catch(() => {}); reader.releaseLock(); }
}
