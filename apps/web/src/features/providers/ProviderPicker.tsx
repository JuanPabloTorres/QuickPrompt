import { useApplicationServices } from '../../components/ServiceContext';
import { NovaActivity, NovaProgress, TransferStatus } from '../../components/ui/Feedback';
import { AppIcon } from '../../components/ui/Icon';
import { useEffect, useMemo, useState } from 'react';
import { ArrowUpRight, Copy, Play, Square } from 'lucide-react';
import { Dialog } from '../../components/ui/Dialog';
import { errorMessage, type Prompt } from '../../services/api';
import { type ProviderCapability } from '../../services/providers';

import { readLocalStream } from '../../services/localStream';
import { useBoundOperation } from '../../services/useBoundOperation';

export function ProviderPicker({ prompt, values, onClose, notify: announce, onUsed }: {
  prompt: Prompt; values: Record<string, string>; onClose: () => void; notify: (text: string) => void; onUsed: () => void;
}) {
  const { providers: providerClient, prompts } = useApplicationServices();
  const [providers, setProviders] = useState<ProviderCapability[]>([]);
  const [selected, setSelected] = useState('ollama');
  const [models, setModels] = useState<string[]>([]);
  const [model, setModel] = useState('qwen3:8b');
  const snapshot = useMemo(() => ({ id: prompt.id, revision: prompt.revision, values }), [prompt.id, prompt.revision, values]);
  const operation = useBoundOperation(snapshot);
  const busy = operation.busy;
  const [compiledResult, setCompiledResult] = useState<{ snapshot: object; text: string } | null>(null);
  const compiled = compiledResult?.snapshot === snapshot ? compiledResult.text : '';
  const [error, setError] = useState('');
  const [response, setResponse] = useState('');
  const [complete, setComplete] = useState(false);
  const [manualUrl, setManualUrl] = useState('');
  const [feedback, setFeedback] = useState('');
  function notify(text: string) { setFeedback(text); announce(text); }
  const available = (provider: ProviderCapability) => provider.modesSupported.includes(provider.providerId === 'ollama' ? 'LOCAL_EXECUTION' : 'COPY_AND_OPEN') && (provider.providerId === 'ollama' || !!provider.destinationUrl) && !['UNAVAILABLE', 'OFFLINE'].includes(provider.availabilityStatus);
  const destinationAvailable = providers.some(provider => provider.providerId === selected && available(provider));
  useEffect(() => {
    const initial = new AbortController();
    setResponse(''); setComplete(false); setManualUrl(''); setFeedback(''); setError('');
    Promise.all([
      providerClient.list(initial.signal).then(result => { if (!initial.signal.aborted) setProviders(result); }),
      prompts.compile(prompt, values, initial.signal).then(result => { if (!initial.signal.aborted) setCompiledResult({ snapshot, text: result.text }); }),
      providerClient.models(initial.signal).then(result => { if (!initial.signal.aborted) { setModels(result.models); if (result.models.includes('qwen3:8b')) setModel('qwen3:8b'); } }),
    ]).catch(error => { if (!initial.signal.aborted) setError(errorMessage(error)); });
    return () => initial.abort();
  }, [snapshot, prompt.id, prompt.revision, values]);
  async function recordUse(expectedText: string, current: AbortController) {
    try { await prompts.recordUse(prompt, values, crypto.randomUUID(), expectedText); if (operation.isCurrent(current)) onUsed(); }
    catch { if (operation.isCurrent(current)) notify('Acción completada. No se pudo registrar el uso.'); }
  }
  async function external() {
    const destination = providers.find(provider => provider.providerId === selected);
    if (!destination?.destinationUrl || !available(destination) || busy) return;
    const current = operation.begin();
    if (!current) return;
    let windowRef: Window | null = null;
    setError(''); setManualUrl('');
    try {
      // Reserve a window during the user gesture; no prompt ever goes into its URL.
      windowRef = window.open('about:blank', '_blank');
      if (windowRef) windowRef.opener = null;
      const result = await providerClient.prepare({ promptId: prompt.id, revision: prompt.revision, values, providerId: selected, confirmed: true }, current.signal);
      if (!operation.isCurrent(current)) { windowRef?.close(); return; }
      if (result.destinationUrl !== destination.destinationUrl) throw new Error('El destino cambió. Selecciónalo de nuevo.');
      await navigator.clipboard.writeText(result.text);
      if (!operation.isCurrent(current)) { windowRef?.close(); return; }
      if (windowRef) windowRef.location.replace(result.destinationUrl); else setManualUrl(result.destinationUrl);
      notify('Texto copiado. Pégalo en el destino y envíalo cuando decidas.');
      await recordUse(result.text, current);
    } catch (error) { windowRef?.close(); if (!current.signal.aborted) setError(errorMessage(error)); }
    finally { operation.finish(current); }
  }
  async function execute() {
    const current = operation.begin();
    if (!current) return;
    setError(''); setResponse(''); setComplete(false);
    try {
      const result = await providerClient.execute({ promptId: prompt.id, revision: prompt.revision, values, model, confirmed: true, expectedText: compiled }, current.signal);
      const text = await readLocalStream(result, prompt.revision, value => { if (operation.isCurrent(current)) setResponse(value); }, current.signal);
      if (!operation.isCurrent(current)) return;
      setResponse(text); setComplete(true);
      await recordUse(compiled, current);
    } catch (error) { if (operation.isCurrent(current)) { setComplete(false); setError(errorMessage(error)); } }
    finally { operation.finish(current); }
  }
  function cancel() { operation.cancel(); setComplete(false); setResponse(''); setError('Cancelado. No se guardó ningún resultado parcial.'); }
  return <Dialog open onOpenChange={open => { if (!open) { operation.cancel(); onClose(); } }} title="Lleva tu instrucción más lejos" description="Ejecuta en tu equipo o copia y abre un destino. El envío a un sitio externo lo decides tú.">
    <div className="provider-options" role="group" aria-label="Destino">{providers.map(provider => <button key={provider.providerId} className="provider-option" disabled={busy || !available(provider)} aria-pressed={selected === provider.providerId} onClick={() => { setSelected(provider.providerId); setError(''); setResponse(''); setComplete(false); }}>{provider.displayName}{!available(provider) && ' · no disponible'}</button>)}</div>
    <section className="transfer-preview" aria-labelledby="transfer-heading"><h3 id="transfer-heading">Revisar prompt final</h3><pre>{compiled}</pre></section>
    {selected === 'ollama' ? <><label htmlFor="execute-model">Modelo instalado</label><select id="execute-model" value={model} disabled={busy} onChange={e => setModel(e.target.value)}><option value="qwen3:8b" disabled={!models.includes('qwen3:8b')}>Qwen3 · 8B{models.includes('qwen3:8b') ? '' : ' · no disponible'}</option><option value="qwen2.5:7b-instruct" disabled={!models.includes('qwen2.5:7b-instruct')}>Qwen2.5 · 7B{models.includes('qwen2.5:7b-instruct') ? '' : ' · no disponible'}</option></select>
      <div className="dialog-actions">{busy ? <button className="secondary" onClick={cancel}><AppIcon glyph={Square} size={16} aria-hidden="true"  />Detener</button> : <button className="primary" onClick={execute} disabled={!compiled || !models.includes(model) || !destinationAvailable}><AppIcon glyph={Play} size={16} aria-hidden="true"  />Ejecutar en este equipo</button>}</div>
      {models.length === 0 && <p className="muted"><NovaActivity state="offline">Ollama no está disponible o no tiene un modelo compatible. Puedes seguir copiando.</NovaActivity></p>}
      {busy && <NovaProgress>Qwen está preparando la respuesta…</NovaProgress>}
      {response && <div className="local-response"><p className="eyebrow">{complete ? 'RESPUESTA COMPLETA · SIN GUARDAR' : busy ? 'RESPUESTA EN CURSO' : 'RESPUESTA INCOMPLETA · SIN GUARDAR'}</p><pre>{response}</pre>{complete && <button className="secondary" onClick={async () => { try { await navigator.clipboard.writeText(response); notify('Respuesta copiada.'); } catch (error) { setError(errorMessage(error)); } }}><AppIcon glyph={Copy} size={16} aria-hidden="true"  />Copiar respuesta</button>}</div>}
    </> : <><p className="muted">Se copiará el texto y se abrirá el sitio. Pegar y enviar son pasos manuales.</p><div className="dialog-actions"><button className="primary" disabled={busy || !compiled || !destinationAvailable} onClick={external}><AppIcon glyph={ArrowUpRight} size={20} aria-hidden="true"  />{busy ? 'Preparando…' : 'Copiar y abrir'}</button></div>{manualUrl && <a className="destination-link" href={manualUrl} target="_blank" rel="noopener noreferrer">Texto copiado. Abrir destino</a>}</>}
    {error && <p className="error" role="alert">{error}</p>}
    {feedback && <TransferStatus>{feedback}</TransferStatus>}
  </Dialog>;
}
