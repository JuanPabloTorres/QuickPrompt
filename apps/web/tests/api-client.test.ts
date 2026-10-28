import { expect, it, vi } from 'vitest';
import { z } from 'zod';
import { ApiError, createApiClient, type HttpTransport } from '../src/services/api';

it('substitutes transport while preserving schema, cancellation and capability headers', async () => {
  const send = vi.fn<HttpTransport['send']>().mockResolvedValue(new Response('{"revision":3}'));
  const client = createApiClient({ send });
  const cancellation = new AbortController();
  const result = await client.request('/prompts', z.object({ revision: z.number().int() }), {
    method: 'POST', body: '{}', signal: cancellation.signal,
    headers: new Headers({ 'X-Nova-Surface-Capability': 'synthetic-capability' }),
  });
  expect(result.revision).toBe(3);
  const [path, options] = send.mock.calls[0];
  expect(path).toBe('/api/v1/prompts');
  expect(options.signal).toBe(cancellation.signal);
  expect(new Headers(options.headers).get('X-Nova-Surface-Capability')).toBe('synthetic-capability');
  expect(new Headers(options.headers).get('X-QuickPrompt-Client')).toBe('nova-v1');
});

it('keeps streaming responses intact for the existing stream verifier', async () => {
  const response = new Response('event: complete\ndata: {"text":"ok","revision":1}\n\n');
  const client = createApiClient({ send: async () => response });
  expect(await client.open('/ai/local/execute')).toBe(response);
  expect(response.bodyUsed).toBe(false);
});

it('keeps error status and rejects incompatible domain responses', async () => {
  const failed = createApiClient({ send: async () => new Response('{"detail":"Revisión obsoleta"}', { status: 409 }) });
  const failure = await failed.request('/prompts', z.unknown()).catch(error => error);
  expect(failure).toBeInstanceOf(ApiError);
  if (!(failure instanceof ApiError)) throw new Error('Se esperaba ApiError.');
  expect(failure.status).toBe(409);
  expect(failure.message).toBe('Revisión obsoleta');
  const invalid = createApiClient({ send: async () => new Response('{"revision":"invalid"}') });
  await expect(invalid.request('/prompts', z.object({ revision: z.number() }))).rejects.toThrow('no es compatible');
});

it.each(['https://example.invalid', '//example.invalid', '/\\example.invalid'])('never dispatches an external API path: %s', async path => {
  const send = vi.fn<HttpTransport['send']>();
  await expect(createApiClient({ send }).open(path)).rejects.toThrow('Ruta de API inválida');
  expect(send).not.toHaveBeenCalled();
});
