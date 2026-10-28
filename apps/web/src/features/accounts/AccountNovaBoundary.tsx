import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { domAnimation, LazyMotion, MotionConfig } from 'motion/react';
import { z } from 'zod';
import { AgentRuntimeServiceProvider } from '../../components/ServiceContext';
import { useUiPortalScope } from '../../components/ui/UiPortalScope';
import type { AccountConnection, AccountPrompt, AccountService } from '../../services/accounts';
import { ApiError } from '../../services/api';
import { terminalLive, type LiveCommand } from '../../services/live';
import { AgentControl } from '../nova/AgentControl';
import { AgentUiProvider } from '../nova/AgentUiContext';
import { clearActivity } from '../nova/activity';
import { NovaLive, type NovaRuntimeSnapshot } from '../nova/NovaLive';
import { SameTabAgent } from '../nova/SameTabAgent';
import { useAccountOperation } from './useAccountOperation';

type Services = ReturnType<AccountService['nova']>;
export type AccountAgentBinding = {
  configuration: ReactNode;
  services: Services | null;
  runId: string | null;
  runtime: NovaRuntimeSnapshot | null;
  controlled: boolean;
  busy: boolean;
  begin(message: string, command: LiveCommand, source?: AccountPrompt): Promise<void>;
};

/** Lifecycle/composition only. Native execution, UI writes and domain approvals keep their existing owners. */
export function AccountNovaBoundary({ accountId, libraryId, service, onExpired, children }: {
  accountId: string; libraryId: string; service: AccountService; onExpired: () => void;
  children: (agent: AccountAgentBinding) => ReactNode;
}) {
  const expiration = useRef(onExpired); expiration.current = onExpired;
  const expire = useCallback(() => expiration.current(), []);
  const [connections, setConnections] = useState<AccountConnection[]>();
  const [connectionId, setConnectionId] = useState('');
  const [reload, setReload] = useState(0);
  const available = !!connections?.some(item => item.id === connectionId);
  const services = useMemo(() => available ? service.nova(accountId, libraryId, connectionId, expire) : null,
    [accountId, libraryId, connectionId, service, expire, available]);
  const scope = useMemo(() => ({ accountId, libraryId, services }), [accountId, libraryId, services]);
  const operation = useAccountOperation(scope, expire);
  const portal = useUiPortalScope();
  const [task, setTask] = useState<{ services: Services; id: string; uncertain?: boolean }>();
  const [snapshot, setSnapshot] = useState<{ services: Services; value: NovaRuntimeSnapshot | null }>();
  const [driver, setDriver] = useState<{ services: Services; id: string | null; active: boolean }>();
  const [inputRequest, setInputRequest] = useState<{ services: Services; id: string; message: string; command: LiveCommand; source?: AccountPrompt }>();
  const [restoring, setRestoring] = useState(false);
  const currentServices = useRef(services); currentServices.current = services;
  const current = task?.services === services ? task : undefined;
  const runtime = snapshot?.services === services && snapshot.value?.view.run.runId === current?.id ? snapshot.value : null;
  const runId = current?.id ?? null;
  const running = !!runId && (!runtime || !terminalLive(runtime.view.run.status));
  const hasSurface = !!runId && !!services?.visibleSession.surfaceCapability(runId);
  const controlled = hasSurface && running && driver?.services === services && driver.id === runId && driver.active;
  const executionId = current?.uncertain ? null : runId;
  const owned = useRef(new Map<Services, Set<string>>());
  const remember = useCallback((ports: Services, id: string) => {
    const runs = owned.current.get(ports) ?? new Set<string>(); runs.add(id); owned.current.set(ports, runs);
    while (runs.size > 3) { const oldest = runs.values().next().value!; ports.visibleSession.releaseSurface(oldest); clearActivity(oldest); runs.delete(oldest); }
  }, []);
  useEffect(() => {
    const controller = new AbortController();
    setConnections(undefined);
    service.connections(controller.signal).then(items => {
      if (!controller.signal.aborted) setConnections(items.filter(item => item.active && item.profileId === 'ollama-qwen3-8b-proposals'));
    }).catch(error => { if (!controller.signal.aborted) operation.fail(error); });
    return () => controller.abort();
  }, [accountId, libraryId, service, reload, operation.fail]);
  useEffect(() => {
    setTask(undefined); setSnapshot(undefined); setDriver(undefined); setInputRequest(undefined); setRestoring(false);
    if (!services) return;
    const controller = new AbortController();
    const timer = window.setTimeout(() => controller.abort(), 4000);
    let disposed = false;
    let stored: string | null = null;
    try { stored = sessionStorage.getItem(services.storageKey); } catch { /* Metadata recovery is optional. */ }
    if (stored && z.string().uuid().safeParse(stored).success) {
      const id = stored; setRestoring(true);
      void services.live.record(id, controller.signal).then(({ run }) => {
        if (!disposed && run.runId === id) { remember(services, id); setTask({ services, id }); }
      }).catch(failure => {
        if (disposed) return;
        if (failure instanceof ApiError && failure.status === 404) {
          try { sessionStorage.removeItem(services.storageKey); } catch { /* No content is stored here. */ }
        } else {
          remember(services, id); setTask({ services, id }); operation.fail(failure);
        }
      }).finally(() => { window.clearTimeout(timer); if (!disposed) setRestoring(false); });
    }
    return () => {
      disposed = true; window.clearTimeout(timer); controller.abort();
      for (const id of owned.current.get(services) ?? []) { services.visibleSession.releaseSurface(id); clearActivity(id); }
      owned.current.delete(services);
    };
  }, [services, remember, operation.fail]);
  useEffect(() => {
    if (!services) return;
    const uncertain = (event: Event) => {
      const id = (event as CustomEvent<string>).detail;
      if (z.string().uuid().safeParse(id).success && services.visibleSession.surfaceCapability(id)) {
        remember(services, id); setTask({ services, id, uncertain: true });
      }
    };
    window.addEventListener('nova-uncertain-start', uncertain);
    return () => window.removeEventListener('nova-uncertain-start', uncertain);
  }, [services, remember]);
  const changed = useCallback((value: NovaRuntimeSnapshot | null) => {
    if (services && currentServices.current === services) {
      setSnapshot({ services, value });
      if (value && current?.uncertain && value.view.run.runId === current.id) {
        const pending = services.visibleSession.pendingClarification?.(current.id);
        if (pending) setInputRequest(previous => previous?.services === services && previous.id === pending.previousId ? { ...previous, id: current.id } : previous);
        operation.clear(); setTask(previous => previous?.id === value.view.run.runId ? { ...previous, uncertain: false } : previous);
      }
    }
  }, [services, current?.id, current?.uncertain, operation.clear]);
  const activeChanged = useCallback((active: boolean) => {
    if (services && currentServices.current === services) setDriver(previous => previous?.services === services && previous.id === executionId && previous.active === active ? previous : { services, id: executionId, active });
  }, [services, executionId]);
  const start = useCallback(async (message: string, command: LiveCommand, source?: AccountPrompt) => {
    if (!services || running || restoring) return false;
    let accepted = false;
    await operation.run(() => services.visibleSession.startVisible(message, crypto.randomUUID(), source, undefined,
      { bindSource: !!source, command }), run => {
        accepted = true;
        setInputRequest({ services, id: run.runId, message, command: { ...command }, source });
        remember(services, run.runId); setSnapshot(undefined); setTask({ services, id: run.runId });
      });
    return accepted;
  }, [services, running, restoring, operation.run, remember]);
  const begin = useCallback(async (message: string, command: LiveCommand, source?: AccountPrompt) => { await start(message, command, source); }, [start]);
  // Only this tab's accepted request can continue a field question. Recovery stores
  // a run ID, never its content, original command or permission to restart it.
  const definition = inputRequest?.services === services && inputRequest.id === runId ? inputRequest : undefined;
  const pendingReply = current?.uncertain && runId ? services?.visibleSession.pendingClarification?.(runId) : undefined;
  const pendingDefinition = pendingReply && inputRequest?.services === services && inputRequest.id === pendingReply.previousId ? inputRequest : undefined;
  const retryContentClarification = () => {
    if (!services || !runId || !pendingDefinition || !pendingReply || currentServices.current !== services || portal?.active === false || operation.busy || !services.visibleSession.retryClarification) return;
    void operation.run(() => services.visibleSession.retryClarification!(runId), run => {
      remember(services, run.runId); setSnapshot(undefined); setTask({ services, id: run.runId });
      setInputRequest({ ...pendingDefinition, id: run.runId });
    });
  };
  const stopRecovered = () => {
    if (!services || !runId) return;
    void operation.run(signal => services.live.control(runId, 'cancel', signal), record => {
      if (!terminalLive(record.status)) throw new Error('No se confirmó la detención.');
      services.visibleSession.releaseSurface(runId);
      setTask(undefined); setSnapshot(undefined);
      try { sessionStorage.removeItem(services.storageKey); } catch { /* No content is stored here. */ }
    });
  };
  const configuration = <section className="account-nova-configuration" data-nova-command="true" aria-label="Equipo autorizado para NOVA">
      <div className="account-section-heading"><div><span className="eyebrow">NOVA PRIVADA</span><h3>Tu agente, en tu biblioteca</h3></div>
        <button type="button" className="secondary" disabled={running || operation.busy} onClick={() => setReload(value => value + 1)}>Actualizar equipos NOVA</button></div>
      <label htmlFor="account-nova-connection">Equipo de NOVA</label><select id="account-nova-connection" value={connectionId} disabled={running || operation.busy || restoring} onChange={event => setConnectionId(event.target.value)}>
        <option value="">Elige un equipo autorizado</option>{connections?.map(item => <option key={item.id} value={item.id}>{item.label} · Qwen3:8b</option>)}
      </select>
      <p className="muted">Mantén abierto el componente local de NOVA en el equipo elegido para que pueda procesar el contenido con Qwen3:8b.</p>
      {!connections ? <p role="status">Consultando equipos…</p> : connections.length === 0 && <p>Vincula un equipo con la capacidad NOVA desde Conexiones locales. La biblioteca manual sigue disponible.</p>}
      {restoring && <p role="status">Comprobando la última ejecución de este equipo…</p>}
      {operation.error && <p role="alert" className="error">{operation.error}</p>}
    </section>;
  return <MotionConfig reducedMotion="user"><LazyMotion features={domAnimation}><AgentUiProvider>
    {children({ configuration, services, runId, runtime, controlled, busy: operation.busy || running || restoring, begin })}
    {runId && (!running || !hasSurface || current?.uncertain) && <button type="button" className="secondary" data-nova-history data-nova-user-control="true" onClick={() => window.dispatchEvent(new Event('nova-open-activity'))}>Ver última actividad de NOVA</button>}
      {running && (!hasSurface || current?.uncertain) && <div data-nova-user-control="true"><p>{current?.uncertain ? 'El inicio sigue sin confirmarse. Se conserva la misma ejecución para no repetirla.' : 'Esta pestaña no recupera el control anterior ni sus aprobaciones.'}</p>
        {pendingReply && pendingDefinition && <section hidden={portal?.active === false} aria-label="Aclaración pendiente de confirmación">
          <p>El servidor aún no confirmó esta aclaración. Puedes reenviar exactamente la misma solicitud, sin editarla ni crear otra ejecución.</p>
          <details><summary>Revisar el encargo y tu respuesta</summary><p>Encargo original</p><pre className="account-memory-preview">{pendingDefinition.message}</pre><p>Tu respuesta</p><pre className="account-memory-preview">{pendingReply.answer}</pre></details>
          <button type="button" className="secondary" disabled={operation.busy} onClick={retryContentClarification}>Reenviar esta misma aclaración</button>
        </section>}
        {runtime?.view.proposal && <details><summary>Ver borrador pendiente, sin permisos de guardado</summary><pre className="account-memory-preview">{runtime.view.proposal.draft.templateText}</pre></details>}
        <button type="button" className="secondary" disabled={operation.busy} onClick={stopRecovered}>{current?.uncertain ? 'Detener solicitud sin confirmar' : 'Detener ejecución guardada'}</button></div>}
    {services && <AgentRuntimeServiceProvider services={services}>
      <SameTabAgent key={`driver:${connectionId}`} runId={executionId} onActive={activeChanged} />
      <NovaLive key={`activity:${connectionId}`} runId={runId} worker={!!runId && !!services.visibleSession.surfaceCapability(runId)} centralControl onReview={() => {}} onRuntimeChange={changed}
        contentClarification={definition && !!services.visibleSession.clarificationCapability?.(definition.id) && (definition.command.operation === 'CREATE_DRAFT' || definition.command.operation === 'PROPOSE_ADAPTATION') ? {
          identity: services, confirm: async (answer, sequence) => {
            if (currentServices.current !== services || !services.visibleSession.clarifyVisible || definition.id !== runId || running || restoring)
              throw new Error('Esta pestaña ya no conserva el encargo de esa pregunta.');
            let accepted = false;
            await operation.run(() => services.visibleSession.clarifyVisible!(definition.id, sequence, answer), run => {
              accepted = true; remember(services, run.runId); setSnapshot(undefined); setTask({ services, id: run.runId });
              setInputRequest({ ...definition, id: run.runId });
            });
            if (!accepted) throw new Error('No se confirmó la continuación. Revisa la actividad y la conexión antes de reintentar.');
          },
        } : undefined}
        inputDefinition={definition ? { updating: definition.command.operation === 'PROPOSE_ADAPTATION', confirm: async names => {
          if (!await start(definition.message, { ...definition.command, inputNames: names }, definition.source)) throw new Error('No se confirmó el inicio. Conserva estos nombres y revisa la conexión antes de reintentar.');
        } } : undefined} />
      <AgentControl runId={controlled ? runId : null} runtime={runtime} />
    </AgentRuntimeServiceProvider>}
  </AgentUiProvider></LazyMotion></MotionConfig>;
}
