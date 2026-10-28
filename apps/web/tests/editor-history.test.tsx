import { useState } from 'react';
import { act, cleanup, fireEvent, render, renderHook, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { InlinePromptEditor } from '../src/features/prompts/InlinePromptEditor';
import { EditorPreparation } from '../src/features/prompts/EditorPreparation';
import { useEditorHistory } from '../src/features/prompts/useEditorHistory';
import type { PromptDraft } from '../src/services/api';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
const initial: PromptDraft = { title: 'Inicial', templateText: 'Explica C# & .NET', description: '', categoryId: 0, revision: 1 };
function routes(handler?: (path: string, options?: RequestInit) => Promise<Response>) {
  const fetch = vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/categories')) return Response.json([{ id: 0, name: 'General' }]);
    if (path.endsWith('/inspect')) return Response.json({ variableNames: [] });
    if (handler) return handler(path, options);
    throw Error('Unexpected request: ' + path);
  });
  vi.stubGlobal('fetch', fetch); return fetch;
}
function Frame({ seed = initial, identity = 'item', locked = false }: { seed?: PromptDraft; identity?: string; locked?: boolean }) {
  const [draft, setDraft] = useState(seed);
  return <InlinePromptEditor draft={draft} onChange={setDraft} promptId={identity} metadataLocked={locked} onSave={vi.fn()} onBack={vi.fn()} saving={false} error="" editing />;
}
function select(start: number, end: number) {
  const field = screen.getByLabelText('Contenido') as HTMLTextAreaElement;
  field.focus(); field.setSelectionRange(start, end); fireEvent.select(field); return field;
}

it('undo/redo restores complete form changes, keeps literal Unicode, and a new edit discards redo', () => {
  routes(); render(<Frame />);
  fireEvent.change(screen.getByLabelText('Título'), { target: { value: 'Órbita 🛰️' } });
  const field = screen.getByLabelText('Contenido'); fireEvent.change(field, { target: { value: 'α\n李 <' } });
  fireEvent.click(screen.getByRole('button', { name: 'Deshacer' }));
  expect(field).toHaveProperty('value', initial.templateText); expect(screen.getByLabelText('Título')).toHaveProperty('value', 'Órbita 🛰️');
  fireEvent.click(screen.getByRole('button', { name: 'Deshacer' })); expect(screen.getByLabelText('Título')).toHaveProperty('value', 'Inicial');
  fireEvent.click(screen.getByRole('button', { name: 'Rehacer' })); fireEvent.click(screen.getByRole('button', { name: 'Rehacer' })); expect(field).toHaveProperty('value', 'α\n李 <');
  fireEvent.keyDown(field, { key: 'z', ctrlKey: true }); expect(field).toHaveProperty('value', initial.templateText);
  fireEvent.keyDown(field, { key: 'z', ctrlKey: true, shiftKey: true }); expect(field).toHaveProperty('value', 'α\n李 <');
  fireEvent.keyDown(field, { key: 'z', ctrlKey: true }); fireEvent.change(field, { target: { value: 'Rama nueva' } });
  expect(screen.getByRole('button', { name: 'Rehacer' })).toHaveProperty('disabled', true);
});

it('retains the original literal through create, rename, undo/redo and removing the renamed variable', async () => {
  let transformations = 0;
  const fetch = routes(async (path, options) => {
    const input = JSON.parse(options!.body as string);
    if (path.endsWith('/rename-variable')) {
      expect(input).toMatchObject({ templateText: 'Explica <tecnologia>', name: 'tema', defaults: {} });
      return Response.json({ templateText: 'Explica <tema>', variableNames: ['tema'], defaults: {}, start: 8, end: 14, previousName: 'tecnologia', name: 'tema' });
    }
    expect(path).toMatch(/\/toggle-variable$/);
    if (transformations++ === 0) return Response.json({ templateText: 'Explica <tecnologia>', variableNames: ['tecnologia'], start: 8, end: 20, action: 'created', name: 'tecnologia', originalText: 'C# & .NET' });
    expect(input.originalText).toBe('C# & .NET');
    return Response.json({ templateText: initial.templateText, variableNames: [], start: 8, end: 17, action: 'removed', name: 'tema', originalText: null });
  });
  render(<Frame />); const field = select(8, 17);
  fireEvent.click(screen.getByRole('button', { name: 'Crear / quitar variable' })); await waitFor(() => expect(field.value).toBe('Explica <tecnologia>'));
  select(8, 20); fireEvent.change(screen.getByLabelText('Nuevo nombre de variable'), { target: { value: 'tema' } });
  fireEvent.click(screen.getByRole('button', { name: 'Renombrar variable' })); await waitFor(() => expect(field.value).toBe('Explica <tema>'));
  fireEvent.click(screen.getByRole('button', { name: 'Deshacer' })); expect(field.value).toBe('Explica <tecnologia>');
  fireEvent.click(screen.getByRole('button', { name: 'Rehacer' })); expect(field.value).toBe('Explica <tema>');
  select(8, 14); fireEvent.click(screen.getByRole('button', { name: 'Crear / quitar variable' })); await waitFor(() => expect(field.value).toBe(initial.templateText));
  expect(fetch.mock.calls.every(([path]) => !path.includes('/prompts'))).toBe(true);
});

it('an invalid rename preserves the draft and an active NOVA proposal cannot rename metadata', async () => {
  const fetch = routes(async () => Response.json({ detail: 'Nombre ya utilizado' }, { status: 400 }));
  const view = render(<Frame seed={{ ...initial, templateText: '<a> <b>' }} />);
  select(0, 3); fireEvent.change(screen.getByLabelText('Nuevo nombre de variable'), { target: { value: 'b' } });
  fireEvent.click(screen.getByRole('button', { name: 'Renombrar variable' })); await screen.findByText('Nombre ya utilizado');
  expect(screen.getByLabelText('Contenido')).toHaveProperty('value', '<a> <b>');
  expect(screen.getByRole('button', { name: 'Deshacer' })).toHaveProperty('disabled', true);
  view.rerender(<Frame locked />); const count = fetch.mock.calls.length;
  fireEvent.click(screen.getByRole('button', { name: 'Renombrar variable' }));
  expect(screen.getByRole('button', { name: 'Renombrar variable' })).toHaveProperty('disabled', true); expect(fetch.mock.calls.length).toBe(count);
});

it('bounds local history and resets it when identity or revision changes', () => {
  const onChange = vi.fn();
  const view = renderHook(({ draft, id }) => useEditorHistory(draft, id, onChange), { initialProps: { draft: initial, id: 'item:1' } });
  act(() => { for (let index = 0; index < 150; index++) view.result.current.commit({ ...initial, templateText: 'Paso ' + index }); });
  let undone = 0; act(() => { while (view.result.current.move('undo')) undone++; });
  expect(undone).toBe(100); expect(view.result.current.redo).toBe(true);
  view.rerender({ draft: { ...initial, revision: 2 }, id: 'item:2' }); expect(view.result.current.undo).toBe(false); expect(view.result.current.redo).toBe(false);
  act(() => view.result.current.commit({ ...initial, templateText: 'Cambio' }));
  view.rerender({ draft: initial, id: 'another:1' }); expect(view.result.current.undo).toBe(false);
});

it('rename reads the real textarea selection even after focus moves to the new-name input', async () => {
  const fetch = routes(async (path, options) => {
    expect(path).toMatch(/\/rename-variable$/);
    expect(JSON.parse(options!.body as string)).toMatchObject({ start: 0, end: 3, name: 'b' });
    return Response.json({ templateText: '<b>', variableNames: ['b'], defaults: {}, start: 0, end: 3, previousName: 'a', name: 'b' });
  });
  render(<Frame seed={{ ...initial, templateText: '<a>' }} />);
  const field = screen.getByLabelText('Contenido') as HTMLTextAreaElement;
  field.focus(); field.setSelectionRange(0, 3); // No synthetic select callback: the DOM is authoritative.
  fireEvent.change(screen.getByLabelText('Nuevo nombre de variable'), { target: { value: 'b' } });
  fireEvent.click(screen.getByRole('button', { name: 'Renombrar variable' }));
  await waitFor(() => expect(field.value).toBe('<b>'));
  expect(fetch.mock.calls.filter(([path]) => path.endsWith('/rename-variable')).length).toBe(1);
});

it('a rename response cannot overwrite a newer draft or cross into NOVA-controlled metadata', async () => {
  let resolve!: (response: Response) => void;
  const pending = new Promise<Response>(done => { resolve = done; });
  const fetch = routes(async path => { expect(path).toMatch(/\/rename-variable$/); return pending; });
  const onChange = vi.fn(), draft = { ...initial, templateText: '<a>' };
  const props = { draft, onChange, onSave: vi.fn(), onBack: vi.fn(), saving: false, error: '', editing: true };
  const view = render(<InlinePromptEditor {...props} />);
  select(0, 3); fireEvent.change(screen.getByLabelText('Nuevo nombre de variable'), { target: { value: 'b' } });
  fireEvent.click(screen.getByRole('button', { name: 'Renombrar variable' }));
  expect(fetch.mock.calls.some(([path]) => path.endsWith('/rename-variable'))).toBe(true);
  view.rerender(<InlinePromptEditor {...props} draft={{ ...draft, title: 'Humano vigente' }} metadataLocked />);
  await act(async () => { resolve(Response.json({ templateText: '<b>', variableNames: ['b'], defaults: {}, start: 0, end: 3, previousName: 'a', name: 'b' })); await pending; });
  expect(onChange).not.toHaveBeenCalled(); expect(screen.getByLabelText('Contenido')).toHaveProperty('value', '<a>');
  expect(screen.getByLabelText('Título')).toHaveProperty('value', 'Humano vigente'); expect(screen.getByRole('button', { name: 'Renombrar variable' })).toHaveProperty('disabled', true);
});

it('bounds total retained text, not only the number of history steps', () => {
  const draft = { ...initial, defaults: { dato: 'x'.repeat(8000) }, templateText: 'a'.repeat(32000) };
  const view = renderHook(() => useEditorHistory(draft, 'bounded', vi.fn()));
  act(() => { for (let index = 0; index < 100; index++) view.result.current.commit({ ...draft, templateText: String(index).padStart(4, '0') + 'a'.repeat(31996) }); });
  let undone = 0; act(() => { while (view.result.current.move('undo')) undone++; });
  expect(undone).toBeGreaterThan(0); expect(undone).toBeLessThan(100);
});

it('draft preview and fields use explicitly renamed defaults and invalidate old readiness', async () => {
  const values = {}, onValues = vi.fn();
  // The inspector response carries the domain-owned variable list.
  const fetch = vi.fn(async (path: string, options?: RequestInit) => path.endsWith('/inspect') ? Response.json({ variableNames: ['tema'] }) : Response.json({ text: 'Explica ' + JSON.parse(options!.body as string).values.tema + '.', missingVariables: [] }));
  vi.stubGlobal('fetch', fetch);
  const props = { source: null, onChange: vi.fn(), values, onValues, onUse: vi.fn() };
  const view = render(<EditorPreparation {...props} draft={{ ...initial, templateText: 'Explica <tema>.', defaults: { tema: 'α' } }} />);
  await screen.findByTestId('compile-ready'); expect(screen.getByTestId('compiled-preview').textContent).toBe('Explica α.'); expect(screen.getByTestId('variable-field')).toHaveProperty('value', 'α');
  view.rerender(<EditorPreparation {...props} draft={{ ...initial, templateText: 'Explica <tema>.', defaults: { tema: 'β' } }} />);
  expect(screen.queryByTestId('compile-ready')).toBeNull(); await screen.findByTestId('compile-ready'); expect(screen.getByTestId('compiled-preview').textContent).toBe('Explica β.');
});
