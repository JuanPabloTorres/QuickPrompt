import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { CompilationHistory } from '../src/features/prompts/CompilationHistory';
import { ApplicationServiceProvider } from '../src/components/ServiceContext';
import { createApplicationServices } from '../src/services/application';
import { createApiClient, type HttpTransport } from '../src/services/api';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.restoreAllMocks(); });
const id = '10000000-0000-4000-8000-000000000001';
const item = { id, templateId: id, revision: 2, title: 'Texto conservado', text: 'α\n<literal> & 李', createdAt: '2026-10-21T10:00:00Z',
  executions: [{ id, model: 'modelo del registro', response: 'Respuesta exacta\nΩ', elapsedMs: 234, createdAt: '2026-10-21T10:01:00Z' }] };
const packet = (page = 0) => ({ page, pageSize: 10, total: 11, responseCount: 1, transferCount: 0, expectedSnapshot: 'A'.repeat(64), items: [item] });
function view(send: HttpTransport['send']) {
  return render(<ApplicationServiceProvider services={createApplicationServices(createApiClient({ send }))}><CompilationHistory onClose={vi.fn()} /></ApplicationServiceProvider>);
}
it('copies stored instruction and associated response literally without regenerating or recording another use', async () => {
  const send = vi.fn<HttpTransport['send']>(async () => Response.json(packet()));
  const writeText = vi.fn(async () => {}); vi.stubGlobal('navigator', { clipboard: { writeText } });
  view(send); await screen.findByRole('heading', { name: 'Texto conservado' });
  fireEvent.click(screen.getByRole('button', { name: 'Copiar instrucción guardada' }));
  await waitFor(() => expect(writeText).toHaveBeenLastCalledWith(item.text));
  fireEvent.click(screen.getByRole('button', { name: 'Copiar respuesta guardada' }));
  await waitFor(() => expect(writeText).toHaveBeenLastCalledWith(item.executions[0].response));
  expect(send).toHaveBeenCalledTimes(1); expect(send.mock.calls[0][0]).toContain('/history?page=0');
  expect(send.mock.calls[0][1].method ?? 'GET').toBe('GET');
});
it('removes previous copy targets while the next page is pending and cancels its request on close', async () => {
  let signal: AbortSignal | undefined;
  const send = vi.fn<HttpTransport['send']>(async (path, options) => {
    if (path.endsWith('page=0')) return Response.json(packet());
    signal = options.signal as AbortSignal; return new Promise<Response>(() => {});
  });
  const rendered = view(send); await screen.findByRole('heading', { name: 'Texto conservado' });
  fireEvent.click(screen.getByRole('button', { name: 'Siguiente' }));
  await screen.findByText('Cargando historial…'); expect(screen.queryByRole('heading', { name: 'Texto conservado' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Copiar instrucción guardada' })).toBeNull();
  rendered.unmount(); expect(signal?.aborted).toBe(true);
});
it('shows failure, retries the same page and handles an empty history', async () => {
  const send = vi.fn<HttpTransport['send']>().mockRejectedValueOnce(Error('Sin conexión')).mockResolvedValueOnce(Response.json({ ...packet(), total: 0, items: [] }));
  view(send); await screen.findByRole('alert'); fireEvent.click(screen.getByRole('button', { name: 'Reintentar historial' }));
  await screen.findByText(/No hay resultados guardados/); expect(send).toHaveBeenCalledTimes(2);
});
it('does not display a valid response for the wrong requested page', async () => {
  view(async () => Response.json(packet(1))); await screen.findByRole('alert');
  expect(screen.queryByText(/Texto conservado/)).toBeNull();
});
it('requires explicit consent and binds clearing to the exact reviewed history', async () => {
  const send = vi.fn<HttpTransport['send']>().mockResolvedValueOnce(Response.json(packet()))
    .mockResolvedValueOnce(new Response(null, { status: 204 }))
    .mockResolvedValueOnce(Response.json({ ...packet(), total: 0, responseCount: 0, items: [] }));
  view(send); await screen.findByRole('heading', { name: 'Texto conservado' });
  fireEvent.click(screen.getByRole('button', { name: 'Borrar historial…' }));
  expect((screen.getByRole('button', { name: 'Confirmar borrado del historial' }) as HTMLButtonElement).disabled).toBe(true);
  expect(send).toHaveBeenCalledTimes(1);
  fireEvent.click(screen.getByLabelText('Confirmo borrar este historial'));
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar borrado del historial' }));
  await screen.findByText(/No hay resultados guardados/);
  expect(send.mock.calls[1][0]).toContain('/history/clear');
  expect(JSON.parse(send.mock.calls[1][1].body as string)).toEqual({ confirmed: true, expectedSnapshot: 'A'.repeat(64) });
});

it('selects stored snapshots independently and does not carry a response or selection into another page', async () => {
  const second = { ...item, id: '20000000-0000-4000-8000-000000000002', title: 'Otra instrucción', text: 'Texto independiente', executions: [] };
  const third = { ...second, id: '30000000-0000-4000-8000-000000000003', title: 'Página siguiente' };
  const send = vi.fn<HttpTransport['send']>().mockResolvedValueOnce(Response.json({ ...packet(), items: [item, second] }))
    .mockResolvedValueOnce(Response.json({ ...packet(1), items: [third] }));
  view(send); await screen.findByRole('heading', { name: item.title });
  fireEvent.click(screen.getByRole('button', { name: /Otra instrucción/ }));
  const reader = screen.getByRole('region', { name: 'Detalle del resultado' });
  expect(within(reader).getByText(second.text).textContent).toBe(second.text);
  expect(within(reader).queryByText(item.text)).toBeNull();
  expect(screen.queryByRole('button', { name: 'Copiar respuesta guardada' })).toBeNull();
  expect(within(reader).getByText('Sin respuesta local guardada')).toBeTruthy();
  expect(screen.getByRole('button', { name: /Otra instrucción/ }).getAttribute('aria-pressed')).toBe('true');
  fireEvent.click(screen.getByRole('button', { name: 'Siguiente' }));
  await screen.findByRole('heading', { name: third.title });
  expect(screen.queryByRole('button', { name: /Otra instrucción/ })).toBeNull();
  expect(send).toHaveBeenCalledTimes(2);
});

it('downloads the original UTF-8 content with distinct instruction and response filenames', async () => {
  const blobs: Blob[] = [], names: string[] = [];
  vi.stubGlobal('URL', { createObjectURL: (blob: Blob) => { blobs.push(blob); return 'blob:test'; }, revokeObjectURL: vi.fn() });
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) { names.push(this.download); });
  view(async () => Response.json(packet())); await screen.findByRole('heading', { name: item.title });
  fireEvent.click(screen.getByRole('button', { name: 'Descargar instrucción' }));
  fireEvent.click(screen.getByRole('button', { name: 'Descargar respuesta' }));
  const readBlob = (blob: Blob) => new Promise<string>(resolve => { const reader = new FileReader(); reader.onload = () => resolve(reader.result as string); reader.readAsText(blob); });
  expect(await Promise.all(blobs.map(readBlob))).toEqual([item.text, item.executions[0].response]);
  expect(names).toEqual([`quickprompt-instruccion-${id}.txt`, `quickprompt-respuesta-${id}.txt`]);
});

it('focuses deletion review, cancels without a write and restores the selected result and focus', async () => {
  const send = vi.fn<HttpTransport['send']>(async () => Response.json(packet()));
  view(send); await screen.findByRole('heading', { name: item.title });
  fireEvent.click(screen.getByRole('button', { name: 'Borrar historial…' }));
  expect(document.activeElement).toBe(screen.getByRole('region', { name: 'Revisión de borrado del historial' }));
  expect(screen.queryByRole('button', { name: 'Copiar instrucción guardada' })).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Conservar historial' }));
  expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Borrar historial…' }));
  expect(screen.getByRole('heading', { name: item.title })).toBeTruthy();
  expect(send).toHaveBeenCalledTimes(1);
});

it('keeps literal content available when clipboard access fails', async () => {
  vi.stubGlobal('navigator', { clipboard: { writeText: vi.fn().mockRejectedValue(Error('Denied')) } });
  view(async () => Response.json(packet())); await screen.findByRole('heading', { name: item.title });
  fireEvent.click(screen.getByRole('button', { name: 'Copiar instrucción guardada' }));
  await screen.findByText('No se pudo copiar. Puedes seleccionar o descargar el texto.');
  expect(screen.getByText(item.text, { normalizer: value => value }).textContent).toBe(item.text);
  expect(screen.getByRole('button', { name: 'Descargar instrucción' })).toBeTruthy();
});
