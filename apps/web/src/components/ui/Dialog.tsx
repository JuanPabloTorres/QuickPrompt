import * as Radix from '@radix-ui/react-dialog';
import { X } from 'lucide-react';
import { useEffect, type ReactNode } from 'react';

export function Dialog({ open, onOpenChange, title, description, children, className, onOpenAutoFocus, onCloseAutoFocus }: {
  open: boolean; onOpenChange: (open: boolean) => void; title: string; description: string; children: ReactNode;
  className?: string; onOpenAutoFocus?: (event: Event) => void; onCloseAutoFocus?: (event: Event) => void;
}) {
  useEffect(() => {
    if (!open) return;
    const previous = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => { document.body.style.overflow = previous; };
  }, [open]);
  return <Radix.Root open={open} onOpenChange={onOpenChange}>
    <Radix.Portal>
      {open && <div className="dialog-overlay" aria-hidden="true" />}
      <Radix.Content data-nova-activity-host="true" className={className ? `dialog-content ${className}` : 'dialog-content'} onOpenAutoFocus={onOpenAutoFocus} onCloseAutoFocus={onCloseAutoFocus}>
        <Radix.Title>{title}</Radix.Title>
        <Radix.Description className="muted">{description}</Radix.Description>
        {children}
        <Radix.Close className="icon-button dialog-close" aria-label="Cerrar diálogo"><X size={20} aria-hidden="true" /></Radix.Close>
      </Radix.Content>
    </Radix.Portal>
  </Radix.Root>;
}
