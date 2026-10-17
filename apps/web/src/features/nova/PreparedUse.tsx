import { useEffect, useRef, useState } from 'react';
import { z } from 'zod';
import { Dialog } from '../../components/ui/Dialog';
import { errorMessage, post, request } from '../../services/api';
import { readLocalStream } from '../../services/localStream';
import type { PreparedDraft } from '../../services/preparation';
import { liveRecordSchema, surfaceCapability } from '../../services/live';

export function PreparedUse({ draft, onClose, onCloseAutoFocus }: { draft: PreparedDraft; onClose: () => void; onCloseAutoFocus?: (event: Event) => void }) {
  const [busy, setBusy] = useState(false); const [response, setResponse] = useState(''); const [complete, setComplete] = useState(false); const [error, setError] = useState(''); const [feedback, setFeedback] = useState('');
  const destinations = { chatgpt: { name: 'ChatGPT', url: 'https://chatgpt.com/' }, claude: { name: 'Claude', url: 'https://claude.ai/' }, gemini: { name: 'Gemini', url: 'https://gemini.google.com/' }, copilot: { name: 'Copilot', url: 'https://copilot.microsoft.com/' }, perplexity: { name: 'Perplexity', url: 'https://www.perplexity.ai/' } };
  type DestinationId = keyof typeof destinations;
  const [providerId, setProviderId] = useState<DestinationId>(() => draft.providerId && draft.providerId in destinations ? draft.providerId as DestinationId : 'chatgpt');
  const selected = destinations[providerId];
  const controller = useRef<AbortController | null>(null);
  const liveId = (() => { try { const id = sessionStorage.getItem('nova-live-run'); return id && surfaceCapability(id) ? id : new URLSearchParams(location.search).get('novaRun'); } catch { return null; } })();
  useEffect(() => { const abort = () => { controller.current?.abort(); setBusy(false); }; window.addEventListener('nova-abort-ui-operation', abort); return () => { controller.current?.abort(); window.removeEventListener('nova-abort-ui-operation', abort); }; }, []);
  function cancelExecution() {
    const runId = liveId;
    if (runId && /^[0-9a-f-]{36}$/i.test(runId)) void request(`/nova/live/${runId}/cancel`, liveRecordSchema, post({})).catch(() => {});
    controller.current?.abort(); setBusy(false); setComplete(false); setResponse('');
  }
  async function execute() {
    if (busy) return;
    const current = new AbortController(); controller.current = current; setBusy(true); setError(''); setComplete(false); setResponse('');
    try {
      const result = await fetch(`/api/v1/nova/drafts/${draft.id}/execute`, { ...post({ operationId: crypto.randomUUID(), confirmed: true, expectedText: draft.compiledText, model: 'qwen3:8b' }, current.signal), headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1', ...(liveId ? { 'X-Nova-Live-Run': liveId } : {}) } });
      const text = await readLocalStream(result, 1, setResponse, current.signal);
      if (!current.signal.aborted) { setResponse(text); setComplete(true); }
    } catch (error) { if (!current.signal.aborted) setError(errorMessage(error)); }
    finally { if (!current.signal.aborted) setBusy(false); }
  }
  async function copyAndOpen() {
    if (busy || !draft.compiledText) return;
    const destination = window.open('about:blank', '_blank'); if (destination) destination.opener = null;
    const current = new AbortController(); controller.current = current;
    setBusy(true); setError('');
    try {
      const providers = await request('/providers', z.array(z.object({ providerId: z.string(), destinationUrl: z.string().nullable(), modesSupported: z.array(z.string()) })), { signal: current.signal });
      const provider = providers.find(p => p.providerId === providerId && p.modesSupported.includes('COPY_AND_OPEN'));
      if (provider?.destinationUrl !== selected.url) throw new Error('El destino no está habilitado. Puedes copiar el texto.');
      await navigator.clipboard.writeText(draft.compiledText);
      if (current.signal.aborted) { destination?.close(); return; }
      if (destination) destination.location.replace(provider.destinationUrl);
      setFeedback('Texto copiado. Pégalo y envíalo cuando decidas.');
    } catch (error) { destination?.close(); if (!current.signal.aborted) setError(errorMessage(error)); } finally { if (!current.signal.aborted) setBusy(false); }
  }
  return <Dialog open onOpenChange={open => { if (!open) { if (busy) cancelExecution(); onClose(); } }} onCloseAutoFocus={onCloseAutoFocus} title="Usa tu prompt preparado" description="Revisa el texto antes de ejecutar en tu equipo o copiar y abrir el destino elegido. Tú decides cuándo pegar y enviar.">
    <div data-testid="prepared-provider-dialog"><pre className="prepared-text">{draft.compiledText}</pre></div>
    <div className="prepared-field"><label htmlFor="prepared-provider">Destino externo</label><select id="prepared-provider" disabled={busy} value={providerId} onChange={event => { setProviderId(event.target.value as DestinationId); setFeedback(''); }}>{Object.entries(destinations).map(([id, destination]) => <option key={id} value={id}>{destination.name}</option>)}</select></div>
    <div className="dialog-actions">{busy ? <button className="secondary" onClick={cancelExecution}>Detener ejecución</button> : <><button className="primary" onClick={execute}>Ejecutar con Qwen3</button><button className="secondary" onClick={copyAndOpen}>Copiar y abrir {selected.name}</button></>}</div>
    {feedback && <p role="status">{feedback} <a href={selected.url} target="_blank" rel="noopener noreferrer">Abrir {selected.name}</a></p>}
    {response && <div className="local-response"><p className="eyebrow">{complete ? 'RESPUESTA COMPLETA · SIN GUARDAR' : 'RESPUESTA EN CURSO'}</p><pre data-testid="local-qwen-response" data-complete={complete}>{response}</pre>{complete && <><span data-testid="local-response-complete" className="sr-only">Respuesta verificada</span><button className="secondary" onClick={async () => { try { await navigator.clipboard.writeText(response); setFeedback('Respuesta copiada.'); } catch (error) { setError(errorMessage(error)); } }}>Copiar respuesta</button></>}</div>}
    {error && <p role="alert" className="error">{error}</p>}
  </Dialog>;
}
