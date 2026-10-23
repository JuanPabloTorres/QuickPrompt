import { downloadPreparedText } from '../../services/textExport';
import { useApplicationServices } from '../../components/ServiceContext';
import { AppIcon } from '../../components/ui/Icon';
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { ArrowLeft, Archive, Copy, Folder, History, Pencil, Share2, Star, Trash2, Undo2 } from 'lucide-react';
import { errorMessage, type Prompt } from '../../services/api';
import { type PromptAction } from '../../services/library';
import { useBoundOperation } from '../../services/useBoundOperation';
import { ClipboardReceipt } from '../../components/ClipboardReceipt';
import { TemplateCopyPreview } from './TemplateCopyPreview';

export function PromptFocus({ prompt, values, onValues, onBack, onEdit, onAction, onDelete, onUsed, notify, onUse, assistance, onGroups, onVersions, onShare, feedback }: {
  prompt: Prompt; values: Record<string, string>; onValues: (values: Record<string, string>) => void;
  onBack: () => void; onEdit: () => void; onAction: (action: PromptAction) => void;
  onDelete: () => void; onUsed: () => void; notify: (text: string) => void;
  onUse?: () => void; assistance?: ReactNode;
  onGroups?: () => void;
  onVersions?: () => void;
  onShare?: () => void;
  feedback?: string;
}) {
  const { prompts } = useApplicationServices();
  const snapshot = useMemo(() => ({ id: prompt.id, revision: prompt.revision, archived: prompt.isArchived, values }), [prompt.id, prompt.revision, prompt.isArchived, values]);
  const operation = useBoundOperation(snapshot);
  const busy = operation.busy;
  const [error, setError] = useState('');
  const [copied, setCopied] = useState<{ snapshot: object; operationId: string; text: string; copyContent: 'RENDERED' | 'TEMPLATE' } | null>(null);
  useEffect(() => {
    window.addEventListener('nova-abort-ui-operation', operation.cancel);
    return () => window.removeEventListener('nova-abort-ui-operation', operation.cancel);
  }, [operation.cancel]);
  const [previewResult, setPreviewResult] = useState<{ id: string; revision: number; values: Record<string, string>; text: string } | null>(null);
  const preview = previewResult?.id === prompt.id && previewResult.revision === prompt.revision && previewResult.values === values && !prompt.isArchived ? previewResult.text : '';
  const hasPending = prompt.variableNames.some(name => !(values[name]?.trim() || prompt.defaults[name]?.trim()));
  const titleRef = useRef<HTMLHeadingElement>(null);
  useEffect(() => { titleRef.current?.focus(); }, [prompt.id]);
  useEffect(() => {
    const controller = new AbortController();
    setPreviewResult(null); setError('');
    if (hasPending || prompt.isArchived) return () => controller.abort();
    const timer = setTimeout(() => {
      prompts.compile(prompt, values, controller.signal)
        .then(result => { if (!controller.signal.aborted) setPreviewResult({ id: prompt.id, revision: prompt.revision, values, text: result.text }); })
        .catch(error => { if (!controller.signal.aborted) setError(errorMessage(error)); });
    }, 180);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [prompt.id, prompt.revision, prompt.isArchived, values, hasPending]);
  async function copy(copyContent: 'RENDERED' | 'TEMPLATE' = 'RENDERED') {
    const controller = operation.begin();
    if (!controller) return;
    setCopied(null);
    setError('');
    try {
      const result = copyContent === 'TEMPLATE' ? { text: prompt.templateText, templateRevision: prompt.revision } : await prompts.compile(prompt, values, controller.signal);
      if (!operation.isCurrent(controller)) return;
      await navigator.clipboard.writeText(result.text);
      if (!operation.isCurrent(controller)) return;
      const operationId = crypto.randomUUID();
      setCopied({ snapshot, operationId, text: result.text, copyContent });
      notify(copyContent === 'TEMPLATE' ? 'Plantilla copiada con sus marcadores.' : 'Copiado. Listo para tu siguiente idea.');
      if (copyContent === 'TEMPLATE') return;
      try {
        await prompts.recordUse({ id: prompt.id, revision: result.templateRevision }, values, operationId, result.text);
        if (operation.isCurrent(controller)) onUsed();
      } catch { if (!controller.signal.aborted) notify('Texto copiado. No se pudo registrar el uso.'); }
    } catch (error) {
      if (!controller.signal.aborted) setError(errorMessage(error));
    } finally { operation.finish(controller); }
  }
  return <section className="focus-space" aria-labelledby="focus-heading" data-testid="prompt-focus" data-prompt-id={prompt.id} data-revision={prompt.revision} data-template-text={prompt.templateText}>
    <div className="focus-top"><button className="text-button" onClick={onBack}><AppIcon glyph={ArrowLeft} size={20} aria-hidden="true"  />Biblioteca</button><span className="eyebrow">VERSIÓN {prompt.revision.toString().padStart(2, '0')}</span></div>
    <p className="eyebrow">{prompt.isArchived ? 'EN EL ARCHIVO' : 'UN PROMPT, INFINITAS POSIBILIDADES'}</p>
    <h1 id="focus-heading" data-testid="focus-title" ref={titleRef} tabIndex={-1}>{prompt.title}</h1>
    {prompt.description && <p className="focus-description muted">{prompt.description}</p>}
    <div className="context-actions">
      <button className="text-button" onClick={() => onAction('favorite')} aria-pressed={prompt.isFavorite}><AppIcon glyph={Star} size={20} aria-hidden="true"  />{prompt.isFavorite ? 'Favorito' : 'Favorito'}</button>
      <button data-nova-control="prompt.edit" className="text-button" onClick={onEdit}><AppIcon glyph={Pencil} size={20} aria-hidden="true"  />Editar</button>
      {onGroups && <button className="text-button" onClick={onGroups}><AppIcon glyph={Folder} size={20} aria-hidden="true"  />Colecciones</button>}
      {onVersions && <button className="text-button" onClick={onVersions}><AppIcon glyph={History} size={20} aria-hidden="true" />Versiones</button>}
      {onShare && <button className="text-button" onClick={onShare}><AppIcon glyph={Share2} size={20} aria-hidden="true" />Compartir plantilla</button>}
      <button className="text-button" onClick={() => onAction('duplicate')}><AppIcon glyph={Copy} size={20} aria-hidden="true"  />Duplicar</button>
      <button className="text-button" onClick={() => onAction(prompt.isArchived ? 'restore' : 'archive')}>{prompt.isArchived ? <AppIcon glyph={Undo2} size={20} aria-hidden="true"  /> : <AppIcon glyph={Archive} size={20} aria-hidden="true"  />}{prompt.isArchived ? 'Recuperar' : 'Archivar'}</button>
      <button className="icon-button danger" onClick={onDelete} aria-label="Eliminar prompt"><AppIcon glyph={Trash2} size={20} aria-hidden="true"  /></button>
    </div>
    {!prompt.isArchived && assistance}
    {prompt.isArchived ? <p className="notice">Recupera este prompt para volver a utilizarlo.</p> : <>
      {prompt.variableNames.length > 0 && <div className="variable-composer"><div className="section-heading"><h2>Dale tu contexto</h2><span className="muted">{prompt.variableNames.length} variables</span></div>
        <div className="variable-grid">{prompt.variableNames.map((name, index) => <div key={name}><label htmlFor={`variable-${index}`}>{name}</label><textarea id={`variable-${index}`} data-testid="variable-field" data-variable-name={name} rows={2} value={values[name] ?? prompt.defaults[name] ?? ''} onChange={e => onValues({ ...values, [name]: e.target.value })} placeholder={`Completa ${name}`} /></div>)}</div>
      </div>}
      <section className="prompt-preview" aria-labelledby="preview-heading"><h2 id="preview-heading">{prompt.variableNames.length ? 'Vista previa del resultado' : 'Tu instrucción'}</h2><pre data-testid="compiled-preview">{preview || prompt.templateText}</pre>{preview && <span data-testid="compile-ready" className="sr-only">Listo para utilizar</span>}{preview && <button type="button" className="text-button" disabled={busy} title="UTF-8 sin BOM, saltos de línea LF" onClick={() => { try { downloadPreparedText(preview); } catch (error) { setError(errorMessage(error)); } }}>Descargar texto (.txt)</button>}</section>
      {error && <p role="alert" className="error">{error}</p>}
      <TemplateCopyPreview text={prompt.templateText} disabled={busy} onCopy={() => void copy('TEMPLATE')} />
      <ClipboardReceipt receipt={copied?.snapshot === snapshot && copied.text === (copied.copyContent === 'TEMPLATE' ? prompt.templateText : preview) ? { operationId: copied.operationId, promptId: prompt.id, revision: prompt.revision, text: copied.text, copyContent: copied.copyContent } : null} />
      <div className="action-dock"><span className="muted">{hasPending ? 'Completa las variables para copiar el texto rellenado. La plantilla conserva sus marcadores.' : 'Tu instrucción. Tu siguiente paso.'}</span><div className="dock-buttons">{onUse && <button data-nova-control="prompt.use" className="secondary" onClick={onUse} disabled={busy || hasPending}>Usar en…</button>}<button data-nova-control="prompt.copy" className="primary" onClick={() => void copy()} disabled={busy || hasPending}><AppIcon glyph={Copy} size={20} aria-hidden="true"  />{busy ? 'Preparando…' : 'Copiar prompt'}</button></div>{feedback && <p className="dock-feedback">{feedback}</p>}</div>
    </>}
  </section>;
}
