import { useEffect, useRef, useState, type ReactNode } from 'react';
import { z } from 'zod';
import { ArrowLeft, Braces, Check } from 'lucide-react';
import { errorMessage, inspectSchema, post, request, type PromptDraft } from '../../services/api';

export function InlinePromptEditor({ draft, onChange, onSave, onBack, saving, error, editing, promptId, preparation }: {
  draft: PromptDraft; onChange: (draft: PromptDraft) => void; onSave: () => void; onBack: () => void;
  saving: boolean; error: string; editing: boolean;
  promptId?: string | null; preparation?: ReactNode;
}) {
  const [variables, setVariables] = useState<string[]>([]);
  const [parseError, setParseError] = useState('');
  const [toggleBusy, setToggleBusy] = useState(false);
  const [toggleFeedback, setToggleFeedback] = useState('');
  const contentRef = useRef<HTMLTextAreaElement>(null);
  const selection = useRef({ start: 0, end: 0 });
  const originals = useRef(new Map<string, string>());
  const toggleController = useRef<AbortController | null>(null);
  useEffect(() => () => toggleController.current?.abort(), []);
  function rememberSelection() { if (contentRef.current) selection.current = { start: contentRef.current.selectionStart, end: contentRef.current.selectionEnd }; }
  async function toggleVariable() {
    if (toggleBusy) return;
    const { start, end } = selection.current;
    const selected = draft.templateText.slice(start, end).replace(/^</, '').replace(/>$/, '');
    // This map only restores literals created in this editor session; the server owns parsing.
    let originalText = originals.current.get(selected);
    if (!originalText) for (const [name, original] of originals.current) {
      const begin = draft.templateText.lastIndexOf(`<${name}>`, start);
      if (begin >= 0 && start >= begin && end <= begin + name.length + 2) originalText = original;
    }
    const current = new AbortController(); toggleController.current = current;
    setToggleBusy(true); setToggleFeedback('');
    try {
      const result = await request('/templates/toggle-variable', z.object({ templateText: z.string(), variableNames: z.array(z.string()), start: z.number(), end: z.number(), action: z.enum(['created', 'removed']), name: z.string(), originalText: z.string().nullable() }), post({ templateText: draft.templateText, start, end, originalText }, current.signal));
      if (current.signal.aborted) return;
      if (result.action === 'created' && result.originalText !== null) originals.current.set(result.name, result.originalText);
      onChange({ ...draft, templateText: result.templateText }); setVariables(result.variableNames); setParseError('');
      selection.current = { start: result.start, end: result.end };
      setToggleFeedback(result.action === 'created' ? `Variable ${result.name} creada. Pulsa el mismo botón para devolverla a texto.` : 'Variable convertida en texto normal.');
      requestAnimationFrame(() => { contentRef.current?.focus(); contentRef.current?.setSelectionRange(result.start, result.end); });
    } catch (error) { if (!current.signal.aborted) setToggleFeedback(errorMessage(error)); }
    finally { if (!current.signal.aborted) setToggleBusy(false); }
  }
  useEffect(() => {
    const controller = new AbortController();
    const timer = setTimeout(() => {
      request('/templates/inspect', inspectSchema, post({ templateText: draft.templateText }, controller.signal))
        .then(result => { if (!controller.signal.aborted) { setVariables(result.variableNames); setParseError(''); } })
        .catch(error => { if (!controller.signal.aborted) { setVariables([]); setParseError(errorMessage(error)); } });
    }, 250);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [draft.templateText]);
  return <section className="editor focus-space" aria-labelledby="editor-heading" data-testid="prompt-editor" data-prompt-id={promptId ?? ''} data-revision={draft.revision ?? 0}>
    <button className="text-button" onClick={onBack}><ArrowLeft size={17} aria-hidden="true" />Volver</button>
    <p className="eyebrow">{editing ? 'DALE UNA NUEVA VERSIÓN' : 'DE LA IDEA A LA INSTRUCCIÓN'}</p>
    <h1 id="editor-heading">{editing ? 'Afina tu prompt.' : 'Crea algo reutilizable.'}</h1>
    <p className="muted">Solo necesitas un título y una instrucción. El resto puede esperar.</p>
    <form onSubmit={event => { event.preventDefault(); onSave(); }}>
      <label htmlFor="prompt-title">Título</label>
      <input id="prompt-title" autoFocus required maxLength={160} value={draft.title} onChange={e => onChange({ ...draft, title: e.target.value })} placeholder="Un nombre fácil de encontrar" />
      <label htmlFor="prompt-content">Contenido</label>
      <textarea id="prompt-content" ref={contentRef} required maxLength={32000} rows={9} disabled={toggleBusy} value={draft.templateText} onSelect={rememberSelection} onChange={e => onChange({ ...draft, templateText: e.target.value })} placeholder="Escribe tu instrucción. Selecciona un texto para convertirlo en variable." aria-describedby="variable-help" />
      <div className="variable-toolbar"><button type="button" className="secondary" disabled={saving || toggleBusy || !draft.templateText} onMouseDown={event => event.preventDefault()} onClick={toggleVariable}><Braces size={17} aria-hidden="true" />{toggleBusy ? 'Aplicando…' : 'Crear / quitar variable'}</button><span className="muted">Selecciona texto o una variable y pulsa el mismo botón.</span></div>
      {toggleFeedback && <p role="status" className="notice">{toggleFeedback}</p>}
      <p id="variable-help" className="variable-hint"><Braces size={16} aria-hidden="true" />{variables.length ? `Variables detectadas: ${variables.join(', ')}` : 'También puedes crear prompts sin variables.'}</p>
      {parseError && <p className="error" role="status">{parseError}</p>}
      <details><summary>Descripción opcional</summary><label htmlFor="prompt-description">Descripción</label><textarea id="prompt-description" rows={2} maxLength={2000} value={draft.description} onChange={e => onChange({ ...draft, description: e.target.value })} /></details>
      {preparation}
      {error && <p className="error" role="alert">{error}</p>}
      <div className="editor-actions"><button type="button" className="text-button" onClick={onBack}>Cancelar</button><button className="primary" data-nova-approval="true" disabled={saving || !!parseError || !draft.title.trim() || !draft.templateText.trim()}><Check size={18} aria-hidden="true" />{saving ? 'Guardando…' : editing ? 'Guardar versión' : 'Guardar prompt'}</button></div>
    </form>
  </section>;
}
