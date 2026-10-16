import { useEffect, useRef, useState, type ReactNode } from 'react';
import { ArrowLeft, Archive, Copy, Folder, Pencil, Star, Trash2, Undo2 } from 'lucide-react';
import { compileSchema, emptySchema, errorMessage, post, request, type Prompt } from '../../services/api';

export function PromptFocus({ prompt, values, onValues, onBack, onEdit, onAction, onDelete, onUsed, notify, onUse, assistance, onGroups, feedback }: {
  prompt: Prompt; values: Record<string, string>; onValues: (values: Record<string, string>) => void;
  onBack: () => void; onEdit: () => void; onAction: (action: string) => void;
  onDelete: () => void; onUsed: () => void; notify: (text: string) => void;
  onUse?: () => void; assistance?: ReactNode;
  onGroups?: () => void;
  feedback?: string;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [preview, setPreview] = useState('');
  const hasPending = prompt.variableNames.some(name => !(values[name]?.trim() || prompt.defaults[name]?.trim()));
  const copyRequest = useRef<AbortController | null>(null);
  const titleRef = useRef<HTMLHeadingElement>(null);
  useEffect(() => { titleRef.current?.focus(); }, [prompt.id]);
  useEffect(() => () => copyRequest.current?.abort(), []);
  useEffect(() => {
    const controller = new AbortController();
    setPreview(''); setError('');
    if (hasPending || prompt.isArchived) return () => controller.abort();
    const timer = setTimeout(() => {
      request(`/prompts/${prompt.id}/compile`, compileSchema, post({ revision: prompt.revision, values }, controller.signal))
        .then(result => { if (!controller.signal.aborted) setPreview(result.text); })
        .catch(error => { if (!controller.signal.aborted) setError(errorMessage(error)); });
    }, 180);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [prompt.id, prompt.revision, prompt.isArchived, values, hasPending]);
  async function copy() {
    if (busy) return;
    const controller = new AbortController(); copyRequest.current = controller;
    setBusy(true); setError('');
    try {
      const result = await request(`/prompts/${prompt.id}/compile`, compileSchema, post({ revision: prompt.revision, values }, controller.signal));
      if (controller.signal.aborted) return;
      await navigator.clipboard.writeText(result.text);
      notify('Copiado. Listo para tu siguiente idea.');
      try {
        await request(`/prompts/${prompt.id}/usage`, emptySchema, post({ revision: result.templateRevision, values, operationId: crypto.randomUUID(), expectedText: result.text }));
        onUsed();
      } catch { if (!controller.signal.aborted) notify('Texto copiado. No se pudo registrar el uso.'); }
    } catch (error) {
      if (!controller.signal.aborted) setError(errorMessage(error));
    } finally { if (!controller.signal.aborted) setBusy(false); }
  }
  return <section className="focus-space" aria-labelledby="focus-heading">
    <div className="focus-top"><button className="text-button" onClick={onBack}><ArrowLeft size={17} aria-hidden="true" />Biblioteca</button><span className="eyebrow">VERSIÓN {prompt.revision.toString().padStart(2, '0')}</span></div>
    <p className="eyebrow">{prompt.isArchived ? 'EN EL ARCHIVO' : 'UN PROMPT, INFINITAS POSIBILIDADES'}</p>
    <h1 id="focus-heading" ref={titleRef} tabIndex={-1}>{prompt.title}</h1>
    {prompt.description && <p className="focus-description muted">{prompt.description}</p>}
    <div className="context-actions">
      <button className="text-button" onClick={() => onAction('favorite')} aria-pressed={prompt.isFavorite}><Star size={17} aria-hidden="true" />{prompt.isFavorite ? 'Favorito' : 'Favorito'}</button>
      <button className="text-button" onClick={onEdit}><Pencil size={17} aria-hidden="true" />Editar</button>
      {onGroups && <button className="text-button" onClick={onGroups}><Folder size={17} aria-hidden="true" />Colecciones</button>}
      <button className="text-button" onClick={() => onAction('duplicate')}><Copy size={17} aria-hidden="true" />Duplicar</button>
      <button className="text-button" onClick={() => onAction(prompt.isArchived ? 'restore' : 'archive')}>{prompt.isArchived ? <Undo2 size={17} aria-hidden="true" /> : <Archive size={17} aria-hidden="true" />}{prompt.isArchived ? 'Recuperar' : 'Archivar'}</button>
      <button className="icon-button danger" onClick={onDelete} aria-label="Eliminar prompt"><Trash2 size={17} aria-hidden="true" /></button>
    </div>
    {!prompt.isArchived && assistance}
    {prompt.isArchived ? <p className="notice">Recupera este prompt para volver a utilizarlo.</p> : <>
      {prompt.variableNames.length > 0 && <div className="variable-composer"><div className="section-heading"><h2>Dale tu contexto</h2><span className="muted">{prompt.variableNames.length} variables</span></div>
        <div className="variable-grid">{prompt.variableNames.map((name, index) => <div key={name}><label htmlFor={`variable-${index}`}>{name}</label><textarea id={`variable-${index}`} rows={2} value={values[name] ?? prompt.defaults[name] ?? ''} onChange={e => onValues({ ...values, [name]: e.target.value })} placeholder={`Completa ${name}`} /></div>)}</div>
      </div>}
      <section className="prompt-preview" aria-labelledby="preview-heading"><h2 id="preview-heading">{prompt.variableNames.length ? 'Vista previa del resultado' : 'Tu instrucción'}</h2><pre>{preview || prompt.templateText}</pre>{preview && <button className="text-button" onClick={() => { const url = URL.createObjectURL(new Blob([preview], { type: 'text/plain;charset=utf-8' })); const anchor = document.createElement('a'); anchor.href = url; anchor.download = 'quickprompt.txt'; anchor.click(); setTimeout(() => URL.revokeObjectURL(url), 1000); }}>Descargar texto (.txt)</button>}</section>
      {error && <p role="alert" className="error">{error}</p>}
      <div className="action-dock"><span className="muted">{hasPending ? 'Completa las variables para continuar.' : 'Tu instrucción. Tu siguiente paso.'}</span><div className="dock-buttons">{onUse && <button className="secondary" onClick={onUse} disabled={busy || hasPending}>Usar en…</button>}<button className="primary" onClick={copy} disabled={busy || hasPending}><Copy size={18} aria-hidden="true" />{busy ? 'Preparando…' : 'Copiar prompt'}</button></div>{feedback && <p className="dock-feedback">{feedback}</p>}</div>
    </>}
  </section>;
}
