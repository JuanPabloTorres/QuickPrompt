import { expect, it, vi } from 'vitest';
import { createApiClient, type HttpTransport } from '../src/services/api';
import { createLibraryServices } from '../src/services/library';

it('archive ports preserve explicit confirmation, reviewed snapshot and cancellation', async () => {
  const send = vi.fn<HttpTransport['send']>().mockResolvedValueOnce(new Response('{"imported":2}')).mockResolvedValueOnce(new Response('{"imported":0}'));
  const { archives } = createLibraryServices(createApiClient({ send }));
  const controller = new AbortController();
  await archives.restore({ data: { version: 2 }, confirmed: false, expectedSnapshot: 'reviewed-snapshot' }, controller.signal);
  expect(send.mock.calls[0][0]).toBe('/api/v1/library/restore');
  expect(JSON.parse(String(send.mock.calls[0][1].body))).toEqual({ data: { version: 2 }, confirmed: false, expectedSnapshot: 'reviewed-snapshot' });
  expect(send.mock.calls[0][1].signal).toBe(controller.signal);
  await archives.import({ data: { version: 1 }, confirmed: true });
  expect(send.mock.calls[1][0]).toBe('/api/v1/library/import');
  expect(JSON.parse(String(send.mock.calls[1][1].body)).confirmed).toBe(true);
});

it('legacy conversion preserves binary source and caller cancellation without consuming it as JSON', async () => {
  const send = vi.fn<HttpTransport['send']>().mockResolvedValue(new Response('{"version":1}'));
  const { archives } = createLibraryServices(createApiClient({ send }));
  const source = new Blob(['synthetic SQLite bytes']); const controller = new AbortController();
  expect(await archives.convertLegacy(source, controller.signal)).toEqual({ version: 1 });
  const [path, options] = send.mock.calls[0];
  expect(path).toBe('/api/v1/library/legacy/convert');
  expect(options.body).toBe(source); expect(options.signal).toBe(controller.signal);
  expect(new Headers(options.headers).get('Content-Type')).toBe('application/octet-stream');
});

it('archive clients reject malformed previews and unverified backups', async () => {
  const send = vi.fn<HttpTransport['send']>().mockResolvedValueOnce(new Response('{"preview":{},"expectedSnapshot":1}')).mockResolvedValueOnce(new Response('{"fileName":"copy.sqlite","integrity":"unknown"}'));
  const { archives } = createLibraryServices(createApiClient({ send }));
  await expect(archives.inspectRestore({})).rejects.toThrow('no es compatible');
  await expect(archives.backup()).rejects.toThrow('no es compatible');
});

it.each([409, 503])('never retries or changes the reviewed state after a server rejection %s', async status => {
  const send = vi.fn<HttpTransport['send']>().mockResolvedValue(new Response('{"detail":"No se aplicó"}', { status }));
  const { archives } = createLibraryServices(createApiClient({ send }));
  await expect(archives.restore({ data: {}, confirmed: true, expectedSnapshot: 'original' })).rejects.toMatchObject({ status });
  expect(send).toHaveBeenCalledTimes(1);
});
