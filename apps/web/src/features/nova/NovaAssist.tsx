import { useEffect, useRef, useState } from 'react';
import { z } from 'zod';
import { ArrowUpRight, Sparkles, Square, X } from 'lucide-react';
import { ApiError, errorMessage, post, promptSchema, request, type Prompt } from '../../services/api';
import type { NovaState } from './NovaCore';
import { observeOperation } from '../../services/live';
import { proposalSchema } from '../../services/commands';
import { semanticSchema } from '../../services/semantic';

const actionSchema = z.object({ semantic: semanticSchema.nullable().optional(), contractVersion: z.literal(1), intent: z.enum(['SEARCH', 'OPEN', 'CREATE_DRAFT', 'IMPROVE_DRAFT', 'FILL_VARIABLES', 'COMPILE', 'RUN_LOCAL', 'SELECT_PROVIDER', 'HELP', 'UNKNOWN']), candidates: z.array(promptSchema).max(3), sourcePromptIds: z.array(z.string().uuid()), message: z.string(), requiresConfirmation: z.boolean(), validationStatus: z.literal('validated') });
type Proposal = z.infer<typeof proposalSchema>;
export function NovaAssist({ message, source, onOpen, onDraft, onValues, onStateChange, onLiveRun, sessionId }: {
  message: string; source?: Prompt; onOpen: (prompt: Prompt) => void;
  onDraft: (text: string, source?: Prompt) => void; onValues?: (values: Record<string, string>) => void;
  onStateChange?: (state: NovaState) => void; onLiveRun?: (id: string) => void; sessionId?: string;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [action, setAction] = useState<z.infer<typeof actionSchema> | null>(null);
  const [proposal, setProposal] = useState<Proposal | null>(null);
  const [suggested, setSuggested] = useState<Record<string, string> | null>(null);
  const [model, setModel] = useState('qwen3:8b');
  const controller = useRef<AbortController | null>(null);
  useEffect(() => { controller.current?.abort(); setBusy(false); setAction(null); setProposal(null); setSuggested(null); setError(''); onStateChange?.('idle'); return () => controller.current?.abort(); }, [message, source?.id, source?.revision, onStateChange]);
  async function work(kind: 'interpret' | 'create' | 'improve' | 'variables') {
    controller.current?.abort(); const current = new AbortController(); controller.current = current;
    setBusy(true); setError(''); setAction(null); setProposal(null); setSuggested(null);
    onStateChange?.('thinking');
    const input = { message, model, promptId: source?.id, revision: source?.revision, sessionId };
    try {
      let observedId: string | undefined;
      if (onLiveRun && sessionId) { const observed = await observeOperation(message || 'Mejora el prompt seleccionado.', sessionId, source, current.signal, model); observedId = observed.runId; onLiveRun(observedId); }
      const observedPost = (body: unknown): RequestInit => ({ ...post(body, current.signal), headers: observedId ? { 'X-Nova-Live-Run': observedId } : undefined });
      if (kind === 'interpret') setAction(await request('/nova/interpret', actionSchema, observedPost(input)));
      else if (kind === 'variables') setSuggested(await request('/nova/variables', z.record(z.string(), z.string()), observedPost(input)));
      else setProposal(await request('/nova/propose', proposalSchema, observedPost({ intent: kind, context: message, model, promptId: source?.id, revision: source?.revision })));
      if (!current.signal.aborted) onStateChange?.(kind === 'interpret' ? 'ready' : 'suggesting');
    } catch (error) { if (!current.signal.aborted) { setError(errorMessage(error)); onStateChange?.(error instanceof ApiError && error.status === 503 ? 'offline' : 'error'); } }
    finally { if (!current.signal.aborted) setBusy(false); }
  }
  function cancel() { controller.current?.abort(); setBusy(false); setError('Operación cancelada. No se guardó ningún cambio.'); onStateChange?.('idle'); }
  return <div className="nova-assist">
    <div className="assist-controls">
      <button className="text-button accent" disabled={busy || (!message.trim() && !source)} onClick={() => work(source ? 'improve' : 'interpret')}><Sparkles size={16} aria-hidden="true" />{source ? 'Mejorar con NOVA' : 'Consultar a NOVA'}</button>
      {!source && <button className="text-button" disabled={busy || !message.trim()} onClick={() => work('create')}>Crear con NOVA</button>}
      {source && source.variableNames.length > 0 && <button className="text-button" disabled={busy} onClick={() => work('variables')}>Sugerir valores</button>}
      <label className="model-select"><span className="sr-only">Modelo para NOVA</span><select value={model} disabled={busy} onChange={e => setModel(e.target.value)}><option value="qwen3:8b">Qwen3 · 8B</option><option value="qwen2.5:7b-instruct">Qwen2.5 · 7B</option></select></label>
    </div>
    {busy && <div className="assist-status" role="status"><span>Procesando en tu equipo…</span><button className="text-button" onClick={cancel}><Square size={14} aria-hidden="true" />Cancelar</button></div>}
    {error && <p className="error" role="alert">{error} La búsqueda y la copia siguen disponibles.</p>}
    {action && <div className="nova-suggestion"><div className="section-heading"><p>{action.message}</p><button className="icon-button" onClick={() => setAction(null)} aria-label="Cerrar sugerencia"><X size={17} aria-hidden="true" /></button></div>
      {action.candidates.map(prompt => <button className="suggestion-candidate" key={prompt.id} onClick={() => onOpen(prompt)}>{prompt.title}<ArrowUpRight size={18} aria-hidden="true" /></button>)}
      {action.intent === 'CREATE_DRAFT' && <button className="secondary" onClick={() => work('create')}>Preparar borrador para revisión</button>}
    </div>}
    {proposal && <div className="nova-suggestion" aria-label="Propuesta de NOVA"><p className="eyebrow">PROPUESTA GENERADA · SIN GUARDAR</p><h3>Revisa la nueva instrucción</h3>{source && <details><summary>Ver el original</summary><pre>{source.templateText}</pre></details>}<pre>{proposal.text}</pre>{proposal.reasons.length > 0 && <p className="muted">{proposal.reasons.join(' ')}</p>}{proposal.warnings.map((text, index) => <p key={index} className="notice">{text}</p>)}<div className="dialog-actions"><button className="text-button" onClick={() => setProposal(null)}>Descartar</button><button className="secondary" onClick={() => onDraft(proposal.text, source)}>Revisar en el editor</button></div></div>}
    {suggested && <div className="nova-suggestion"><p className="eyebrow">VALORES PROPUESTOS · REQUIEREN REVISIÓN</p>{Object.entries(suggested).map(([name, value]) => <p key={name}><strong>{name}: </strong>{value}</p>)}<div className="dialog-actions"><button className="text-button" onClick={() => setSuggested(null)}>Descartar</button><button className="secondary" onClick={() => { onValues?.(suggested); setSuggested(null); }}>Llevar al formulario</button></div></div>}
  </div>;
}
