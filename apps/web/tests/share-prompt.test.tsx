import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { SharePrompt } from '../src/features/prompts/SharePrompt';
import type { Prompt } from '../src/services/api';

const prompt: Prompt = { id: '40000000-0000-4000-8000-000000000001', title: 'Guía pública', description: 'Descripción', templateText: 'Explica <tema>.\nαβ',
  categoryId: 0, revision: 2, isFavorite: false, isArchived: false, uses: 8, lastUsedAt: null, createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-02T00:00:00Z', variableNames: ['tema'], defaults: { tema: 'PRIVATE_DEFAULT' } };
const packet = { format: 'quickprompt-template', formatVersion: 1, prompt: { title: prompt.title, template: prompt.templateText, description: prompt.description, category: 'General' } };
afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.restoreAllMocks(); });

it('previews only a revision-bound portable package and cancels without downloading or writing', async () => {
  const fetcher = vi.fn(async (_url: string) => Response.json(packet)); vi.stubGlobal('fetch', fetcher);
  const close = vi.fn(); render(<SharePrompt prompt={prompt} onClose={close} onReload={vi.fn()} />);
  await screen.findByText(prompt.templateText, { normalizer: text => text });
  expect(screen.queryByText('PRIVATE_DEFAULT')).toBeNull();
  expect(fetcher.mock.calls[0][0]).toBe(`/api/v1/prompts/${prompt.id}/share?revision=2`);
  fireEvent.click(screen.getByRole('button', { name: 'Cancelar' })); expect(close).toHaveBeenCalledOnce();
  expect(fetcher).toHaveBeenCalledTimes(1);
});

it('rejects a response that contains non-portable private fields', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => Response.json({ ...packet, defaults: prompt.defaults })));
  render(<SharePrompt prompt={prompt} onClose={vi.fn()} onReload={vi.fn()} />);
  await screen.findByRole('alert');
  expect(screen.getByRole('button', { name: 'Descargar plantilla (.json)' })).toHaveProperty('disabled', true);
  expect(screen.queryByText('PRIVATE_DEFAULT')).toBeNull();
});

it('downloads only the reviewed portable bytes after an explicit click', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => Response.json(packet)));
  let blob: Blob | undefined;
  const create = vi.fn((value: Blob) => { blob = value; return 'blob:portable-template'; });
  vi.stubGlobal('URL', { createObjectURL: create, revokeObjectURL: vi.fn() });
  const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) { expect(this.download).toBe('quickprompt-plantilla.json'); });
  render(<SharePrompt prompt={prompt} onClose={vi.fn()} onReload={vi.fn()} />);
  await screen.findByText(prompt.templateText, { normalizer: text => text }); expect(create).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'Descargar plantilla (.json)' })); expect(click).toHaveBeenCalledOnce();
  const text = await new Promise<string>(resolve => { const reader = new FileReader(); reader.onload = () => resolve(reader.result as string); reader.readAsText(blob!); });
  expect(JSON.parse(text)).toEqual(packet); expect(text).not.toContain('PRIVATE_DEFAULT'); expect(text).not.toContain(prompt.id);
});

it('a late response from an earlier revision never becomes the downloadable preview', async () => {
  let resolveOld!: (response: Response) => void;
  vi.stubGlobal('fetch', vi.fn(async (url: string) => url.endsWith('revision=2') ? new Promise<Response>(resolve => { resolveOld = resolve; })
    : Response.json({ ...packet, prompt: { ...packet.prompt, template: 'Texto vigente.' } })));
  const view = render(<SharePrompt prompt={prompt} onClose={vi.fn()} onReload={vi.fn()} />);
  view.rerender(<SharePrompt prompt={{ ...prompt, revision: 3, templateText: 'Texto vigente.' }} onClose={vi.fn()} onReload={vi.fn()} />);
  await screen.findByText('Texto vigente.'); resolveOld(Response.json(packet));
  await vi.waitFor(() => expect(screen.queryByText(prompt.templateText, { normalizer: text => text })).toBeNull());
});
