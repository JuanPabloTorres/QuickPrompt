// A code-owned DOM port. Model output never supplies selectors or captions.
export type AgentControl = 'library.home' | 'prompt.new' | 'prompt.edit' | 'prompt.use' | 'prompt.copy' | 'prompt.copy-template' | 'prompt.template-preview' | 'editor.prepare' | 'provider.execute' | 'dialog.close' | 'approval.apply';
type Owner = 'application' | 'library' | 'workspace' | 'provider';
const owners: Record<AgentControl, Owner> = {
  'library.home': 'application', 'prompt.new': 'library', 'prompt.edit': 'workspace', 'prompt.use': 'workspace', 'prompt.copy': 'workspace',
  'prompt.copy-template': 'workspace', 'prompt.template-preview': 'workspace', 'editor.prepare': 'workspace', 'provider.execute': 'provider', 'dialog.close': 'provider', 'approval.apply': 'workspace',
};
const workspaceMarkers = new Set(['focus-title', 'compile-ready', 'compiled-preview', 'template-preview', 'variables-inspected', 'draft-prepared', 'editor-preparation', 'clipboard-receipt']);
const providerMarkers = new Set(['local-qwen-response', 'local-response-complete']);
export const shown = (node: Element | null): node is HTMLElement => node instanceof HTMLElement && node.isConnected && !!node.getClientRects().length;

export class AgentUiSurface {
  private unique<T extends HTMLElement>(nodes: T[]): T | null {
    const visible = nodes.filter(shown);
    if (visible.length > 1) throw new Error('UI_TARGET_AMBIGUOUS');
    return visible[0] ?? null;
  }
  private markerIn(id: string, root: ParentNode | null): HTMLElement | null {
    return this.unique(Array.from(root?.querySelectorAll<HTMLElement>('[data-testid]') ?? []).filter(node => node.dataset.testid === id));
  }
  workspace(): HTMLElement | null {
    return this.unique(Array.from(document.querySelectorAll<HTMLElement>('[data-testid="prompt-editor"], [data-testid="prompt-focus"]')));
  }
  library(): HTMLElement | null { return this.unique(Array.from(document.querySelectorAll<HTMLElement>('[data-nova-surface="library"]'))); }
  provider(): HTMLElement | null { return this.markerIn('prepared-provider-dialog', document)?.closest<HTMLElement>('[role="dialog"]') ?? null; }
  marker(id: string): HTMLElement | null {
    const root = workspaceMarkers.has(id) ? this.workspace() : providerMarkers.has(id) ? this.provider() : id === 'library-ready' ? this.library() : document;
    return this.markerIn(id, root);
  }
  variables(): HTMLTextAreaElement[] {
    const fields = Array.from(this.workspace()?.querySelectorAll<HTMLTextAreaElement>('[data-testid="variable-field"]') ?? []).filter(shown);
    if (new Set(fields.map(field => field.dataset.variableName)).size !== fields.length) throw new Error('UI_TARGET_AMBIGUOUS');
    return fields;
  }
  variable(name: string): HTMLTextAreaElement | null { return this.unique(this.variables().filter(node => node.dataset.variableName === name)); }
  candidates(): HTMLElement[] { return Array.from(this.library()?.querySelectorAll<HTMLElement>('[data-testid="library-prompt"]') ?? []).filter(shown); }
  prompt(id: string | null): HTMLElement | null { return this.unique(this.candidates().filter(node => node.dataset.promptId === id)); }
  input(id: string): HTMLInputElement | HTMLTextAreaElement | null {
    const root = id === 'library-search' ? this.library() : this.workspace();
    return this.unique(Array.from(root?.querySelectorAll<HTMLInputElement | HTMLTextAreaElement>('input[id], textarea[id]') ?? []).filter(node => node.id === id));
  }
  providerSelect(): HTMLSelectElement | null {
    return this.unique(Array.from(this.provider()?.querySelectorAll<HTMLSelectElement>('select[id="prepared-provider"]') ?? []));
  }
  control(key: AgentControl, runId?: string): HTMLElement | null {
    const scopes: Record<Owner, () => ParentNode | null> = {
      application: () => document, library: () => this.library(), workspace: () => this.workspace(), provider: () => this.provider(),
    };
    const root = scopes[owners[key]]();
    if (key === 'approval.apply' && !runId) throw new Error('UI_PRECONDITION');
    return this.unique(Array.from(root?.querySelectorAll<HTMLElement>('[data-nova-control]') ?? []).filter(node => node.dataset.novaControl === key && (key !== 'approval.apply' || node.dataset.novaRunId === runId)));
  }
}

export const agentUiSurface = new AgentUiSurface();
