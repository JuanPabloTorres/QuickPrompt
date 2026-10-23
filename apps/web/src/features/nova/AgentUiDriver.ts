import { createAgentUiCommands, type AgentUiCommandFactory, type AgentUiCommandRegistry, type CursorTarget } from './AgentUiCommands';
import { flushSync } from 'react-dom';
import { agentUiSurface, shown, type AgentControl } from './AgentUiSurface';

import type { UiObservation as UiState, LiveUiAction as UiAction } from '../../services/live';
export type { UiObservation as UiState, LiveUiAction as UiAction } from '../../services/live';
export type { CursorTarget } from './AgentUiCommands';
const test = (id: string) => agentUiSurface.marker(id);
const input = (id: string) => agentUiSurface.input(id);
const delay = (ms: number) => new Promise<void>(resolve => setTimeout(resolve, ms));

export function readUiState(): UiState {
  const root = agentUiSurface.workspace();
  const deletion = test('prompt-delete-review');
  const editing = root?.dataset.testid === 'prompt-editor';
  const fields = agentUiSurface.variables();
  const values = Object.fromEntries(fields.map(field => [field.dataset.variableName!, field.value]));
  const ready = shown(test('compile-ready')), response = test('local-qwen-response');
  const copied = test('clipboard-receipt');
  return {
    mode: editing ? 'create' : root ? 'focus' : 'discover',
    candidates: agentUiSurface.candidates().map(node => ({ id: node.dataset.promptId!, revision: Number(node.dataset.revision), title: node.getAttribute('aria-label') ?? '' })),
    promptId: root?.dataset.promptId || null, revision: root?.dataset.promptId ? Number(root.dataset.revision) : null,
    title: editing ? input('prompt-title')!.value : root?.querySelector('[data-testid="focus-title"]')?.textContent ?? null,
    templateText: editing ? input('prompt-content')!.value : root?.dataset.templateText ?? null,
    values, variableNames: Object.keys(values), draftChanged: editing && (shown(test('draft-prepared')) || !root?.dataset.promptId && !!input('prompt-content')?.value),
    compiledReady: ready, compiledText: ready ? test('compiled-preview')?.textContent ?? null : null,
    templatePreviewText: shown(test('template-preview')) ? test('template-preview')!.textContent : null,
    clipboard: shown(copied) ? { operationId: copied.dataset.operationId!, promptId: copied.dataset.promptId || null,
      revision: copied.dataset.revision ? Number(copied.dataset.revision) : null, text: copied.dataset.clipboardText!, ...(copied.dataset.copyContent === 'TEMPLATE' ? { copyContent: 'TEMPLATE' as const } : {}) } : null,
    response: shown(response) ? response.textContent : null, responseComplete: shown(response) && response.dataset.complete === 'true',
    deletionPromptId: shown(deletion) ? deletion.dataset.deletePromptId || null : null,
    deletionRevision: shown(deletion) ? Number(deletion.dataset.deleteRevision) : null,
    deletionRunId: shown(deletion) ? deletion.dataset.deleteRunId || null : null,
    deletionConfirmationReady: shown(deletion) && deletion.dataset.deleteReady === 'true',
    providerOpen: shown(test('prepared-provider-dialog')), providerId: agentUiSurface.providerSelect()?.value ?? null, error: Array.from(document.querySelectorAll('[role="alert"]')).some(shown) ? 'REACT_REPORTED_ERROR' : null,
  };
}

// Only code-owned logical tools map to DOM. Model text is always data, never selectors or script.
export class AgentUiDriver {
  paused = false;
  stopped = false;
  intervention = false;
  speed: 'normal' | 'fast' = 'normal';

  private previous: UiState | null = null;
  private writing = false;
  private readonly commands: AgentUiCommandRegistry;
  constructor(private target: (target: CursorTarget | null) => void, private progress?: (status: 'TARGET_LOCATED' | 'EXECUTED') => void | Promise<void>, commandFactory: AgentUiCommandFactory = createAgentUiCommands) {
    this.commands = commandFactory({
      readState: readUiState, marker: test, input, surface: agentUiSurface,
      boundary: () => this.boundary(), until: (read, timeout) => this.until(read, timeout),
      locate: (read, operation) => this.locate(read, operation), click: (key, runId) => this.click(key, runId),
      write: (id, text, signal) => this.write(id, text, signal), settled: () => this.settled(),
      fillVariable: (name, value) => this.fillVariable(name, value),
    });
  }
  stop() { this.stopped = true; window.dispatchEvent(new Event('nova-abort-ui-operation')); this.target(null); }
  intervene() { this.intervention = true; this.stop(); }
  async boundary() {
    while (this.paused) { this.check(); await delay(40); }
    this.check();
  }
  private check() { if (this.stopped) throw new Error(this.intervention ? 'MANUAL_INTERVENTION' : 'UI_CANCELLED'); }
  async until<T>(read: () => T | null | false, timeout = 20000): Promise<T> {
    let remaining = timeout;
    while (remaining > 0) {
      await this.boundary(); const started = Date.now();
      const value = read(); if (value) return value;
      await delay(40); remaining -= Date.now() - started;
    }
    throw new Error('UI_TARGET_TIMEOUT');
  }
  async locate(read: () => HTMLElement | null, operation: CursorTarget['operation']) {
    const node = await this.until(() => { const element = read(); return shown(element) ? element : null; });
    await this.boundary();
    // HTMLElement.click() does not perform the focus step of a native pointer
    // click. Claim the real target before layout/scroll/progress can uncover a
    // previously focused workspace field and hide this control's panel.
    if (operation === 'click') node.focus({ preventScroll: true });
    const bounds = node.getBoundingClientRect();
    if (bounds.top < 0 || bounds.bottom > window.innerHeight || bounds.left < 0 || bounds.right > window.innerWidth) node.scrollIntoView({ block: 'center', behavior: 'instant' });
    this.target({ element: node, operation });
    await this.progress?.('TARGET_LOCATED');
    await delay(this.speed === 'fast' || matchMedia('(prefers-reduced-motion: reduce)').matches ? 0 : 130);
    await this.boundary(); if (!shown(node) || read() !== node) throw new Error('UI_TARGET_CHANGED'); return node;
  }
  async click(key: AgentControl, runId?: string) {
    const node = await this.locate(() => agentUiSurface.control(key, runId), 'click');
    if (node instanceof HTMLButtonElement && node.disabled) throw new Error('UI_CONTROL_DISABLED');
    node.click(); await delay(30);
  }
  async write(id: string | (() => HTMLInputElement | HTMLTextAreaElement | null), text: string, signal?: AbortSignal) {
    if (this.writing) throw new Error('UI_CONCURRENT_WRITE'); this.writing = true;
    try {
      const read = typeof id === 'string' ? () => input(id) : id;
      const node = await this.locate(read, 'type') as HTMLInputElement | HTMLTextAreaElement;
      if (node.disabled || node.readOnly || node.closest('[inert]') || text.length > node.maxLength && node.maxLength >= 0) throw new Error('UI_FIELD_UNAVAILABLE');
      node.focus(); let expected = node.value;
      const characters = Array.from(text); // Do not split surrogate pairs while typing.
      const step = this.speed === 'fast' ? Math.max(characters.length, 1) : Math.max(12, Math.ceil(characters.length / 45));
      const setter = Object.getOwnPropertyDescriptor(node instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype, 'value')!.set!;
      for (let count = Math.min(step, characters.length); ; count = Math.min(count + step, characters.length)) {
        await this.boundary(); if (signal?.aborted) throw new Error('UI_CANCELLED');
        if (!node.isConnected || node.value !== expected) throw new Error('MANUAL_INTERVENTION');
        if (!shown(node) || read() !== node) throw new Error('UI_TARGET_CHANGED');
        expected = characters.slice(0, count).join('');
        flushSync(() => { setter.call(node, expected); node.dispatchEvent(new Event('input', { bubbles: true })); });
        if (node.value !== expected) throw new Error('UI_VALUE_MISMATCH');
        if (count === characters.length) break;
        await delay(45);
      }
      if (node.value !== text) throw new Error('UI_VALUE_MISMATCH');
    } finally { this.writing = false; }
  }
  async proposal(text: string, signal: AbortSignal) { await this.write('prompt-content', text, signal); }
  private async settled() {
    await this.until(() => {
      const state = readUiState();
      if (state.error) return state;
      if (state.mode === 'discover') return state;
      if (state.mode === 'create' && !shown(test('variables-inspected'))) return null;
      return state.variableNames.some(name => !state.values[name]?.trim()) || state.compiledReady ? state : null;
    });
    if (this.speed !== 'fast') await delay(100); return readUiState();
  }
  async execute(action: UiAction): Promise<UiState> {
    await this.boundary(); const before = readUiState(), command = this.commands.find(action.tool);
    if (this.previous && !command?.allowObservedChange && (before.mode !== this.previous.mode || before.promptId !== this.previous.promptId || before.revision !== this.previous.revision || before.title !== this.previous.title || before.templateText !== this.previous.templateText || JSON.stringify(before.values) !== JSON.stringify(this.previous.values))) throw new Error('MANUAL_INTERVENTION');
    if (action.target && !command?.allowsExternalTarget && before.promptId !== action.target) throw new Error('UI_IDENTITY_MISMATCH');
    if (!command) throw new Error('UNKNOWN_UI_TOOL');
    if (command.kind === 'immediate') {
      const state = command.capture(action, before);
      this.previous = state; this.target(null); await this.progress?.('EXECUTED'); return state;
    }
    await command.execute(action, before);
    await this.boundary(); const state = await this.settled();
    if (state.error) throw new Error('UI_REPORTED_ERROR');
    this.previous = state; this.target(null); await this.progress?.('EXECUTED'); return state;
  }
  private async fillVariable(name: string, value: string) {
    const field = agentUiSurface.variable(name);
    if (!field) throw new Error('UNKNOWN_VARIABLE'); await this.write(() => agentUiSurface.variable(name), value);
    await this.until(() => readUiState().values[name] === value);
    if (this.speed !== 'fast') await delay(300);
  }
}
