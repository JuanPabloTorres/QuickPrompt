import { z } from 'zod';
import { readServerEvents } from './serverEvents';

const progressSchema = z.object({ text: z.string().max(64000) });
const completeSchema = progressSchema.extend({ revision: z.number().int().positive() });
// Only a complete, revision-bound result may be copied or acknowledged as used.
export async function readLocalStream(response: Response, revision: number, progress: (text: string) => void, signal: AbortSignal): Promise<string> {
  if (!response.ok || !response.body) throw new Error('No se pudo iniciar la ejecución local. Revisa el modelo y vuelve a intentarlo.');
  for await (const event of readServerEvents(response, signal, 1000000)) {
    const data: unknown = JSON.parse(event.data);
    if (event.type === 'error') throw new Error(z.object({ message: z.string() }).parse(data).message);
    if (event.type === 'progress') progress(progressSchema.parse(data).text);
    else if (event.type === 'complete') {
      const result = completeSchema.parse(data);
      if (result.revision !== revision) throw new Error('La respuesta pertenece a otra revisión.');
      return result.text;
    } else throw new Error('Evento local desconocido.');
  }
  throw new Error('Respuesta incompleta. Puedes reintentar; no se guardó el resultado.');
}
