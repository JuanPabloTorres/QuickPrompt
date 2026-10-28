import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { PreparedUse } from '../src/features/nova/PreparedUse';
import { NovaPreferences } from '../src/features/settings/NovaPreferences';
import type { PreparedDraft } from '../src/services/preparation';

const destination = { providerId: 'custom', displayName: 'Destino del catálogo', destinationUrl: 'https://example.invalid/approved', modesSupported: ['COPY_AND_OPEN'], canSubmit: false, availabilityStatus: 'MANUAL_PASTE' };
const draft = { id: '10000000-0000-4000-8000-000000000001', compiledText: 'Texto del borrador.', providerId: 'custom' } as PreparedDraft;
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

it.each(['changed-url', 'offline'])('prepared use consults the shared catalog and rejects a %s destination before copying', async change => {
  const copy = vi.fn(), replace = vi.fn(), close = vi.fn();
  vi.stubGlobal('navigator', { clipboard: { writeText: copy } });
  vi.spyOn(window, 'open').mockReturnValue({ opener: null, close, location: { replace } } as unknown as Window);
  let reads = 0;
  vi.stubGlobal('fetch', vi.fn(async () => Response.json([++reads === 1 ? destination : change === 'offline' ? { ...destination, availabilityStatus: 'OFFLINE' } : { ...destination, destinationUrl: 'https://example.invalid/changed' }])));
  render(<PreparedUse draft={draft} onClose={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Copiar y abrir Destino del catálogo' }));
  await screen.findByRole('alert'); expect(copy).not.toHaveBeenCalled(); expect(replace).not.toHaveBeenCalled(); expect(close).toHaveBeenCalledOnce();
});

it('prepared use waits for verified clipboard success before opening the catalog destination', async () => {
  const order: string[] = []; const replace = vi.fn(() => order.push('open'));
  vi.stubGlobal('navigator', { clipboard: { writeText: vi.fn(async (text: string) => { expect(text).toBe(draft.compiledText); order.push('copy'); }) } });
  vi.spyOn(window, 'open').mockReturnValue({ opener: null, close: vi.fn(), location: { replace } } as unknown as Window);
  vi.stubGlobal('fetch', vi.fn(async () => Response.json([destination])));
  render(<PreparedUse draft={draft} onClose={vi.fn()} />); fireEvent.click(await screen.findByRole('button', { name: 'Copiar y abrir Destino del catálogo' }));
  await waitFor(() => expect(replace).toHaveBeenCalledWith(destination.destinationUrl)); expect(order).toEqual(['copy', 'open']);
});

it('preference edits are local until explicit save and forgetting clears only preferences', async () => {
  const calls: { method: string; body: unknown }[] = [];
  vi.stubGlobal('fetch', vi.fn(async (_: string, options: RequestInit) => {
    calls.push({ method: options.method ?? 'GET', body: options.body ? JSON.parse(String(options.body)) : null });
    return options.method === 'DELETE' ? new Response(null, { status: 204 }) : Response.json(options.body ? JSON.parse(String(options.body)).preferences : { language: null, format: null, providerId: null });
  }));
  render(<NovaPreferences />); fireEvent.click(screen.getByText('Preferencias de NOVA')); await act(async () => {});
  fireEvent.change(screen.getByLabelText('Idioma de respuesta'), { target: { value: 'en' } }); expect(calls).toHaveLength(1);
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar preferencias' })); await screen.findByText('Preferencias confirmadas en este equipo.');
  expect(calls[1]).toEqual({ method: 'PUT', body: { preferences: { language: 'en', format: null, providerId: null }, confirmed: true } });
  fireEvent.click(screen.getByRole('button', { name: 'Eliminar preferencias' })); await screen.findByText('Preferencias eliminadas.');
  expect(calls[2].method).toBe('DELETE'); expect(screen.getByLabelText('Idioma de respuesta')).toHaveProperty('value', '');
});
