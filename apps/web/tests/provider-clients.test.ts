import { expect, it, vi } from 'vitest';
import { createApiClient, type HttpTransport } from '../src/services/api';
import { createProviderClient } from '../src/services/providers';
import { createPreferenceClient } from '../src/services/preferences';

it('preserves stream identity and explicit execution approval without consuming the response', async () => {
  const response = new Response('event: complete\ndata: {"text":"ok","revision":3}\n\n');
  const send = vi.fn<HttpTransport['send']>().mockResolvedValue(response); const client = createProviderClient(createApiClient({ send }));
  const controller = new AbortController();
  const input = { promptId: 'synthetic', revision: 3, values: { tema: 'α' }, model: 'qwen3:8b', confirmed: false, expectedText: 'Explica α.' };
  expect(await client.execute(input, controller.signal)).toBe(response); expect(response.bodyUsed).toBe(false);
  expect(send.mock.calls[0][0]).toBe('/api/v1/ai/local/execute');
  expect(JSON.parse(String(send.mock.calls[0][1].body))).toEqual(input); expect(send.mock.calls[0][1].signal).toBe(controller.signal);
});

it('rejects incomplete provider contracts instead of inventing availability or permission', async () => {
  const client = createProviderClient(createApiClient({ send: async () => Response.json([{ providerId: 'custom', modesSupported: ['COPY_AND_OPEN'] }]) }));
  await expect(client.list()).rejects.toThrow('no es compatible');
});

it('does not navigate, execute or retry while preparing a destination', async () => {
  const send = vi.fn<HttpTransport['send']>().mockResolvedValue(new Response('{"detail":"No confirmado"}', { status: 422 }));
  const client = createProviderClient(createApiClient({ send }));
  const input = { promptId: 'synthetic', revision: 2, values: {}, providerId: 'chatgpt', confirmed: false };
  await expect(client.prepare(input)).rejects.toMatchObject({ status: 422 }); expect(send).toHaveBeenCalledTimes(1);
  expect(send.mock.calls[0][0]).toBe('/api/v1/transfers/prepare'); expect(JSON.parse(String(send.mock.calls[0][1].body))).toEqual(input);
});

it('preference substitution preserves nullable choices, explicit approval, deletion and abort signal', async () => {
  const choices = { language: null, format: 'JSON' as const, providerId: null };
  const send = vi.fn<HttpTransport['send']>().mockResolvedValueOnce(Response.json(choices)).mockResolvedValueOnce(new Response(null, { status: 204 }));
  const client = createPreferenceClient(createApiClient({ send })); const controller = new AbortController();
  expect(await client.save(choices, false, controller.signal)).toEqual(choices);
  expect(JSON.parse(String(send.mock.calls[0][1].body))).toEqual({ preferences: choices, confirmed: false });
  expect(send.mock.calls[0][1].method).toBe('PUT'); expect(send.mock.calls[0][1].signal).toBe(controller.signal);
  await client.forget(controller.signal); expect(send.mock.calls[1][1].method).toBe('DELETE');
  expect(send.mock.calls[1][1].body).toBeUndefined();
});
