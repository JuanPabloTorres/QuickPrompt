import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { useState } from 'react';
import { afterEach, expect, it, vi } from 'vitest';
import { PromptVersions } from '../src/features/prompts/PromptVersions';
import type { Prompt } from '../src/services/api';

const prompt: Prompt = { id: '40000000-0000-4000-8000-000000000001', title: 'Actual', description: 'Descripción actual', templateText: 'Revisa <material>.', categoryId: 0, revision: 2,
  isFavorite: true, isArchived: false, uses: 3, lastUsedAt: null, createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-02T00:00:00Z', variableNames: ['material'], defaults: { material: 'código' } };
const versions = [
  { revision: 2, createdAt: prompt.updatedAt, snapshot: { title: prompt.title, description: prompt.description, template: prompt.templateText, variables: prompt.defaults } },
  { revision: 1, createdAt: prompt.createdAt, snapshot: { title: 'Órbita 🛰️', description: 'Anterior', template: 'Explica <tema>.\nNo inventes datos.', variables: { tema: 'ciencia α\nβ' } } },
];
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('compares actual history and cancels without writing or navigating', async () => {
  const fetcher = vi.fn(async () => Response.json(versions)); vi.stubGlobal('fetch', fetcher);
  const close = vi.fn(), restored = vi.fn(); render(<PromptVersions prompt={prompt} onClose={close} onRestored={restored} />);
  const selector = await screen.findByRole('combobox', { name: 'Versión para comparar' });
  fireEvent.change(selector, { target: { value: '1' } });
  expect(within(screen.getByRole('region', { name: 'Versión seleccionada' })).getByText(versions[1].snapshot.template, { normalizer: text => text })).toBeTruthy();
  expect(within(screen.getByRole('region', { name: 'Versión actual' })).getByText(prompt.templateText)).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Cancelar' }));
  expect(close).toHaveBeenCalledOnce(); expect(restored).not.toHaveBeenCalled(); expect(fetcher).toHaveBeenCalledTimes(1);
});

it('restores only after explicit confirmation and sends historical plus current revisions, never a client snapshot', async () => {
  const result = { ...prompt, title: versions[1].snapshot.title, templateText: versions[1].snapshot.template, revision: 3 };
  const fetcher = vi.fn(async (_url: string, options: RequestInit) => Response.json(options?.method === 'POST' ? result : versions)); vi.stubGlobal('fetch', fetcher);
  const restored = vi.fn(); render(<PromptVersions prompt={prompt} onClose={vi.fn()} onRestored={restored} />);
  fireEvent.change(await screen.findByRole('combobox'), { target: { value: '1' } });
  expect(fetcher).toHaveBeenCalledTimes(1);
  fireEvent.click(screen.getByRole('button', { name: 'Restaurar versión 1' }));
  await vi.waitFor(() => expect(restored).toHaveBeenCalledWith(result));
  expect(fetcher.mock.calls[1][0]).toBe(`/api/v1/prompts/${prompt.id}/versions/1/restore`);
  expect(JSON.parse(fetcher.mock.calls[1][1].body as string)).toEqual({ revision: 2, confirmed: true });
});

it('keeps the comparison and current content after a conflict without retrying a write', async () => {
  const fetcher = vi.fn(async (_url: string, options: RequestInit) => options?.method === 'POST'
    ? Response.json({ detail: 'El prompt cambió. Recarga antes de guardar.' }, { status: 409 }) : Response.json(versions)); vi.stubGlobal('fetch', fetcher);
  const restored = vi.fn(); render(<PromptVersions prompt={prompt} onClose={vi.fn()} onRestored={restored} />);
  fireEvent.change(await screen.findByRole('combobox'), { target: { value: '1' } });
  fireEvent.click(screen.getByRole('button', { name: 'Restaurar versión 1' }));
  expect(await screen.findByRole('alert')).toHaveProperty('textContent', 'El prompt cambió. Recarga antes de guardar.');
  expect(screen.getByRole('combobox')).toHaveProperty('value', '1'); expect(restored).not.toHaveBeenCalled();
  expect(fetcher).toHaveBeenCalledTimes(2);
});

it('refreshes the current revision after a conflict and requires a new explicit selection before another restore', async () => {
  const fresh = { ...prompt, revision: 3, templateText: 'Edición humana protegida.' };
  let versionReads = 0;
  const fetcher = vi.fn(async (url: string, options: RequestInit) => {
    if (options?.method === 'POST') return Response.json({ detail: 'El prompt cambió.' }, { status: 409 });
    if (url.endsWith('/versions')) return Response.json(++versionReads === 1 ? versions : [
      { revision: 3, createdAt: fresh.updatedAt, snapshot: { title: fresh.title, description: fresh.description, template: fresh.templateText, variables: fresh.defaults } }, ...versions,
    ]);
    return Response.json(fresh);
  });
  vi.stubGlobal('fetch', fetcher);
  function Comparison() { const [current, setCurrent] = useState(prompt); return <PromptVersions prompt={current} onClose={vi.fn()} onRestored={vi.fn()} onCurrentChanged={setCurrent} />; }
  render(<Comparison />);
  fireEvent.change(await screen.findByRole('combobox'), { target: { value: '1' } });
  fireEvent.click(screen.getByRole('button', { name: 'Restaurar versión 1' }));
  await screen.findByRole('alert'); fireEvent.click(screen.getByRole('button', { name: 'Actualizar comparación' }));
  await screen.findByText(fresh.templateText, { selector: '[aria-label="Versión actual"] pre' });
  expect(screen.getByRole('combobox')).toHaveProperty('value', '3');
  expect(screen.getByRole('button', { name: 'Restaurar versión 3' })).toHaveProperty('disabled', true);
  expect(fetcher.mock.calls.filter(([, options]) => options?.method === 'POST')).toHaveLength(1);
});
