import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { AccountConnection, AccountService, DeviceReview } from '../../services/accounts';
import { Dialog } from '../../components/ui/Dialog';
import { useAccountOperation } from './useAccountOperation';
import { companionPresenceLabel } from '../../services/companionPresence';
import { useCompanionPresenceClock } from './useCompanionPresenceClock';

const profileLabels: Record<string, string> = { 'mcp-library-read': 'MCP · lectura y compilación', 'mcp-library-import': 'MCP · lectura e importación',
  'ollama-qwen3-8b': 'Equipo local · ejecutar instrucciones', 'ollama-qwen3-8b-proposals': 'Equipo local · propuestas de NOVA' };
const scopeLabels: Record<string, string> = { 'prompts:read': 'Leer y completar variables', 'shares:import': 'Importar versiones aceptadas', 'prompts:create': 'Guardar prompts nuevos', 'collections:import': 'Leer e importar colecciones aceptadas' };

/** Explicit consent only. Authorization is distinct from a running model or completed job. */
export function AccountConnections({ service, onExpired }: { service: AccountService; onExpired: () => void }) {
  const [connections, setConnections] = useState<AccountConnection[]>();
  const presenceClock = useCompanionPresenceClock(connections);
  const [code, setCode] = useState(() => new URLSearchParams(window.location.search).get('user_code') ?? '');
  const [review, setReview] = useState<DeviceReview>();
  const [revoke, setRevoke] = useState<AccountConnection>();
  const scope = useMemo(() => ({ service }), [service]);
  const operation = useAccountOperation(scope, onExpired);
  const connectionReads = useRef(0);
  const readConnections = useCallback((signal: AbortSignal) => {
    connectionReads.current++;
    return service.connections(signal);
  }, [service]);
  useEffect(() => {
    const controller = new AbortController();
    const request = ++connectionReads.current;
    setConnections(undefined);
    service.connections(controller.signal).then(items => { if (!controller.signal.aborted && request === connectionReads.current) setConnections(items); })
      .catch(error => { if (!controller.signal.aborted && request === connectionReads.current) operation.fail(error); });
    return () => controller.abort();
  }, [service, operation.fail]);
  function decide(action: 'approve' | 'deny') {
    if (!review) return;
    void operation.run(async signal => { await service.decideDevice(code, action, signal); return readConnections(signal); }, items => {
      setConnections(items); setReview(undefined); setCode('');
      if (window.location.pathname === '/connect/verify') window.history.replaceState(null, '', '/');
    }, action === 'approve' ? 'Autorización guardada. Abre el componente local del perfil autorizado para procesar tus solicitudes con Qwen3:8b.' : 'Solicitud rechazada. No se otorgó acceso al equipo.');
  }
  return <section aria-labelledby="account-connections-title">
    <h2 id="account-connections-title">Conexiones y permisos</h2>
    <p>Autoriza un equipo mediante el código que muestre el componente de QuickPrompt. Revisa su nombre y permisos antes de aprobar.</p>
    <p className="accounts-stage-note">Mantén abierto el componente local del perfil que vayas a usar y Ollama con Qwen3:8b. Una autorización vigente permite conectar el equipo; no confirma que esté ejecutándose.</p>
    <form className="account-new-library" onSubmit={event => { event.preventDefault(); void operation.run(signal => service.reviewDevice(code.trim(), signal), setReview); }}>
      <div><label htmlFor="device-user-code">Código del equipo</label><input id="device-user-code" value={code} maxLength={80} autoComplete="off" spellCheck={false} required disabled={operation.busy}
        onChange={event => { setCode(event.target.value); setReview(undefined); operation.clear(); }} /></div>
      <button className="secondary" disabled={operation.busy || !code.trim()}>Revisar solicitud</button>
    </form>
    {operation.error && <p role="alert" className="error">{operation.error}</p>}
    {operation.notice && <p role="status">{operation.notice}</p>}
    {review && <div className="account-publication-source" role="region" aria-label="Permisos solicitados">
      <h3>{review.label}</h3><p>Modelo permitido: {review.model}</p><ul>{review.permissions.map(permission => <li key={permission}>{permission}</li>)}</ul>
      <p>Aprueba sólo si tú iniciaste esta solicitud. El código tiene una vigencia máxima de {review.expiresInMinutes} minutos.</p>
      <button className="primary" disabled={operation.busy} onClick={() => decide('approve')}>Aprobar este equipo</button>{' '}
      <button className="secondary" disabled={operation.busy} onClick={() => decide('deny')}>Rechazar solicitud</button>
    </div>}
    <h3>Conexiones autorizadas</h3>
    <button className="text-button" disabled={operation.busy} onClick={() => void operation.run(readConnections, setConnections)}>Actualizar conexiones</button>
    {!connections ? !operation.error && <p role="status">Cargando conexiones…</p> : connections.length === 0 ? <p>No tienes equipos autorizados.</p>
      : <ul className="account-library-list">{connections.map(item => <li key={item.id}><div><h3>{item.label}</h3><p>{item.active ? 'Autorización vigente' : item.revoked ? 'Revocada' : 'Vencida o invalidada'} · {item.scopes?.length ? item.scopes.map(scope => scopeLabels[scope] ?? scope).join(' · ') : profileLabels[item.profileId] ?? item.profileId}</p>{item.libraryId && <p>Biblioteca autorizada: {item.libraryId}</p>}<span>Vence: {new Date(item.expiresAt).toLocaleString()}</span></div>
        {(item.profileId === 'ollama-qwen3-8b' || item.profileId === 'ollama-qwen3-8b-proposals') && <p>{companionPresenceLabel(item, presenceClock.now, presenceClock.receivedAt)}. La señal no verifica la generación del modelo.</p>}
        {!item.revoked && <button className="secondary" disabled={operation.busy} onClick={() => setRevoke(item)}>Revocar autorización</button>}</li>)}</ul>}
    {revoke && <Dialog open title={`Revocar ${revoke.label}`} description="La conexión perderá acceso y no podrá renovar su credencial. Será necesario volver a autorizarla."
      onOpenChange={open => { if (!open && !operation.busy) setRevoke(undefined); }}>
      {operation.error && <p role="alert" className="error">{operation.error}</p>}
      <button className="secondary" disabled={operation.busy} onClick={() => setRevoke(undefined)}>Conservar autorización</button>{' '}
      <button className="danger" disabled={operation.busy} onClick={() => void operation.run(async signal => { await service.revokeConnection(revoke.id, signal); return readConnections(signal); }, items => { setConnections(items); setRevoke(undefined); }, 'Autorización revocada.')}>
        Confirmar revocación</button>
    </Dialog>}
  </section>;
}
