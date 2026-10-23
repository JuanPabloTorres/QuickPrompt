import { useState } from 'react';
import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { AgentUiDriver, readUiState, type UiAction } from '../src/features/nova/AgentUiDriver';
import { agentUiSurface } from '../src/features/nova/AgentUiSurface';

beforeEach(() => {
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([{}] as unknown as DOMRectList);
  HTMLElement.prototype.scrollIntoView = vi.fn();
  vi.stubGlobal('matchMedia', () => ({ matches: true }));
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

it('observes values and readiness only from the workspace that owns the prompt', () => {
  render(<><section data-testid="prompt-focus" data-prompt-id="active" data-revision="3" data-template-text="Explica <tema>.">
    <h1 data-testid="focus-title">Actual</h1><textarea data-testid="variable-field" data-variable-name="tema" defaultValue="Material real" />
    <pre data-testid="compiled-preview">Explica &lt;tema&gt;.</pre>
  </section><aside><textarea data-testid="variable-field" data-variable-name="otro" defaultValue="Datos ajenos" />
    <span data-testid="compile-ready" /><pre data-testid="compiled-preview">Resultado ajeno</pre></aside></>);
  const state = readUiState();
  expect(state.values).toEqual({ tema: 'Material real' }); expect(state.variableNames).toEqual(['tema']);
  expect(state.compiledReady).toBe(false); expect(state.compiledText).toBeNull();
});

it('fills the active workspace field without touching a same-named field elsewhere', async () => {
  const foreign = vi.fn(), own = vi.fn();
  function Workspace() {
    const [value, setValue] = useState('Original');
    return <><aside><textarea aria-label="Ajeno" data-testid="variable-field" data-variable-name="tema" defaultValue="Conservar" onChange={foreign} /></aside>
      <section data-testid="prompt-focus" data-prompt-id="active" data-revision="1" data-template-text="Explica <tema>.">
        <h1 data-testid="focus-title">Actual</h1><textarea aria-label="Real" data-testid="variable-field" data-variable-name="tema" value={value} onChange={event => { own(event.target.value); setValue(event.target.value); }} />
        <span data-testid="compile-ready" /><pre data-testid="compiled-preview">Explica {value}.</pre>
      </section></>;
  }
  render(<Workspace />); const driver = new AgentUiDriver(vi.fn()); driver.speed = 'fast';
  const until = driver.until.bind(driver); driver.until = (read, timeout = 20000) => until(read, Math.min(timeout, 250));
  const result = await driver.execute({ tool: 'ui_fill_variable', target: 'active', arguments: { name: 'tema', value: 'Nuevo contexto' } } as unknown as UiAction);
  expect(result.values).toEqual({ tema: 'Nuevo contexto' }); expect(screen.getByLabelText('Ajeno')).toHaveProperty('value', 'Conservar');
  expect(foreign).not.toHaveBeenCalled(); expect(own).toHaveBeenCalledWith('Nuevo contexto');
});

it('rejects a read-only field before dispatching input or replacing its value', async () => {
  render(<section data-testid="prompt-editor"><input id="prompt-title" aria-label="Título protegido" defaultValue="Conservar" readOnly /><textarea id="prompt-content" defaultValue="Texto." /></section>);
  const node = screen.getByLabelText('Título protegido'); const input = vi.fn(); node.addEventListener('input', input);
  const driver = new AgentUiDriver(vi.fn()); driver.speed = 'fast';
  await expect(driver.write('prompt-title', 'Sobrescribir')).rejects.toThrow('UI_FIELD_UNAVAILABLE');
  expect(node).toHaveProperty('value', 'Conservar'); expect(input).not.toHaveBeenCalled();
});

it('uses declared controls within their owner independently of captions and foreign buttons', async () => {
  const own = vi.fn(), foreign = vi.fn();
  render(<><button data-nova-control="prompt.edit" onClick={foreign}>Editar</button>
    <section data-testid="prompt-focus"><button data-nova-control="prompt.edit" onClick={own}>Modify instruction</button></section></>);
  const driver = new AgentUiDriver(vi.fn()); driver.speed = 'fast'; await driver.click('prompt.edit');
  expect(own).toHaveBeenCalledOnce(); expect(foreign).not.toHaveBeenCalled();
});

it('rejects ambiguous controls and fields before choosing or writing any of them', async () => {
  const clicked = vi.fn();
  render(<section data-testid="prompt-focus"><button data-nova-control="prompt.edit" onClick={clicked}>A</button><button data-nova-control="prompt.edit" onClick={clicked}>B</button>
    <textarea data-testid="variable-field" data-variable-name="tema" defaultValue="Primero" /><textarea data-testid="variable-field" data-variable-name="tema" defaultValue="Segundo" /></section>);
  const driver = new AgentUiDriver(vi.fn()); driver.speed = 'fast';
  await expect(driver.click('prompt.edit')).rejects.toThrow('UI_TARGET_AMBIGUOUS');
  expect(() => readUiState()).toThrow('UI_TARGET_AMBIGUOUS'); expect(clicked).not.toHaveBeenCalled();
  expect(screen.getAllByRole('textbox').map(field => (field as HTMLTextAreaElement).value)).toEqual(['Primero', 'Segundo']);
});

it('revalidates control identity after progress before emitting the click', async () => {
  const clicked = vi.fn(); render(<section data-testid="prompt-focus"><button data-nova-control="prompt.edit" onClick={clicked}>Modificar</button></section>);
  const driver = new AgentUiDriver(vi.fn(), () => { screen.getByText('Modificar').removeAttribute('data-nova-control'); }); driver.speed = 'fast';
  await expect(driver.click('prompt.edit')).rejects.toThrow('UI_TARGET_CHANGED'); expect(clicked).not.toHaveBeenCalled();
});

it('revalidates a variable binding after progress before emitting input', async () => {
  render(<section data-testid="prompt-focus" data-prompt-id="active" data-revision="1" data-template-text="Explica <tema>.">
    <textarea aria-label="Binding real" data-testid="variable-field" data-variable-name="tema" defaultValue="Conservar" />
  </section>);
  const field = screen.getByLabelText('Binding real'), changed = vi.fn(); field.addEventListener('input', changed);
  const driver = new AgentUiDriver(vi.fn(), () => { field.removeAttribute('data-variable-name'); }); driver.speed = 'fast';
  const until = driver.until.bind(driver); driver.until = (read, timeout = 20000) => until(read, Math.min(timeout, 250));
  await expect(driver.execute({ tool: 'ui_fill_variable', target: 'active', arguments: { name: 'tema', value: 'Sobrescribir' } } as unknown as UiAction)).rejects.toThrow('UI_TARGET_CHANGED');
  expect(field).toHaveProperty('value', 'Conservar'); expect(changed).not.toHaveBeenCalled();
});

it('binds approval controls to the current run even if another activity panel is visible', async () => {
  const own = vi.fn(), foreign = vi.fn();
  render(<><aside data-testid="nova-live" data-run-id="other"><button data-nova-control="approval.apply" onClick={foreign}>Aplicar</button></aside>
    <section data-testid="prompt-editor"><button data-nova-control="approval.apply" data-nova-run-id="current" onClick={own}>Apply approved draft</button></section></>);
  const driver = new AgentUiDriver(vi.fn()); driver.speed = 'fast'; await driver.click('approval.apply', 'current');
  expect(own).toHaveBeenCalledOnce(); expect(foreign).not.toHaveBeenCalled(); expect(agentUiSurface.control('approval.apply', 'missing')).toBeNull();
});

it('closes only the declared provider dialog and rejects multiple active workspaces', async () => {
  const own = vi.fn(), foreign = vi.fn();
  render(<><div role="dialog"><button data-nova-control="dialog.close" onClick={foreign}>Close foreign</button></div>
    <div role="dialog"><div data-testid="prepared-provider-dialog" /><button data-nova-control="dialog.close" onClick={own}>Fermer</button></div></>);
  const driver = new AgentUiDriver(vi.fn()); driver.speed = 'fast'; await driver.click('dialog.close'); expect(own).toHaveBeenCalledOnce(); expect(foreign).not.toHaveBeenCalled();
  render(<><section data-testid="prompt-focus" /><section data-testid="prompt-editor" /></>); expect(() => readUiState()).toThrow('UI_TARGET_AMBIGUOUS');
});
