export type NovaState = 'idle' | 'listening' | 'thinking' | 'searching' | 'suggesting' | 'ready' | 'error' | 'offline';
const labels: Record<NovaState, string> = { idle: 'Tu espacio, a tu ritmo', listening: 'Te escucho', thinking: 'Preparando una propuesta', searching: 'Buscando en tu biblioteca', suggesting: 'Una propuesta para revisar', ready: 'Listo para usar', error: 'Podemos intentarlo de nuevo', offline: 'Biblioteca disponible · IA sin conexión' };
export function NovaCore({ state }: { state: NovaState }) {
  return <div className={`nova-presence nova-${state}`}>
    <svg className="nova-core" viewBox="0 0 80 80" aria-hidden="true">
      <circle className="nova-orbit" cx="40" cy="40" r="34" /><circle cx="40" cy="40" r="25" />
      <path d="M40 28v24M28 40h24" /><circle className="nova-heart" cx="40" cy="40" r="5" />
    </svg>
    <span className="eyebrow" role="status">NOVA <span className="muted">/ {labels[state]}</span></span>
  </div>;
}
