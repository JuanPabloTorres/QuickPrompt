import { flushSync } from 'react-dom';

export interface UiState {
  mode: string; candidates: { id: string; title: string; revision: number }[];
  promptId: string | null; revision: number | null; title: string | null; templateText: string | null;
  values: Record<string, string>; variableNames: string[]; draftChanged: boolean;
  compiledReady: boolean; compiledText: string | null; response: string | null;
  responseComplete: boolean; providerOpen: boolean; providerId: string | null; error: string | null;
}
export interface UiAction { runId: string; actionId: string; sequence: number; tool: string; target: string | null; arguments: Record<string, unknown>; timestamp: string; executionStatus: string; verificationStatus: string }
export interface CursorTarget { element: HTMLElement; operation: 'target' | 'click' | 'type' }
const shown = (node: Element | null): node is HTMLElement => node instanceof HTMLElement && !!node.getClientRects().length;
const test = (id: string) => document.querySelector<HTMLElement>(`[data-testid="${id}"]`);
const input = (id: string) => document.getElementById(id) as HTMLInputElement | HTMLTextAreaElement | null;
const button = (name: string) => Array.from(document.querySelectorAll('button')).find(node => shown(node) && (node.textContent?.trim() === name || node.getAttribute('aria-label') === name)) ?? null;
const delay = (ms: number) => new Promise<void>(resolve => setTimeout(resolve, ms));

export function readUiState(): UiState {
  const editor = test('prompt-editor'), focus = test('prompt-focus');
  const root = shown(editor) ? editor : shown(focus) ? focus : null;
  const editing = root === editor && !!root;
  const fields = Array.from(document.querySelectorAll<HTMLTextAreaElement>('[data-testid="variable-field"]')).filter(shown);
  const values = Object.fromEntries(fields.map(field => [field.dataset.variableName!, field.value]));
  const ready = shown(test('compile-ready')), response = test('local-qwen-response');
  return {
    mode: editing ? 'create' : root ? 'focus' : 'discover',
    candidates: Array.from(document.querySelectorAll<HTMLElement>('[data-testid="library-prompt"]')).filter(shown).map(node => ({ id: node.dataset.promptId!, revision: Number(node.dataset.revision), title: node.getAttribute('aria-label') ?? '' })),
    promptId: root?.dataset.promptId || null, revision: root?.dataset.promptId ? Number(root.dataset.revision) : null,
    title: editing ? input('prompt-title')!.value : root?.querySelector('[data-testid="focus-title"]')?.textContent ?? null,
    templateText: editing ? input('prompt-content')!.value : root?.dataset.templateText ?? null,
    values, variableNames: Object.keys(values), draftChanged: editing && (shown(test('draft-prepared')) || !root?.dataset.promptId && !!input('prompt-content')?.value),
    compiledReady: ready, compiledText: ready ? test('compiled-preview')?.textContent ?? null : null,
    response: shown(response) ? response.textContent : null, responseComplete: shown(response) && response.dataset.complete === 'true',
    providerOpen: shown(test('prepared-provider-dialog')), providerId: shown(test('prepared-provider-dialog')) ? (document.getElementById('prepared-provider') as HTMLSelectElement)?.value ?? null : null, error: Array.from(document.querySelectorAll('[role="alert"]')).some(shown) ? 'REACT_REPORTED_ERROR' : null,
  };
}

// Only code-owned logical tools map to DOM. Model text is always data, never selectors or script.
export class AgentUiDriver {
  paused = false;
  stopped = false;
  intervention = false;
  speed: 'normal' | 'fast' = 'normal';
  private editRequest = '';
  private localAllowed = false;
  private providerId: string | null = null;
  private working: Record<string, unknown> | null = null;
  private previous: UiState | null = null;
  private writing = false;
  constructor(private target: (target: CursorTarget | null) => void, private progress?: (status: 'TARGET_LOCATED' | 'EXECUTED') => Promise<void>) {}
  stop() { this.stopped = true; window.dispatchEvent(new Event('nova-abort-ui-operation')); this.target(null); }
  intervene() { this.intervention = true; this.stop(); }
  async boundary() {
    while (this.paused || document.visibilityState === 'hidden') { this.check(); await delay(40); }
    this.check();
  }
  private check() { if (this.stopped) throw new Error(this.intervention ? 'MANUAL_INTERVENTION' : 'UI_CANCELLED'); }
  async until<T>(read: () => T | null | false, timeout = 20000): Promise<T> {
    const end = Date.now() + timeout;
    while (Date.now() < end) { await this.boundary(); const value = read(); if (value) return value; await delay(40); }
    throw new Error('UI_TARGET_TIMEOUT');
  }
  async locate(read: () => HTMLElement | null, operation: CursorTarget['operation']) {
    const node = await this.until(() => { const element = read(); return shown(element) ? element : null; });
    await this.boundary(); node.scrollIntoView({ block: 'center', behavior: 'instant' });
    this.target({ element: node, operation });
    await this.progress?.('TARGET_LOCATED');
    await delay(this.speed === 'fast' || matchMedia('(prefers-reduced-motion: reduce)').matches ? 0 : 130);
    await this.boundary(); if (!shown(node)) throw new Error('UI_TARGET_CHANGED'); return node;
  }
  async click(name: string) {
    const node = await this.locate(() => button(name), 'click');
    if (node instanceof HTMLButtonElement && node.disabled) throw new Error('UI_CONTROL_DISABLED');
    node.click(); await delay(30);
  }
  async write(id: string | HTMLInputElement | HTMLTextAreaElement, text: string, signal?: AbortSignal) {
    if (this.writing) throw new Error('UI_CONCURRENT_WRITE'); this.writing = true;
    try {
      const node = await this.locate(() => typeof id === 'string' ? input(id) : id, 'type') as HTMLInputElement | HTMLTextAreaElement;
      if (node.disabled || text.length > node.maxLength && node.maxLength >= 0) throw new Error('UI_FIELD_UNAVAILABLE');
      node.focus(); let expected = node.value;
      const characters = Array.from(text); // Do not split surrogate pairs while typing.
      const step = this.speed === 'fast' ? Math.max(characters.length, 1) : Math.max(12, Math.ceil(characters.length / 45));
      const setter = Object.getOwnPropertyDescriptor(node instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype, 'value')!.set!;
      for (let count = Math.min(step, characters.length); ; count = Math.min(count + step, characters.length)) {
        await this.boundary(); if (signal?.aborted) throw new Error('UI_CANCELLED');
        if (!node.isConnected || node.value !== expected) throw new Error('MANUAL_INTERVENTION');
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
      return state.mode === 'create' ? shown(test('variables-inspected')) && state : state.variableNames.length === 0 || Object.values(state.values).some(value => !value.trim()) || state.compiledReady ? state : null;
    });
    await delay(100); return readUiState();
  }
  async execute(action: UiAction): Promise<UiState> {
    await this.boundary(); const args = action.arguments, tool = action.tool, before = readUiState();
    if (this.previous && tool !== 'commit_approved' && tool !== 'set_working_context' && (before.mode !== this.previous.mode || before.promptId !== this.previous.promptId || before.revision !== this.previous.revision || before.title !== this.previous.title || before.templateText !== this.previous.templateText || JSON.stringify(before.values) !== JSON.stringify(this.previous.values))) throw new Error('MANUAL_INTERVENTION');
    if (action.target && tool !== 'ui_open_prompt' && tool !== 'set_working_context' && before.promptId !== action.target) throw new Error('UI_IDENTITY_MISMATCH');
    if (tool === 'start') { this.editRequest = String(args.editRequest); this.localAllowed = args.localExecutionAllowed === true; this.providerId = typeof args.providerId === 'string' && args.providerId !== 'ollama' ? args.providerId : null; }
    else if (tool === 'set_working_context') this.working = args;
    else if (tool === 'ui_navigate_library') { if (before.mode === 'create') throw new Error('UI_EXISTING_DRAFT'); await this.click('QuickPrompt NOVA, biblioteca'); await this.until(() => readUiState().mode === 'discover'); }
    else if (tool === 'ui_search_prompt') {
      if (before.mode !== 'discover') throw new Error('UI_PRECONDITION');
      const field = document.querySelector<HTMLInputElement>('.library-search input') ?? Array.from(document.querySelectorAll<HTMLInputElement>('input')).find(node => node.id === 'library-search');
      if (!field) throw new Error('UI_TARGET_NOT_FOUND'); await this.write(field, String(args.query));
      await this.until(() => shown(test('library-ready')) && field.value === args.query); await delay(350);
      await this.until(() => shown(test('library-ready')));
    } else if (tool === 'ui_open_prompt') {
      const node = await this.locate(() => Array.from(document.querySelectorAll<HTMLElement>('[data-testid="library-prompt"]')).find(item => item.dataset.promptId === action.target) ?? null, 'click'); node.click();
      await this.until(() => readUiState().mode === 'focus' && readUiState().promptId === action.target);
    } else if (tool === 'ui_open_create_prompt') {
      if (before.mode === 'focus') { await this.click('QuickPrompt NOVA, biblioteca'); await this.until(() => readUiState().mode === 'discover'); }
      else if (before.mode !== 'discover') throw new Error('UI_PRECONDITION'); await this.click('Nuevo prompt');
      await this.until(() => readUiState().mode === 'create');
      if (input('prompt-content')?.value || input('prompt-title')?.value) throw new Error('UI_EXISTING_DRAFT');
    } else if (tool === 'ui_prepare_creation') {
      if (before.mode !== 'create' || before.promptId || before.templateText || before.title) throw new Error('UI_PRECONDITION');
      await this.write('prompt-title', String(args.title)); await this.write('prompt-content', String(args.templateText));
      await this.settled(); if (readUiState().templateText !== args.templateText || readUiState().title !== args.title) throw new Error('UI_VALUE_MISMATCH');
    } else if (tool === 'ui_open_editor') { await this.click('Editar'); await this.until(() => readUiState().mode === 'create'); }
    else if (tool === 'ui_load_active_draft') {
      if (!this.working || this.working.promptId !== action.target) throw new Error('UI_WORKING_DRAFT_MISMATCH');
      if (before.mode === 'focus') { await this.click('Editar'); await this.until(() => readUiState().mode === 'create'); }
      await this.write('prompt-title', String(this.working.title)); await this.write('prompt-content', String(this.working.templateText)); await this.settled();
      for (const [name, value] of Object.entries(this.working.values as Record<string, string>)) if (value.trim()) await this.fillVariable(name, value);
    } else if (tool === 'ui_fill_variable') await this.fillVariable(String(args.name), String(args.value));
    else if (tool === 'ui_prepare_update') {
      await this.write('editor-change', this.editRequest); await this.click('Preparar cambio en el editor');
      await this.until(() => shown(test('draft-prepared')) && test('editor-preparation')?.dataset.busy === 'false' && input('prompt-content')?.value !== before.templateText || shown(document.querySelector('.editor-preparation [role="alert"]')), 150000);
    } else if (tool === 'ui_show_compiled_prompt') await this.locate(() => test('compiled-preview'), 'target');
    else if (tool === 'ui_open_provider_picker' || tool === 'ui_execute_local') {
      if (!before.compiledReady) throw new Error('MISSING_INPUTS');
      if (!before.providerOpen) { await this.click('Usar en…'); await this.until(() => shown(test('prepared-provider-dialog'))); }
      if (this.providerId) {
        const select = await this.locate(() => document.getElementById('prepared-provider'), 'target') as HTMLSelectElement;
        if (!Array.from(select.options).some(option => option.value === this.providerId)) throw new Error('PROVIDER_UNAVAILABLE');
        await this.boundary(); select.focus(); flushSync(() => { Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value')!.set!.call(select, this.providerId); select.dispatchEvent(new Event('change', { bubbles: true })); });
        if (select.value !== this.providerId) throw new Error('UI_PROVIDER_MISMATCH');
      }
      if (tool === 'ui_execute_local') { if (!this.localAllowed) throw new Error('LOCAL_EXECUTION_NOT_ALLOWED'); await this.click('Ejecutar con Qwen3'); await this.until(() => shown(test('local-response-complete')), 150000); }
    } else if (tool === 'prepare_review') { if (before.providerOpen) { await this.click('Cerrar diálogo'); await this.until(() => !shown(test('prepared-provider-dialog'))); } }
    else if (tool === 'commit_approved') {
      if (before.providerOpen) { await this.click('Cerrar diálogo'); await this.until(() => !shown(test('prepared-provider-dialog'))); }
      if (!button('Aplicar versión aprobada')) {
        const activity = await this.until(() => button('Actividad de NOVA'));
        if (activity.getAttribute('aria-expanded') === 'false') await this.click('Actividad de NOVA');
      }
      await this.until(() => button('Aplicar versión aprobada')); await this.click('Aplicar versión aprobada');
      await this.until(() => readUiState().mode === 'focus' && readUiState().revision === (before.revision ?? 0) + 1);
    } else if (tool !== 'ui_read_visible_state' && tool !== 'ui_verify_prompt_state') throw new Error('UNKNOWN_UI_TOOL');
    await this.boundary(); const state = await this.settled();
    if (state.error) throw new Error('UI_REPORTED_ERROR');
    this.previous = state; this.target(null); await this.progress?.('EXECUTED'); return state;
  }
  private async fillVariable(name: string, value: string) {
    const field = Array.from(document.querySelectorAll<HTMLTextAreaElement>('[data-testid="variable-field"]')).find(node => node.dataset.variableName === name && shown(node));
    if (!field) throw new Error('UNKNOWN_VARIABLE'); await this.write(field, value);
    await this.until(() => readUiState().values[name] === value); await delay(300);
  }
}

let currentDriver: AgentUiDriver | null = null;
export const setAgentDriver = (driver: AgentUiDriver | null) => { currentDriver = driver; };
export async function applyAgentProposal(text: string, signal: AbortSignal): Promise<boolean> {
  if (!currentDriver) return false; await currentDriver.proposal(text, signal); return true;
}
