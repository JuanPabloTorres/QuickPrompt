import type { LucideIcon, LucideProps } from 'lucide-react';

export type IconSize = 16 | 20 | 24 | 32;
/** Decorative by default; meaningful standalone icons require a label. */
export function AppIcon({ glyph: Glyph, size = 20, label, disabled = false, ...props }: Omit<LucideProps, 'size' | 'aria-label'> & { glyph: LucideIcon; size?: IconSize; label?: string; disabled?: boolean }) {
  return <Glyph {...props} size={size} strokeWidth={1.75} color="currentColor" className={`app-icon${props.className ? ` ${props.className}` : ''}`} aria-hidden={label ? undefined : true} aria-label={label} role={label ? 'img' : undefined} data-disabled={disabled || undefined} focusable="false" />;
}
