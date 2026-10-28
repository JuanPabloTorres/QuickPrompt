import { Copy } from 'lucide-react';
import { AppIcon } from '../../components/ui/Icon';

// The reusable definition has its own visible evidence. It never emits the
// compiled-ready marker or substitutes future input values.
export function TemplateCopyPreview({ text, disabled, onCopy }: { text: string; disabled: boolean; onCopy: () => void }) {
  return <details className="prompt-preview template-copy-preview">
    <summary data-nova-control="prompt.template-preview">Plantilla con marcadores</summary>
    <p className="muted">Conserva los campos para reutilizarla en otro momento.</p>
    <pre data-testid="template-preview">{text}</pre>
    <button type="button" className="secondary" data-nova-control="prompt.copy-template" disabled={disabled || !text.trim()} onClick={onCopy}>
      <AppIcon glyph={Copy} size={16} aria-hidden="true" />Copiar plantilla con marcadores
    </button>
  </details>;
}
