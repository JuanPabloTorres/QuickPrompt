import type { LucideIcon, LucideProps } from 'lucide-react';
import type { ComponentPropsWithoutRef } from 'react';

export type IconSize = 16 | 20 | 24 | 32;
/** Decorative by default; meaningful standalone icons require a label. */
export function AppIcon({ glyph: Glyph, size = 20, label, disabled = false, ...props }: Omit<LucideProps, 'size' | 'aria-label'> & { glyph: LucideIcon; size?: IconSize; label?: string; disabled?: boolean }) {
  return <Glyph {...props} size={size} strokeWidth={1.75} color="currentColor" className={`app-icon${props.className ? ` ${props.className}` : ''}`} aria-hidden={label ? undefined : true} aria-label={label} role={label ? 'img' : undefined} data-disabled={disabled || undefined} focusable="false" />;
}

/** Compact action with one name for assistive technology and the pointer hint. */
export function IconAction({ label, glyph, className, type = 'button', children, ...button }: ComponentPropsWithoutRef<'button'> & { label: string; glyph: LucideIcon }) {
  return <button {...button} type={type} className={className ? `icon-button ${className}` : 'icon-button'} aria-label={label} title={label}>{children ?? <AppIcon glyph={glyph} />}</button>;
}
