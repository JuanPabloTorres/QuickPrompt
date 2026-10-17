import { useEffect, useState } from 'react';
import type { CursorTarget } from './AgentUiDriver';

export function NovaAgentCursor({ target }: { target: CursorTarget | null }) {
  const [rect, setRect] = useState<DOMRect | null>(null);
  useEffect(() => {
    if (!target) { setRect(null); return; }
    let frame = 0;
    const update = () => { const next = target.element.isConnected ? target.element.getBoundingClientRect() : null; setRect(previous => previous && next && previous.top === next.top && previous.left === next.left && previous.width === next.width && previous.height === next.height ? previous : next); frame = requestAnimationFrame(update); };
    update(); return () => cancelAnimationFrame(frame);
  }, [target]);
  if (!target || !rect) return null;
  return <div className={`nova-agent-target nova-agent-${target.operation}`} data-testid="nova-agent-cursor" data-operation={target.operation} aria-hidden="true" style={{ top: rect.top, left: rect.left, width: rect.width, height: rect.height }}><span className="nova-agent-pointer">✦ <span>NOVA{target.operation === 'type' ? ' · escribiendo' : ''}</span></span></div>;
}
