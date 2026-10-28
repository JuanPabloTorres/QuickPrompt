import { useMemo, useState } from 'react';
import type { AccountPrompt, AccountService } from '../../services/accounts';
import { PromptVariableFields } from '../prompts/PromptVariableFields';
import { useAccountEditorInspection } from './useAccountEditorInspection';
import { downloadPreparedText } from '../../services/textExport';
import { AccountPromptVersions } from './AccountPromptVersions';
import { useAccountOperation } from './useAccountOperation';

export function AccountPromptFocus({ prompt, service, onExpired, onEdit, onClose, onSaved }: {
  prompt: AccountPrompt; service: AccountService; onExpired: () => void; onEdit: () => void; onClose: () => void; onSaved: (prompt: AccountPrompt) => void;
}) {
  const scope = useMemo(() => ({ prompt, service }), [prompt, service]);
  const operation = useAccountOperation(scope, onExpired);
  const [versions, setVersions] = useState(false);
  const [values, setValues] = useState<Record<string, string>>({});
  const inspection = useAccountEditorInspection(prompt.libraryId, prompt, prompt.template, values, service, onExpired);
  return <section className="account-remembered" data-testid="prompt-focus" data-prompt-id={prompt.id} data-revision={prompt.revision} data-template-text={prompt.template} aria-label={`Detalle de ${prompt.title}`}>
    <button type="button" className="text-button" data-nova-control="library.home" onClick={onClose}>Volver a los prompts</button>
    <div className="account-section-heading"><div><span className="eyebrow">VERSIÓN {prompt.revision} · BIBLIOTECA PRIVADA</span><h2 data-testid="focus-title">{prompt.title}</h2></div>
      <button type="button" className="secondary" data-nova-control="prompt.edit" onClick={onEdit}>Editar prompt</button></div>
    <div className="account-actions"><button type="button" className="secondary" onClick={() => setVersions(true)}>Ver versiones</button><button type="button" className="text-button" disabled={operation.busy} onClick={() => void operation.run(signal => service.exportPrompt(prompt, signal), packet => downloadPreparedText(JSON.stringify(packet, null, 2), `quickprompt-${prompt.id}-v${prompt.revision}.json`), 'Plantilla portátil preparada sin valores privados.')}>Exportar plantilla JSON</button></div>
    {versions && <AccountPromptVersions prompt={prompt} service={service} onExpired={onExpired} onClose={() => setVersions(false)} onSaved={saved => { setVersions(false); onSaved(saved); }} />}
    <pre className="account-memory-preview" data-testid="template-preview">{prompt.template}</pre>
    {inspection.pending && <p role="status">Comprobando variables…</p>}
    {inspection.variableNames.length > 0 && <PromptVariableFields workspace names={inspection.variableNames} values={values} idPrefix="account-focus-value" maxLength={8000}
      onValueChange={(name, value) => setValues(previous => ({ ...previous, [name]: value }))} />}
    {inspection.result && <span className="sr-only" data-testid="variables-inspected">Variables comprobadas</span>}
    {inspection.result?.compiledText != null && <><span className="sr-only" data-testid="compile-ready">Lista para usar</span><h3>Instrucción preparada</h3><pre className="account-memory-preview" data-testid="compiled-preview">{inspection.result.compiledText}</pre></>}
    {operation.error && <p role="alert" className="error">{operation.error}</p>}
    {operation.notice && <p role="status">{operation.notice}</p>}
    {inspection.error && <p className="error" role="alert">{inspection.error} <button type="button" className="text-button" onClick={inspection.retry}>Reintentar vista previa</button></p>}
  </section>;
}
