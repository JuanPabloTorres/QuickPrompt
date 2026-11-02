import { AppIcon } from '../../components/ui/Icon';
import { Copy, Pencil, Star, Trash2 } from 'lucide-react';
import type { Prompt } from '../../services/api';
import { PromptEntry } from './PromptEntry';

export function PromptStream({ items, open, view = 'rows', onFavorite, onEdit, onDuplicate, onDelete, busy = false }: {
  items: Prompt[]; open: (prompt: Prompt) => void; view?: 'cards' | 'rows'; busy?: boolean;
  onFavorite?: (prompt: Prompt) => void; onEdit?: (prompt: Prompt) => void; onDuplicate?: (prompt: Prompt) => void; onDelete?: (prompt: Prompt) => void;
}) {
  return <ol className={`prompt-stream prompt-stream-${view}`} aria-label="Prompts guardados" data-view={view}>
    {items.map(prompt => <li key={prompt.id}><PromptEntry id={prompt.id} revision={prompt.revision} title={prompt.title} preview={prompt.description || prompt.templateText} variableCount={prompt.variableNames.length} favorite={prompt.isFavorite} onOpen={() => open(prompt)}>
      <div className="prompt-entry-actions" aria-label={`Acciones de ${prompt.title}`}>
        {onFavorite && <button type="button" className="icon-button" disabled={busy} aria-label={`${prompt.isFavorite ? 'Quitar de' : 'Añadir a'} favoritos: ${prompt.title}`} title={prompt.isFavorite ? 'Quitar de favoritos' : 'Añadir a favoritos'} aria-pressed={prompt.isFavorite} onClick={() => onFavorite(prompt)}><AppIcon glyph={Star} size={20} fill={prompt.isFavorite ? 'currentColor' : 'none'} aria-hidden="true"  /></button>}
        {onEdit && <button type="button" className="icon-button" disabled={busy} aria-label={`Editar: ${prompt.title}`} title="Editar prompt" onClick={() => onEdit(prompt)}><AppIcon glyph={Pencil} size={20} aria-hidden="true"  /></button>}
        {onDuplicate && <button type="button" className="icon-button" disabled={busy} aria-label={`Duplicar: ${prompt.title}`} title="Duplicar prompt" onClick={() => onDuplicate(prompt)}><AppIcon glyph={Copy} size={20} aria-hidden="true"  /></button>}
        {onDelete && <button type="button" className="icon-button danger" disabled={busy} aria-label={`Eliminar: ${prompt.title}`} title="Eliminar prompt" onClick={() => onDelete(prompt)}><AppIcon glyph={Trash2} size={20} aria-hidden="true"  /></button>}
      </div>
    </PromptEntry></li>)}
  </ol>;
}
