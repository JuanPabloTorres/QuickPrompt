import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { CompilationHistory } from '../src/features/prompts/CompilationHistory';
import { ApplicationServiceProvider } from '../src/components/ServiceContext';
import { createApplicationServices } from '../src/services/application';
import { createApiClient, type HttpTransport } from '../src/services/api';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
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
  view(send); await screen.findByText(/Texto conservado/); fireEvent.click(screen.getByText(/Texto conservado/));
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
  const rendered = view(send); await screen.findByText(/Texto conservado/);
  fireEvent.click(screen.getByRole('button', { name: 'Siguiente' }));
  await screen.findByText('Cargando historial…'); expect(screen.queryByText(/Texto conservado/)).toBeNull();
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
  view(send); await screen.findByText(/Texto conservado/);
  fireEvent.click(screen.getByRole('button', { name: 'Borrar historial…' }));
  expect((screen.getByRole('button', { name: 'Confirmar borrado del historial' }) as HTMLButtonElement).disabled).toBe(true);
  expect(send).toHaveBeenCalledTimes(1);
  fireEvent.click(screen.getByLabelText('Confirmo borrar este historial'));
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar borrado del historial' }));
  await screen.findByText(/No hay resultados guardados/);
  expect(send.mock.calls[1][0]).toContain('/history/clear');
  expect(JSON.parse(send.mock.calls[1][1].body as string)).toEqual({ confirmed: true, expectedSnapshot: 'A'.repeat(64) });
});
