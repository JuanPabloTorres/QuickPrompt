import { AppIcon } from '../../components/ui/Icon';
import { useEffect, useId, useRef, useState, type ReactNode } from 'react';
import { m, useReducedMotionConfig } from 'motion/react';
import { Check, History, LoaderCircle, Pause, X } from 'lucide-react';
import { NovaLogo, type NovaVisualState } from '../../design/NovaLogo';
import { canvasMotion, panelMotion, stillCanvasMotion, stillPanelMotion } from '../../design/motion';
import { liveLabels, terminalLive } from '../../services/live';

export function AgentDock({ children, actions, context, hasActivity = false, suspended = false }: { children: ReactNode; actions: ReactNode; context: string; hasActivity?: boolean; suspended?: boolean }) {
  const reduced = useReducedMotionConfig();
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
      if (open.current && focus) (root.current?.querySelector<HTMLTextAreaElement>('.nova-input textarea') ?? root.current?.querySelector<HTMLButtonElement>('.quick-action'))?.focus();
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
    if (expanded && focusRequested.current) { focusRequested.current = false; (root.current?.querySelector<HTMLTextAreaElement>('.nova-input textarea') ?? root.current?.querySelector<HTMLButtonElement>('.quick-action'))?.focus(); }
  }, [expanded]);
  useEffect(() => {
    if (!expanded) return;
    // On compact screens, uncover a focused workspace control without pausing
    // execution or taking focus from that control. Ordinary outside interaction
    // leaves the assistant open when it does not obscure the focused element.
    const uncover = (target: EventTarget | null) => {
      if (!(target instanceof HTMLElement) || root.current?.contains(target) || target.closest('[role="dialog"], .nova-live')) return;
      const panel = root.current?.querySelector<HTMLElement>('.nova-dock-panel')?.getBoundingClientRect();
      const field = target.getBoundingClientRect();
      if (panel && field.width > 0 && field.height > 0 && field.left < panel.right && field.right > panel.left && field.top < panel.bottom && field.bottom > panel.top) setExpanded(false);
    };
    const focused = (event: FocusEvent) => uncover(event.target);
    let frame = 0;
    const layout = () => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => uncover(document.activeElement));
    };
    // Focus can already be on a field when the panel opens, the viewport changes,
    // or the document scrolls. Recheck on those events without polling the agent.
    layout();
    document.addEventListener('focusin', focused, true);
    window.addEventListener('resize', layout);
    document.addEventListener('scroll', layout, { capture: true, passive: true });
    return () => {
      cancelAnimationFrame(frame);
      document.removeEventListener('focusin', focused, true);
      window.removeEventListener('resize', layout);
      document.removeEventListener('scroll', layout, true);
    };
  }, [expanded]);
  function close() { setExpanded(false); trigger.current?.focus(); }
  const working = !!status && !terminalLive(status), paused = status === 'PAUSED', review = status === 'AWAITING_APPROVAL';
  const statusText = status ? liveLabels[status as keyof typeof liveLabels] ?? 'NOVA está trabajando' : 'Tu asistente de prompts';
  useEffect(() => {
    const node = root.current; if (!node || typeof ResizeObserver === 'undefined') return;
    const style = document.documentElement.style, previous = style.getPropertyValue('--nova-dock-height');
    const resize = () => style.setProperty('--nova-dock-height', `${Math.ceil(node.getBoundingClientRect().height)}px`);
    const observer = new ResizeObserver(resize); observer.observe(node); resize();
    return () => { observer.disconnect(); if (previous) style.setProperty('--nova-dock-height', previous); else style.removeProperty('--nova-dock-height'); };
  }, []);
  return <m.aside hidden={suspended} ref={root} className="nova-dock" data-testid="nova-dock" data-expanded={expanded} data-nova-command="true" aria-label="Asistente NOVA" variants={reduced ? stillCanvasMotion : canvasMotion} initial="hidden" animate="visible" onKeyDown={event => {
    if (event.key !== 'Escape' || event.defaultPrevented || !expanded) return;
    if ((event.target as HTMLElement).closest('[role="dialog"]') || root.current?.querySelector('.quick-more-trigger[aria-expanded="true"]')) return;
    if (root.current?.querySelector('.nova-live-trigger[aria-expanded="true"]')) {
      event.preventDefault(); event.stopPropagation(); window.dispatchEvent(new Event('nova-close-activity')); return;
    }
    event.preventDefault(); event.stopPropagation(); close();
  }}>
    <m.section className="nova-dock-panel" id={panelId} aria-label="Panel de NOVA" hidden={!expanded} initial={false} variants={reduced ? stillPanelMotion : panelMotion} animate={expanded ? 'open' : 'closed'}>
      <div className="nova-dock-heading"><span><NovaLogo state={visual} /><strong>NOVA</strong><span className="nova-dock-context" title={context}>{context}</span></span><div className="nova-dock-tools">{hasActivity && <button type="button" className="icon-button" data-nova-history="true" aria-label="Ver última actividad de NOVA" title="Ver última actividad de NOVA" onClick={() => window.dispatchEvent(new Event('nova-open-activity'))}><AppIcon glyph={History} size={20} /></button>}<button type="button" className="icon-button" aria-label="Cerrar panel de NOVA" onClick={close}><AppIcon glyph={X} size={20} /></button></div></div>
      <p className="nova-dock-help">Elige una acción y después indica los detalles.</p>
      {actions}{children}
      <div className="nova-dock-live" data-nova-dock-live-host="true" />
    </m.section>
    <button ref={trigger} type="button" className="nova-dock-orb" data-working={working} aria-label={expanded ? 'Cerrar asistente NOVA' : 'Abrir asistente NOVA'} title={`NOVA · ${statusText}`} aria-describedby={statusId} aria-expanded={expanded} aria-controls={panelId} onClick={() => { focusRequested.current = !expanded; if (expanded) close(); else setExpanded(true); }}>
      <NovaLogo size={32} state={visual} />
      {working && <span className="nova-dock-badge">{paused ? <AppIcon glyph={Pause} size={16} aria-hidden="true"  /> : review ? <AppIcon glyph={Check} size={16} aria-hidden="true"  /> : <AppIcon glyph={LoaderCircle} size={16} className="nova-working" aria-hidden="true"  />}</span>}
    </button>
    <span id={statusId} className="sr-only" role="status">{statusText}</span>
  </m.aside>;
}
