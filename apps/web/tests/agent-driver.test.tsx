import { useEffect, useState } from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { AgentUiDriver, type UiAction } from '../src/features/nova/AgentUiDriver';
import { PreparationCanvas } from '../src/features/nova/PreparationCanvas';
import { createRef } from 'react';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });
function workingAction(tool: string, args: Record<string, unknown> = {}): UiAction {
  return { runId: crypto.randomUUID(), actionId: crypto.randomUUID(), sequence: 1, timestamp: new Date().toISOString(),
    tool, arguments: args, target: null, executionStatus: 'PENDING', verificationStatus: 'PENDING' };
}
function setup() {
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([{ top: 0, left: 0, width: 100, height: 30 }] as unknown as DOMRectList);
  HTMLElement.prototype.scrollIntoView = vi.fn(); vi.stubGlobal('matchMedia', () => ({ matches: true }));
  const changed = vi.fn();
  function Editor() { const [title, setTitle] = useState(''), [body, setBody] = useState(''); return <section data-testid="prompt-editor" data-prompt-id=""><input id="prompt-title" aria-label="Título" value={title} onChange={event => { changed(event.target.value); setTitle(event.target.value); }} /><textarea id="prompt-content" aria-label="Contenido" value={body} onChange={event => setBody(event.target.value)} /><span data-testid="variables-inspected" /><span data-testid="compile-ready" /></section>; }
  render(<Editor />); return { changed, driver: new AgentUiDriver(vi.fn()) };
}
it('writes through controlled React onChange in observable chunks with exact Unicode content', async () => {
  const { changed, driver } = setup(); const text = 'Título técnico α 🚀 '.repeat(8);
  await driver.write('prompt-title', text); expect(changed.mock.calls.length).toBeGreaterThan(2); expect(screen.getByLabelText('Título')).toHaveProperty('value', text);
});
it('adapts a source-free working draft through native fields without requiring a saved prompt', async () => {
  const { driver } = setup(); driver.speed = 'fast';
  const action = (tool: string, args: Record<string, unknown> = {}) => ({ tool, arguments: args, target: null } as UiAction);
  await driver.write('prompt-title', 'Borrador'); await driver.write('prompt-content', 'Explica <tema>.');
  await driver.execute(action('set_working_context', { promptId: null, revision: null, title: 'Borrador', templateText: 'Explica <tema>.', values: {} }));
  const state = await driver.execute(action('ui_prepare_update', { title: 'Borrador', templateText: 'Explica <tema> con un ejemplo.' }));
  expect(state.promptId).toBeNull(); expect(state.templateText).toBe('Explica <tema> con un ejemplo.');
  expect(screen.getByLabelText('Contenido')).toHaveProperty('value', state.templateText);
});
it('applies a validated saved revision only to its matching editor identity', async () => {
  const { driver } = setup(); driver.speed = 'fast'; const id = crypto.randomUUID();
  document.querySelector('[data-testid="prompt-editor"]')!.setAttribute('data-prompt-id', id);
  await driver.write('prompt-title', 'Registro'); await driver.write('prompt-content', 'Contenido original.');
  const action = { ...workingAction('ui_prepare_update', { promptId: id, title: 'Registro', templateText: 'Contenido revisado.' }), target: id };
  const state = await driver.execute(action);
  expect(state.promptId).toBe(id); expect(state.templateText).toBe('Contenido revisado.');
  await expect(driver.execute({ ...action, target: crypto.randomUUID() })).rejects.toThrow();
  expect(screen.getByLabelText('Contenido')).toHaveProperty('value', 'Contenido revisado.');
});
it.each(['discover', 'focus'])('restores an unsaved artifact from %s through a temporary editor without editing the saved record', async initialMode => {
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([{}] as unknown as DOMRectList);
  HTMLElement.prototype.scrollIntoView = vi.fn(); vi.stubGlobal('matchMedia', () => ({ matches: true }));
  const edited = vi.fn(), savedId = crypto.randomUUID();
  function Workspace() {
    const [mode, setMode] = useState(initialMode), [title, setTitle] = useState(''), [body, setBody] = useState('');
    return <><button data-nova-control="library.home" aria-label="QuickPrompt NOVA, biblioteca" onClick={() => setMode('discover')}>Biblioteca</button>
      {mode === 'focus' && <section data-testid="prompt-focus" data-prompt-id={savedId} data-revision="1" data-template-text="Original guardado."><h1 data-testid="focus-title">Guardado</h1><button data-nova-control="prompt.edit" onClick={edited}>Editar</button><span data-testid="compile-ready" /><pre data-testid="compiled-preview">Original guardado.</pre></section>}
      {mode === 'discover' && <section data-nova-surface="library"><button data-nova-control="prompt.new" onClick={() => setMode('create')}>Nuevo prompt</button></section>}
      {mode === 'create' && <section data-testid="prompt-editor" data-prompt-id=""><input id="prompt-title" value={title} onChange={event => setTitle(event.target.value)} /><textarea id="prompt-content" value={body} onChange={event => setBody(event.target.value)} /><span data-testid="variables-inspected" /><span data-testid="compile-ready" /><pre data-testid="compiled-preview">{body}</pre></section>}</>;
  }
  render(<Workspace />); const driver = new AgentUiDriver(vi.fn()); driver.speed = 'fast';
  await driver.execute(workingAction('set_working_context', { promptId: null, revision: null, title: 'Temporal', templateText: 'Explica con ejemplos.', values: {} }));
  const state = await driver.execute(workingAction('ui_load_active_draft', { promptId: null }));
  expect(state.mode).toBe('create'); expect(state.promptId).toBeNull(); expect(state.templateText).toBe('Explica con ejemplos.'); expect(edited).not.toHaveBeenCalled();
});
it('preserves a different manual draft instead of overwriting it while restoring a checkpoint', async () => {
  const { driver, changed } = setup(); driver.speed = 'fast';
  await driver.write('prompt-title', 'Mi texto'); await driver.write('prompt-content', 'Mi edición manual.');
  await driver.execute(workingAction('set_working_context', { promptId: null, revision: null, title: 'Anterior', templateText: 'Contenido anterior.', values: {} }));
  const before = changed.mock.calls.length;
  await expect(driver.execute(workingAction('ui_load_active_draft', { promptId: null }))).rejects.toThrow('UI_WORKING_DRAFT_MISMATCH');
  expect(screen.getByLabelText('Contenido')).toHaveProperty('value', 'Mi edición manual.'); expect(changed.mock.calls.length).toBe(before);
});
it('pause stops chunks, resume continues and cancel prevents late writes', async () => {
  const { changed, driver } = setup(); const task = driver.write('prompt-title', 'Contenido extenso '.repeat(20));
  await waitFor(() => expect(changed).toHaveBeenCalled()); driver.paused = true; const count = changed.mock.calls.length;
  await new Promise(resolve => setTimeout(resolve, 100)); expect(changed).toHaveBeenCalledTimes(count);
  driver.paused = false; await waitFor(() => expect(changed.mock.calls.length).toBeGreaterThan(count)); driver.stop(); await expect(task).rejects.toThrow('UI_CANCELLED');
  const final = changed.mock.calls.length; await new Promise(resolve => setTimeout(resolve, 100)); expect(changed).toHaveBeenCalledTimes(final);
});
it('manual intervention stops writes and arbitrary tool names are rejected', async () => {
  const { driver } = setup(); driver.intervene(); await expect(driver.write('prompt-title', 'Sobrescribir')).rejects.toThrow('MANUAL_INTERVENTION');
  const other = new AgentUiDriver(vi.fn()); await expect(other.execute({ tool: 'eval', arguments: {}, target: null } as UiAction)).rejects.toThrow('UNKNOWN_UI_TOOL');
});
it('preserves a real concurrent edit instead of overwriting it after removing incidental-input pauses', async () => {
  const { changed, driver } = setup(); const task = driver.write('prompt-title', 'Contenido generado '.repeat(30));
  await waitFor(() => expect(changed).toHaveBeenCalled());
  fireEvent.change(screen.getByLabelText('Título'), { target: { value: 'Mi edición real' } });
  await expect(task).rejects.toThrow('MANUAL_INTERVENTION');
  expect(screen.getByLabelText('Título')).toHaveProperty('value', 'Mi edición real');
});
it('writes in a background document and excludes explicit pause time from control deadlines', async () => {
  const { driver } = setup(); driver.speed = 'fast';
  vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('hidden');
  await driver.write('prompt-title', 'Un primer intento completo');
  expect(screen.getByLabelText('Título')).toHaveProperty('value', 'Un primer intento completo');
  driver.paused = true;
  let ready = false; const pending = driver.until(() => ready && 'ready', 60);
  await new Promise(resolve => setTimeout(resolve, 140)); ready = true; driver.paused = false;
  await expect(pending).resolves.toBe('ready');
});
it('visual execution in this tab is the default with no parallel-browser option', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => Response.json([])));
  render(<PreparationCanvas inputRef={createRef()} onState={vi.fn()} onSaved={vi.fn()} sessionId={crypto.randomUUID()} />);
  expect(screen.getByRole('textbox', { name: '¿Qué quieres hacer hoy?' })).toBeTruthy(); expect(screen.queryByLabelText('Modo de NOVA')).toBeNull(); expect(screen.queryByText('Opciones de preparación')).toBeNull();
});
it('selects the requested provider through the controlled select and closes it for review without external navigation', async () => {
  const { driver } = setup(); driver.speed = 'fast'; const change = vi.fn(), open = vi.spyOn(window, 'open').mockImplementation(() => null);
  function Picker() { const [destination, setDestination] = useState('chatgpt'), [visible, setVisible] = useState(true); return <><span data-testid="compile-ready" />{visible && <div role="dialog"><div data-testid="prepared-provider-dialog" /><label htmlFor="prepared-provider">Destino externo</label><select id="prepared-provider" value={destination} onChange={event => { change(event.target.value); setDestination(event.target.value); }}><option value="chatgpt">ChatGPT</option><option value="claude">Claude</option></select><button data-nova-control="dialog.close" aria-label="Cerrar diálogo" onClick={() => setVisible(false)}>Cerrar</button></div>}</>; }
  render(<Picker />);
  const action = (tool: string, args: Record<string, unknown> = {}) => ({ tool, arguments: args, target: null } as UiAction);
  await driver.execute(action('start', { localExecutionAllowed: false, editRequest: '', providerId: 'claude' }));
  const prepared = await driver.execute(action('ui_open_provider_picker', { promptId: null }));
  expect(prepared.providerId).toBe('claude'); expect(change).toHaveBeenCalledWith('claude'); expect(open).not.toHaveBeenCalled();
  expect((await driver.execute(action('prepare_review'))).providerOpen).toBe(false);
});
it.each([false, true])('opens collapsed activity before applying an approved version or copy through its real control (copy=%s)', async copy => {
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([{}] as unknown as DOMRectList);
  HTMLElement.prototype.scrollIntoView = vi.fn();
  const applied = vi.fn(), id = crypto.randomUUID(), sourceId = crypto.randomUUID(), runId = crypto.randomUUID();
  function ApprovedEditor() {
    const [expanded, setExpanded] = useState(false), [saved, setSaved] = useState(false);
    useEffect(() => { const open = () => setExpanded(true); window.addEventListener('nova-open-activity', open); return () => window.removeEventListener('nova-open-activity', open); }, []);
    return <>{saved ? <section data-testid="prompt-focus" data-prompt-id={id} data-revision="1" data-template-text="Contenido aprobado."><h1 data-testid="focus-title">Aprobado</h1><span data-testid="compile-ready" /></section>
      : <section data-testid="prompt-editor" data-prompt-id={copy ? sourceId : ''} data-revision={copy ? 7 : undefined}><input id="prompt-title" value="Aprobado" readOnly /><textarea id="prompt-content" value="Contenido aprobado." readOnly /><span data-testid="variables-inspected" /><span data-testid="compile-ready" /></section>}
      <button aria-label="Actividad de NOVA" aria-expanded={expanded} onClick={() => setExpanded(previous => !previous)}>NOVA</button>
      {expanded && <aside data-testid="nova-live" data-run-id={runId}><button data-nova-control="approval.apply" onClick={() => { applied(); setSaved(true); }}>Aplicar versión aprobada</button></aside>}
    </>;
  }
  render(<ApprovedEditor />);
  // A prior native write leaves focus on the workspace. Panel layout must see
  // the actual next click target before it decides to uncover that old field.
  screen.getByDisplayValue('Contenido aprobado.').focus();
  const progress = vi.fn(async (phase: string) => {
    if (phase === 'TARGET_LOCATED') expect(document.activeElement?.textContent).toBe('Aplicar versión aprobada');
  });
  const driver = new AgentUiDriver(vi.fn(), progress); driver.speed = 'fast';
  const state = await driver.execute({ runId, tool: 'commit_approved', arguments: copy ? { expectedRevision: 1 } : {}, target: null } as UiAction);
  expect(applied).toHaveBeenCalledOnce(); expect(state.promptId).toBe(id); expect(state.revision).toBe(1);
});
it('reports an approved-write rejection immediately instead of waiting for a saved revision', async () => {
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([{}] as unknown as DOMRectList);
  HTMLElement.prototype.scrollIntoView = vi.fn();
  function RejectedEditor() {
    const [error, setError] = useState(false);
    return <><section data-testid="prompt-editor"><input id="prompt-title" value="Aprobado" readOnly /><textarea id="prompt-content" value="Contenido aprobado." readOnly /></section>
      <aside data-testid="nova-live" data-run-id="rejected-run"><button data-nova-control="approval.apply" onClick={() => setError(true)}>Aplicar versión aprobada</button></aside>{error && <p role="alert">La propuesta ya no admite este contenido.</p>}</>;
  }
  render(<RejectedEditor />); const driver = new AgentUiDriver(vi.fn()); driver.speed = 'fast';
  await expect(driver.execute({ runId: 'rejected-run', tool: 'commit_approved', arguments: {}, target: null } as UiAction)).rejects.toThrow('UI_REPORTED_ERROR');
});
