import { afterEach, expect, it, vi } from 'vitest';
import { createEditorContextPreparer, type EditorContextSnapshot } from '../src/features/prompts/EditorContextPreparation';
import { createApiClient, type HttpTransport } from '../src/services/api';
import { createApplicationServices } from '../src/services/application';
import type { EditorDraftInput, PreparedDraft } from '../src/services/preparation';

const sessionId = '11000000-0000-4000-8000-000000000001';
const source = { id: '22000000-0000-4000-8000-000000000002', revision: 3 };
const ready: PreparedDraft = { id: '33000000-0000-4000-8000-000000000003', state: 'READY', intent: 'CREATE_PROMPT', decision: 'CREATE_NEW',
  title: 'Contexto', description: '', templateText: 'Explica <tema>.', compiledText: null, variables: [], values: [], missingVariables: [],
  question: '', reason: '', candidates: [], sourceId: null, sourceRevision: null, categoryId: 0, remembered: false, createdAt: '' };
const snapshot = (): EditorContextSnapshot => ({ source, title: 'Contexto', templateText: 'Explica <tema>.', values: { tema: 'C# & .NET 🚀' }, sessionId });
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });

const payloadCases: { name: string; source: EditorContextSnapshot['source']; defaults?: Record<string, string>;
  values: Record<string, string>; expected: Record<string, string> }[] = [
  { name: 'new source and allowed names', source: null, defaults: undefined, values: { tema: 'React', fuera: 'Excluir' }, expected: { tema: 'React' } },
  { name: 'explicit values over defaults', source, defaults: { tema: 'Anterior', audiencia: 'Lectores' }, values: { tema: 'Actual' }, expected: { tema: 'Actual', audiencia: 'Lectores' } },
  { name: 'explicit blank suppresses a default', source, defaults: { tema: 'Anterior' }, values: { tema: '' }, expected: {} },
  { name: 'nonempty literal whitespace is preserved', source, defaults: undefined, values: { tema: '  dato literal\n' }, expected: { tema: '  dato literal\n' } },
];
it.each(payloadCases)('prepares one typed payload for $name', async item => {
  const inspect = vi.fn().mockResolvedValue({ variableNames: ['tema', 'audiencia'] });
  const editorDraft = vi.fn().mockResolvedValue(ready);
  const signal = new AbortController().signal;
  const result = await createEditorContextPreparer({ inspect }, { editorDraft }).prepare({ ...snapshot(), source: item.source, defaults: item.defaults, values: item.values }, signal);
  expect(inspect).toHaveBeenCalledExactlyOnceWith('Explica <tema>.', signal);
  expect(editorDraft).toHaveBeenCalledExactlyOnceWith({ promptId: item.source?.id ?? null, revision: item.source?.revision ?? null,
    title: 'Contexto', templateText: 'Explica <tema>.', values: item.expected, sessionId }, signal);
  expect(result).toBe(ready);
});

it('selects own dictionary entries without treating Object properties as variable values', async () => {
  const editorDraft = vi.fn().mockResolvedValue(ready);
  const values: Record<string, string> = JSON.parse('{"__proto__":"dato propio"}');
  await createEditorContextPreparer({ inspect: async () => ({ variableNames: ['__proto__', 'constructor', 'toString'] }) }, { editorDraft })
    .prepare({ ...snapshot(), values });
  expect(editorDraft.mock.calls[0][0].values).toEqual(Object.fromEntries([['__proto__', 'dato propio']]));
});

it('captures source identity, text and values before waiting on an inspector', async () => {
  let release!: (value: { variableNames: string[] }) => void;
  const inspect = vi.fn(() => new Promise<{ variableNames: string[] }>(resolve => { release = resolve; }));
  const editorDraft = vi.fn().mockResolvedValue(ready);
  const input = { ...snapshot(), source: { ...source }, values: { tema: 'Original' }, defaults: { audiencia: 'Lectores' } };
  const pending = createEditorContextPreparer({ inspect }, { editorDraft }).prepare(input);
  input.source.revision = 99; input.title = 'Alterado'; input.templateText = 'Otro texto'; input.values.tema = 'Otro valor'; input.defaults.audiencia = 'Otra audiencia';
  release({ variableNames: ['tema', 'audiencia'] }); await pending;
  expect(editorDraft.mock.calls[0][0]).toEqual({ promptId: source.id, revision: 3, title: 'Contexto', templateText: 'Explica <tema>.',
    values: { tema: 'Original', audiencia: 'Lectores' }, sessionId });
});

it.each(['before', 'inspection', 'submission'])('preserves cancellation at %s without retry or false acknowledgement', async stage => {
  const controller = new AbortController(), reason = Error('Detenido');
  let releaseInspection!: (value: { variableNames: string[] }) => void, releaseSubmission!: (value: PreparedDraft) => void;
  const inspect = vi.fn(() => stage === 'inspection' ? new Promise<{ variableNames: string[] }>(resolve => { releaseInspection = resolve; }) : Promise.resolve({ variableNames: ['tema'] }));
  const editorDraft = vi.fn(() => stage === 'submission' ? new Promise<PreparedDraft>(resolve => { releaseSubmission = resolve; }) : Promise.resolve(ready));
  if (stage === 'before') controller.abort(reason);
  const pending = createEditorContextPreparer({ inspect }, { editorDraft }).prepare(snapshot(), controller.signal);
  if (stage === 'inspection') { controller.abort(reason); releaseInspection({ variableNames: ['tema'] }); }
  if (stage === 'submission') { await vi.waitFor(() => expect(editorDraft).toHaveBeenCalledOnce()); controller.abort(reason); releaseSubmission(ready); }
  await expect(pending).rejects.toBe(reason);
  expect(inspect).toHaveBeenCalledTimes(stage === 'before' ? 0 : 1);
  expect(editorDraft).toHaveBeenCalledTimes(stage === 'submission' ? 1 : 0);
});

it.each(['inspection', 'submission'])('preserves the %s error without another request', async stage => {
  const failure = Error('Fallo de puerto');
  const inspect = vi.fn(async () => { if (stage === 'inspection') throw failure; return { variableNames: ['tema'] }; });
  const editorDraft = vi.fn(async () => { throw failure; });
  await expect(createEditorContextPreparer({ inspect }, { editorDraft }).prepare(snapshot())).rejects.toBe(failure);
  expect(inspect).toHaveBeenCalledOnce(); expect(editorDraft).toHaveBeenCalledTimes(stage === 'submission' ? 1 : 0);
});

it('uses each supplied transport, existing JSON validation, headers and cancellation signal without global fetch', async () => {
  const globalFetch = vi.fn(() => { throw Error('Global transport forbidden'); }); vi.stubGlobal('fetch', globalFetch);
  const make = (label: string) => {
    const inputs: EditorDraftInput[] = [];
    const send = vi.fn<HttpTransport['send']>().mockImplementation(async (path, options) => {
      if (path.endsWith('/templates/inspect')) return Response.json({ variableNames: ['tema'] });
      if (path.endsWith('/nova/editor-drafts')) { inputs.push(JSON.parse(String(options.body))); return Response.json({ ...ready, title: label }); }
      throw Error('Unexpected route ' + path);
    });
    const services = createApplicationServices(createApiClient({ send }));
    return { send, inputs, preparer: createEditorContextPreparer(services.templates, services.preparation) };
  };
  const first = make('Primera conexión'), second = make('Segunda conexión'), signal = new AbortController().signal;
  expect((await first.preparer.prepare(snapshot(), signal)).title).toBe('Primera conexión');
  expect((await second.preparer.prepare({ ...snapshot(), source: null }, signal)).title).toBe('Segunda conexión');
  expect(first.inputs[0]).toEqual({ promptId: source.id, revision: 3, title: 'Contexto', templateText: 'Explica <tema>.', values: { tema: 'C# & .NET 🚀' }, sessionId });
  expect(second.inputs[0]).toMatchObject({ promptId: null, revision: null });
  for (const client of [first, second]) {
    expect(client.send).toHaveBeenCalledTimes(2);
    for (const [, options] of client.send.mock.calls) { expect(options.signal).toBe(signal); expect(new Headers(options.headers).get('X-QuickPrompt-Client')).toBe('nova-v1'); }
  }
  expect(globalFetch).not.toHaveBeenCalled();
});
