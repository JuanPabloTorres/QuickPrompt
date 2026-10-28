import { useMemo, useState } from 'react';
import { Dialog } from '../../components/ui/Dialog';
import { InlineSpinner } from '../../components/ui/Feedback';
import type { AccountPrompt, AccountPromptDraft, AccountService, SavedEditorContext } from '../../services/accounts';
import { PromptVariableFields } from '../prompts/PromptVariableFields';
import { useAccountOperation } from './useAccountOperation';
import { useAccountEditorInspection } from './useAccountEditorInspection';

export function AccountPromptEditor({ source, restored, libraryId, service, onExpired, onRemembered, saving, error, onSave, onClose, controlled = false, approvedRunId }: { source: AccountPrompt | null; restored?: SavedEditorContext; libraryId: string; service: AccountService; onExpired: () => void; onRemembered: () => void; saving: boolean; error: string; onSave: (draft: AccountPromptDraft, asNew?: boolean) => void; onClose: () => void; controlled?: boolean; approvedRunId?: string }) {
  const [title, setTitle] = useState(restored?.title ?? source?.title ?? '');
  const [template, setTemplate] = useState(restored?.templateText ?? source?.template ?? '');
  const [memory, setMemory] = useState(restored);
  const [sessionId] = useState(() => restored?.id ?? crypto.randomUUID());
  const [values, setValues] = useState<Record<string, string>>(restored?.values ?? {});
  const [showTemplate, setShowTemplate] = useState(false);
  const inspection = useAccountEditorInspection(libraryId, restored?.sourceStale ? null : source, template, values, service, onExpired);
  const names = inspection.variableNames;
  const scope = useMemo(() => ({ libraryId, sessionId, service }), [libraryId, sessionId, service]);
  const remembering = useAccountOperation(scope, onExpired);
  const busy = saving || remembering.busy;
  function remember() {
    if (busy) return;
    void remembering.run(signal => service.rememberEditor(libraryId, { sessionId, expectedDraftRevision: memory?.draftRevision ?? 0,
      expectedDraftId: memory?.draftId ?? null, expectedMemoryRevision: memory?.memoryRevision ?? 0, confirmed: true,
      promptId: source?.id ?? null, revision: source?.revision ?? null, title, templateText: template, values }, signal), saved => {
      setMemory(saved); onRemembered();
    }, 'Borrador y valores conservados sólo en esta cuenta y biblioteca.');
  }
  return <Dialog open onOpenChange={open => { if (!open && !busy) onClose(); }} title={source ? 'Editar prompt privado' : 'Crear prompt privado'} description="Sólo se guarda en la biblioteca seleccionada. Compartir requiere una publicación explícita.">
    <form className="account-prompt-form" data-testid="prompt-editor" data-prompt-id={source?.id ?? ''} data-revision={source?.revision ?? ''} onSubmit={event => { event.preventDefault(); if (!controlled || approvedRunId) onSave({ title, template }); }}>
      <fieldset disabled={busy}><label htmlFor="prompt-title">Título</label><input id="prompt-title" required maxLength={160} autoFocus value={title} onChange={event => setTitle(event.target.value)} />
        <label htmlFor="prompt-content">Contenido</label><textarea id="prompt-content" required rows={10} maxLength={32000} value={template} onChange={event => { setTemplate(event.target.value); setValues({}); }} aria-describedby="account-prompt-help" />
        <p id="account-prompt-help" className="muted">Escribe {'<nombre_de_variable>'} donde quieras completar un dato al usar el prompt.</p>
        {inspection.pending && <p role="status">Comprobando variables y vista previa…</p>}
        {names.length > 0 && <div className="account-memory-values"><h3>Datos de este borrador</h3><PromptVariableFields names={names} values={values} idPrefix="account-memory-value" workspace maxLength={8000} onValueChange={(name, value) => setValues(previous => ({ ...previous, [name]: value }))} /></div>}
        {inspection.result?.missingVariables.length ? <p className="muted">Completa los datos para ver la instrucción preparada.</p> : null}
        {inspection.result && <span className="sr-only" data-testid="variables-inspected">Variables comprobadas</span>}
        {inspection.result?.compiledText != null && <><span className="sr-only" data-testid="compile-ready">Lista para usar</span><pre data-testid="compiled-preview" aria-label="Vista previa del borrador" className="account-memory-preview">{inspection.result.compiledText}</pre></>}
        {source && (template !== source.template || title !== source.title) && <span className="sr-only" data-testid="draft-prepared">Borrador modificado</span>}
        <button type="button" className="text-button" data-nova-control="prompt.template-preview" onClick={() => setShowTemplate(value => !value)}>{showTemplate ? 'Ocultar plantilla' : 'Ver plantilla exacta'}</button>
        {showTemplate && <pre data-testid="template-preview" className="account-memory-preview">{template}</pre>}
      </fieldset>
      {inspection.error && <p className="error" role="alert">{inspection.error} <button type="button" className="text-button" onClick={inspection.retry}>Reintentar vista previa</button></p>}
      {restored?.sourceStale && <p className="error" role="alert">El origen cambió desde que conservaste este borrador. Revisa el contenido; puedes guardarlo como un prompt nuevo.</p>}
      <div className="account-memory-save"><button type="button" className="secondary" disabled={busy || controlled || !title.trim() || !template.trim() || restored?.sourceStale} onClick={remember}>{remembering.busy && <InlineSpinner />}Conservar borrador</button><p className="muted">Guarda este contexto y sus valores durante siete días. No modifica el prompt guardado ni conserva permisos de ejecución.</p></div>
      {remembering.error && <p className="error" role="alert">{remembering.error}</p>}{remembering.notice && <p role="status" className="account-notice">{remembering.notice}</p>}
      {error && <p role="alert" className="error">{error} Conserva tu texto antes de cerrar y recargar la revisión.</p>}
      <div className="dialog-actions"><button type="button" className="secondary" disabled={busy || controlled} onClick={onClose}>Cancelar</button>{source && <button type="button" className="secondary" disabled={busy || controlled || !title.trim() || !template.trim()} onClick={() => onSave({ title, template }, true)}>Guardar como nuevo</button>}<button className="primary" data-nova-control={approvedRunId ? 'approval.apply' : undefined} data-nova-run-id={approvedRunId} disabled={busy || controlled && !approvedRunId || !title.trim() || !template.trim() || restored?.sourceStale}>{saving && <InlineSpinner />}{approvedRunId ? 'Aplicar versión aprobada' : 'Guardar prompt'}</button></div>
    </form>
  </Dialog>;
}
