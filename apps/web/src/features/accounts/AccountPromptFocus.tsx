import { useEffect, useMemo, useRef, useState } from 'react';
import type { AccountPrompt, AccountPromptAction, AccountService } from '../../services/accounts';
import { PromptVariableFields } from '../prompts/PromptVariableFields';
import { useAccountEditorInspection } from './useAccountEditorInspection';
import { downloadPreparedText } from '../../services/textExport';
import { AccountPromptVersions } from './AccountPromptVersions';
import { useAccountOperation } from './useAccountOperation';
import { PromptDetailHeader } from '../prompts/PromptDetailHeader';

export function AccountPromptFocus({ prompt, service, onExpired, onEdit, onClose, onSaved, onUse, onAction, onCollections, onShare, onDelete, busy = false }: {
  prompt: AccountPrompt; service: AccountService; onExpired: () => void; onEdit: () => void; onClose: () => void; onSaved: (prompt: AccountPrompt) => void;
  onUse?: (values: Record<string, string>) => void; onAction?: (action: AccountPromptAction) => void; onCollections?: () => void; onShare?: () => void; onDelete?: () => void; busy?: boolean;
}) {
  const scope = useMemo(() => ({ prompt, service }), [prompt, service]);
  const operation = useAccountOperation(scope, onExpired);
  const [versions, setVersions] = useState(false);
  const [values, setValues] = useState<Record<string, string>>({});
  const titleRef = useRef<HTMLHeadingElement>(null);
  useEffect(() => { titleRef.current?.focus(); }, [prompt.id]);
  const inspection = useAccountEditorInspection(prompt.libraryId, prompt, prompt.template, values, service, onExpired);
  // Rich snapshots already carry server-owned names. Keep their controlled fields
  // mounted across metadata revisions; only a current inspection enables use.
  const names = prompt.variableNames ?? inspection.variableNames;
  return <section className="focus-space account-focus" data-testid="prompt-focus" data-prompt-id={prompt.id} data-revision={prompt.revision} data-template-text={prompt.template} aria-label={`Detalle de ${prompt.title}`}>
    <PromptDetailHeader prompt={prompt} titleRef={titleRef} homeControl backLabel="Volver a los prompts" editLabel="Editar prompt" versionsLabel="Ver versiones" storageLabel="Guardado en tu biblioteca privada"
      busy={busy || operation.busy} onBack={onClose} onEdit={onEdit} onAction={onAction} onGroups={onCollections} onShare={onShare} onDelete={onDelete} onVersions={() => setVersions(true)} />
    <button type="button" className="text-button" disabled={busy || operation.busy} onClick={() => void operation.run(signal => service.exportPrompt(prompt, signal), packet => downloadPreparedText(JSON.stringify(packet, null, 2), `quickprompt-${prompt.id}-v${prompt.revision}.json`), 'Plantilla portátil preparada sin valores privados.')}>Exportar plantilla JSON</button>
    {versions && <AccountPromptVersions prompt={prompt} service={service} onExpired={onExpired} onClose={() => setVersions(false)} onSaved={saved => { setVersions(false); onSaved(saved); }} />}
    {prompt.isArchived && <p className="notice">Este prompt está archivado. Recupéralo para volver a utilizarlo.</p>}
    <div className="detail-workspace"><section className="variable-composer detail-context"><p className="eyebrow">PERSONALIZA ESTE USO</p><h2>Dale tu contexto</h2><p className="muted">Estos datos se usan en esta preparación. La plantilla guardada conserva sus marcadores.</p>
    {inspection.pending && <p role="status">Comprobando variables…</p>}
    {names.length > 0 && <div className="variable-grid"><PromptVariableFields workspace names={names} values={values} idPrefix="account-focus-value" maxLength={8000}
      onValueChange={(name, value) => setValues(previous => ({ ...previous, [name]: value }))} /></div>}
    {inspection.result && <span className="sr-only" data-testid="variables-inspected">Variables comprobadas</span>}
    {inspection.result && !names.length && <p className="muted">Esta instrucción no necesita datos adicionales.</p>}
    </section><section className="prompt-preview detail-output"><p className="eyebrow">REVISA Y UTILIZA</p><h2>Plantilla guardada</h2><pre data-testid="template-preview">{prompt.template}</pre>
    {inspection.result?.compiledText != null && <><span className="sr-only" data-testid="compile-ready">Lista para usar</span><h3>Instrucción preparada</h3><pre data-testid="compiled-preview">{inspection.result.compiledText}</pre></>}
    {onUse && <div className="action-dock"><p className="muted">Prepara una instrucción con estos mismos datos. Podrás copiar el resultado y confirmar su uso.</p><button type="button" className="primary" data-nova-control="prompt.use" disabled={busy || operation.busy || !!prompt.isArchived || inspection.pending || !inspection.result || !!inspection.result.missingVariables.length} onClick={() => onUse({ ...values })}>Usar prompt</button></div>}
    </section></div>
    {operation.error && <p role="alert" className="error">{operation.error}</p>}
    {operation.notice && <p role="status">{operation.notice}</p>}
    {inspection.error && <p className="error" role="alert">{inspection.error} <button type="button" className="text-button" onClick={inspection.retry}>Reintentar vista previa</button></p>}
  </section>;
}
