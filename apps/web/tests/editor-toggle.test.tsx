import { useState } from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { InlinePromptEditor } from '../src/features/prompts/InlinePromptEditor';
import { LearningCenter } from '../src/features/help/LearningCenter';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
it('one button wraps the selected text and restores the original through the server contract', async () => {
  let changes = 0;
  const fetch = vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/inspect')) return Response.json({ variableNames: [] });
    const input = JSON.parse(options?.body as string) as { templateText: string; start: number; end: number; originalText?: string };
    expect(input.start).toBe(8); expect(input.end).toBe(changes ? 14 : 12);
    if (changes++) { expect(input.originalText).toBe('tema'); return Response.json({ templateText: 'Explica tema.', variableNames: [], start: 8, end: 12, action: 'removed', name: 'tema', originalText: null }); }
    return Response.json({ templateText: 'Explica <tema>.', variableNames: ['tema'], start: 8, end: 14, action: 'created', name: 'tema', originalText: 'tema' });
  }); vi.stubGlobal('fetch', fetch);
  function Editor() { const [draft, setDraft] = useState({ title: 'Mi prompt', templateText: 'Explica tema.', description: '', categoryId: 0 }); return <InlinePromptEditor draft={draft} onChange={setDraft} onSave={vi.fn()} onBack={vi.fn()} saving={false} error="" editing={false} />; }
  render(<Editor />); const content = screen.getByLabelText('Contenido') as HTMLTextAreaElement;
  content.setSelectionRange(8, 12); fireEvent.select(content); fireEvent.click(screen.getByRole('button', { name: 'Crear / quitar variable' }));
  await waitFor(() => expect(content.value).toBe('Explica <tema>.'));
  await waitFor(() => expect(content.selectionStart).toBe(8)); content.setSelectionRange(8, 14); fireEvent.select(content);
  fireEvent.click(screen.getByRole('button', { name: 'Crear / quitar variable' }));
  await waitFor(() => expect(content.value).toBe('Explica tema.')); expect(changes).toBe(2);
});
it('the guide distinguishes a prompt from a model response and offers four accessible video players', () => {
  render(<LearningCenter onClose={vi.fn()} onImported={vi.fn()} />);
  expect(screen.getByText('NOVA prepara el prompt.')).not.toBeNull(); expect(document.querySelectorAll('video[controls]')).toHaveLength(4);
  expect(screen.getByRole('button', { name: 'Añadir biblioteca práctica' })).not.toBeNull(); expect(screen.getByRole('button', { name: 'Volver a mis prompts' })).not.toBeNull();
});
