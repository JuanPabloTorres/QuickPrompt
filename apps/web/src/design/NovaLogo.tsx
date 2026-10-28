import type { CSSProperties } from 'react';

export type NovaVisualState = 'idle' | 'searching' | 'thinking' | 'generating' | 'navigating' | 'validating' | 'ready' | 'error' | 'offline';

/** Official user-supplied artwork. State feedback surrounds it without recoloring it. */
export function NovaLogo({ size = 24, state = 'idle', label }: { size?: 16 | 24 | 32 | 64 | 128; state?: NovaVisualState; label?: string }) {
  return <span className="nova-logo" data-state={state} role={label ? 'img' : undefined} aria-label={label} aria-hidden={label ? undefined : true} style={{ '--nova-logo-size': `${size}px` } as CSSProperties}>
    <img src="/branding/quickprompt_official.png" alt="" width={size} height={size} draggable={false} decoding="async" />
  </span>;
}
