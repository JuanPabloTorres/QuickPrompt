import type { ReactNode } from 'react';
import { ArrowUpRight, Braces, FileText, Star } from 'lucide-react';
import { AppIcon } from '../../components/ui/Icon';

/** Shared prompt identity/preview presentation; consumers retain their DTOs and effects. */
export function PromptEntry({ id, revision, title, preview, variableCount, favorite, onOpen, children }: {
  id: string; revision: number; title: string; preview: string; variableCount?: number; favorite?: boolean; onOpen: () => void; children: ReactNode;
}) {
  return <article className="prompt-entry">
    <button className="prompt-item" data-testid="library-prompt" data-prompt-id={id} data-revision={revision} aria-label={title} aria-describedby={`prompt-description-${id}`} onClick={onOpen}>
      <span data-testid={`identity-${id}`} hidden />
      <span className="prompt-file-icon"><AppIcon glyph={FileText} size={20} aria-hidden="true" /></span>
      <span className="prompt-summary"><span className="prompt-title">{title}{favorite && <AppIcon glyph={Star} className="favorite-mark" size={16} label="Favorito" />}</span>
        <span className="prompt-description" id={`prompt-description-${id}`}>{preview}</span><span className="prompt-meta">{variableCount !== undefined && <span><AppIcon glyph={Braces} size={16} aria-hidden="true" />{variableCount ? `${variableCount} variables` : 'Sin variables'}</span>}<span className="prompt-version">v{revision}</span></span></span>
      <AppIcon glyph={ArrowUpRight} className="stream-arrow" size={20} aria-hidden="true" />
    </button>
    {children}
  </article>;
}
