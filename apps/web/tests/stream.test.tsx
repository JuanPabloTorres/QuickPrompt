import { expect, it, vi } from 'vitest';
import { readLocalStream } from '../src/services/localStream';

function response(text: string) {
  const bytes = new TextEncoder().encode(text);
  // One byte per chunk also splits Unicode code points, JSON and SSE delimiters.
  return new Response(new ReadableStream({ start(controller) { for (const byte of bytes) controller.enqueue(new Uint8Array([byte])); controller.close(); } }));
}
it('decodes split UTF-8 and requires a complete matching revision', async () => {
  const progress = vi.fn();
  const result = await readLocalStream(response('event: progress\ndata: {"text":"α"}\n\nevent: complete\ndata: {"text":"α 李\nfin","revision":3}\n\n'.replace('李\nfin', '李\\nfin')), 3, progress, new AbortController().signal);
  expect(progress).toHaveBeenCalledWith('α'); expect(result).toBe('α 李\nfin');
});
it('rejects partial streams so partial output cannot be acknowledged', async () => {
  await expect(readLocalStream(response('event: progress\ndata: {"text":"partial"}\n\n'), 1, vi.fn(), new AbortController().signal)).rejects.toThrow('Respuesta incompleta');
});
it('rejects unknown events, incomplete completion payloads and mismatched revisions', async () => {
  for (const text of ['event: tool\ndata: {}\n\n', 'event: complete\ndata: {"text":"partial"}\n\n', 'event: complete\ndata: {"text":"wrong","revision":2}\n\n']) {
    await expect(readLocalStream(response(text), 1, vi.fn(), new AbortController().signal)).rejects.toThrow();
  }
});
it('cancellation prevents late progress callbacks', async () => {
  const controller = new AbortController(); controller.abort(); const progress = vi.fn();
  await expect(readLocalStream(response('event: progress\ndata: {"text":"late"}\n\n'), 1, progress, controller.signal)).rejects.toThrow('cancelada');
  expect(progress).not.toHaveBeenCalled();
});
