import type { Prompt } from '../../services/api';
import type { TemplateClient } from '../../services/library';
import type { PreparationClient, PreparedDraft } from '../../services/preparation';

export interface EditorContextSnapshot {
  readonly source: Pick<Prompt, 'id' | 'revision'> | null;
  readonly title: string;
  readonly templateText: string;
  readonly defaults?: Readonly<Record<string, string>>;
  readonly values: Readonly<Record<string, string>>;
  readonly sessionId: string;
}
export interface EditorContextPreparer {
  prepare(snapshot: EditorContextSnapshot, signal?: AbortSignal): Promise<PreparedDraft>;
}

class InspectedEditorContextPreparer implements EditorContextPreparer {
  constructor(private readonly templates: Pick<TemplateClient, 'inspect'>,
    private readonly preparation: Pick<PreparationClient, 'editorDraft'>) {}

  async prepare(snapshot: EditorContextSnapshot, signal?: AbortSignal): Promise<PreparedDraft> {
    signal?.throwIfAborted();
    const identity = { promptId: snapshot.source?.id ?? null, revision: snapshot.source?.revision ?? null,
      title: snapshot.title, templateText: snapshot.templateText, sessionId: snapshot.sessionId };
    const available = new Map(Object.entries({ ...snapshot.defaults, ...snapshot.values }));
    const inspected = await this.templates.inspect(identity.templateText, signal);
    signal?.throwIfAborted();
    const values = Object.fromEntries(inspected.variableNames.filter(name => available.get(name)?.trim()).map(name => [name, available.get(name)!]));
    const result = await this.preparation.editorDraft({ ...identity, values }, signal);
    signal?.throwIfAborted();
    return result;
  }
}

export function createEditorContextPreparer(templates: Pick<TemplateClient, 'inspect'>,
  preparation: Pick<PreparationClient, 'editorDraft'>): EditorContextPreparer {
  return new InspectedEditorContextPreparer(templates, preparation);
}
