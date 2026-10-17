import { useState } from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { AgentUiDriver, type UiAction } from '../src/features/nova/AgentUiDriver';
import { PreparationCanvas } from '../src/features/nova/PreparationCanvas';
import { createRef } from 'react';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });
function setup() {
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([{ top: 0, left: 0, width: 100, height: 30 }] as unknown as DOMRectList);
  HTMLElement.prototype.scrollIntoView = vi.fn(); vi.stubGlobal('matchMedia', () => ({ matches: true }));
  const changed = vi.fn();
  function Editor() { const [title, setTitle] = useState(''), [body, setBody] = useState(''); return <section data-testid="prompt-editor" data-prompt-id=""><input id="prompt-title" aria-label="Título" value={title} onChange={event => { changed(event.target.value); setTitle(event.target.value); }} /><textarea id="prompt-content" aria-label="Contenido" value={body} onChange={event => setBody(event.target.value)} /><span data-testid="variables-inspected" /></section>; }
  render(<Editor />); return { changed, driver: new AgentUiDriver(vi.fn()) };
}
it('writes through controlled React onChange in observable chunks with exact Unicode content', async () => {
  const { changed, driver } = setup(); const text = 'Título técnico α 🚀 '.repeat(8);
  await driver.write('prompt-title', text); expect(changed.mock.calls.length).toBeGreaterThan(2); expect(screen.getByLabelText('Título')).toHaveProperty('value', text);
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
  expect(screen.getByLabelText('Modo de NOVA')).toHaveProperty('value', 'SAME_TAB'); expect(screen.queryByText('Navegador visible · Edge')).toBeNull();
});
it('selects the requested provider through the controlled select and closes it for review without external navigation', async () => {
  const { driver } = setup(); driver.speed = 'fast'; const change = vi.fn(), open = vi.spyOn(window, 'open').mockImplementation(() => null);
  function Picker() { const [destination, setDestination] = useState('chatgpt'), [visible, setVisible] = useState(true); return <><span data-testid="compile-ready" />{visible && <div><div data-testid="prepared-provider-dialog" /><label htmlFor="prepared-provider">Destino externo</label><select id="prepared-provider" value={destination} onChange={event => { change(event.target.value); setDestination(event.target.value); }}><option value="chatgpt">ChatGPT</option><option value="claude">Claude</option></select><button aria-label="Cerrar diálogo" onClick={() => setVisible(false)}>Cerrar</button></div>}</>; }
  render(<Picker />);
  const action = (tool: string, args: Record<string, unknown> = {}) => ({ tool, arguments: args, target: null } as UiAction);
  await driver.execute(action('start', { localExecutionAllowed: false, editRequest: '', providerId: 'claude' }));
  const prepared = await driver.execute(action('ui_open_provider_picker', { promptId: null }));
  expect(prepared.providerId).toBe('claude'); expect(change).toHaveBeenCalledWith('claude'); expect(open).not.toHaveBeenCalled();
  expect((await driver.execute(action('prepare_review'))).providerOpen).toBe(false);
});
it('opens collapsed activity before applying an approved version through its real control', async () => {
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([{}] as unknown as DOMRectList);
  HTMLElement.prototype.scrollIntoView = vi.fn();
  const applied = vi.fn(), id = crypto.randomUUID();
  function ApprovedEditor() {
    const [expanded, setExpanded] = useState(false), [saved, setSaved] = useState(false);
    return <>{saved ? <section data-testid="prompt-focus" data-prompt-id={id} data-revision="1" data-template-text="Contenido aprobado."><h1 data-testid="focus-title">Aprobado</h1></section>
      : <section data-testid="prompt-editor" data-prompt-id=""><input id="prompt-title" value="Aprobado" readOnly /><textarea id="prompt-content" value="Contenido aprobado." readOnly /><span data-testid="variables-inspected" /></section>}
      <button aria-label="Actividad de NOVA" aria-expanded={expanded} onClick={() => setExpanded(previous => !previous)}>NOVA</button>
      {expanded && <button onClick={() => { applied(); setSaved(true); }}>Aplicar versión aprobada</button>}
    </>;
  }
  render(<ApprovedEditor />); const driver = new AgentUiDriver(vi.fn()); driver.speed = 'fast';
  const state = await driver.execute({ tool: 'commit_approved', arguments: {}, target: null } as UiAction);
  expect(applied).toHaveBeenCalledOnce(); expect(state.promptId).toBe(id); expect(state.revision).toBe(1);
});
