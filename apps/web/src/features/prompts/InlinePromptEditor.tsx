import { useApplicationServices } from '../../components/ServiceContext';
import { SaveIndicator } from '../../components/ui/Feedback';
import { AppIcon } from '../../components/ui/Icon';
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { ArrowLeft, Braces, Pencil, Redo2, Undo2 } from 'lucide-react';
import { errorMessage, type PromptDraft } from '../../services/api';

import { CategorySelector } from './CategorySelector';
import { useBoundOperation } from '../../services/useBoundOperation';
import { useEditorHistory } from './useEditorHistory';

export function InlinePromptEditor({ draft, onChange, onSave, onBack, saving, error, editing, promptId, preparation, sourceDefaults, metadataLocked = false }: {
  draft: PromptDraft; onChange: (draft: PromptDraft) => void; onSave: () => void; onBack: () => void;
  saving: boolean; error: string; editing: boolean;
  promptId?: string | null; preparation?: ReactNode;
  sourceDefaults?: Record<string, string>; metadataLocked?: boolean;
}) {
  const { templates } = useApplicationServices();
  const [variables, setVariables] = useState<string[]>([]);
  const [inspectedText, setInspectedText] = useState<string | null>(null);
  const [parseError, setParseError] = useState('');
  const snapshot = useMemo(() => ({ promptId, draft, metadataLocked }), [promptId, draft, metadataLocked]);
  const operation = useBoundOperation(snapshot);
  const toggleBusy = operation.busy;
  const [toggleFeedback, setToggleFeedback] = useState('');
  const [variableName, setVariableName] = useState('');
  const contentRef = useRef<HTMLTextAreaElement>(null);
  const selection = useRef({ start: 0, end: 0 });
  const history = useEditorHistory(draft, `${promptId ?? 'new'}:${draft.revision ?? 0}:${metadataLocked}`, onChange);
  function rememberSelection() {
    if (contentRef.current) {
      selection.current = { start: contentRef.current.selectionStart, end: contentRef.current.selectionEnd };
      history.capture(selection.current);
    }
  }
  function restoreSelection(start: number, end: number, text: string) {
    selection.current = { start, end };
    requestAnimationFrame(() => {
      const field = contentRef.current;
      if (field?.isConnected && field.value === text && (document.activeElement === field || document.activeElement === document.body)) {
        field.focus(); field.setSelectionRange(start, end);
      }
    });
  }
  function moveHistory(direction: 'undo' | 'redo') {
    if (saving || toggleBusy) return;
    const restored = history.move(direction); setToggleFeedback('');
    if (restored) restoreSelection(restored.selection.start, restored.selection.end, restored.draft.templateText);
  }
  async function toggleVariable() {
    if (toggleBusy) return;
    rememberSelection();
    const { start, end } = selection.current;
    const selected = draft.templateText.slice(start, end).replace(/^</, '').replace(/>$/, '');
    // This map only restores literals created in this editor session; the server owns parsing.
    let originalText = history.originals().get(selected);
    if (!originalText) for (const [name, original] of history.originals()) {
      const begin = draft.templateText.lastIndexOf(`<${name}>`, start);
      if (begin >= 0 && start >= begin && end <= begin + name.length + 2) originalText = original;
    }
    const current = operation.begin();
    if (!current) return;
    setToggleFeedback('');
    try {
      const result = await templates.toggle({ templateText: draft.templateText, start, end, originalText }, current.signal);
      if (!operation.isCurrent(current)) return;
      const originals = new Map(history.originals());
      if (result.action === 'created' && result.originalText !== null) originals.set(result.name, result.originalText);
      if (!result.variableNames.includes(result.name)) originals.delete(result.name);
      const defaults = draft.defaults && Object.fromEntries(Object.entries(draft.defaults).filter(([name]) => result.variableNames.includes(name)));
      history.commit({ ...draft, templateText: result.templateText, ...(draft.defaults ? { defaults } : {}) }, { start: result.start, end: result.end }, originals);
      setVariables(result.variableNames); setParseError('');
      setToggleFeedback(result.action === 'created' ? `Variable ${result.name} creada. Pulsa el mismo botón para devolverla a texto.` : 'Variable convertida en texto normal.');
      restoreSelection(result.start, result.end, result.templateText);
    } catch (error) { if (!current.signal.aborted) setToggleFeedback(errorMessage(error)); }
    finally { operation.finish(current); }
  }
  async function renameVariable() {
    if (saving || metadataLocked || !variableName.trim()) return;
    rememberSelection();
    const current = operation.begin(); if (!current) return;
    setToggleFeedback('');
    try {
      const result = await templates.rename({ templateText: draft.templateText, ...selection.current, name: variableName, defaults: draft.defaults ?? sourceDefaults ?? {} }, current.signal);
      if (!operation.isCurrent(current)) return;
      const originals = new Map(history.originals());
      const original = originals.get(result.previousName);
      originals.delete(result.previousName); if (original !== undefined) originals.set(result.name, original);
      history.commit({ ...draft, templateText: result.templateText, defaults: result.defaults }, { start: result.start, end: result.end }, originals);
      setVariables(result.variableNames); setParseError(''); setToggleFeedback(`Variable renombrada a ${result.name}.`);
      restoreSelection(result.start, result.end, result.templateText);
    } catch (error) { if (operation.isCurrent(current)) setToggleFeedback(errorMessage(error)); }
    finally { operation.finish(current); }
  }
  useEffect(() => {
    const controller = new AbortController();
    const timer = setTimeout(() => {
      templates.inspect(draft.templateText, controller.signal)
        .then(result => { if (!controller.signal.aborted) { setVariables(result.variableNames); setInspectedText(draft.templateText); setParseError(''); } })
        .catch(error => { if (!controller.signal.aborted) { setVariables([]); setInspectedText(null); setParseError(errorMessage(error)); } });
    }, 250);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [draft.templateText]);
  return <section className="editor focus-space" aria-labelledby="editor-heading" data-testid="prompt-editor" data-prompt-id={promptId ?? ''} data-revision={draft.revision ?? 0}>
    <button className="text-button" onClick={onBack}><AppIcon glyph={ArrowLeft} size={20} aria-hidden="true"  />Volver</button>
    <p className="eyebrow">{editing ? 'DALE UNA NUEVA VERSIÓN' : 'DE LA IDEA A LA INSTRUCCIÓN'}</p>
    <h1 id="editor-heading">{editing ? 'Afina tu prompt.' : 'Crea algo reutilizable.'}</h1>
    <p className="muted">Solo necesitas un título y una instrucción. El resto puede esperar.</p>
    <form onSubmit={event => { event.preventDefault(); onSave(); }}>
      <label htmlFor="prompt-title">Título</label>
      <input id="prompt-title" autoFocus required maxLength={160} value={draft.title} onChange={e => history.commit({ ...draft, title: e.target.value })} placeholder="Un nombre fácil de encontrar" />
      <label htmlFor="prompt-content">Contenido</label>
      <textarea id="prompt-content" ref={contentRef} required maxLength={32000} rows={9} disabled={toggleBusy} value={draft.templateText} onSelect={rememberSelection} onBlur={rememberSelection} onBeforeInput={rememberSelection} onKeyDown={event => {
        if ((event.ctrlKey || event.metaKey) && !event.altKey && ['z', 'y'].includes(event.key.toLowerCase())) {
          event.preventDefault(); moveHistory(event.key.toLowerCase() === 'y' || event.shiftKey ? 'redo' : 'undo');
        }
      }} onChange={e => history.commit({ ...draft, templateText: e.target.value }, { start: e.target.selectionStart, end: e.target.selectionEnd })} placeholder="Escribe tu instrucción. Selecciona un texto para convertirlo en variable." aria-describedby="variable-help" />
      <div className="variable-toolbar"><button type="button" className="secondary" disabled={saving || toggleBusy || !draft.templateText} onMouseDown={event => event.preventDefault()} onClick={toggleVariable}><AppIcon glyph={Braces} size={20} aria-hidden="true"  />{toggleBusy ? 'Aplicando…' : 'Crear / quitar variable'}</button><span className="muted">Selecciona texto o una variable y pulsa el mismo botón.</span></div>
      <div className="variable-toolbar" role="group" aria-label="Historial del editor">
        <button type="button" className="secondary" disabled={saving || toggleBusy || !history.undo} onMouseDown={event => event.preventDefault()} onClick={() => moveHistory('undo')}><AppIcon glyph={Undo2} size={20} aria-hidden="true" />Deshacer</button>
        <button type="button" className="secondary" disabled={saving || toggleBusy || !history.redo} onMouseDown={event => event.preventDefault()} onClick={() => moveHistory('redo')}><AppIcon glyph={Redo2} size={20} aria-hidden="true" />Rehacer</button>
      </div>
      <details><summary>Renombrar una variable</summary><label htmlFor="editor-variable-name">Nuevo nombre de variable</label><input id="editor-variable-name" maxLength={80} value={variableName} disabled={saving || toggleBusy || metadataLocked} onChange={event => setVariableName(event.target.value)} />
        <p className="muted">Selecciona la variable en el contenido. Se actualizarán todas sus apariciones y su valor por defecto.</p>
        {metadataLocked && <p className="muted">Termina la tarea actual de NOVA para cambiar nombres y valores por defecto.</p>}
        <button type="button" className="secondary" disabled={saving || toggleBusy || metadataLocked || !variableName.trim()} onClick={renameVariable}><AppIcon glyph={Pencil} size={20} aria-hidden="true" />Renombrar variable</button>
      </details>
      {inspectedText === draft.templateText && variables.length > 0 && <details><summary>Valores por defecto de las variables</summary>
        <p className="muted">Se usan cuando dejas el campo vacío. Sin valor por defecto, la variable sigue siendo obligatoria. Se guardan como parte del prompt.</p>
        {metadataLocked && <p className="muted">Termina la tarea actual de NOVA para cambiar estos valores.</p>}
        {variables.map((name, index) => <div key={name}>
          <label htmlFor={`editor-default-${index}`}>Valor por defecto de {name}</label>
          <textarea id={`editor-default-${index}`} data-testid="variable-default" data-variable-name={name} rows={2} maxLength={8000}
            disabled={saving || toggleBusy || metadataLocked} value={(draft.defaults ?? sourceDefaults)?.[name] ?? ''}
            onChange={event => {
              const defaults = Object.fromEntries(Object.entries(draft.defaults ?? sourceDefaults ?? {}).filter(([key]) => variables.includes(key)));
              if (event.target.value) defaults[name] = event.target.value; else delete defaults[name];
              history.commit({ ...draft, defaults });
            }} />
        </div>)}
      </details>}
      {toggleFeedback && <p role="status" className="notice">{toggleFeedback}</p>}
      <p id="variable-help" className="variable-hint"><AppIcon glyph={Braces} size={16} aria-hidden="true"  />{variables.length ? `Variables detectadas: ${variables.join(', ')}` : 'También puedes crear prompts sin variables.'}</p>
      {parseError && <p className="error" role="status">{parseError}</p>}
      <details><summary>Categoría y descripción opcionales</summary><CategorySelector value={draft.categoryId} disabled={saving} onChange={categoryId => history.commit({ ...draft, categoryId })} /><label htmlFor="prompt-description">Descripción</label><textarea id="prompt-description" rows={2} maxLength={2000} value={draft.description} onChange={e => history.commit({ ...draft, description: e.target.value })} /></details>
      {preparation}
      {error && <p className="error" role="alert">{error}</p>}
      <div className="editor-actions"><button type="button" className="text-button" onClick={onBack}>Cancelar</button><button className="primary" data-nova-approval="true" disabled={saving || !!parseError || !draft.title.trim() || !draft.templateText.trim()}><SaveIndicator busy={saving}>{saving ? 'Guardando…' : editing ? 'Guardar versión' : 'Guardar prompt'}</SaveIndicator></button></div>
    </form>
  </section>;
}
