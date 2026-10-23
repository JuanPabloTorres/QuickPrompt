import { useLayoutEffect } from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { EditorPreparation } from '../src/features/prompts/EditorPreparation';
import { PromptFocus } from '../src/features/prompts/PromptFocus';
import type { Prompt, PromptDraft } from '../src/services/api';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
it('copies only the current compiled draft and disables copy immediately after a text change', async () => {
  const writeText = vi.fn().mockResolvedValue(undefined);
  vi.stubGlobal('navigator', { clipboard: { writeText } });
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    const body = JSON.parse(options!.body as string);
    return Response.json(path.endsWith('/inspect') ? { variableNames: [] } : { text: body.templateText, missingVariables: [] });
  }));
  const values = {};
  function Frame({ text }: { text: string }) { return <EditorPreparation source={null} draft={{ title: 'Copia', description: '', categoryId: 0, templateText: text }} values={values} onChange={vi.fn()} onValues={vi.fn()} onUse={vi.fn()} />; }
  const view = render(<Frame text="Texto completo 🚀." />);
  expect(screen.getByRole('button', { name: 'Copiar borrador' })).toHaveProperty('disabled', true);
  expect(screen.getByRole('button', { name: 'Descargar texto (.txt)' })).toHaveProperty('disabled', true);
  await screen.findByTestId('compile-ready'); fireEvent.click(screen.getByRole('button', { name: 'Copiar borrador' }));
  await waitFor(() => expect(writeText).toHaveBeenCalledWith('Texto completo 🚀.'));
  view.rerender(<Frame text="Contenido nuevo." />);
  expect(screen.getByRole('button', { name: 'Copiar borrador' })).toHaveProperty('disabled', true);
  expect(screen.getByRole('button', { name: 'Descargar texto (.txt)' })).toHaveProperty('disabled', true);
  fireEvent.click(screen.getByRole('button', { name: 'Copiar borrador' })); expect(writeText).toHaveBeenCalledTimes(1);
  await screen.findByTestId('compile-ready'); fireEvent.click(screen.getByRole('button', { name: 'Copiar borrador' }));
  await waitFor(() => expect(writeText).toHaveBeenLastCalledWith('Contenido nuevo.'));
});
it('invalidates editor readiness in the commit that changes its text, before effects run', async () => {
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    const body = JSON.parse(options!.body as string);
    return Response.json(path.endsWith('/inspect') ? { variableNames: [] } : { text: body.templateText, missingVariables: [] });
  }));
  const checks: boolean[] = [], values = {};
  function Frame({ text }: { text: string }) {
    useLayoutEffect(() => { checks.push(!!document.querySelector('[data-testid="compile-ready"]')); }, [text]);
    const draft: PromptDraft = { title: 'Prueba', description: '', templateText: text, categoryId: 0 };
    return <EditorPreparation source={null} draft={draft} values={values} onChange={vi.fn()} onValues={vi.fn()} onUse={vi.fn()} />;
  }
  const view = render(<Frame text="Primera instrucción." />); await screen.findByTestId('compile-ready');
  view.rerender(<Frame text="Segunda instrucción." />);
  expect(checks.at(-1)).toBe(false); expect(screen.queryByTestId('compile-ready')).toBeNull();
  await screen.findByTestId('compile-ready'); expect(screen.getByTestId('compiled-preview').textContent).toBe('Segunda instrucción.');
});
it('never exposes a previously compiled value as ready in a changed focus view', async () => {
  vi.stubGlobal('fetch', vi.fn(async (_path: string, options?: RequestInit) => Response.json({ text: 'Explica ' + JSON.parse(options!.body as string).values.tema + '.', templateRevision: 1 })));
  const prompt: Prompt = { id: '55efc1e9-f51d-424e-9e3a-cf543fdf0ceb', title: 'Ejemplo', templateText: 'Explica <tema>.', description: '', revision: 1, categoryId: 0, isFavorite: false, isArchived: false, variableNames: ['tema'], defaults: {}, uses: 0, lastUsedAt: null, createdAt: '', updatedAt: '' };
  const checks: boolean[] = [];
  function Frame({ values }: { values: Record<string, string> }) {
    useLayoutEffect(() => { checks.push(!!document.querySelector('[data-testid="compile-ready"]')); }, [values]);
    return <PromptFocus prompt={prompt} values={values} onValues={vi.fn()} onBack={vi.fn()} onEdit={vi.fn()} onAction={vi.fn()} onDelete={vi.fn()} onUsed={vi.fn()} notify={vi.fn()} />;
  }
  const view = render(<Frame values={{ tema: 'SQLite' }} />); await screen.findByTestId('compile-ready');
  view.rerender(<Frame values={{ tema: 'React' }} />); expect(checks.at(-1)).toBe(false);
  await waitFor(() => expect(screen.getByTestId('compiled-preview').textContent).toBe('Explica React.'));
});
