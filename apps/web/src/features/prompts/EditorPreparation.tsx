import { useEffect, useRef, useState } from 'react';
import { z } from 'zod';
import { errorMessage, post, request, type Prompt, type PromptDraft } from '../../services/api';
import { applyAgentProposal } from '../nova/AgentUiDriver';
import { proposalSchema } from '../../services/commands';

export function EditorPreparation({ source, draft, onChange, values, onValues, onUse, onObserve }: { source: Prompt | null; draft: PromptDraft; onChange: (draft: PromptDraft) => void; values: Record<string, string>; onValues: (values: Record<string, string>) => void; onUse: () => void; onObserve?: (message: string, source: Prompt, signal: AbortSignal) => Promise<string> }) {
  const [instruction, setInstruction] = useState(''), [busy, setBusy] = useState(false), [error, setError] = useState('');
  const [names, setNames] = useState<string[]>([]), [compiled, setCompiled] = useState<string | null>(null);
  const [inspected, setInspected] = useState(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => { const abort = () => { controller.current?.abort(); setBusy(false); }; window.addEventListener('nova-abort-ui-operation', abort); return () => { abort(); window.removeEventListener('nova-abort-ui-operation', abort); }; }, []);
  useEffect(() => {
    const current = new AbortController(); setCompiled(null); setInspected(false); setError('');
    const timer = setTimeout(() => { void request('/templates/inspect', z.object({ variableNames: z.array(z.string()) }), post({ templateText: draft.templateText }, current.signal)).then(async result => {
      if (current.signal.aborted) return; setNames(result.variableNames); setInspected(true); const available = Object.fromEntries(result.variableNames.map(name => [name, values[name] ?? source?.defaults[name] ?? '']));
      const resultText = await request('/templates/compile', z.object({ text: z.string().nullable(), missingVariables: z.array(z.string()) }), post({ templateText: draft.templateText, values: available }, current.signal));
      if (!current.signal.aborted) setCompiled(resultText.text);
    }).catch(error => { if (!current.signal.aborted) setError(errorMessage(error)); }); }, 180);
    return () => { clearTimeout(timer); current.abort(); };
  }, [draft.templateText, values, source?.defaults]);
  async function prepare() {
    if (!source || busy || !instruction.trim()) return; const current = new AbortController(); controller.current = current; setBusy(true); setError('');
    try { const runId = await onObserve?.(instruction, source, current.signal);
      const proposal = await request('/nova/propose', proposalSchema, { ...post({ intent: 'improve', context: instruction, promptId: source.id, revision: source.revision, model: 'qwen3:8b' }, current.signal), headers: runId ? { 'X-Nova-Live-Run': runId } : undefined });
      if (proposal.templateId !== source.id || proposal.baseRevision !== source.revision || proposal.text === source.templateText) throw new Error('No se pudo verificar una modificación del origen seleccionado.');
      if (!current.signal.aborted && !await applyAgentProposal(proposal.text, current.signal) && !current.signal.aborted) onChange({ ...draft, templateText: proposal.text });
    } catch (error) { if (!current.signal.aborted) setError(errorMessage(error)); } finally { if (!current.signal.aborted) setBusy(false); }
  }
  return <div className="editor-preparation" data-testid="editor-preparation" data-busy={busy}>
    {inspected && <span data-testid="variables-inspected" className="sr-only">Variables comprobadas</span>}
    {source && <><label htmlFor="editor-change">Cambio que necesitas</label><textarea id="editor-change" rows={2} value={instruction} maxLength={4000} disabled={busy} onChange={event => setInstruction(event.target.value)} /><div className="dialog-actions"><button type="button" className="secondary" disabled={busy || !instruction.trim()} onClick={() => void prepare()}>{busy ? 'Consultando Qwen…' : 'Preparar cambio en el editor'}</button>{busy && <button type="button" className="secondary" onClick={() => { controller.current?.abort(); setBusy(false); }}>Detener cambio</button>}</div></>}
    {source && draft.templateText !== source.templateText && <p className="notice" data-testid="draft-prepared">Borrador modificado · original sin guardar</p>}
    {names.length > 0 && <div className="variable-grid">{names.map((name, index) => <div key={name}><label htmlFor={`editor-variable-${index}`}>{name}</label><textarea id={`editor-variable-${index}`} data-testid="variable-field" data-variable-name={name} rows={2} maxLength={8000} value={values[name] ?? source?.defaults[name] ?? ''} onChange={event => onValues({ ...values, [name]: event.target.value })} /></div>)}</div>}
    <section className="prompt-preview" aria-label="Vista previa del borrador"><h2>Vista previa del resultado</h2><pre data-testid="compiled-preview">{compiled ?? draft.templateText}</pre>{compiled !== null && <span data-testid="compile-ready" className="muted">Listo para utilizar</span>}</section>
    {<button type="button" className="secondary" disabled={busy || compiled === null || !draft.templateText.trim()} onClick={onUse}>Usar en…</button>}
    {error && <p className="error" role="alert">{error}</p>}
  </div>;
}
