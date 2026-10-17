import { lazy, Suspense, useEffect, useId, useRef, useState, type RefObject } from 'react';
import { ArrowRight, Copy, Save, Square, X } from 'lucide-react';
import { z } from 'zod';
import { Dialog } from '../../components/ui/Dialog';
import { emptySchema, errorMessage, post, promptSchema, request, type Prompt } from '../../services/api';
import { preparedSchema, PreparationFailure, readPreparation, stageLabels, type PreparedDraft, type PreparationState } from '../../services/preparation';
import type { NovaState } from './NovaCore';
import { startVisible } from '../../services/live';

const PreparedUse = lazy(() => import('./PreparedUse').then(module => ({ default: module.PreparedUse })));
export function PreparationCanvas({ inputRef, onState, onSaved, initialMessage = '', onMessage, sessionId, contextPrompt, contextValues, retrievalFilter, onLiveRun, onLiveCommand, onPrepared, onContext, contextKey }: { inputRef: RefObject<HTMLTextAreaElement | null>; onState: (value: NovaState) => void; onSaved: (prompt: Prompt) => void; initialMessage?: string; onMessage?: (value: string) => void; sessionId: string; contextPrompt?: Prompt | null; contextValues?: Record<string, string>; retrievalFilter?: { groupId?: string; favorites?: boolean }; onLiveRun?: (id: string) => void; onLiveCommand?: (message: string) => Promise<boolean>; onPrepared?: (draft: PreparedDraft) => Promise<boolean>; onContext?: () => Promise<void>; contextKey?: string }) {
  const [executionMode, setExecutionMode] = useState('SAME_TAB');
  const [message, setMessage] = useState(initialMessage); const [remember, setRemember] = useState(false); const [busy, setBusy] = useState(false); const [preparing, setPreparing] = useState(false); const [stage, setStage] = useState<PreparationState>('RECEIVED');
  const [draft, setDraft] = useState<PreparedDraft | null>(null); const [recent, setRecent] = useState<PreparedDraft[]>([]); const [fields, setFields] = useState<Record<string, string>>({});
  const [feedback, setFeedback] = useState(''); const [error, setError] = useState(''); const [using, setUsing] = useState(false); const [saved, setSaved] = useState(false); const [adjusting, setAdjusting] = useState(false); const [adjustment, setAdjustment] = useState('');
  const [resultOpen, setResultOpen] = useState(false);
  const [saveOpen, setSaveOpen] = useState(false); const [responseText, setResponseText] = useState('');
  const fieldId = useId();
  const controller = useRef<AbortController | null>(null); const retry = useRef<{ signature: string; id: string } | null>(null); const resultRef = useRef<HTMLDivElement>(null);
  const resultTrigger = useRef<HTMLElement | null>(null); const resultLauncher = useRef<HTMLButtonElement>(null); const useButton = useRef<HTMLButtonElement>(null);
  const history = useRef<PreparedDraft[]>([]);
  const preparedText = useRef<HTMLPreElement>(null);
  useEffect(() => {
    if (onLiveRun && resultOpen && draft && resultRef.current?.isConnected) void request(`/nova/live/${draft.id}/ui-observed`, emptySchema, post({ preparedId: draft.id, sourceId: draft.sourceId, sourceRevision: draft.sourceRevision, renderedText: preparedText.current?.textContent ?? '' })).catch(() => {});
  }, [draft, resultOpen, onLiveRun]);
  function showResult(result: PreparedDraft) { if (draft && draft.id !== result.id) history.current = [...history.current, draft].slice(-20); setDraft(result); setFields({}); setSaved(false); }
  async function restoreDraft(result: PreparedDraft) {
    setBusy(true); setError('');
    try { await request(`/nova/sessions/${sessionId}/draft-context`, emptySchema, post({ draftId: result.id })); setDraft(result); setFields({}); setSaved(false); setResultOpen(true); setFeedback('Borrador recuperado como contexto de NOVA. Los prompts guardados se conservan.'); return true; }
    catch (error) { setError(errorMessage(error)); return false; } finally { setBusy(false); }
  }
  useEffect(() => { const current = new AbortController(); request('/nova/drafts', z.array(preparedSchema), { signal: current.signal }).then(setRecent).catch(() => {}); return () => { current.abort(); controller.current?.abort(); }; }, []);
  async function prepare(text = message, source?: Prompt) {
    if (busy || !text.trim()) return;
    if (onLiveCommand) { try { if (await onLiveCommand(text)) return; } catch (error) { setError(errorMessage(error)); return; } }
    if (executionMode === 'SAME_TAB') {
      setBusy(true); setError('');
      try { await onContext?.(); const run = await startVisible(text, sessionId, contextPrompt ?? undefined, contextValues); onLiveRun?.(run.runId); }
      catch (error) { setError(errorMessage(error)); } finally { setBusy(false); }
      return;
    }
    const openWhenReady = !resultOpen || source !== undefined;
    if (openWhenReady) resultTrigger.current = inputRef.current;
    const current = new AbortController(); controller.current = current;
    const signature = JSON.stringify({ text, remember, source: source?.id, revision: source?.revision, sessionId, retrievalFilter, contextKey, contextPrompt: contextPrompt ? { id: contextPrompt.id, revision: contextPrompt.revision } : null, contextValues });
    const operationId = retry.current?.signature === signature ? retry.current.id : crypto.randomUUID(); retry.current = { signature, id: operationId };
    setBusy(true); setPreparing(true); setError(''); setFeedback(''); setResponseText(''); setStage('RECEIVED'); onState('thinking');
    try {
      await onContext?.();
      if (contextPrompt && !contextPrompt.isArchived) await request(`/nova/sessions/${sessionId}/context`, emptySchema, post({ promptId: contextPrompt.id, revision: contextPrompt.revision, values: contextValues, allowStaleContext: true }, current.signal));
      const response = await fetch('/api/v1/nova/prepare', { ...post({ operationId, sessionId, message: text, rememberDraft: remember, sourcePromptId: source?.id, sourceRevision: source?.revision, filter: retrievalFilter }, current.signal), headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' } });
      if (response.ok) onLiveRun?.(operationId);
      const result = await readPreparation(response, operationId, value => { if (!current.signal.aborted) setStage(value); }, current.signal, text => { if (!current.signal.aborted) setResponseText(text); });
      if (!current.signal.aborted) { retry.current = null; showResult(result); setAdjusting(false); setAdjustment(''); onState('idle'); if (openWhenReady) { setResultOpen(false); if (!(await onPrepared?.(result))) setResultOpen(true); } if (result.remembered) setRecent(previous => [result, ...previous.filter(d => d.id !== result.id)].slice(0, 20)); }
    } catch (error) { if (!current.signal.aborted) { if (error instanceof PreparationFailure) retry.current = null; setError(errorMessage(error)); setStage('FAILED'); } }
    finally { if (!current.signal.aborted) { setBusy(false); setPreparing(false); onState('idle'); } }
  }
  async function complete() {
    if (!draft || busy) return; setBusy(true); setError('');
    try { const result = await request(`/nova/drafts/${draft.id}/complete`, preparedSchema, post({ operationId: crypto.randomUUID(), values: fields })); showResult(result); onLiveRun?.(result.id); setStage(result.state); if (result.remembered) setRecent(previous => [result, ...previous.filter(d => d.id !== result.id)].slice(0, 20)); }
    catch (error) { setError(errorMessage(error)); } finally { setBusy(false); }
  }
  async function save(updateSource = false) {
    if (!draft || busy) return; setBusy(true); setError('');
    try { const prompt = await request(`/nova/drafts/${draft.id}/save`, promptSchema, post({ confirmed: true, updateSource })); setSaveOpen(false); setSaved(true); setFeedback(`«${prompt.title}» está en tu biblioteca. Los valores completados no se guardan como valores por defecto.`); onSaved(prompt); }
    catch (error) { setError(errorMessage(error)); } finally { setBusy(false); }
  }
  function cancel() { controller.current?.abort(); retry.current = null; setBusy(false); setPreparing(false); setResponseText(''); setStage('CANCELLED'); setFeedback('Preparación detenida. El resultado anterior se conserva.'); onState('idle'); }
  function closeResult() { setUsing(false); setResultOpen(false); }
  const progress = preparing && <div className="preparation-progress" role="status"><span>{stageLabels[stage]}</span><button className="text-button" onClick={cancel}><Square size={15} aria-hidden="true" />Detener</button></div>;
  const status = <>{error && <p role="alert" className="error">{error}</p>}{feedback && <p role="status" className="notice">{feedback}</p>}</>;
  return <div className="preparation-canvas">
    <form className="nova-input" data-nova-command="true" onSubmit={event => { event.preventDefault(); void prepare(); }}><label className="sr-only" htmlFor="search">¿Qué quieres hacer hoy?</label><textarea id="search" ref={inputRef} value={message} maxLength={16000} rows={2} disabled={busy} onChange={event => { setMessage(event.target.value); onMessage?.(event.target.value); }} placeholder="Cuéntame qué quieres preparar…" onKeyDown={event => { if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); void prepare(); } }} />{message && <button type="button" className="icon-button" aria-label="Borrar solicitud" disabled={busy} onClick={() => { setMessage(''); onMessage?.(''); inputRef.current?.focus(); }}><X size={18} aria-hidden="true" /></button>}<button className="icon-button" disabled={busy || !message.trim()} aria-label="Preparar prompt"><ArrowRight size={21} aria-hidden="true" /></button></form>
<details className="preparation-options"><summary>Opciones de preparación</summary><label className="execution-mode">Presentación del resultado<select aria-label="Modo de NOVA" disabled={busy} value={executionMode} onChange={event => setExecutionMode(event.target.value)}><option value="SAME_TAB">Ver cómo se prepara</option><option value="LIVE">Solo mostrar el resultado</option></select></label></details>
    <div className="preparation-meta">{executionMode === 'LIVE' && <label className="remember-draft"><input type="checkbox" checked={remember} disabled={busy} onChange={event => setRemember(event.target.checked)} />Conservar borrador, valores y respuesta local en este equipo</label>}<span className="keyboard-hint">Ctrl K · Shift Enter para otra línea</span></div>
    <p className="search-hint">NOVA busca primero en tu biblioteca. Tú decides qué guardar y cuándo usarlo.</p>
    {!resultOpen && progress}
    {preparing && responseText && <pre className="prepared-text" aria-label="Respuesta local en curso">{responseText}</pre>}
    {draft && <div className="prepared-launcher"><button className="secondary" ref={resultLauncher} aria-haspopup="dialog" onClick={event => { resultTrigger.current = event.currentTarget; setResultOpen(true); }}>Ver resultado de NOVA</button><span className="muted">{draft.title || 'Resultado preparado'}</span></div>}
    {draft && <Dialog open={resultOpen} onOpenChange={open => { if (!open) closeResult(); }} title={draft.title || 'Una cosa antes de continuar'} description={draft.reason || 'Revisa tu instrucción y completa los datos que falten.'} className="preparation-dialog" onOpenAutoFocus={event => { event.preventDefault(); resultRef.current?.focus({ preventScroll: true }); }} onCloseAutoFocus={event => { event.preventDefault(); const target = resultTrigger.current; (target && !target.matches(':disabled') ? target : resultLauncher.current ?? inputRef.current)?.focus(); }}>
      <div className="prepared-result" tabIndex={-1} ref={resultRef}>
      <p className="eyebrow">{stageLabels[draft.state]} · {draft.decision === 'REUSE_EXISTING' ? 'DESDE TU BIBLIOTECA' : draft.decision === 'ADAPT_EXISTING' ? 'ADAPTACIÓN · ORIGINAL CONSERVADO' : 'BORRADOR'}</p>
      {progress}
      {draft.templateText && <pre className="prepared-text" ref={preparedText}>{draft.compiledText ?? draft.templateText}</pre>}
      {(draft.state === 'NEEDS_INFORMATION' || draft.missingVariables.length > 0) && <div className="prepared-missing"><p>{draft.question || 'Para utilizar este prompt, completa sus datos.'}</p>{draft.missingVariables.map((name, index) => <div className="prepared-field" key={name}><label htmlFor={`${fieldId}-${index}`}>{name}</label><textarea id={`${fieldId}-${index}`} value={fields[name] ?? ''} maxLength={8000} rows={3} onChange={event => setFields(previous => ({ ...previous, [name]: event.target.value }))} /></div>)}{draft.missingVariables.length > 0 ? <button className="primary" disabled={busy || draft.missingVariables.some(name => !fields[name]?.trim())} onClick={complete}>Completar prompt</button> : <>{draft.candidates.filter(candidate => candidate.score >= 2).map(candidate => <button className="secondary candidate-choice" disabled={busy} key={candidate.prompt.id} onClick={() => void prepare(message, candidate.prompt)}><strong>{candidate.prompt.title}</strong><span className="muted">{candidate.prompt.description || candidate.prompt.templateText.slice(0, 160)}</span></button>)}<p className="muted">También puedes cerrar esta ventana y precisar tu solicitud.</p></>}</div>}
      {draft.templateText && <div className="prepared-actions"><button className="primary" disabled={busy || !draft.compiledText} onClick={async () => { try { await navigator.clipboard.writeText(draft.compiledText!); setFeedback('Prompt preparado copiado.'); } catch (error) { setError(errorMessage(error)); } }}><Copy size={16} aria-hidden="true" />Copiar</button><button className="secondary" disabled={busy || saved} onClick={() => draft.decision === 'ADAPT_EXISTING' ? setSaveOpen(true) : void save()}><Save size={16} aria-hidden="true" />{saved ? 'Guardado' : 'Guardar'}</button><button className="secondary" ref={useButton} disabled={busy || !draft.compiledText} onClick={() => setUsing(true)}>Usar en…</button><button className="text-button" disabled={busy} onClick={() => setAdjusting(value => !value)}>Ajustar</button></div>}
      {draft.localResponse && <div className="local-response"><p className="eyebrow">RESPUESTA DE QWEN · SIN GUARDAR</p><pre>{draft.localResponse}</pre></div>}
      {adjusting && <form className="prepared-adjust" onSubmit={event => { event.preventDefault(); void prepare(adjustment); }}><label htmlFor={`${fieldId}-adjust`}>Ajuste que necesitas</label><textarea id={`${fieldId}-adjust`} value={adjustment} maxLength={2000} onChange={event => setAdjustment(event.target.value)} /><button className="secondary" disabled={busy || !adjustment.trim()}>Preparar ajuste</button></form>}
      {history.current.length > 0 && <button className="text-button" disabled={busy} onClick={() => { const previous = history.current.at(-1); if (previous) void restoreDraft(previous).then(restored => { if (restored) history.current.pop(); }); }}>Volver al borrador anterior</button>}
      {status}
      <div className="dialog-actions"><button className="secondary" onClick={closeResult}>Cerrar resultado</button></div>
      </div>
      {using && <Suspense fallback={<p role="status">Preparando destinos…</p>}><PreparedUse draft={draft} onClose={() => setUsing(false)} onCloseAutoFocus={event => { event.preventDefault(); useButton.current?.focus(); }} /></Suspense>}
      <Dialog open={saveOpen} onOpenChange={setSaveOpen} title="Guardar adaptación" description="Elige cómo conservar la propuesta que acabas de revisar."><div className="dialog-actions"><button className="secondary" disabled={busy} onClick={() => void save()}>Guardar como copia</button><button className="primary" disabled={busy} onClick={() => void save(true)}>Confirmar nueva versión</button><button className="text-button" disabled={busy} onClick={() => setSaveOpen(false)}>Cancelar</button></div>{error && <p className="error" role="alert">{error}</p>}</Dialog>
    </Dialog>}
    {!resultOpen && status}
    {recent.length > 0 && <details className="recovered-drafts"><summary>Borradores conservados ({recent.length})</summary>{recent.map(item => <div key={item.id}><button className="text-button" disabled={busy} onClick={event => { resultTrigger.current = event.currentTarget; void restoreDraft(item); }}>{item.title || item.question}</button><button className="text-button" aria-label={`Olvidar ${item.title || 'borrador'}`} disabled={busy} onClick={async () => { try { await request(`/nova/drafts/${item.id}`, emptySchema, { method: 'DELETE' }); setRecent(previous => previous.filter(d => d.id !== item.id)); if (draft?.id === item.id) setDraft(null); } catch (error) { setError(errorMessage(error)); } }}>Olvidar</button></div>)}</details>}
  </div>;
}
