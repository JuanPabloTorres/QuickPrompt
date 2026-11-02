import { flushSync } from 'react-dom';
import { shown, type AgentControl, type AgentUiSurface } from './AgentUiSurface';
import type { UiObservation as UiState, LiveUiAction as UiAction } from '../../services/live';
export interface CursorTarget {
    element: HTMLElement;
    operation: 'target' | 'click' | 'type';
}
export interface AgentUiCommandHost {
    readState(): UiState;
    marker(id: string): HTMLElement | null;
    input(id: string): HTMLInputElement | HTMLTextAreaElement | null;
    surface: Pick<AgentUiSurface, 'prompt' | 'providerSelect' | 'control'>;
    boundary(): Promise<void>;
    until<T>(read: () => T | null | false, timeout?: number): Promise<T>;
    locate(read: () => HTMLElement | null, operation: CursorTarget['operation']): Promise<HTMLElement>;
    click(key: AgentControl, runId?: string): Promise<void>;
    write(id: string | (() => HTMLInputElement | HTMLTextAreaElement | null), text: string, signal?: AbortSignal): Promise<void>;
    settled(): Promise<UiState>;
    fillVariable(name: string, value: string): Promise<void>;
}
interface CommandPolicy {
    readonly names: readonly string[];
    readonly allowObservedChange?: boolean;
    readonly allowsExternalTarget?: boolean;
}
export type AgentUiCommand = CommandPolicy & ({
    readonly kind: 'settled';
    readonly execute: (action: UiAction, before: UiState) => Promise<void>;
} | {
    readonly kind: 'immediate';
    readonly capture: (action: UiAction, before: UiState) => UiState;
});
// Trusted composition only. Model output selects an existing protocol ID; it
// cannot register functions, selectors, policies or permission exemptions.
export class AgentUiCommandRegistry {
    private readonly commands: ReadonlyMap<string, AgentUiCommand>;
    constructor(definitions: readonly AgentUiCommand[]) {
        const commands = new Map<string, AgentUiCommand>();
        for (const definition of definitions) {
            if (!definition.names.length || (definition.allowObservedChange !== undefined && typeof definition.allowObservedChange !== 'boolean')
                || (definition.allowsExternalTarget !== undefined && typeof definition.allowsExternalTarget !== 'boolean')
                || (definition.kind === 'settled' ? typeof definition.execute !== 'function' : definition.kind !== 'immediate' || typeof definition.capture !== 'function'))
                throw new Error('INVALID_UI_COMMAND_REGISTRY');
            const policy = { names: Object.freeze([...definition.names]), allowObservedChange: definition.allowObservedChange, allowsExternalTarget: definition.allowsExternalTarget };
            const command: AgentUiCommand = definition.kind === 'settled'
                ? Object.freeze({ ...policy, kind: 'settled', execute: definition.execute.bind(definition) })
                : Object.freeze({ ...policy, kind: 'immediate', capture: definition.capture.bind(definition) });
            for (const name of command.names) {
                if (typeof name !== 'string' || !name.trim() || name !== name.trim() || commands.has(name))
                    throw new Error('INVALID_UI_COMMAND_REGISTRY');
                commands.set(name, command);
            }
        }
        this.commands = commands;
    }
    find(name: string): AgentUiCommand | undefined { return this.commands.get(name); }
    get names(): string[] { return [...this.commands.keys()]; }
}
export type AgentUiCommandFactory = (host: AgentUiCommandHost) => AgentUiCommandRegistry;
export function createAgentUiCommands(host: AgentUiCommandHost): AgentUiCommandRegistry {
    const session: {
        editRequest: string;
        localAllowed: boolean;
        providerId: string | null;
        working: Record<string, unknown> | null;
    } = {
        editRequest: '', localAllowed: false, providerId: null, working: null,
    };
    async function writeDescription(args: Record<string, unknown>) {
        if (typeof args.description !== 'string') return; // Older non-authoring commands have no metadata change.
        const disclosure = host.surface.control('editor.metadata');
        if (disclosure?.closest('details')?.open === false) await host.click('editor.metadata');
        await host.write('prompt-description', args.description);
        if (host.readState().description !== args.description) throw new Error('UI_METADATA_MISMATCH');
    }
    return new AgentUiCommandRegistry([
        { names: ["ui_review_deletion"], allowsExternalTarget: true, kind: 'settled', execute: async (action, _before) => {
                const args = action.arguments;
                if (!action.target || !Number.isInteger(args.revision))
                    throw new Error('UI_PRECONDITION');
                let completed = false;
                let failure: unknown;
                window.dispatchEvent(new CustomEvent('nova-review-deletion', { detail: {
                        runId: action.runId, promptId: action.target, revision: args.revision,
                        resolve: () => { completed = true; }, reject: (error: unknown) => { failure = error; completed = true; },
                    } }));
                await host.until(() => {
                    if (failure)
                        throw failure;
                    return completed;
                }, 25000);
                await host.until(() => host.readState().deletionRunId === action.runId && host.readState().deletionPromptId === action.target && host.readState().deletionRevision === args.revision);
            } },
        { names: ["ui_enable_deletion_confirmation"], allowsExternalTarget: true, kind: 'immediate', capture: (action, before) => {
                const args = action.arguments;
                if (before.deletionRunId !== action.runId || before.deletionPromptId !== action.target || before.deletionRevision !== args.revision)
                    throw new Error('UI_IDENTITY_MISMATCH');
                window.dispatchEvent(new CustomEvent('nova-enable-deletion', { detail: { runId: action.runId, promptId: action.target, revision: args.revision } }));
                // Capture synchronously before a human can confirm and close the dialog.
                const state = host.readState();
                if (!state.deletionConfirmationReady)
                    throw new Error('UI_PRECONDITION');
                return state;
            } },
        { names: ["ui_verify_deletion"], allowObservedChange: true, allowsExternalTarget: true, kind: 'settled', execute: async (action, _before) => {
                await host.until(() => {
                    const state = host.readState();
                    return shown(host.marker('library-ready')) && !state.deletionPromptId && state.promptId !== action.target && !state.candidates.some(candidate => candidate.id === action.target);
                }, 12000);
            } },
        { names: ["start"], kind: 'settled', execute: async (action, _before) => {
                const args = action.arguments;
                session.editRequest = String(args.editRequest);
                session.localAllowed = args.localExecutionAllowed === true;
                session.providerId = typeof args.providerId === 'string' ? args.providerId : null;
            } },
        { names: ["set_working_context"], allowObservedChange: true, allowsExternalTarget: true, kind: 'settled', execute: async (action, _before) => {
                const args = action.arguments;
                session.working = args;
            } },
        { names: ["ui_navigate_library"], kind: 'settled', execute: async (_action, before) => {
                if (before.mode === 'create')
                    throw new Error('UI_EXISTING_DRAFT');
                await host.click('library.home');
                await host.until(() => host.readState().mode === 'discover');
            } },
        { names: ["ui_search_prompt"], kind: 'settled', execute: async (action, before) => {
                const args = action.arguments;
                if (before.mode !== 'discover')
                    throw new Error('UI_PRECONDITION');
                const field = host.input('library-search');
                if (!field)
                    throw new Error('UI_TARGET_NOT_FOUND');
                await host.write('library-search', String(args.query));
                await host.until(() => shown(host.marker('library-ready')) && field.value === args.query);
            } },
        { names: ["ui_open_prompt"], allowsExternalTarget: true, kind: 'settled', execute: async (action, _before) => {
                const node = await host.locate(() => host.surface.prompt(action.target), 'click');
                node.click();
                await host.until(() => host.readState().mode === 'focus' && host.readState().promptId === action.target);
            } },
        { names: ["ui_open_create_prompt"], kind: 'settled', execute: async (_action, before) => {
                if (before.mode === 'focus') {
                    await host.click('library.home');
                    await host.until(() => host.readState().mode === 'discover');
                }
                else if (before.mode !== 'discover')
                    throw new Error('UI_PRECONDITION');
                await host.click('prompt.new');
                await host.until(() => host.readState().mode === 'create');
                if (host.input('prompt-content')?.value || host.input('prompt-title')?.value)
                    throw new Error('UI_EXISTING_DRAFT');
            } },
        { names: ["ui_prepare_creation"], kind: 'settled', execute: async (action, before) => {
                const args = action.arguments;
                if (before.mode !== 'create' || before.promptId || before.templateText || before.title)
                    throw new Error('UI_PRECONDITION');
                await host.write('prompt-title', String(args.title));
                await host.write('prompt-content', String(args.templateText));
                await writeDescription(args);
                await host.settled();
                if (host.readState().templateText !== args.templateText || host.readState().title !== args.title)
                    throw new Error('UI_VALUE_MISMATCH');
            } },
        { names: ["ui_open_editor"], kind: 'settled', execute: async (_action, _before) => {
                await host.click('prompt.edit');
                await host.until(() => host.readState().mode === 'create');
            } },
        { names: ["ui_load_active_draft"], kind: 'settled', execute: async (action, before) => {
                if (!session.working || session.working.promptId !== action.target)
                    throw new Error('UI_WORKING_DRAFT_MISMATCH');
                if (!session.working.promptId) {
                    if (before.mode === 'create' && (before.promptId || before.templateText && (before.templateText !== session.working.templateText || before.title !== session.working.title)))
                        throw new Error('UI_WORKING_DRAFT_MISMATCH');
                    if (before.mode === 'focus') {
                        await host.click('library.home');
                        await host.until(() => host.readState().mode === 'discover');
                    }
                    if (host.readState().mode === 'discover') {
                        await host.click('prompt.new');
                        await host.until(() => host.readState().mode === 'create');
                    }
                }
                else if (before.mode === 'focus') {
                    await host.click('prompt.edit');
                    await host.until(() => host.readState().mode === 'create');
                }
                await host.write('prompt-title', String(session.working.title));
                await host.write('prompt-content', String(session.working.templateText));
                await host.settled();
                for (const [name, value] of Object.entries(session.working.values as Record<string, string>))
                    if (value.trim())
                        await host.fillVariable(name, value);
            } },
        { names: ["ui_fill_variable"], kind: 'settled', execute: async (action, _before) => {
                const args = action.arguments;
                await host.fillVariable(String(args.name), String(args.value));
            } },
        { names: ["ui_prepare_update"], kind: 'settled', execute: async (action, before) => {
                const args = action.arguments;
                if (typeof args.templateText === 'string' && typeof args.title === 'string') {
                    if (before.mode !== 'create' || before.promptId !== action.target || !before.promptId && (!session.working || before.templateText !== session.working.templateText))
                        throw new Error('UI_WORKING_DRAFT_MISMATCH');
                    await host.write('prompt-title', args.title);
                    await host.write('prompt-content', args.templateText);
                    await writeDescription(args);
                    await host.settled();
                    if (host.readState().templateText !== args.templateText || host.readState().title !== args.title)
                        throw new Error('UI_VALUE_MISMATCH');
                }
                else {
                    await host.write('editor-change', session.editRequest);
                    await host.click('editor.prepare');
                    await host.until(() => shown(host.marker('draft-prepared')) && host.marker('editor-preparation')?.dataset.busy === 'false' && host.input('prompt-content')?.value !== before.templateText || shown(host.marker('editor-preparation')?.querySelector('[role="alert"]') ?? null), 150000);
                }
            } },
        { names: ["ui_copy_compiled_prompt"], kind: 'settled', execute: async (_action, before) => {
                if (!before.compiledReady || !before.compiledText) throw new Error('UI_PRECONDITION');
                await host.until(() => {
                    const control = host.surface.control('prompt.copy');
                    return control && !(control instanceof HTMLButtonElement && control.disabled);
                });
                await host.click('prompt.copy');
                await host.until(() => {
                    const current = host.readState();
                    if (current.error) throw new Error('UI_CLIPBOARD_FAILED');
                    const receipt = current.clipboard;
                    if (!receipt || receipt.operationId === before.clipboard?.operationId) return false;
                    if (receipt.promptId !== before.promptId || receipt.revision !== before.revision
                        || receipt.text !== before.compiledText || current.compiledText !== before.compiledText)
                        throw new Error('UI_CLIPBOARD_MISMATCH');
                    return true;
                });
            } },
        { names: ["ui_show_template"], kind: 'settled', execute: async () => {
                if (!shown(host.marker('template-preview'))) await host.click('prompt.template-preview');
                await host.locate(() => host.marker('template-preview'), 'target');
                const state = host.readState();
                if (!state.templateText || state.templatePreviewText !== state.templateText) throw new Error('UI_TEMPLATE_MISMATCH');
            } },
        { names: ["ui_copy_template"], kind: 'settled', execute: async (_action, before) => {
                if (!before.templateText || before.templatePreviewText !== before.templateText) throw new Error('UI_PRECONDITION');
                await host.click('prompt.copy-template');
                await host.until(() => {
                    const current = host.readState(), receipt = current.clipboard;
                    if (current.error) throw new Error('UI_CLIPBOARD_FAILED');
                    if (!receipt || receipt.operationId === before.clipboard?.operationId) return false;
                    if (receipt.copyContent !== 'TEMPLATE' || receipt.promptId !== before.promptId || receipt.revision !== before.revision
                        || receipt.text !== before.templateText || current.templateText !== before.templateText) throw new Error('UI_CLIPBOARD_MISMATCH');
                    return true;
                });
            } },
        { names: ["ui_show_compiled_prompt"], kind: 'settled', execute: async (_action, _before) => {
                await host.locate(() => host.marker('compiled-preview'), 'target');
            } },
        { names: ["ui_open_provider_picker", "ui_execute_local"], kind: 'settled', execute: async (action, before) => {
                const tool = action.tool;
                if (!before.compiledReady)
                    throw new Error('MISSING_INPUTS');
                if (!before.providerOpen) {
                    await host.click('prompt.use');
                    await host.until(() => shown(host.marker('prepared-provider-dialog')));
                }
                if (session.providerId) {
                    const select = await host.locate(() => host.surface.providerSelect(), 'target') as HTMLSelectElement;
                    await host.until(() => {
                        if (select.dataset.providerState === 'error') throw new Error('PROVIDER_UNAVAILABLE');
                        return select.dataset.providerState !== 'loading';
                    });
                    if (!Array.from(select.options).some(option => option.value === session.providerId))
                        throw new Error('PROVIDER_UNAVAILABLE');
                    await host.boundary();
                    select.focus();
                    flushSync(() => { Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value')!.set!.call(select, session.providerId); select.dispatchEvent(new Event('change', { bubbles: true })); });
                    if (select.value !== session.providerId)
                        throw new Error('UI_PROVIDER_MISMATCH');
                }
                if (tool === 'ui_execute_local') {
                    if (!session.localAllowed)
                        throw new Error('LOCAL_EXECUTION_NOT_ALLOWED');
                    await host.click('provider.execute');
                    await host.until(() => shown(host.marker('local-response-complete')), 150000);
                }
            } },
        { names: ["prepare_review"], kind: 'settled', execute: async (_action, before) => {
                if (before.providerOpen) {
                    await host.click('dialog.close');
                    await host.until(() => !shown(host.marker('prepared-provider-dialog')));
                }
            } },
        { names: ["commit_approved"], allowObservedChange: true, kind: 'settled', execute: async (action, before) => {
                const args = action.arguments;
                if (before.providerOpen) {
                    await host.click('dialog.close');
                    await host.until(() => !shown(host.marker('prepared-provider-dialog')));
                }
                // Persistence belongs to the stable editor, never a moving activity portal.
                await host.until(() => host.surface.control('approval.apply', action.runId));
                await host.click('approval.apply', action.runId);
                const expectedRevision = typeof args.expectedRevision === 'number' ? args.expectedRevision : (before.revision ?? 0) + 1;
                await host.until(() => {
                    const state = host.readState();
                    if (state.error)
                        throw new Error('UI_REPORTED_ERROR');
                    return state.mode === 'focus' && state.revision === expectedRevision;
                });
            } },
        { names: ['ui_read_visible_state', 'ui_verify_prompt_state'], kind: 'settled', execute: async () => { } }
    ]);
}
