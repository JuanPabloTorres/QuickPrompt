import { AppIcon } from '../../components/ui/Icon';
import { ArrowUpRight, Braces, Copy, FileText, Pencil, Star, Trash2 } from 'lucide-react';
import type { Prompt } from '../../services/api';

export function PromptStream({ items, open, view = 'rows', onFavorite, onEdit, onDuplicate, onDelete, busy = false }: {
  items: Prompt[]; open: (prompt: Prompt) => void; view?: 'cards' | 'rows'; busy?: boolean;
  onFavorite?: (prompt: Prompt) => void; onEdit?: (prompt: Prompt) => void; onDuplicate?: (prompt: Prompt) => void; onDelete?: (prompt: Prompt) => void;
}) {
  return <ol className={`prompt-stream prompt-stream-${view}`} aria-label="Prompts guardados" data-view={view}>
    {items.map(prompt => <li key={prompt.id}><article className="prompt-entry">
      <button className="prompt-item" data-testid="library-prompt" data-prompt-id={prompt.id} data-revision={prompt.revision} aria-label={prompt.title} aria-describedby={`prompt-description-${prompt.id}`} onClick={() => open(prompt)}>
        <span data-testid={`identity-${prompt.id}`} hidden />
        <span className="prompt-file-icon"><AppIcon glyph={FileText} size={20} aria-hidden="true"  /></span>
        <span className="prompt-summary"><span className="prompt-title">{prompt.title}{prompt.isFavorite && <AppIcon glyph={Star} className="favorite-mark" size={16} label="Favorito"  />}</span>
          <span className="prompt-description" id={`prompt-description-${prompt.id}`}>{prompt.description || prompt.templateText}</span><span className="prompt-meta"><span><AppIcon glyph={Braces} size={16} aria-hidden="true"  />{prompt.variableNames.length ? `${prompt.variableNames.length} variables` : 'Sin variables'}</span><span className="prompt-version">v{prompt.revision}</span></span></span>
        <AppIcon glyph={ArrowUpRight} className="stream-arrow" size={20} aria-hidden="true"  />
      </button>
      <div className="prompt-entry-actions" aria-label={`Acciones de ${prompt.title}`}>
        {onFavorite && <button type="button" className="icon-button" disabled={busy} aria-label={`${prompt.isFavorite ? 'Quitar de' : 'Añadir a'} favoritos: ${prompt.title}`} title={prompt.isFavorite ? 'Quitar de favoritos' : 'Añadir a favoritos'} aria-pressed={prompt.isFavorite} onClick={() => onFavorite(prompt)}><AppIcon glyph={Star} size={20} fill={prompt.isFavorite ? 'currentColor' : 'none'} aria-hidden="true"  /></button>}
        {onEdit && <button type="button" className="icon-button" disabled={busy} aria-label={`Editar: ${prompt.title}`} title="Editar prompt" onClick={() => onEdit(prompt)}><AppIcon glyph={Pencil} size={20} aria-hidden="true"  /></button>}
        {onDuplicate && <button type="button" className="icon-button" disabled={busy} aria-label={`Duplicar: ${prompt.title}`} title="Duplicar prompt" onClick={() => onDuplicate(prompt)}><AppIcon glyph={Copy} size={20} aria-hidden="true"  /></button>}
        {onDelete && <button type="button" className="icon-button danger" disabled={busy} aria-label={`Eliminar: ${prompt.title}`} title="Eliminar prompt" onClick={() => onDelete(prompt)}><AppIcon glyph={Trash2} size={20} aria-hidden="true"  /></button>}
      </div>
    </article></li>)}
  </ol>;
}
