import { downloadPreparedText } from '../../services/textExport';
import { useApplicationServices } from '../../components/ServiceContext';
import { useEffect, useRef, useState } from 'react';
import { Dialog } from '../../components/ui/Dialog';
import { errorMessage } from '../../services/api';
import { type ProviderCapability } from '../../services/providers';
import { readLocalStream } from '../../services/localStream';
import { type PreparedDraft } from '../../services/preparation';


export function PreparedUse({ draft, onClose, onCloseAutoFocus }: { draft: PreparedDraft; onClose: () => void; onCloseAutoFocus?: (event: Event) => void }) {
  const { providers: providerClient, preparation, live, visibleSession } = useApplicationServices();
  const { surfaceCapability } = visibleSession;
  const [busy, setBusy] = useState(false); const [response, setResponse] = useState(''); const [complete, setComplete] = useState(false); const [error, setError] = useState(''); const [feedback, setFeedback] = useState('');
  const [destinations, setDestinations] = useState<ProviderCapability[]>([]);
  const [providerState, setProviderState] = useState<'loading' | 'ready' | 'error'>('loading');
  const [providerId, setProviderId] = useState(draft.providerId ?? 'chatgpt');
  const selected = destinations.find(item => item.providerId === providerId && item.modesSupported.includes('COPY_AND_OPEN') && item.destinationUrl && !['UNAVAILABLE', 'OFFLINE'].includes(item.availabilityStatus));
  useEffect(() => {
    const current = new AbortController();
    providerClient.list(current.signal).then(items => { if (!current.signal.aborted) { setDestinations(items); setProviderState('ready'); } })
      .catch(error => { if (!current.signal.aborted) { setProviderState('error'); setError(errorMessage(error)); } });
    return () => current.abort();
  }, []);
  const controller = useRef<AbortController | null>(null);
  const liveId = (() => { try { const id = sessionStorage.getItem('nova-live-run'); return id && surfaceCapability(id) ? id : new URLSearchParams(location.search).get('novaRun'); } catch { return null; } })();
  useEffect(() => { const abort = () => { controller.current?.abort(); setBusy(false); }; window.addEventListener('nova-abort-ui-operation', abort); return () => { controller.current?.abort(); window.removeEventListener('nova-abort-ui-operation', abort); }; }, []);
  function cancelExecution() {
    const runId = liveId;
    if (runId && /^[0-9a-f-]{36}$/i.test(runId)) void live.control(runId, 'cancel').catch(() => {});
    controller.current?.abort(); setBusy(false); setComplete(false); setResponse('');
  }
  async function execute() {
    if (busy) return;
    const current = new AbortController(); controller.current = current; setBusy(true); setError(''); setComplete(false); setResponse('');
    try {
      const result = await preparation.execute(draft.id, { operationId: crypto.randomUUID(), confirmed: true, expectedText: draft.compiledText, model: 'qwen3:8b' }, liveId, current.signal);
      const text = await readLocalStream(result, 1, setResponse, current.signal);
      if (!current.signal.aborted) { setResponse(text); setComplete(true); }
    } catch (error) { if (!current.signal.aborted) setError(errorMessage(error)); }
    finally { if (!current.signal.aborted) setBusy(false); }
  }
  async function copyAndOpen() {
    if (busy || !draft.compiledText || !selected) return;
    const destination = window.open('about:blank', '_blank'); if (destination) destination.opener = null;
    const current = new AbortController(); controller.current = current;
    setBusy(true); setError('');
    try {
      const providers = await providerClient.list(current.signal);
      const provider = providers.find(p => p.providerId === providerId && p.modesSupported.includes('COPY_AND_OPEN'));
      if (!provider?.destinationUrl || provider.destinationUrl !== selected.destinationUrl || ['UNAVAILABLE', 'OFFLINE'].includes(provider.availabilityStatus)) throw new Error('El destino no está habilitado. Puedes copiar el texto.');
      await navigator.clipboard.writeText(draft.compiledText);
      if (current.signal.aborted) { destination?.close(); return; }
      if (destination) destination.location.replace(provider.destinationUrl);
      setFeedback('Texto copiado. Pégalo y envíalo cuando decidas.');
    } catch (error) { destination?.close(); if (!current.signal.aborted) setError(errorMessage(error)); } finally { if (!current.signal.aborted) setBusy(false); }
  }
  return <Dialog open onOpenChange={open => { if (!open) { if (busy) cancelExecution(); onClose(); } }} onCloseAutoFocus={onCloseAutoFocus} title="Usa tu prompt preparado" description="Revisa el texto antes de ejecutar en tu equipo o copiar y abrir el destino elegido. Tú decides cuándo pegar y enviar.">
    <div data-testid="prepared-provider-dialog"><pre className="prepared-text">{draft.compiledText}</pre></div>
    <button type="button" className="text-button" disabled={busy || !draft.compiledText} title="UTF-8 sin BOM, saltos de línea LF" onClick={() => { try { downloadPreparedText(draft.compiledText!); } catch (error) { setError(errorMessage(error)); } }}>Descargar texto (.txt)</button>
    <div className="prepared-field"><label htmlFor="prepared-provider">Destino</label><select id="prepared-provider" data-provider-state={providerState} disabled={busy || providerState !== 'ready'} value={providerId} onChange={event => { setProviderId(event.target.value); setFeedback(''); }}>{providerState === 'loading' && <option value={providerId}>Cargando destinos…</option>}{destinations.map(destination => <option key={destination.providerId} value={destination.providerId}>{destination.displayName}</option>)}</select></div>
    <div className="dialog-actions">{busy ? <button className="secondary" onClick={cancelExecution}>Detener ejecución</button> : <><button data-nova-control="provider.execute" className="primary" onClick={execute}>Ejecutar con Qwen3</button>{selected && <button className="secondary" onClick={copyAndOpen}>Copiar y abrir {selected.displayName}</button>}</>}</div>
    {feedback && <p role="status">{feedback} {selected?.destinationUrl && <a href={selected.destinationUrl} target="_blank" rel="noopener noreferrer">Abrir {selected.displayName}</a>}</p>}
    {response && <div className="local-response"><p className="eyebrow">{complete ? 'RESPUESTA COMPLETA · SIN GUARDAR' : 'RESPUESTA EN CURSO'}</p><pre data-testid="local-qwen-response" data-complete={complete}>{response}</pre>{complete && <><span data-testid="local-response-complete" className="sr-only">Respuesta verificada</span><button className="secondary" onClick={async () => { try { await navigator.clipboard.writeText(response); setFeedback('Respuesta copiada.'); } catch (error) { setError(errorMessage(error)); } }}>Copiar respuesta</button></>}</div>}
    {error && <p role="alert" className="error">{error}</p>}
  </Dialog>;
}
