import { expect, it, vi } from 'vitest';
import { readServerEvents } from '../src/services/serverEvents';
import { createApiClient } from '../src/services/api';
import { createAccountService } from '../src/services/accounts';

const library = '10000000-0000-4000-8000-000000000001';
const foreign = '20000000-0000-4000-8000-000000000002';
const job = { id: foreign, libraryId: library, kind: 'CompilePrompt', status: 'Queued', cancelRequested: false,
  createdAt: '', startedAt: null, finishedAt: null, errorCode: null };
const frame = (signal: string, items: unknown = null, libraryId = library) => `event: execution-state\r\ndata: ${JSON.stringify({ libraryId, signal, items })}\r\n\r\n`;
function response(text: string) { return new Response(text, { headers: { 'Content-Type': 'text/event-stream' } }); }
const signal = () => new AbortController().signal;

it('frames split UTF-8 and CRLF, comments and multiline data without owning domain events', async () => {
  const bytes = new TextEncoder().encode(': heartbeat\r\n\r\nevent: example\r\ndata: α 李\r\ndata: second\r\n\r\n');
  let position = 0;
  const stream = new ReadableStream<Uint8Array>({ pull(controller) { if (position < bytes.length) controller.enqueue(bytes.slice(position, ++position)); else controller.close(); } });
  const items = [];
  for await (const item of readServerEvents(new Response(stream), signal())) items.push(item);
  expect(items).toEqual([{ type: 'example', data: 'α 李\nsecond' }]);
});

it('rejects truncated frames, invalid UTF-8 and oversized events', async () => {
  async function consume(value: Response, limit?: number) { for await (const _ of readServerEvents(value, signal(), limit)) { /* framing only */ } }
  await expect(consume(response('event: example\ndata: {}\n'))).rejects.toThrow('incompleto');
  await expect(consume(new Response(new Uint8Array([255, 255])))).rejects.toThrow();
  await expect(consume(response('data: ' + 'a'.repeat(100) + '\n\n'), 20)).rejects.toThrow('grande');
});

it('cancels a blocked reader immediately and releases its stream', async () => {
  const cancelled = vi.fn(); const controller = new AbortController();
  const stream = new ReadableStream<Uint8Array>({ cancel: cancelled });
  const iterator = readServerEvents(new Response(stream), controller.signal);
  const pending = iterator.next(); controller.abort();
  await expect(pending).rejects.toMatchObject({ name: 'AbortError' });
  expect(cancelled).toHaveBeenCalledTimes(1); expect(stream.locked).toBe(false);
});

it('follows queue snapshots with the account transport and accepts only a terminal receipt', async () => {
  const done = { ...job, status: 'Completed', finishedAt: '2026-10-26T18:00:00Z' };
  const send = vi.fn(async (_path: string, _init: RequestInit) => response(frame('snapshot', [job]) + frame('heartbeat') + frame('snapshot', [done]) + frame('complete')));
  const client = createAccountService(createApiClient({ send }, '')); const observed = vi.fn(); const current = signal();
  await client.watchExecutions(library, observed, current);
  expect(observed.mock.calls).toEqual([[[job]], [[done]]]); expect(send).toHaveBeenCalledTimes(1);
  expect(send.mock.calls[0][0]).toBe(`/api/accounts/v1/libraries/${library}/executions/events`);
  expect(send.mock.calls[0][1]).toMatchObject({ signal: current, credentials: 'same-origin' });
});

it('renews only on an explicit server receipt and never after a dropped connection', async () => {
  const send = vi.fn().mockResolvedValueOnce(response(frame('snapshot', [job]) + frame('renew')))
    .mockResolvedValueOnce(response(frame('snapshot', [{ ...job, status: 'Cancelled' }]) + frame('complete')));
  await createAccountService(createApiClient({ send }, '')).watchExecutions(library, vi.fn(), signal());
  expect(send).toHaveBeenCalledTimes(2);
  const dropped = vi.fn(async () => response(frame('snapshot', [job])));
  await expect(createAccountService(createApiClient({ send: dropped }, '')).watchExecutions(library, vi.fn(), signal())).rejects.toThrow('perdió');
  expect(dropped).toHaveBeenCalledTimes(1);
});

it('bounds repeated server renewals without submitting any execution', async () => {
  const send = vi.fn(async (_path: string, _init: RequestInit) => response(frame('snapshot', [job]) + frame('renew')));
  await expect(createAccountService(createApiClient({ send }, '')).watchExecutions(library, vi.fn(), signal())).rejects.toThrow('límite');
  expect(send).toHaveBeenCalledTimes(40);
  expect(send.mock.calls.every(call => call[1].method === undefined && call[1].body === undefined)).toBe(true);
});

it('rejects cross-library snapshots, inconsistent completion and unsolicited data before displaying it', async () => {
  for (const text of [frame('snapshot', [job], foreign), frame('snapshot', [{ ...job, libraryId: foreign }]), frame('complete'),
    frame('snapshot', [job]) + frame('complete'), frame('snapshot', []) + frame('renew'), frame('snapshot', [job, job])]) {
    const observe = vi.fn(); const send = vi.fn(async () => response(text));
    await expect(createAccountService(createApiClient({ send }, '')).watchExecutions(library, observe, signal())).rejects.toThrow();
    expect(send).toHaveBeenCalledTimes(1);
    if (text.startsWith(frame('snapshot', [job], foreign)) || text.startsWith(frame('snapshot', [{ ...job, libraryId: foreign }]))) expect(observe).not.toHaveBeenCalled();
  }
});

it('propagates lost permission and HTTP errors without silently reconnecting', async () => {
  for (const failure of [response(frame('access-lost')), new Response(null, { status: 401 }), new Response(null, { status: 404 }), response(frame('unavailable'))]) {
    const send = vi.fn(async () => failure);
    await expect(createAccountService(createApiClient({ send }, '')).watchExecutions(library, vi.fn(), signal())).rejects.toHaveProperty('status');
    expect(send).toHaveBeenCalledTimes(1);
  }
});
