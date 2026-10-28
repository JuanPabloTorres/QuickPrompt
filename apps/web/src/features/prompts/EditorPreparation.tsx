import { useApplicationServices } from '../../components/ServiceContext';
import { useEffect, useMemo, useRef, useState } from 'react';
import { errorMessage, type Prompt, type PromptDraft } from '../../services/api';

import { useAgentProposalSession } from '../nova/AgentUiContext';

import { Copy } from 'lucide-react';
import { AppIcon } from '../../components/ui/Icon';
import { ClipboardReceipt } from '../../components/ClipboardReceipt';
import { useBoundOperation } from '../../services/useBoundOperation';
import { downloadPreparedText } from '../../services/textExport';
import { TemplateCopyPreview } from './TemplateCopyPreview';
import { PromptVariableFields } from './PromptVariableFields';

export function EditorPreparation({ source, draft, onChange, values, onValues, onUse, onObserve }: { source: Prompt | null; draft: PromptDraft; onChange: (draft: PromptDraft) => void; values: Record<string, string>; onValues: (values: Record<string, string>) => void; onUse: () => void; onObserve?: (message: string, source: Prompt, signal: AbortSignal) => Promise<string> }) {
  const { templates, preparation } = useApplicationServices();
  const proposalSession = useAgentProposalSession();
  const applyAgentProposal = (text: string, signal: AbortSignal) => proposalSession?.apply(text, signal) ?? Promise.resolve(false);
  const defaults = draft.defaults ?? source?.defaults;
  const [instruction, setInstruction] = useState(''), [busy, setBusy] = useState(false), [error, setError] = useState('');
  const [names, setNames] = useState<string[]>([]), [compiled, setCompiled] = useState<string | null>(null);
  const [preparedFor, setPreparedFor] = useState<{ text: string; values: Record<string, string>; defaults: Prompt['defaults'] | undefined } | null>(null);
  const [copiedText, setCopiedText] = useState<string | null>(null);
  const copySnapshot = useMemo(() => ({ id: source?.id, revision: source?.revision, text: draft.templateText, values, defaults }), [source?.id, source?.revision, draft.templateText, values, defaults]);
  const copyOperation = useBoundOperation(copySnapshot);
  const [copyReceipt, setCopyReceipt] = useState<{ snapshot: object; operationId: string; text: string; copyContent: 'RENDERED' | 'TEMPLATE' } | null>(null);
  useEffect(() => {
    window.addEventListener('nova-abort-ui-operation', copyOperation.cancel);
    return () => window.removeEventListener('nova-abort-ui-operation', copyOperation.cancel);
  }, [copyOperation.cancel]);
  const currentPreparation = preparedFor?.text === draft.templateText && preparedFor.values === values && preparedFor.defaults === defaults;
  const controller = useRef<AbortController | null>(null);
  useEffect(() => { const abort = () => { controller.current?.abort(); setBusy(false); }; window.addEventListener('nova-abort-ui-operation', abort); return () => { abort(); window.removeEventListener('nova-abort-ui-operation', abort); }; }, []);
  useEffect(() => {
    const current = new AbortController(); setCompiled(null); setError('');
    const timer = setTimeout(() => { void templates.inspect(draft.templateText, current.signal).then(async result => {
      if (current.signal.aborted) return; setNames(result.variableNames); const available = Object.fromEntries(result.variableNames.map(name => [name, values[name] ?? defaults?.[name] ?? '']));
      const resultText = await templates.compile(draft.templateText, available, current.signal);
      if (!current.signal.aborted) { setCompiled(resultText.text); setPreparedFor({ text: draft.templateText, values, defaults }); }
    }).catch(error => { if (!current.signal.aborted) setError(errorMessage(error)); }); }, 180);
    return () => { clearTimeout(timer); current.abort(); };
  }, [draft.templateText, values, defaults]);
  async function prepare() {
    if (!source || busy || !instruction.trim()) return; const current = new AbortController(); controller.current = current; setBusy(true); setError('');
    try { const runId = await onObserve?.(instruction, source, current.signal);
      const proposal = await preparation.propose({ intent: 'improve', context: instruction, promptId: source.id, revision: source.revision, model: 'qwen3:8b' }, runId, current.signal);
      if (proposal.templateId !== source.id || proposal.baseRevision !== source.revision || proposal.text === source.templateText) throw new Error('No se pudo verificar una modificación del origen seleccionado.');
      if (!current.signal.aborted && !await applyAgentProposal(proposal.text, current.signal) && !current.signal.aborted) onChange({ ...draft, templateText: proposal.text });
    } catch (error) { if (!current.signal.aborted) setError(errorMessage(error)); } finally { if (!current.signal.aborted) setBusy(false); }
  }
  async function copy(copyContent: 'RENDERED' | 'TEMPLATE' = 'RENDERED') {
    if (busy || !draft.templateText.trim() || copyContent === 'RENDERED' && (!currentPreparation || compiled === null)) return;
    const text = copyContent === 'TEMPLATE' ? draft.templateText : compiled!;
    const current = copyOperation.begin(); if (!current) return;
    setCopyReceipt(null); setCopiedText(null); setError('');
    try {
      await navigator.clipboard.writeText(text);
      if (copyOperation.isCurrent(current)) {
        setCopiedText(text);
        setCopyReceipt({ snapshot: copySnapshot, operationId: crypto.randomUUID(), text, copyContent });
      }
    } catch (error) { if (copyOperation.isCurrent(current)) setError(errorMessage(error)); }
    finally { copyOperation.finish(current); }
  }
  return <div className="editor-preparation" data-testid="editor-preparation" data-busy={busy}>
    {currentPreparation && <span data-testid="variables-inspected" className="sr-only">Variables comprobadas</span>}
    {source && <><label htmlFor="editor-change">Cambio que necesitas</label><textarea id="editor-change" rows={2} value={instruction} maxLength={4000} disabled={busy} onChange={event => setInstruction(event.target.value)} /><div className="dialog-actions"><button type="button" data-nova-control="editor.prepare" className="secondary" disabled={busy || !instruction.trim()} onClick={() => void prepare()}>{busy ? 'Consultando Qwen…' : 'Preparar cambio en el editor'}</button>{busy && <button type="button" className="secondary" onClick={() => { controller.current?.abort(); setBusy(false); }}>Detener cambio</button>}</div></>}
    {source && draft.templateText !== source.templateText && <p className="notice" data-testid="draft-prepared">Borrador modificado · original sin guardar</p>}
    {names.length > 0 && <div className="variable-grid"><PromptVariableFields names={names} values={values} defaults={defaults} idPrefix="editor-variable" workspace maxLength={8000} onValueChange={(name, value) => onValues({ ...values, [name]: value })} /></div>}
    <section className="prompt-preview" aria-label="Vista previa del borrador"><h2>Vista previa del resultado</h2><pre data-testid="compiled-preview">{currentPreparation ? compiled ?? draft.templateText : draft.templateText}</pre>{currentPreparation && compiled !== null && <span data-testid="compile-ready" className="muted">Listo para utilizar</span>}</section>
    <button type="button" className="text-button" disabled={busy || copyOperation.busy || !currentPreparation || compiled === null} title="UTF-8 sin BOM, saltos de línea LF" onClick={() => { try { if (currentPreparation && compiled !== null) downloadPreparedText(compiled); } catch (error) { setError(errorMessage(error)); } }}>Descargar texto (.txt)</button>
    <TemplateCopyPreview text={draft.templateText} disabled={busy || copyOperation.busy} onCopy={() => void copy('TEMPLATE')} />
    <ClipboardReceipt receipt={copyReceipt?.snapshot === copySnapshot && (copyReceipt.copyContent === 'TEMPLATE' ? copyReceipt.text === draft.templateText : currentPreparation && copyReceipt.text === compiled) ? { operationId: copyReceipt.operationId, promptId: source?.id ?? null, revision: source?.revision ?? null, text: copyReceipt.text, copyContent: copyReceipt.copyContent } : null} />
    <div className="dialog-actions"><button type="button" data-nova-control="prompt.use" className="secondary" disabled={busy || !currentPreparation || compiled === null || !draft.templateText.trim()} onClick={onUse}>Usar en…</button><button type="button" data-nova-control="prompt.copy" className="secondary" disabled={busy || copyOperation.busy || !currentPreparation || compiled === null || !draft.templateText.trim()} onClick={() => void copy()}><AppIcon glyph={Copy} aria-hidden="true" />Copiar borrador</button></div>
    {currentPreparation && compiled !== null && copiedText === compiled && <p role="status">Borrador copiado. El original se conserva.</p>}
    {copyReceipt?.snapshot === copySnapshot && copyReceipt.copyContent === 'TEMPLATE' && <p role="status">Plantilla copiada con sus marcadores. El original se conserva.</p>}
    {error && <p className="error" role="alert">{error}</p>}
  </div>;
}
