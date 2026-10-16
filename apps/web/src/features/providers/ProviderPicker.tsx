import { useEffect, useRef, useState } from 'react';
import { z } from 'zod';
import { ArrowUpRight, Copy, Play, Square } from 'lucide-react';
import { Dialog } from '../../components/ui/Dialog';
import { compileSchema, emptySchema, errorMessage, post, request, type Prompt } from '../../services/api';
import { readLocalStream } from '../../services/localStream';

const providersSchema = z.array(z.object({ providerId: z.string(), displayName: z.string(), destinationUrl: z.string().url().nullable(), modesSupported: z.array(z.string()), canSubmit: z.boolean(), availabilityStatus: z.string() }));
const transferSchema = compileSchema.extend({ destinationUrl: z.string().url(), displayName: z.string(), mode: z.literal('COPY_AND_OPEN') });
export function ProviderPicker({ prompt, values, onClose, notify: announce, onUsed }: {
  prompt: Prompt; values: Record<string, string>; onClose: () => void; notify: (text: string) => void; onUsed: () => void;
}) {
  const [providers, setProviders] = useState<z.infer<typeof providersSchema>>([]);
  const [selected, setSelected] = useState('ollama');
  const [models, setModels] = useState<string[]>([]);
  const [model, setModel] = useState('qwen3:8b');
  const [compiled, setCompiled] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [response, setResponse] = useState('');
  const [complete, setComplete] = useState(false);
  const [manualUrl, setManualUrl] = useState('');
  const [feedback, setFeedback] = useState('');
  function notify(text: string) { setFeedback(text); announce(text); }
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    const initial = new AbortController();
    Promise.all([
      request('/providers', providersSchema, { signal: initial.signal }).then(setProviders),
      request(`/prompts/${prompt.id}/compile`, compileSchema, post({ revision: prompt.revision, values }, initial.signal)).then(result => setCompiled(result.text)),
      request('/ai/local/models', z.object({ models: z.array(z.string()) }), { signal: initial.signal }).then(result => { setModels(result.models); if (result.models.includes('qwen3:8b')) setModel('qwen3:8b'); }),
    ]).catch(error => { if (!initial.signal.aborted) setError(errorMessage(error)); });
    return () => { initial.abort(); controller.current?.abort(); };
  }, [prompt.id, prompt.revision, values]);
  async function recordUse(expectedText: string) {
    try { await request(`/prompts/${prompt.id}/usage`, emptySchema, post({ revision: prompt.revision, values, operationId: crypto.randomUUID(), expectedText })); onUsed(); }
    catch { notify('Acción completada. No se pudo registrar el uso.'); }
  }
  async function external() {
    const destination = providers.find(provider => provider.providerId === selected);
    if (!destination?.destinationUrl || busy) return;
    // Reserve a window during the user gesture; no prompt ever goes into its URL.
    const windowRef = window.open('about:blank', '_blank');
    if (windowRef) windowRef.opener = null;
    const current = new AbortController(); controller.current = current;
    setBusy(true); setError(''); setManualUrl('');
    try {
      const result = await request('/transfers/prepare', transferSchema, post({ promptId: prompt.id, revision: prompt.revision, values, providerId: selected, confirmed: true }, current.signal));
      if (result.destinationUrl !== destination.destinationUrl) throw new Error('El destino cambió. Selecciónalo de nuevo.');
      await navigator.clipboard.writeText(result.text);
      if (current.signal.aborted) { windowRef?.close(); return; }
      if (windowRef) windowRef.location.replace(result.destinationUrl); else setManualUrl(result.destinationUrl);
      notify('Texto copiado. Pégalo en el destino y envíalo cuando decidas.');
      await recordUse(result.text);
    } catch (error) { windowRef?.close(); if (!current.signal.aborted) setError(errorMessage(error)); }
    finally { if (!current.signal.aborted) setBusy(false); }
  }
  async function execute() {
    if (busy) return;
    const current = new AbortController(); controller.current = current;
    setBusy(true); setError(''); setResponse(''); setComplete(false);
    try {
      const result = await fetch('/api/v1/ai/local/execute', { ...post({ promptId: prompt.id, revision: prompt.revision, values, model, confirmed: true, expectedText: compiled }, current.signal), headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' } });
      const text = await readLocalStream(result, prompt.revision, value => { if (controller.current === current && !current.signal.aborted) setResponse(value); }, current.signal);
      if (current.signal.aborted || controller.current !== current) return;
      setResponse(text); setComplete(true);
      await recordUse(compiled);
    } catch (error) { if (controller.current === current && !current.signal.aborted) { setComplete(false); setError(errorMessage(error)); } }
    finally { if (controller.current === current && !current.signal.aborted) setBusy(false); }
  }
  function cancel() { controller.current?.abort(); setBusy(false); setComplete(false); setResponse(''); setError('Cancelado. No se guardó ningún resultado parcial.'); }
  return <Dialog open onOpenChange={open => { if (!open) { controller.current?.abort(); onClose(); } }} title="Lleva tu instrucción más lejos" description="Ejecuta en tu equipo o copia y abre un destino. El envío a un sitio externo lo decides tú.">
    <div className="provider-options" role="group" aria-label="Destino">{providers.map(provider => <button key={provider.providerId} className="provider-option" disabled={busy} aria-pressed={selected === provider.providerId} onClick={() => { setSelected(provider.providerId); setError(''); setResponse(''); setComplete(false); }}>{provider.displayName}</button>)}</div>
    <section className="transfer-preview" aria-labelledby="transfer-heading"><h3 id="transfer-heading">Revisar prompt final</h3><pre>{compiled}</pre></section>
    {selected === 'ollama' ? <><label htmlFor="execute-model">Modelo instalado</label><select id="execute-model" value={model} disabled={busy} onChange={e => setModel(e.target.value)}><option value="qwen3:8b" disabled={!models.includes('qwen3:8b')}>Qwen3 · 8B{models.includes('qwen3:8b') ? '' : ' · no disponible'}</option><option value="qwen2.5:7b-instruct" disabled={!models.includes('qwen2.5:7b-instruct')}>Qwen2.5 · 7B{models.includes('qwen2.5:7b-instruct') ? '' : ' · no disponible'}</option></select>
      <div className="dialog-actions">{busy ? <button className="secondary" onClick={cancel}><Square size={16} aria-hidden="true" />Detener</button> : <button className="primary" onClick={execute} disabled={!compiled || !models.includes(model)}><Play size={16} aria-hidden="true" />Ejecutar en este equipo</button>}</div>
      {models.length === 0 && <p className="muted">Ollama no está disponible o no tiene un modelo compatible. Puedes seguir copiando.</p>}
      {response && <div className="local-response"><p className="eyebrow">{complete ? 'RESPUESTA COMPLETA · SIN GUARDAR' : busy ? 'RESPUESTA EN CURSO' : 'RESPUESTA INCOMPLETA · SIN GUARDAR'}</p><pre>{response}</pre>{complete && <button className="secondary" onClick={async () => { try { await navigator.clipboard.writeText(response); notify('Respuesta copiada.'); } catch (error) { setError(errorMessage(error)); } }}><Copy size={16} aria-hidden="true" />Copiar respuesta</button>}</div>}
    </> : <><p className="muted">Se copiará el texto y se abrirá el sitio. Pegar y enviar son pasos manuales.</p><div className="dialog-actions"><button className="primary" disabled={busy || !compiled} onClick={external}><ArrowUpRight size={17} aria-hidden="true" />{busy ? 'Preparando…' : 'Copiar y abrir'}</button></div>{manualUrl && <a className="destination-link" href={manualUrl} target="_blank" rel="noopener noreferrer">Texto copiado. Abrir destino</a>}</>}
    {error && <p className="error" role="alert">{error}</p>}
    {feedback && <p className="notice" role="status">{feedback}</p>}
  </Dialog>;
}
