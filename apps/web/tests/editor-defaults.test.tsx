import { useState } from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { InlinePromptEditor } from '../src/features/prompts/InlinePromptEditor';
import type { PromptDraft } from '../src/services/api';

afterEach(() => {cleanup(); vi.unstubAllGlobals();});
const seed: PromptDraft = {title:'Prueba de defaults',templateText:'Explica <tema> para <audiencia>.',description:'',categoryId:0,revision:2};
function setup({locked=false, saving=false, defaults={tema:'C# & .NET 🚀',audiencia:'principiantes'}} = {}) {
  const save = vi.fn();
  const fetch = vi.fn(async (path:string, options?:RequestInit) => {
    if(path.endsWith('/categories')) return Response.json([{id:0,name:'General'}]);
    if(path.endsWith('/inspect')) return Response.json({variableNames: JSON.parse(options!.body as string).templateText === seed.templateText ? ['tema','audiencia'] : []});
    throw Error('Unexpected route '+path);
  });
  vi.stubGlobal('fetch',fetch);
  function Frame() {
    const [draft,setDraft] = useState<PromptDraft>(seed);
    return <InlinePromptEditor draft={draft} onChange={setDraft} sourceDefaults={defaults} metadataLocked={locked}
      saving={saving} editing promptId="defaults-owner" error="" onBack={vi.fn()} onSave={() => save(draft)} />;
  }
  const view = render(<Frame />);
  return {view,save,fetch};
}
async function field(name='tema') {
  await waitFor(() => expect(screen.getByText('Valores por defecto de las variables')).toBeTruthy());
  fireEvent.click(screen.getByText('Valores por defecto de las variables'));
  return screen.getByLabelText('Valor por defecto de '+name) as HTMLTextAreaElement;
}
it('edits literal defaults through the owning draft and preserves other defaults and template',async () => {
  const {save,fetch} = setup(); const control = await field();
  expect(control.value).toBe('C# & .NET 🚀');
  fireEvent.change(control,{target:{value:'α\n<literal> & 李'}});
  fireEvent.submit(screen.getByRole('button',{name:'Guardar versión'}).closest('form')!);
  expect(save).toHaveBeenCalledWith({...seed,defaults:{tema:'α\n<literal> & 李',audiencia:'principiantes'}});
  expect(fetch.mock.calls.every(([path]) => !path.includes('/prompts'))).toBe(true);
});
it('removing a default makes the field required and undo/redo restores both states',async () => {
  const {save} = setup(); const control = await field();
  fireEvent.change(control,{target:{value:''}});
  fireEvent.submit(screen.getByRole('button',{name:'Guardar versión'}).closest('form')!);
  expect(save.mock.calls.at(-1)![0].defaults).toEqual({audiencia:'principiantes'});
  fireEvent.click(screen.getByRole('button',{name:'Deshacer'})); expect(control.value).toBe('C# & .NET 🚀');
  fireEvent.click(screen.getByRole('button',{name:'Rehacer'})); expect(control.value).toBe('');
});
it.each([{locked:true},{saving:true}])('locks default controls during an active proposal or save: %j',async settings => {
  setup(settings); const control = await field(); expect(control.disabled).toBe(true);
});
it('hides old default targets immediately when the template changes',async () => {
  setup(); await field();
  fireEvent.change(screen.getByLabelText('Contenido'),{target:{value:'Texto sin campos.'}});
  expect(screen.queryByLabelText('Valor por defecto de tema')).toBeNull();
  await waitFor(() => expect(screen.queryByText('Valores por defecto de las variables')).toBeNull());
});
