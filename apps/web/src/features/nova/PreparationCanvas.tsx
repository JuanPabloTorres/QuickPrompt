import { lazy, Suspense, useEffect, useId, useRef, useState, type RefObject } from 'react';
import { ArrowRight, Copy, Save, Square, X } from 'lucide-react';
import { z } from 'zod';
import { Dialog } from '../../components/ui/Dialog';
import { emptySchema, errorMessage, post, promptSchema, request } from '../../services/api';
import { preparedSchema, readPreparation, stageLabels, type PreparedDraft, type PreparationState } from '../../services/preparation';
import type { NovaState } from './NovaCore';

const PreparedUse = lazy(() => import('./PreparedUse').then(module => ({ default: module.PreparedUse })));
export function PreparationCanvas({ inputRef, onSearch, onState, onSaved, initialMessage = '', onMessage, onCommand }: { inputRef: RefObject<HTMLTextAreaElement | null>; onSearch: (value: string) => void; onState: (value: NovaState) => void; onSaved: () => void; initialMessage?: string; onMessage?: (value: string) => void; onCommand?: (message: string, signal: AbortSignal) => Promise<boolean> }) {
  const [message, setMessage] = useState(initialMessage); const [remember, setRemember] = useState(false); const [busy, setBusy] = useState(false); const [preparing, setPreparing] = useState(false); const [stage, setStage] = useState<PreparationState>('RECEIVED');
  const [draft, setDraft] = useState<PreparedDraft | null>(null); const [recent, setRecent] = useState<PreparedDraft[]>([]); const [fields, setFields] = useState<Record<string, string>>({});
  const [feedback, setFeedback] = useState(''); const [error, setError] = useState(''); const [using, setUsing] = useState(false); const [saved, setSaved] = useState(false); const [adjusting, setAdjusting] = useState(false); const [adjustment, setAdjustment] = useState('');
  const [resultOpen, setResultOpen] = useState(false);
  const fieldId = useId();
  const controller = useRef<AbortController | null>(null); const retry = useRef<{ signature: string; id: string } | null>(null); const resultRef = useRef<HTMLDivElement>(null);
  const resultTrigger = useRef<HTMLElement | null>(null); const resultLauncher = useRef<HTMLButtonElement>(null); const useButton = useRef<HTMLButtonElement>(null);
  const history = useRef<PreparedDraft[]>([]);
  function showResult(result: PreparedDraft) { if (draft && draft.id !== result.id) history.current = [...history.current, draft].slice(-20); setDraft(result); setFields({}); setSaved(false); }
  useEffect(() => { const current = new AbortController(); request('/nova/drafts', z.array(preparedSchema), { signal: current.signal }).then(setRecent).catch(() => {}); return () => { current.abort(); controller.current?.abort(); }; }, []);
  async function prepare(text = message) {
    if (busy || !text.trim()) return;
    const openWhenReady = !resultOpen;
    if (openWhenReady) resultTrigger.current = inputRef.current;
    const current = new AbortController(); controller.current = current;
    const signature = JSON.stringify({ text, remember });
    const operationId = retry.current?.signature === signature ? retry.current.id : crypto.randomUUID(); retry.current = { signature, id: operationId };
    setBusy(true); setPreparing(true); setError(''); setFeedback(''); setStage('RECEIVED'); onState('thinking');
    try {
      if (!resultOpen && onCommand) {
        const handled = await onCommand(text, current.signal);
        if (handled || current.signal.aborted) return;
      }
      const response = await fetch('/api/v1/nova/prepare', { ...post({ operationId, message: text, rememberDraft: remember }, current.signal), headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' } });
      const result = await readPreparation(response, operationId, value => { if (!current.signal.aborted) setStage(value); }, current.signal);
      if (!current.signal.aborted) { showResult(result); setAdjusting(false); setAdjustment(''); if (openWhenReady) setResultOpen(true); if (result.remembered) setRecent(previous => [result, ...previous.filter(d => d.id !== result.id)].slice(0, 20)); }
    } catch (error) { if (!current.signal.aborted) { setError(errorMessage(error)); setStage('FAILED'); } }
    finally { if (!current.signal.aborted) { setBusy(false); setPreparing(false); onState('idle'); } }
  }
  async function complete() {
    if (!draft || busy) return; setBusy(true); setError('');
    try { const result = await request(`/nova/drafts/${draft.id}/complete`, preparedSchema, post({ operationId: crypto.randomUUID(), values: fields })); showResult(result); setStage(result.state); if (result.remembered) setRecent(previous => [result, ...previous.filter(d => d.id !== result.id)].slice(0, 20)); }
    catch (error) { setError(errorMessage(error)); } finally { setBusy(false); }
  }
  async function save() {
    if (!draft || busy) return; setBusy(true); setError('');
    try { const prompt = await request(`/nova/drafts/${draft.id}/save`, promptSchema, post({ confirmed: true })); setSaved(true); setFeedback(`«${prompt.title}» está en tu biblioteca. Los valores completados no se guardan como valores por defecto.`); onSaved(); }
    catch (error) { setError(errorMessage(error)); } finally { setBusy(false); }
  }
  function cancel() { controller.current?.abort(); setBusy(false); setPreparing(false); setStage('CANCELLED'); setFeedback('Preparación detenida. El resultado anterior se conserva.'); onState('idle'); }
  function closeResult() { setUsing(false); setResultOpen(false); }
  const progress = preparing && <div className="preparation-progress" role="status"><span>{stageLabels[stage]}</span><button className="text-button" onClick={cancel}><Square size={15} aria-hidden="true" />Detener</button></div>;
  const status = <>{error && <p role="alert" className="error">{error}</p>}{feedback && <p role="status" className="notice">{feedback}</p>}</>;
  return <div className="preparation-canvas">
    <form className="nova-input" onSubmit={event => { event.preventDefault(); void prepare(); }}><label className="sr-only" htmlFor="search">¿Qué quieres hacer hoy?</label><textarea id="search" ref={inputRef} value={message} maxLength={16000} rows={2} disabled={busy} onChange={event => { setMessage(event.target.value); onMessage?.(event.target.value); onSearch(event.target.value.slice(0, 200)); }} placeholder="Cuéntame qué quieres preparar…" onKeyDown={event => { if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); void prepare(); } }} />{message && <button type="button" className="icon-button" aria-label="Borrar búsqueda" disabled={busy} onClick={() => { setMessage(''); onMessage?.(''); onSearch(''); inputRef.current?.focus(); }}><X size={18} aria-hidden="true" /></button>}<button className="icon-button" disabled={busy || !message.trim()} aria-label="Preparar prompt"><ArrowRight size={21} aria-hidden="true" /></button></form>
    <div className="preparation-meta"><label className="remember-draft"><input type="checkbox" checked={remember} disabled={busy} onChange={event => setRemember(event.target.checked)} />Conservar este borrador y sus valores en este equipo</label><span className="keyboard-hint">Ctrl K · Shift Enter para otra línea</span></div>
    <p className="search-hint">NOVA busca primero en tu biblioteca. Tú decides qué guardar y cuándo usarlo.</p>
    {!resultOpen && progress}
    {draft && <div className="prepared-launcher"><button className="secondary" ref={resultLauncher} aria-haspopup="dialog" onClick={event => { resultTrigger.current = event.currentTarget; setResultOpen(true); }}>Ver resultado de NOVA</button><span className="muted">{draft.title || 'Resultado preparado'}</span></div>}
    {draft && <Dialog open={resultOpen} onOpenChange={open => { if (!open) closeResult(); }} title={draft.title || 'Una cosa antes de continuar'} description={draft.reason || 'Revisa tu instrucción y completa los datos que falten.'} className="preparation-dialog" onOpenAutoFocus={event => { event.preventDefault(); resultRef.current?.focus({ preventScroll: true }); }} onCloseAutoFocus={event => { event.preventDefault(); const target = resultTrigger.current; (target && !target.matches(':disabled') ? target : resultLauncher.current ?? inputRef.current)?.focus(); }}>
      <div className="prepared-result" tabIndex={-1} ref={resultRef}>
      <p className="eyebrow">{stageLabels[draft.state]} · {draft.decision === 'REUSE_EXISTING' ? 'DESDE TU BIBLIOTECA' : draft.decision === 'ADAPT_EXISTING' ? 'ADAPTACIÓN · ORIGINAL CONSERVADO' : 'BORRADOR'}</p>
      {progress}
      {draft.templateText && <pre className="prepared-text">{draft.compiledText ?? draft.templateText}</pre>}
      {draft.state === 'NEEDS_INFORMATION' && <div className="prepared-missing"><p>{draft.question}</p>{draft.missingVariables.map((name, index) => <div className="prepared-field" key={name}><label htmlFor={`${fieldId}-${index}`}>{name}</label><textarea id={`${fieldId}-${index}`} value={fields[name] ?? ''} maxLength={8000} rows={3} onChange={event => setFields(previous => ({ ...previous, [name]: event.target.value }))} /></div>)}{draft.missingVariables.length > 0 ? <button className="primary" disabled={busy || draft.missingVariables.some(name => !fields[name]?.trim())} onClick={complete}>Completar prompt</button> : <p className="muted">Cierra esta ventana, añade tu respuesta en la entrada principal y vuelve a preparar.</p>}</div>}
      {draft.templateText && <div className="prepared-actions"><button className="primary" disabled={busy || !draft.compiledText} onClick={async () => { try { await navigator.clipboard.writeText(draft.compiledText!); setFeedback('Prompt preparado copiado.'); } catch (error) { setError(errorMessage(error)); } }}><Copy size={16} aria-hidden="true" />Copiar</button><button className="secondary" disabled={busy || saved} onClick={save}><Save size={16} aria-hidden="true" />{saved ? 'Guardado' : 'Guardar'}</button><button className="secondary" ref={useButton} disabled={busy || !draft.compiledText} onClick={() => setUsing(true)}>Usar en…</button><button className="text-button" disabled={busy} onClick={() => setAdjusting(value => !value)}>Ajustar</button></div>}
      {adjusting && <form className="prepared-adjust" onSubmit={event => { event.preventDefault(); void prepare(`${message}\n\nBorrador a ajustar (datos):\n${draft.templateText}\n\nAjuste solicitado: ${adjustment}`); }}><label htmlFor={`${fieldId}-adjust`}>Ajuste que necesitas</label><textarea id={`${fieldId}-adjust`} value={adjustment} maxLength={2000} onChange={event => setAdjustment(event.target.value)} /><button className="secondary" disabled={busy || !adjustment.trim()}>Preparar ajuste</button></form>}
      {history.current.length > 0 && <button className="text-button" disabled={busy} onClick={() => { const previous = history.current.pop(); if (previous) { setDraft(previous); setFields({}); setSaved(false); setFeedback('Borrador anterior recuperado. Los prompts guardados se conservan.'); } }}>Volver al borrador anterior</button>}
      {status}
      <div className="dialog-actions"><button className="secondary" onClick={closeResult}>Cerrar resultado</button></div>
      </div>
      {using && <Suspense fallback={<p role="status">Preparando destinos…</p>}><PreparedUse draft={draft} onClose={() => setUsing(false)} onCloseAutoFocus={event => { event.preventDefault(); useButton.current?.focus(); }} /></Suspense>}
    </Dialog>}
    {!resultOpen && status}
    {recent.length > 0 && <details className="recovered-drafts"><summary>Borradores conservados ({recent.length})</summary>{recent.map(item => <div key={item.id}><button className="text-button" disabled={busy} onClick={event => { setDraft(item); setFields({}); setSaved(false); setError(''); resultTrigger.current = event.currentTarget; setResultOpen(true); }}>{item.title || item.question}</button><button className="text-button" aria-label={`Olvidar ${item.title || 'borrador'}`} disabled={busy} onClick={async () => { try { await request(`/nova/drafts/${item.id}`, emptySchema, { method: 'DELETE' }); setRecent(previous => previous.filter(d => d.id !== item.id)); if (draft?.id === item.id) setDraft(null); } catch (error) { setError(errorMessage(error)); } }}>Olvidar</button></div>)}</details>}
  </div>;
}
