import { AppIcon } from './Icon';
import * as Radix from '@radix-ui/react-dialog';
import { X } from 'lucide-react';
import { useEffect, useRef, type ReactNode } from 'react';
import { useUiPortalScope } from './UiPortalScope';

export function Dialog({ open, onOpenChange, title, description, children, className, onOpenAutoFocus, onCloseAutoFocus }: {
  open: boolean; onOpenChange: (open: boolean) => void; title: string; description: string; children: ReactNode;
  className?: string; onOpenAutoFocus?: (event: Event) => void; onCloseAutoFocus?: (event: Event) => void;
}) {
  const scope = useUiPortalScope();
  const suspended = scope?.active === false;
  const opener = useRef<HTMLElement | null>(null);
  useEffect(() => {
    if (!open) return;
    const previous = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => { document.body.style.overflow = previous; };
  }, [open]);
  return <Radix.Root open={open} onOpenChange={next => { if (!suspended) onOpenChange(next); }}>
    <Radix.Portal container={scope?.dialogHost ?? undefined}>
      {open && <div className="dialog-overlay" aria-hidden="true" />}
      <Radix.Content data-nova-activity-host="true" className={className ? `dialog-content ${className}` : 'dialog-content'} onOpenAutoFocus={event => { opener.current = document.activeElement instanceof HTMLElement ? document.activeElement : null; onOpenAutoFocus?.(event); }} onCloseAutoFocus={event => { onCloseAutoFocus?.(event); if (!event.defaultPrevented && opener.current?.isConnected) { event.preventDefault(); opener.current.focus(); } }}>
        <Radix.Title>{suspended ? 'Comprobando sesión' : title}</Radix.Title>
        <Radix.Description className="muted">{suspended ? 'El contenido está oculto hasta confirmar tu acceso. Tu trabajo permanece en pausa.' : description}</Radix.Description>
        {scope ? <div hidden={suspended} style={suspended ? undefined : { display: 'contents' }}>{children}</div> : children}
        {suspended && <button type="button" className="secondary" data-nova-user-control="true" onClick={scope.onRetry}>Volver a comprobar acceso</button>}
        <Radix.Close disabled={suspended} data-nova-control="dialog.close" className="icon-button dialog-close" aria-label="Cerrar diálogo"><AppIcon glyph={X} size={20} aria-hidden="true"  /></Radix.Close>
      </Radix.Content>
    </Radix.Portal>
  </Radix.Root>;
}
