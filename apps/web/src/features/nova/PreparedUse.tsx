import { useEffect, useRef, useState } from 'react';
import { z } from 'zod';
import { Dialog } from '../../components/ui/Dialog';
import { errorMessage, post, request } from '../../services/api';
import { readLocalStream } from '../../services/localStream';
import type { PreparedDraft } from '../../services/preparation';

export function PreparedUse({ draft, onClose, onCloseAutoFocus }: { draft: PreparedDraft; onClose: () => void; onCloseAutoFocus?: (event: Event) => void }) {
  const [busy, setBusy] = useState(false); const [response, setResponse] = useState(''); const [complete, setComplete] = useState(false); const [error, setError] = useState(''); const [feedback, setFeedback] = useState('');
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), []);
  async function execute() {
    if (busy) return;
    const current = new AbortController(); controller.current = current; setBusy(true); setError(''); setComplete(false); setResponse('');
    try {
      const result = await fetch(`/api/v1/nova/drafts/${draft.id}/execute`, { ...post({ confirmed: true, expectedText: draft.compiledText, model: 'qwen3:8b' }, current.signal), headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' } });
      const text = await readLocalStream(result, 1, setResponse, current.signal);
      if (!current.signal.aborted) { setResponse(text); setComplete(true); }
    } catch (error) { if (!current.signal.aborted) setError(errorMessage(error)); }
    finally { if (!current.signal.aborted) setBusy(false); }
  }
  async function chatgpt() {
    if (busy || !draft.compiledText) return;
    const destination = window.open('about:blank', '_blank'); if (destination) destination.opener = null;
    const current = new AbortController(); controller.current = current;
    setBusy(true); setError('');
    try {
      const providers = await request('/providers', z.array(z.object({ providerId: z.string(), destinationUrl: z.string().nullable(), modesSupported: z.array(z.string()) })), { signal: current.signal });
      const provider = providers.find(p => p.providerId === 'chatgpt' && p.modesSupported.includes('COPY_AND_OPEN'));
      if (provider?.destinationUrl !== 'https://chatgpt.com/') throw new Error('El destino no está habilitado. Puedes copiar el texto.');
      await navigator.clipboard.writeText(draft.compiledText);
      if (current.signal.aborted) { destination?.close(); return; }
      if (destination) destination.location.replace(provider.destinationUrl);
      setFeedback('Texto copiado. Pégalo y envíalo cuando decidas.');
    } catch (error) { destination?.close(); if (!current.signal.aborted) setError(errorMessage(error)); } finally { if (!current.signal.aborted) setBusy(false); }
  }
  return <Dialog open onOpenChange={open => !open && onClose()} onCloseAutoFocus={onCloseAutoFocus} title="Usa tu prompt preparado" description="Revisa el texto antes de ejecutar en tu equipo o copiar y abrir ChatGPT.">
    <pre className="prepared-text">{draft.compiledText}</pre>
    <div className="dialog-actions">{busy ? <button className="secondary" onClick={() => { controller.current?.abort(); setBusy(false); setComplete(false); setResponse(''); }}>Detener ejecución</button> : <><button className="primary" onClick={execute}>Ejecutar con Qwen3</button><button className="secondary" onClick={chatgpt}>Copiar y abrir ChatGPT</button></>}</div>
    {feedback && <p role="status">{feedback} <a href="https://chatgpt.com/" target="_blank" rel="noopener noreferrer">Abrir ChatGPT</a></p>}
    {response && <div className="local-response"><p className="eyebrow">{complete ? 'RESPUESTA COMPLETA · SIN GUARDAR' : 'RESPUESTA EN CURSO'}</p><pre>{response}</pre>{complete && <button className="secondary" onClick={async () => { try { await navigator.clipboard.writeText(response); setFeedback('Respuesta copiada.'); } catch (error) { setError(errorMessage(error)); } }}>Copiar respuesta</button>}</div>}
    {error && <p role="alert" className="error">{error}</p>}
  </Dialog>;
}
