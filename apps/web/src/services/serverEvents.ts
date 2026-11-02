export type ServerEvent = { type: string; data: string };

/** Transport framing only; each consumer owns event schemas, identity and terminal receipts. */
export async function* readServerEvents(response: Response, signal: AbortSignal, maximumEventLength = 1200000): AsyncGenerator<ServerEvent> {
  if (!response.body) throw new Error('La conexión no entregó un flujo de eventos.');
  const reader = response.body.getReader();
  const decoder = new TextDecoder('utf-8', { fatal: true });
  let buffer = '';
  const abort = () => { void reader.cancel().catch(() => {}); };
  signal.addEventListener('abort', abort, { once: true });
  try {
    while (true) {
      if (signal.aborted) throw new DOMException('Operación cancelada', 'AbortError');
      const chunk = await reader.read();
      if (signal.aborted) throw new DOMException('Operación cancelada', 'AbortError');
      buffer += decoder.decode(chunk.value, { stream: !chunk.done });
      let boundary: RegExpExecArray | null;
      while ((boundary = /\r?\n\r?\n/.exec(buffer))) {
        if (boundary.index > maximumEventLength) throw new Error('Evento demasiado grande.');
        const frame = buffer.slice(0, boundary.index);
        buffer = buffer.slice(boundary.index + boundary[0].length);
        let type = 'message';
        const data: string[] = [];
        for (const line of frame.split(/\r?\n/)) {
          const colon = line.indexOf(':');
          const field = colon < 0 ? line : line.slice(0, colon);
          const value = colon < 0 ? '' : line.slice(colon + 1).replace(/^ /, '');
          if (field === 'event') type = value;
          if (field === 'data') data.push(value);
        }
        if (data.length) yield { type, data: data.join('\n') };
      }
      if (buffer.length > maximumEventLength) throw new Error('Evento demasiado grande.');
      if (chunk.done) {
        if (buffer.trim()) throw new Error('El flujo terminó con un evento incompleto.');
        return;
      }
    }
  } finally {
    signal.removeEventListener('abort', abort);
    await reader.cancel().catch(() => {});
    reader.releaseLock();
  }
}
