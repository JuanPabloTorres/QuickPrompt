import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PromptFocus } from '../src/features/prompts/PromptFocus';
import { Dialog } from '../src/components/ui/Dialog';
import type { Prompt } from '../src/services/api';

const prompt: Prompt = { id: '55efc1e9-f51d-424e-9e3a-cf543fdf0ceb', title: 'Repeat Unicode', description: '', templateText: '<x> · <x>', categoryId: 0, isFavorite: false, isArchived: false, revision: 3, createdAt: '', updatedAt: '', uses: 0, lastUsedAt: null, variableNames: ['x'], defaults: {} };
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
function focus(onUsed = vi.fn(), notify = vi.fn()) {
  return render(<PromptFocus prompt={prompt} values={{ x: 'α\nβ <x>' }} onValues={vi.fn()} onBack={vi.fn()} onEdit={vi.fn()} onAction={vi.fn()} onDelete={vi.fn()} onUsed={onUsed} notify={notify} />);
}
describe('Copy acknowledgment', () => {
  it('waits for required variables without making failing preview requests', async () => {
    const fetch = vi.fn(); vi.stubGlobal('fetch', fetch);
    render(<PromptFocus prompt={prompt} values={{}} onValues={vi.fn()} onBack={vi.fn()} onEdit={vi.fn()} onAction={vi.fn()} onDelete={vi.fn()} onUsed={vi.fn()} notify={vi.fn()} />);
    expect((screen.getByRole('button', { name: 'Copiar prompt' }) as HTMLButtonElement).disabled).toBe(true);
    await new Promise(resolve => setTimeout(resolve, 250));
    expect(fetch).not.toHaveBeenCalled(); expect(screen.queryByRole('alert')).toBeNull();
  });
  it('does not register usage when the browser refuses the clipboard', async () => {
    const calls: string[] = [];
    vi.stubGlobal('fetch', vi.fn(async (url: string) => { calls.push(url); return new Response(JSON.stringify({ text: 'α\nβ <x> · α\nβ <x>', templateRevision: 3 })); }));
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: vi.fn().mockRejectedValue(new Error('Clipboard denied')) } });
    const onUsed = vi.fn(); focus(onUsed);
    fireEvent.click(screen.getByRole('button', { name: 'Copiar prompt' }));
    await screen.findByText('Clipboard denied');
    expect(calls.some(url => url.endsWith('/usage'))).toBe(false);
    expect(onUsed).not.toHaveBeenCalled();
  });
  it('copies the exact compiled snapshot before recording use', async () => {
    const events: string[] = [];
    const text = 'α\nβ <x> · α\nβ <x>';
    vi.stubGlobal('fetch', vi.fn(async (url: string) => {
      if (url.endsWith('/usage')) { events.push('usage'); return new Response(null, { status: 204 }); }
      return new Response(JSON.stringify({ text, templateRevision: 3 }));
    }));
    const clipboard = vi.fn(async (value: string) => { expect(value).toBe(text); events.push('clipboard'); });
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: clipboard } });
    const onUsed = vi.fn(); focus(onUsed);
    fireEvent.click(screen.getByRole('button', { name: 'Copiar prompt' }));
    await waitFor(() => expect(onUsed).toHaveBeenCalledOnce());
    expect(events).toEqual(['clipboard', 'usage']);
  });
  it('does not write after a revision conflict', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ detail: 'El prompt cambió.' }), { status: 409 })));
    const clipboard = vi.fn(); Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: clipboard } });
    focus(); fireEvent.click(screen.getByRole('button', { name: 'Copiar prompt' }));
    await screen.findByRole('alert'); expect(clipboard).not.toHaveBeenCalled();
  });
});
it('confirmation dialog has a name and Escape closes it', async () => {
  const onOpenChange = vi.fn();
  render(<Dialog open onOpenChange={onOpenChange} title="Eliminar" description="Confirma la eliminación."><button>Cancelar</button></Dialog>);
  expect(screen.getByRole('dialog', { name: 'Eliminar' })).toBeTruthy();
  fireEvent.keyDown(document, { key: 'Escape' });
  await waitFor(() => expect(onOpenChange).toHaveBeenCalledWith(false));
});
