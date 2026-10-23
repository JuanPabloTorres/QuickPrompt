import { useState } from 'react';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { AgentUiDriver, type UiAction, type UiState } from '../src/features/nova/AgentUiDriver';
import { AgentUiCommandRegistry, createAgentUiCommands, type AgentUiCommand, type AgentUiCommandHost } from '../src/features/nova/AgentUiCommands';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });
const definition = (names: string[]): AgentUiCommand => ({ names, kind: 'settled', execute: vi.fn(async () => {}) });
const action = (tool: string, arguments_: Record<string, unknown> = {}, target: string | null = null): UiAction => ({
  tool, arguments: arguments_, target, runId: crypto.randomUUID(), actionId: crypto.randomUUID(), sequence: 1,
  timestamp: new Date().toISOString(), executionStatus: 'PENDING', verificationStatus: 'PENDING',
});

it.each([{ names: [] }, { names: [''] }, { names: [' bad'] }, { names: ['same', 'same'] }])('rejects empty or invalid alias definitions without executing them: $names', ({ names }) => {
  const command = definition(names); expect(() => new AgentUiCommandRegistry([command])).toThrow('INVALID_UI_COMMAND_REGISTRY');
  if (command.kind === 'settled') expect(command.execute).not.toHaveBeenCalled();
});

it('rejects duplicate aliases across providers before any effect', () => {
  const one = definition(['one', 'shared']), two = definition(['shared']);
  expect(() => new AgentUiCommandRegistry([one, two])).toThrow('INVALID_UI_COMMAND_REGISTRY');
  if (one.kind === 'settled' && two.kind === 'settled') { expect(one.execute).not.toHaveBeenCalled(); expect(two.execute).not.toHaveBeenCalled(); }
});

it('freezes descriptor metadata and names while retaining the supplied provider receiver', async () => {
  class Provider {
    names = ['one', 'alias']; kind = 'settled' as const; allowObservedChange = false; calls = 0;
    async execute() { this.calls++; }
  }
  const provider = new Provider(), registry = new AgentUiCommandRegistry([provider]);
  provider.names[0] = 'mutated'; provider.allowObservedChange = true; registry.names.push('forged');
  const command = registry.find('one')!;
  expect(registry.find('alias')).toBe(command); expect(registry.find('mutated')).toBeUndefined(); expect(registry.find('forged')).toBeUndefined();
  expect(registry.find('constructor')).toBeUndefined(); expect(registry.find('__proto__')).toBeUndefined();
  expect(Object.isFrozen(command)).toBe(true); expect(Object.isFrozen(command.names)).toBe(true); expect(command.allowObservedChange).toBe(false);
  if (command.kind === 'settled') await command.execute(action('one'), {} as UiState);
  expect(provider.calls).toBe(1);
});

function Frame({ id = '' }: { id?: string }) {
  const [body, setBody] = useState('');
  return <section data-testid="prompt-editor" data-prompt-id={id} data-revision="1">
    <input id="prompt-title" defaultValue="Fixture" />
    <textarea aria-label="Contenido" id="prompt-content" value={body} onChange={event => setBody(event.target.value)} />
    <span data-testid="variables-inspected" /><span data-testid="compile-ready" /><pre data-testid="compiled-preview">{body}</pre>
  </section>;
}
function dimensions() {
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([{ top: 0, left: 0, width: 100, height: 30 }] as unknown as DOMRectList);
  HTMLElement.prototype.scrollIntoView = vi.fn(); vi.stubGlobal('matchMedia', () => ({ matches: true }));
}

it('a substituted command uses real driver primitives and the common observation path', async () => {
  dimensions(); render(<Frame />); const progress = vi.fn(); let invoke = vi.fn();
  const driver = new AgentUiDriver(vi.fn(), progress, host => {
    invoke = vi.fn(async current => { await host.write('prompt-content', String(current.arguments.templateText)); });
    return new AgentUiCommandRegistry([{ names: ['ui_prepare_creation'], kind: 'settled', execute: invoke }]);
  }); driver.speed = 'fast';
  const state = await driver.execute(action('ui_prepare_creation', { templateText: 'Composición real 🚀.' }));
  expect(state.templateText).toBe('Composición real 🚀.'); expect(screen.getByLabelText('Contenido')).toHaveProperty('value', state.templateText);
  expect(invoke).toHaveBeenCalledOnce(); expect(progress).toHaveBeenLastCalledWith('EXECUTED');
  await expect(driver.execute(action('ui_open_prompt'))).rejects.toThrow('UNKNOWN_UI_TOOL'); expect(invoke).toHaveBeenCalledOnce();
});

it('substitution cannot skip default identity and intervening-edit guards', async () => {
  dimensions(); const id = crypto.randomUUID(); render(<Frame id={id} />); const invoke = vi.fn(async () => {});
  const driver = new AgentUiDriver(vi.fn(), undefined, () => new AgentUiCommandRegistry([{ names: ['ui_prepare_creation'], kind: 'settled', execute: invoke }])); driver.speed = 'fast';
  await expect(driver.execute(action('ui_prepare_creation', {}, crypto.randomUUID()))).rejects.toThrow('UI_IDENTITY_MISMATCH'); expect(invoke).not.toHaveBeenCalled();
  await driver.execute(action('ui_prepare_creation', {}, id)); expect(invoke).toHaveBeenCalledOnce();
  fireEvent.change(screen.getByLabelText('Contenido'), { target: { value: 'Edición humana.' } });
  await expect(driver.execute(action('ui_prepare_creation', {}, id))).rejects.toThrow('MANUAL_INTERVENTION'); expect(invoke).toHaveBeenCalledOnce();
});

it('default command factories isolate their per-driver execution permission', async () => {
  dimensions(); const complete = document.createElement('span'); document.body.appendChild(complete); const click = vi.fn(async () => {});
  const host: AgentUiCommandHost = {
    readState: () => ({} as UiState), marker: () => complete, input: () => null,
    surface: { prompt: () => null, providerSelect: () => null, control: () => null },
    boundary: async () => {}, until: async read => { const value = read(); if (!value) throw new Error('Missing fixture state'); return value; },
    locate: async () => complete, click, write: vi.fn(async () => {}), settled: async () => ({} as UiState), fillVariable: vi.fn(async () => {}),
  };
  const one = createAgentUiCommands(host), two = createAgentUiCommands(host), state = { compiledReady: true, providerOpen: true } as UiState;
  const startOne = one.find('start')!, startTwo = two.find('start')!, executeOne = one.find('ui_execute_local')!, executeTwo = two.find('ui_execute_local')!;
  if (startOne.kind !== 'settled' || startTwo.kind !== 'settled' || executeOne.kind !== 'settled' || executeTwo.kind !== 'settled') throw new Error('Unexpected fixture command kind');
  await startOne.execute(action('start', { localExecutionAllowed: true }), state); await startTwo.execute(action('start', { localExecutionAllowed: false }), state);
  await executeOne.execute(action('ui_execute_local'), state); expect(click).toHaveBeenCalledWith('provider.execute');
  await expect(executeTwo.execute(action('ui_execute_local'), state)).rejects.toThrow('LOCAL_EXECUTION_NOT_ALLOWED'); expect(click).toHaveBeenCalledOnce(); complete.remove();
});

it('waits for the provider catalog before checking an empty select and rejects a loaded missing provider', async () => {
  const select = document.createElement('select'); select.dataset.providerState = 'loading'; document.body.appendChild(select);
  const click = vi.fn(async () => {});
  const host: AgentUiCommandHost = {
    readState: () => ({} as UiState), marker: () => select, input: () => null,
    surface: { prompt: () => null, providerSelect: () => select, control: () => null },
    boundary: async () => {}, until: async read => { for (let i = 0; i < 100; i++) { const value = read(); if (value) return value; await new Promise(resolve => setTimeout(resolve, 1)); } throw new Error('TEST_TIMEOUT'); },
    locate: async () => select, click, write: vi.fn(async () => {}), settled: async () => ({} as UiState), fillVariable: vi.fn(async () => {}),
  };
  const commands = createAgentUiCommands(host), start = commands.find('start')!, open = commands.find('ui_open_provider_picker')!;
  if (start.kind !== 'settled' || open.kind !== 'settled') throw new Error('Unexpected fixture command kind');
  const state = { compiledReady: true, providerOpen: true } as UiState;
  await start.execute(action('start', { providerId: 'ollama' }), state);
  let finished = false;
  const pending = open.execute(action('ui_open_provider_picker'), state).then(() => { finished = true; });
  await new Promise(resolve => setTimeout(resolve, 5)); expect(finished).toBe(false);
  select.add(new Option('Ollama local', 'ollama')); select.dataset.providerState = 'ready';
  await pending; expect(select.value).toBe('ollama'); expect(click).not.toHaveBeenCalled();
  select.replaceChildren();
  await expect(open.execute(action('ui_open_provider_picker'), state)).rejects.toThrow('PROVIDER_UNAVAILABLE');
  select.remove();
});
