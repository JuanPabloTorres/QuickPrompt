import { useEffect, useMemo, useRef, useState } from 'react';
import { Dialog } from '../../components/ui/Dialog';
import type { AccountConnection, AccountExecution, AccountExecutionResult, AccountPrompt, AccountService } from '../../services/accounts';
import { downloadPreparedText } from '../../services/textExport';
import { PromptVariableFields } from '../prompts/PromptVariableFields';
import { SavedText, historyDate } from '../prompts/HistoryResult';
import { useAccountOperation } from './useAccountOperation';
import { useAccountPreparedCopy } from './useAccountPreparedCopy';

const labels: Record<AccountExecution['status'], string> = { Queued: 'En cola', Running: 'Preparando', Completed: 'Resultado guardado', Failed: 'No se pudo preparar', Cancelled: 'Cancelada', Interrupted: 'Interrumpida; no se repetirá sola' };
const active = (job: AccountExecution) => job.status === 'Queued' || job.status === 'Running';
const localIssues: Record<string, string> = { MODEL_UNAVAILABLE: 'Ollama no está disponible en tu equipo. Ábrelo antes de solicitar otra ejecución.',
  MODEL_TIMEOUT: 'El modelo superó el tiempo permitido. La ejecución se detuvo.', MODEL_FAILED: 'El modelo no entregó una respuesta válida.',
  CONNECTION_REVOKED: 'Se revocó la autorización de este equipo.', SESSION_REVOKED: 'Tu sesión fue invalidada. Vuelve a autorizar el equipo.',
  LEASE_EXPIRED: 'Se perdió la conexión con el equipo. No se repetirá la ejecución automáticamente.',
  QUEUE_EXPIRED: 'El equipo no recogió la solicitud a tiempo.', HOST_STOPPED: 'El componente local se detuvo.' };

/** Explicit preparation or local execution. The server owns compilation, destination authority and quota. */
export function UseAccountPromptDialog({ prompt, service, onExpired, onSubmitted, onClose, initialValues }: { prompt: AccountPrompt; service: AccountService; onExpired: () => void; onSubmitted: (job: AccountExecution) => void; onClose: () => void; initialValues?: Record<string, string> }) {
  const [names, setNames] = useState<string[]>();
  const [values, setValues] = useState<Record<string, string>>(() => ({ ...initialValues }));
  const [inspectRefresh, setInspectRefresh] = useState(0);
  const [connections, setConnections] = useState<AccountConnection[]>();
  const [target, setTarget] = useState('');
  const [localError, setLocalError] = useState('');
  const receipt = useRef<{ fingerprint: string; id: string } | null>(null);
  const scope = useMemo(() => ({ prompt, service }), [prompt, service]);
  const operation = useAccountOperation(scope, onExpired);
  useEffect(() => {
    const controller = new AbortController();
    service.options(controller.signal).then(async options => {
      if (!options.delegation) return;
      const items = await service.connections(controller.signal);
      if (!controller.signal.aborted) setConnections(items.filter(item => item.active && item.profileId === 'ollama-qwen3-8b'));
    }).catch(() => { if (!controller.signal.aborted) setLocalError('No se pudieron consultar los equipos locales. Puedes preparar la instrucción sin IA.'); });
    return () => controller.abort();
  }, [service]);
  useEffect(() => {
    const controller = new AbortController();
    service.renderPrompt(prompt, {}, controller.signal).then(result => {
      if (!controller.signal.aborted) setNames(result.missingVariables);
    }).catch(error => { if (!controller.signal.aborted) operation.fail(error); });
    return () => controller.abort();
  }, [prompt, service, operation.fail, inspectRefresh]);
  function submit() {
    const fingerprint = JSON.stringify({ values: Object.entries(values).sort(([a], [b]) => a.localeCompare(b)), target });
    if (receipt.current?.fingerprint !== fingerprint) receipt.current = { fingerprint, id: crypto.randomUUID() };
    const operationId = receipt.current.id;
    void operation.run(signal => target ? service.runLocalPrompt(prompt, values, operationId, target, signal) : service.compilePrompt(prompt, values, operationId, signal), job => {
      if (job.libraryId !== prompt.libraryId || job.kind !== (target ? 'RunLocalPrompt' : 'CompilePrompt')) throw new Error('La preparación no corresponde a esta biblioteca y destino.');
      onSubmitted(job);
    });
  }
  return <Dialog open onOpenChange={open => { if (!open) { operation.cancel(); onClose(); } }} title={`Usar ${prompt.title}`} description={target ? `Versión ${prompt.revision}. Enviará sólo esta instrucción al equipo elegido para ejecutarla con Qwen3:8b. El equipo debe estar abierto y conectado.` : `Versión ${prompt.revision}. Completa los datos y conserva una instrucción lista para copiar. Esta preparación no llama a un modelo.`}>
    <form className="account-prompt-form" onSubmit={event => { event.preventDefault(); submit(); }}>
      <fieldset disabled={operation.busy}><legend className="eyebrow">Datos de esta preparación</legend>
        {connections && <div><label htmlFor="account-use-target">Cómo usar la instrucción</label><select id="account-use-target" value={target} onChange={event => setTarget(event.target.value)}>
          <option value="">Preparar para copiar · sin IA</option>{connections.map(item => <option key={item.id} value={item.id}>Qwen3:8b en {item.label}</option>)}
        </select>{connections.length === 0 && <p>No hay equipos autorizados. Vincula uno desde Conexiones locales para ejecutar con IA.</p>}</div>}
        {localError && <p role="status">{localError}</p>}
        {names === undefined ? !operation.error && <p role="status">Consultando las variables…</p> : names.length ? <PromptVariableFields names={names} values={values} idPrefix="account-use-value" maxLength={8000} onValueChange={(name, value) => setValues(current => ({ ...current, [name]: value }))} /> : <p>Esta instrucción no necesita variables.</p>}
      </fieldset>
      {operation.error && <p role="alert" className="error">{operation.error} Tus datos permanecen aquí; puedes reintentar la misma solicitud.</p>}
      {names === undefined && operation.error && <button type="button" className="secondary" onClick={() => { operation.clear(); setInspectRefresh(value => value + 1); }}>Volver a consultar variables</button>}
      <div className="dialog-actions"><button className="secondary" type="button" onClick={onClose}>Cerrar</button><button className="primary" disabled={operation.busy || names === undefined || names.some(name => !values[name]?.trim())}>{target ? 'Ejecutar en mi equipo' : 'Preparar resultado'}</button></div>
    </form>
  </Dialog>;
}

/** Observe this library's real queue states; disconnect and scope changes discard the subscription. */
export function AccountExecutions({ libraryId, service, refresh, onExpired, onCatalogChanged, requestedId, onRequestedShown }: { libraryId: string; service: AccountService; refresh: number; onExpired: () => void; onCatalogChanged?: () => void; requestedId?: string; onRequestedShown?: () => void }) {
  const [snapshot, setSnapshot] = useState<{ scope: object; jobs: AccountExecution[] }>();
  const [reload, setReload] = useState(0);
  const [selected, setSelected] = useState<AccountExecution>();
  const [forgetting, setForgetting] = useState<AccountExecution>();
  const scope = useMemo(() => ({ libraryId, service }), [libraryId, service]);
  const catalogChanged = useRef(onCatalogChanged); catalogChanged.current = onCatalogChanged;
  const completedModels = useRef<{ scope: object; signature: string } | undefined>(undefined);
  const requestedShown = useRef<{ scope: object; id: string } | undefined>(undefined);
  const jobs = snapshot?.scope === scope ? snapshot.jobs : undefined;
  const operation = useAccountOperation(scope, onExpired);
  useEffect(() => {
    if (!requestedId) { requestedShown.current = undefined; return; }
    if (requestedShown.current?.scope === scope && requestedShown.current.id === requestedId) return;
    const requested = requestedId && jobs?.find(job => job.id === requestedId);
    if (!requested || active(requested)) return;
    requestedShown.current = { scope, id: requestedId };
    if (requested.status === 'Completed') setSelected(requested);
    onRequestedShown?.();
  }, [scope, jobs, requestedId, onRequestedShown]);
  useEffect(() => {
    const controller = new AbortController();
    operation.clear();
    void service.watchExecutions(libraryId, items => {
      if (controller.signal.aborted) return;
      if (items.some(item => item.libraryId !== libraryId)) throw new Error('La respuesta no corresponde a esta biblioteca.');
      setSnapshot({ scope, jobs: items });
      const signature = items.filter(item => item.kind === 'RunLocalPrompt' && item.status === 'Completed').map(item => item.id).sort().join(',');
      if (completedModels.current?.scope !== scope || completedModels.current.signature !== signature) {
        completedModels.current = { scope, signature };
        if (signature) catalogChanged.current?.();
      }
    }, controller.signal).catch(failure => { if (!controller.signal.aborted) operation.fail(failure); });
    return () => controller.abort();
  }, [scope, libraryId, service, refresh, reload, operation.clear, operation.fail]);
  return <section className="account-remembered" aria-label="Preparaciones recientes">
    <div className="account-section-heading"><div><span className="eyebrow">RESULTADOS PRIVADOS</span><h3>Preparaciones recientes</h3></div><button className="secondary" disabled={operation.busy} onClick={() => setReload(value => value + 1)}>Actualizar preparaciones</button></div>
    <p className="muted">Hasta 100 solicitudes de esta biblioteca, con el trabajo activo primero. Los datos enviados y resultados caducan siete días después de terminar; puedes olvidarlos antes. El recibo sin contenido se conserva para evitar repeticiones y mantener las cuotas.</p>
    {operation.error && forgetting?.libraryId !== libraryId && <p role="alert" className="error">{operation.error}</p>}
    {!jobs ? !operation.error && <p role="status">Cargando preparaciones…</p> : jobs.length ? <ul className="account-memory-list">{jobs.map(job => <li key={job.id}>
      <div><strong>{job.forgottenAt ? 'Contenido olvidado' : job.kind === 'RunLocalPrompt' && job.status === 'Running' ? 'Ejecutando en tu equipo' : labels[job.status]}</strong><span>{historyDate(job.createdAt)}</span>{job.cancelRequested && active(job) && <span role="status">Cancelación solicitada…</span>}
        {job.kind === 'RunLocalPrompt' && job.errorCode && <p className="muted">{localIssues[job.errorCode] ?? 'La ejecución terminó sin respuesta. Revisa tu equipo antes de volver a solicitarla.'}</p>}</div>
      {active(job) ? <button className="secondary" disabled={operation.busy || job.cancelRequested} onClick={() => { void operation.run(signal => service.cancelExecution(libraryId, job.id, signal), () => setReload(value => value + 1)); }}>Detener preparación</button>
        : !job.forgottenAt && <div className="account-actions">{job.status === 'Completed' && <button className="secondary" onClick={() => setSelected(job)}>Ver resultado</button>}
          <button className="text-button" disabled={operation.busy} onClick={() => setForgetting(job)} aria-label={`Olvidar contenido de ${historyDate(job.createdAt)}`}>Olvidar contenido</button></div>}
    </li>)}</ul> : <p>Aún no has preparado una instrucción en esta biblioteca.</p>}
    {selected?.libraryId === libraryId && <AccountExecutionResultDialog key={`${libraryId}:${selected.id}`} libraryId={libraryId} job={selected} service={service} onExpired={onExpired} onUsed={onCatalogChanged} onClose={() => setSelected(undefined)} />}
    {forgetting?.libraryId === libraryId && <Dialog open onOpenChange={open => { if (!open && !operation.busy) setForgetting(undefined); }} title="Olvidar contenido de la ejecución" description={`Se eliminarán los datos enviados, la instrucción preparada y la respuesta de la ejecución del ${historyDate(forgetting.createdAt)}. No se pueden recuperar desde QuickPrompt. Tus prompts, borradores y copias descargadas se conservan; el recibo evita repetir la ejecución.`}>
      <div className="dialog-actions"><button className="secondary" disabled={operation.busy} onClick={() => setForgetting(undefined)}>Conservar contenido</button>
        <button className="destructive" disabled={operation.busy} onClick={() => {
          void operation.run(signal => service.forgetExecution(libraryId, forgetting.id, signal), value => {
            if (value.id !== forgetting.id || value.libraryId !== libraryId || !value.forgottenAt) { operation.fail(new Error('El recibo no confirma el olvido de esta ejecución.')); return; }
            setForgetting(undefined); setSelected(undefined); setReload(current => current + 1);
          });
        }}>Olvidar definitivamente</button></div>
      {operation.error && <p role="alert" className="error">{operation.error} Puedes reintentar el mismo olvido.</p>}
    </Dialog>}
  </section>;
}

function AccountExecutionResultDialog({ libraryId, job, service, onExpired, onClose, onUsed }: { libraryId: string; job: AccountExecution; service: AccountService; onExpired: () => void; onClose: () => void; onUsed?: () => void }) {
  const scope = useMemo(() => ({ libraryId, id: job.id, service }), [libraryId, job.id, service]);
  const [loaded, setLoaded] = useState<{ scope: object; result: AccountExecutionResult }>();
  const result = loaded?.scope === scope ? loaded.result : undefined;
  const operation = useAccountOperation(scope, onExpired);
  const preparedCopy = useAccountPreparedCopy(libraryId, result, service, onExpired, onUsed);
  useEffect(() => {
    const controller = new AbortController();
    service.executionResult(libraryId, job.id, controller.signal).then(value => {
      if (controller.signal.aborted) return;
      if (value.executionId !== job.id) throw new Error('El resultado no corresponde a esta preparación.');
      setLoaded({ scope, result: value });
    }).catch(failure => { if (!controller.signal.aborted) { setLoaded(undefined); operation.fail(failure); } });
    return () => controller.abort();
  }, [scope, libraryId, job.id, service, operation.fail]);
  return <Dialog open onOpenChange={open => { if (!open) onClose(); }} title={result?.title ?? 'Resultado privado'} description={result ? result.response ? `Respuesta del equipo autorizado con ${result.model}. Instrucción de la versión ${result.promptRevision} conservada por separado.` : `Instrucción compilada de la versión ${result.promptRevision}. No es una respuesta generada por IA.` : 'Recuperando el resultado guardado.'}>
    <div className="account-share-content">
      {operation.error && <p role="alert" className="error">{operation.error}</p>}
      {operation.notice && <p role="status">{operation.notice}</p>}
      {preparedCopy.notice && <p role="status">{preparedCopy.notice}</p>}
      {preparedCopy.error && <p role="alert" className="error">{preparedCopy.error}{preparedCopy.pending ? ' La instrucción ya se copió; su uso no está confirmado.' : ' No se registró un uso.'}</p>}
      {preparedCopy.conflict && <p>La revisión ya no coincide o cambió su disponibilidad. El resultado se conserva; no se fuerza otra revisión ni se repite la copia.</p>}
      {preparedCopy.pending && !preparedCopy.conflict && <button type="button" className="secondary" disabled={preparedCopy.busy} onClick={preparedCopy.retry}>Reintentar sólo el registro de uso</button>}
      {result && <SavedText text={result.text} kind="instrucción" filename={`quickprompt-${result.executionId}.txt`}
        copyDisabled={operation.busy || preparedCopy.busy || preparedCopy.pending} onCopy={preparedCopy.copy}
        onDownload={(text, filename) => downloadPreparedText(text, filename)} />}
      {result?.response && <SavedText text={result.response} kind="respuesta" filename={`quickprompt-respuesta-${result.executionId}.txt`}
        copyDisabled={operation.busy || preparedCopy.busy}
        onCopy={text => { void operation.run(async () => navigator.clipboard.writeText(text), () => {}, 'Respuesta copiada.'); }}
        onDownload={(text, filename) => downloadPreparedText(text, filename)} />}
    </div>
  </Dialog>;
}
