import { AppIcon } from '../../components/ui/Icon';
import { useEffect, useId, useRef, useState, type ReactNode } from 'react';
import { Check, ChevronDown, ChevronUp, History, LoaderCircle, Pause } from 'lucide-react';
import { NovaLogo, type NovaVisualState } from '../../design/NovaLogo';
import { liveLabels, terminalLive } from '../../services/live';

function focusCommand(root: HTMLElement | null) {
  (root?.querySelector<HTMLElement>('.nova-input textarea') ?? root?.querySelector<HTMLElement>('.quick-action')
    ?? root?.querySelector<HTMLElement>('[data-nova-command] select:not(:disabled), [data-nova-command] textarea:not(:disabled)'))?.focus();
}

export function AgentDock({ children, actions, context, hasActivity = false, suspended = false }: { children: ReactNode; actions: ReactNode; context: string; hasActivity?: boolean; suspended?: boolean }) {
  const [expanded, setExpanded] = useState(false);
  const [status, setStatus] = useState('');
  const [visual, setVisual] = useState<NovaVisualState>('idle');
  const root = useRef<HTMLElement>(null);
  const suspendedRef = useRef(suspended); suspendedRef.current = suspended;
  useEffect(() => { if (suspended) { focusRequested.current = false; setExpanded(false); } }, [suspended]);
  const trigger = useRef<HTMLButtonElement>(null), focusRequested = useRef(false), open = useRef(expanded);
  open.current = expanded;
  const panelId = useId(), statusId = useId();
  useEffect(() => {
    const reveal = (event: Event) => {
      if (suspendedRef.current) return;
      const focus = (event as CustomEvent<{ focus?: boolean }>).detail?.focus !== false;
      if (open.current && focus) focusCommand(root.current);
      else { focusRequested.current = focus; setExpanded(true); }
    };
    const activity = (event: Event) => setStatus((event as CustomEvent<string>).detail);
    const presentation = (event: Event) => setVisual((event as CustomEvent<NovaVisualState>).detail);
    window.addEventListener('nova-open-assistant', reveal); window.addEventListener('nova-agent-status', activity);
    window.addEventListener('nova-visual-state', presentation);
    return () => { window.removeEventListener('nova-open-assistant', reveal); window.removeEventListener('nova-agent-status', activity); window.removeEventListener('nova-visual-state', presentation); };
  }, []);
  useEffect(() => {
    window.dispatchEvent(new CustomEvent('nova-assistant-visibility', { detail: expanded }));
    if (expanded && focusRequested.current) { focusRequested.current = false; focusCommand(root.current); }
  }, [expanded]);
  function close() { setExpanded(false); trigger.current?.focus(); }
  const working = !!status && !terminalLive(status), paused = status === 'PAUSED', review = status === 'AWAITING_APPROVAL';
  const statusText = status ? liveLabels[status as keyof typeof liveLabels] ?? 'NOVA está trabajando' : 'Tu asistente de prompts';
  return <aside hidden={suspended} ref={root} className="nova-dock" data-testid="nova-dock" data-expanded={expanded} data-nova-command="true" aria-label="Asistente NOVA" onKeyDown={event => {
    if (event.key !== 'Escape' || event.defaultPrevented || !expanded) return;
    if ((event.target as HTMLElement).closest('[role="dialog"]') || root.current?.querySelector('.quick-more-trigger[aria-expanded="true"]')) return;
    if (root.current?.querySelector('.nova-live-trigger[aria-expanded="true"]')) {
      event.preventDefault(); event.stopPropagation(); window.dispatchEvent(new Event('nova-close-activity')); return;
    }
    event.preventDefault(); event.stopPropagation(); close();
  }}>
    <div className="nova-dock-heading">
      <button ref={trigger} type="button" className="nova-dock-toggle" data-working={working} aria-label={expanded ? 'Cerrar panel de NOVA' : 'Abrir asistente NOVA'} title={`NOVA · ${statusText}`} aria-describedby={statusId} aria-expanded={expanded} aria-controls={panelId} onClick={() => { if (expanded) close(); else { focusRequested.current = true; setExpanded(true); } }}>
        <NovaLogo size={24} state={visual} />
        {working && <AppIcon glyph={paused ? Pause : review ? Check : LoaderCircle} size={16} className={paused || review ? undefined : 'nova-working'} />}
        <AppIcon glyph={expanded ? ChevronUp : ChevronDown} size={16} />
      </button>
      <span className="nova-dock-context" title={context}><strong>NOVA</strong> · {context}</span>
      {expanded && hasActivity && <button type="button" className="icon-button" data-nova-history="true" aria-label="Ver última actividad de NOVA" title="Ver última actividad de NOVA" onClick={() => window.dispatchEvent(new Event('nova-open-activity'))}><AppIcon glyph={History} /></button>}
    </div>
    <section className="nova-dock-panel" id={panelId} aria-label="Panel de NOVA" hidden={!expanded}>
      {actions}{children}
      <div className="nova-dock-live" data-nova-dock-live-host="true" />
    </section>
    <span id={statusId} className="sr-only" role="status">{statusText}</span>
  </aside>;
}
