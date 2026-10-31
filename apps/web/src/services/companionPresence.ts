import type { AccountConnection } from './accounts';

/** Use only a freshly returned server view for this advisory preflight; the server still owns admission. */
export function companionObserved(connection: AccountConnection | undefined): boolean {
  return !!connection?.active && (connection.presence?.status === 'POLLING' || connection.presence?.status === 'WORKING');
}

/** Cosmetic expiry cannot revoke consent, grant a surface capability or stop an existing run. */
export const companionSnapshotLifetimeMs = 20_000;

export function companionPresenceDeadline(connection: AccountConnection, receivedAt: number): number | null {
  if (!connection.active || !['POLLING', 'WORKING'].includes(connection.presence?.status ?? '')) return null;
  const expiresAt = Date.parse(connection.presence?.expiresAt ?? '');
  return Number.isFinite(expiresAt) ? Math.min(expiresAt, receivedAt + companionSnapshotLifetimeMs) : receivedAt;
}

export function companionPresenceLabel(connection: AccountConnection, now = Date.now(), receivedAt = now): string {
  const deadline = companionPresenceDeadline(connection, receivedAt);
  const status = !connection.active ? 'NOT_OBSERVED' : deadline !== null && deadline <= now ? 'STALE' : connection.presence?.status;
  switch (status) {
    case 'POLLING': return 'Consultando trabajos';
    case 'WORKING': return 'Procesando un trabajo';
    case 'STOPPED': return 'Componente detenido';
    case 'STALE': return 'Sin señal reciente';
    default: return 'Disponibilidad no observada';
  }
}

export const companionUnavailable = 'No hay una señal reciente de este componente local. Ábrelo manualmente y vuelve a enviar la solicitud cuando consulte QuickPrompt. La autorización puede seguir vigente; la generación del modelo no está verificada.';
