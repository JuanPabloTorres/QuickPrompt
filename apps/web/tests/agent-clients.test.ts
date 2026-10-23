import { expect, it, vi } from 'vitest';
import { ApiError, createApiClient, type HttpTransport } from '../src/services/api';
import { createLiveClient, type LiveEvents, type LiveUiReceipt } from '../src/services/live';
import { createPreparationClient } from '../src/services/preparation';

const first = '10000000-0000-4000-8000-000000000001';
const second = '10000000-0000-4000-8000-000000000002';

it('isolates surface capabilities per run and preserves receipt identity without replay', async () => {
  const send = vi.fn<HttpTransport['send']>().mockResolvedValue(new Response(null, { status: 204 }));
  const client = createLiveClient(createApiClient({ send }));
  const signal = new AbortController().signal;
  const receipt: LiveUiReceipt = { actionId: first, sequence: 7, executionStatus: 'INTERRUPTED', verificationStatus: 'UNKNOWN', observation: null, errorCode: 'UI_VALUE_MISMATCH' };
  await client.progress(first, 'surface-first', { actionId: first, sequence: 7, executionStatus: 'TARGET_LOCATED' }, signal);
  await client.receipt(first, 'surface-first', receipt, signal);
  await client.receipt(second, 'surface-second', { ...receipt, actionId: second }, signal);
  expect(send).toHaveBeenCalledTimes(3);
  expect(send.mock.calls.map(([path]) => path)).toEqual([
    `/api/v1/nova/live/${first}/actions/progress`,
    `/api/v1/nova/live/${first}/actions/receipt`,
    `/api/v1/nova/live/${second}/actions/receipt`,
  ]);
  expect(send.mock.calls.map(([, options]) => new Headers(options.headers).get('X-Nova-Surface'))).toEqual(['surface-first', 'surface-first', 'surface-second']);
  expect(JSON.parse(send.mock.calls[1][1].body as string)).toEqual(receipt);
  expect(send.mock.calls.every(([, options]) => options.signal === signal)).toBe(true);
});

it('keeps rejected approval/deletion requests rejected and never retries effects', async () => {
  const send = vi.fn<HttpTransport['send']>().mockImplementation(async () => Response.json({ detail: 'Aprobación caducada' }, { status: 409 }));
  const client = createLiveClient(createApiClient({ send }));
  const approval = { approvalId: second, hash: 'synthetic-hash', revision: 3, confirmed: false };
  await expect(client.deletion(first, approval)).rejects.toBeInstanceOf(ApiError);
  expect(send).toHaveBeenCalledTimes(1);
  expect(JSON.parse(send.mock.calls[0][1].body as string)).toEqual(approval);
  await expect(client.approve(first, { ...approval, confirmed: true })).rejects.toBeInstanceOf(ApiError);
  expect(send).toHaveBeenCalledTimes(2);
});

it('substitutes the event connection without giving event data an execution capability', () => {
  const stream = new EventTarget() as EventTarget & LiveEvents;
  stream.close = vi.fn(); stream.onopen = null; stream.onerror = null;
  const connect = vi.fn(() => stream);
  const client = createLiveClient(createApiClient(), connect);
  expect(client.events(first, 12)).toBe(stream);
  expect(connect).toHaveBeenCalledWith(`/api/v1/nova/live/${first}/events?after=12`);
  expect('receipt' in stream).toBe(false);
});

it('preserves operation identity, confirmation, cancellation and unread preparation streams', async () => {
  const send = vi.fn<HttpTransport['send']>().mockImplementation(async () => new Response('event: complete\ndata: {}\n\n'));
  const client = createPreparationClient(createApiClient({ send }));
  const cancellation = new AbortController();
  const input = { operationId: first, sessionId: second, message: 'Completa los campos.', rememberDraft: false };
  const response = await client.prepare(input, cancellation.signal);
  expect(response.bodyUsed).toBe(false);
  expect(JSON.parse(send.mock.calls[0][1].body as string)).toEqual(input);
  const execution = { operationId: second, confirmed: true, expectedText: 'Texto exacto 🚀', model: 'qwen3:8b' };
  const result = await client.execute(first, execution, second, cancellation.signal);
  expect(result.bodyUsed).toBe(false);
  expect(JSON.parse(send.mock.calls[1][1].body as string)).toEqual(execution);
  expect(new Headers(send.mock.calls[1][1].headers).get('X-Nova-Live-Run')).toBe(second);
  expect(send.mock.calls[1][1].signal).toBe(cancellation.signal);
  await client.execute(first, execution, null, cancellation.signal);
  expect(new Headers(send.mock.calls[2][1].headers).has('X-Nova-Live-Run')).toBe(false);
});
