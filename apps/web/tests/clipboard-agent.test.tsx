import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { PromptFocus } from '../src/features/prompts/PromptFocus';
import { EditorPreparation } from '../src/features/prompts/EditorPreparation';
import { AgentUiDriver, readUiState, type UiAction } from '../src/features/nova/AgentUiDriver';
import type { Prompt } from '../src/services/api';

const prompt: Prompt = { id: '55efc1e9-f51d-424e-9e3a-cf543fdf0ceb', title: 'Fixture', templateText: 'Explica <tema>.', description: '', revision: 3, categoryId: 0, isFavorite: false, isArchived: false, variableNames: ['tema'], defaults: {}, uses: 0, lastUsedAt: null, createdAt: '', updatedAt: '' };
const values = { tema: 'energía' };
const props = { prompt, values, onValues: vi.fn(), onBack: vi.fn(), onEdit: vi.fn(), onAction: vi.fn(), onDelete: vi.fn(), onUsed: vi.fn(), notify: vi.fn() };
const action = (tool = 'ui_copy_compiled_prompt'): UiAction => ({ tool, arguments: { promptId: prompt.id }, target: prompt.id, runId: crypto.randomUUID(), actionId: crypto.randomUUID(), sequence: 1, timestamp: new Date().toISOString(), executionStatus: 'PENDING', verificationStatus: 'PENDING' });
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });
function setup(write = vi.fn().mockResolvedValue(undefined)) {
  vi.stubGlobal('navigator', { clipboard: { writeText: write } });
  const usage: unknown[] = [];
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    const body = JSON.parse(options!.body as string);
    if (path.endsWith('/usage')) { usage.push(body); return new Response(null, { status: 204 }); }
    if (path.endsWith('/inspect')) return Response.json({ variableNames: ['tema'] });
    return Response.json({ text: `Explica ${body.values.tema}.`, templateRevision: 3, missingVariables: [] });
  }));
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([{ top: 0, left: 0, width: 100, height: 30 }] as unknown as DOMRectList);
  HTMLElement.prototype.scrollIntoView = vi.fn(); vi.stubGlobal('matchMedia', () => ({ matches: true }));
  return { write, usage };
}

it('the real driver clicks the owning Copy control and demands a fresh exact receipt on each request', async () => {
  const { write, usage } = setup(); render(<PromptFocus {...props} />); await screen.findByTestId('compile-ready');
  const driver = new AgentUiDriver(vi.fn()); driver.speed = 'fast';
  const one = await driver.execute(action());
  expect(one.clipboard).toEqual({ operationId: expect.any(String), promptId: prompt.id, revision: 3, text: 'Explica energía.' });
  await waitFor(() => expect(usage).toHaveLength(1));
  const two = await driver.execute({ ...action(), sequence: 2 });
  expect(two.clipboard?.operationId).not.toBe(one.clipboard?.operationId);
  expect(write).toHaveBeenCalledTimes(2); expect(write).toHaveBeenLastCalledWith('Explica energía.');
  expect(usage).toEqual([expect.objectContaining({ operationId: one.clipboard!.operationId, expectedText: 'Explica energía.' }), expect.objectContaining({ operationId: two.clipboard!.operationId, expectedText: 'Explica energía.' })]);
});

it('preparing a copy only inspects the preview and never writes the clipboard', async () => {
  const { write, usage } = setup(); render(<PromptFocus {...props} />); await screen.findByTestId('compile-ready');
  const driver = new AgentUiDriver(vi.fn()); driver.speed = 'fast';
  const state = await driver.execute(action('ui_show_compiled_prompt'));
  expect(state.clipboard).toBeNull(); expect(write).not.toHaveBeenCalled(); expect(usage).toEqual([]);
});

it('template copying preserves unfilled markers and produces a typed receipt without compiled readiness or a recorded use', async () => {
  const { write, usage } = setup(); render(<PromptFocus {...props} values={{}} />);
  const driver = new AgentUiDriver(vi.fn()); driver.speed = 'fast';
  const preview = await driver.execute(action('ui_show_template'));
  expect(preview.templatePreviewText).toBe(prompt.templateText);
  expect(preview.compiledReady).toBe(false); expect(write).not.toHaveBeenCalled();
  const copied = await driver.execute(action('ui_copy_template'));
  expect(copied.clipboard).toEqual({ operationId: expect.any(String), promptId: prompt.id, revision: prompt.revision,
    text: prompt.templateText, copyContent: 'TEMPLATE' });
  expect(write).toHaveBeenCalledWith('Explica <tema>.'); expect(usage).toEqual([]);
});

it('a rendered copy after template copy publishes its new receipt only after asynchronous compilation and the write finish', async () => {
  const { write } = setup(); render(<PromptFocus {...props} />); await screen.findByTestId('compile-ready');
  const driver = new AgentUiDriver(vi.fn()); driver.speed = 'fast';
  await driver.execute(action('ui_show_template'));await driver.execute(action('ui_copy_template'));
  let finish!: (value: Response) => void;
  const previous = fetch;
  vi.stubGlobal('fetch', vi.fn((url: string, options?: RequestInit) => url.endsWith('/compile')
    ? new Promise<Response>(resolve => { finish = resolve; }) : previous(url, options)));
  fireEvent.click(screen.getByRole('button', { name: 'Copiar prompt' }));
  expect(readUiState().clipboard).toBeNull();
  expect(write).toHaveBeenLastCalledWith(prompt.templateText);
  await act(async () => { finish(Response.json({ text: 'Explica energía.', templateRevision: 3 })); });
  await waitFor(() => expect(readUiState().clipboard?.text).toBe('Explica energía.'));
  expect(write).toHaveBeenCalledTimes(2);
});

it('clipboard rejection produces an error and no successful receipt or recorded use', async () => {
  const { usage } = setup(vi.fn().mockRejectedValue(new Error('Portapapeles denegado')));
  render(<PromptFocus {...props} />); await screen.findByTestId('compile-ready');
  const driver = new AgentUiDriver(vi.fn()); driver.speed = 'fast';
  await expect(driver.execute(action())).rejects.toThrow('UI_CLIPBOARD_FAILED');
  expect(readUiState().clipboard).toBeNull(); expect(usage).toEqual([]);
});

it.each(['changed-values', 'cancelled'])('does not publish a delayed write receipt after %s', async reason => {
  let complete!: () => void; const pending = new Promise<void>(resolve => { complete = resolve; });
  const { write, usage } = setup(vi.fn(() => pending));
  const view = render(<PromptFocus {...props} />); await screen.findByTestId('compile-ready');
  fireEvent.click(screen.getByRole('button', { name: 'Copiar prompt' })); await waitFor(() => expect(write).toHaveBeenCalledOnce());
  if (reason === 'changed-values') view.rerender(<PromptFocus {...props} values={{ tema: 'otro' }} />);
  else act(() => { window.dispatchEvent(new Event('nova-abort-ui-operation')); });
  await act(async () => { complete(); await pending; });
  expect(readUiState().clipboard).toBeNull(); expect(usage).toEqual([]);
});

it('the same command copies an unsaved editor artifact without saving or registering use of its source', async () => {
  const { write, usage } = setup();
  render(<section data-testid="prompt-editor"><input id="prompt-title" defaultValue="Nuevo" /><textarea id="prompt-content" defaultValue="Explica <tema>." />
    <EditorPreparation source={null} draft={{ title: 'Nuevo', templateText: 'Explica <tema>.', description: '', categoryId: 0 }} values={values} onValues={vi.fn()} onChange={vi.fn()} onUse={vi.fn()} />
  </section>); await screen.findByTestId('compile-ready');
  const driver = new AgentUiDriver(vi.fn()); driver.speed = 'fast';
  const state = await driver.execute({ ...action(), target: null, arguments: { promptId: null } });
  expect(state.clipboard).toEqual({ operationId: expect.any(String), promptId: null, revision: null, text: 'Explica energía.' });
  expect(write).toHaveBeenCalledOnce(); expect(usage).toEqual([]);
});
