import { downloadPreparedText } from '../../services/textExport';
import { useApplicationServices } from '../../components/ServiceContext';
import { AppIcon } from '../../components/ui/Icon';
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Braces, Check, Copy, FileText } from 'lucide-react';
import { errorMessage, type Prompt } from '../../services/api';
import { type PromptAction } from '../../services/library';
import { useBoundOperation } from '../../services/useBoundOperation';
import { ClipboardReceipt } from '../../components/ClipboardReceipt';
import { TemplateCopyPreview } from './TemplateCopyPreview';
import { PromptVariableFields } from './PromptVariableFields';
import { PromptDetailHeader } from './PromptDetailHeader';

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
    <PromptDetailHeader prompt={prompt} titleRef={titleRef} onBack={onBack} onEdit={onEdit} onAction={onAction} onDelete={onDelete} onGroups={onGroups} onVersions={onVersions} onShare={onShare} />
    {!prompt.isArchived && assistance}
    {prompt.isArchived ? <p className="notice detail-archived"><AppIcon glyph={FileText} size={20} />Recupera este prompt para volver a utilizarlo.</p> : <div className="detail-workspace">
      <div className="detail-context-column"><section className="variable-composer detail-context" aria-labelledby="context-heading"><div className="detail-section-heading"><span className="detail-step" aria-hidden="true">01</span><div><p className="eyebrow">PERSONALIZA ESTE USO</p><h2 id="context-heading">{prompt.variableNames.length ? 'Dale tu contexto' : 'Todo listo para empezar'}</h2></div><AppIcon glyph={Braces} size={20} /></div>
        <p className="detail-section-description">{prompt.variableNames.length ? 'Completa los datos de esta ocasión. La instrucción guardada conserva sus marcadores.' : 'Esta instrucción no necesita variables. Puedes copiarla o elegir dónde utilizarla.'}</p>
        <div className="variable-grid"><PromptVariableFields names={prompt.variableNames} values={values} defaults={prompt.defaults} idPrefix="variable" workspace placeholder={name => `Completa ${name}`} onValueChange={(name, value) => onValues({ ...values, [name]: value })} /></div>
      </section>
      </div>
      <div className="detail-output-column"><section className="prompt-preview detail-output" aria-labelledby="preview-heading"><div className="detail-section-heading"><span className="detail-step" aria-hidden="true">02</span><div><p className="eyebrow">REVISA Y UTILIZA</p><h2 id="preview-heading">{prompt.variableNames.length ? 'Vista previa del resultado' : 'Tu instrucción'}</h2></div></div><div className="detail-preview-status" data-ready={!!preview}>{preview ? <><AppIcon glyph={Check} size={16} />{prompt.variableNames.length ? 'Texto preparado con tus datos' : 'Texto preparado para usar'}</> : hasPending ? 'Completa el contexto para preparar el texto' : 'Preparando vista previa…'}</div><pre data-testid="compiled-preview">{preview || prompt.templateText}</pre>{preview && <span data-testid="compile-ready" className="sr-only">Listo para utilizar</span>}{preview && <button type="button" className="text-button detail-download" disabled={busy} title="UTF-8 sin BOM, saltos de línea LF" onClick={() => { try { downloadPreparedText(preview); } catch (error) { setError(errorMessage(error)); } }}>Descargar texto (.txt)</button>}
      {error && <p role="alert" className="error">{error}</p>}
      <TemplateCopyPreview text={prompt.templateText} disabled={busy} onCopy={() => void copy('TEMPLATE')} />
      <ClipboardReceipt receipt={copied?.snapshot === snapshot && copied.text === (copied.copyContent === 'TEMPLATE' ? prompt.templateText : preview) ? { operationId: copied.operationId, promptId: prompt.id, revision: prompt.revision, text: copied.text, copyContent: copied.copyContent } : null} />
      <div className="action-dock"><span className="muted">{hasPending ? 'Completa las variables para copiar el texto rellenado. La plantilla conserva sus marcadores.' : 'Tu instrucción. Tu siguiente paso.'}</span><div className="dock-buttons">{onUse && <button data-nova-control="prompt.use" className="secondary" onClick={onUse} disabled={busy || hasPending}>Usar en…</button>}<button data-nova-control="prompt.copy" className="primary" onClick={() => void copy()} disabled={busy || hasPending}><AppIcon glyph={Copy} size={20} aria-hidden="true"  />{busy ? 'Preparando…' : 'Copiar prompt'}</button></div>{feedback && <p className="dock-feedback">{feedback}</p>}</div>
      </section></div>
    </div>}
  </section>;
}
