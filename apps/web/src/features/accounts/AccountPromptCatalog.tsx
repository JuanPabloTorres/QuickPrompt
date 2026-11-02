import { Archive, Copy, Folder, Pencil, Play, RotateCcw, Share2, Star, Trash2 } from 'lucide-react';
import { AppIcon } from '../../components/ui/Icon';
import type { AccountPrompt, AccountPromptAction } from '../../services/accounts';
import { PromptEntry } from '../prompts/PromptEntry';

export function AccountPromptCatalog({ items, view, busy, onOpen, onUse, onEdit, onCollections, onShare, onAction, onDelete }: {
  items: AccountPrompt[]; view: 'cards' | 'rows'; busy: boolean;
  onOpen: (prompt: AccountPrompt) => void; onUse: (prompt: AccountPrompt) => void; onEdit: (prompt: AccountPrompt) => void;
  onCollections: (prompt: AccountPrompt) => void; onShare: (prompt: AccountPrompt) => void;
  onAction: (prompt: AccountPrompt, action: AccountPromptAction) => void; onDelete: (prompt: AccountPrompt) => void;
}) {
  return <ol className={`prompt-stream prompt-stream-${view}`} aria-label="Prompts guardados" data-view={view}>
    {items.map(prompt => <li key={prompt.id}><PromptEntry id={prompt.id} revision={prompt.revision} title={prompt.title} preview={prompt.description || prompt.template} favorite={prompt.isFavorite} variableCount={prompt.variableNames?.length} onOpen={() => onOpen(prompt)}>
      <div className="prompt-entry-actions account-prompt-actions" aria-label={`Acciones de ${prompt.title}`}>
        <button type="button" className="icon-button" disabled={busy} aria-label={`Usar ${prompt.title}`} title="Usar prompt" onClick={() => onUse(prompt)}><AppIcon glyph={Play} /></button>
        <button type="button" className="icon-button" disabled={busy} aria-label={`Editar ${prompt.title}`} title="Editar prompt" onClick={() => onEdit(prompt)}><AppIcon glyph={Pencil} /></button>
        <button type="button" className="icon-button" disabled={busy} aria-label={`${prompt.isFavorite ? 'Quitar de' : 'Añadir a'} favoritos: ${prompt.title}`} title="Alternar favorito" aria-pressed={!!prompt.isFavorite} onClick={() => onAction(prompt, 'favorite')}><AppIcon glyph={Star} fill={prompt.isFavorite ? 'currentColor' : 'none'} /></button>
        <button type="button" className="icon-button" disabled={busy} aria-label={`${prompt.isArchived ? 'Restaurar' : 'Archivar'}: ${prompt.title}`} title={prompt.isArchived ? 'Restaurar prompt' : 'Archivar prompt'} onClick={() => onAction(prompt, prompt.isArchived ? 'restore' : 'archive')}><AppIcon glyph={prompt.isArchived ? RotateCcw : Archive} /></button>
        <button type="button" className="icon-button" disabled={busy} aria-label={`Duplicar: ${prompt.title}`} title="Duplicar prompt" onClick={() => onAction(prompt, 'duplicate')}><AppIcon glyph={Copy} /></button>
        <button type="button" className="icon-button" disabled={busy} aria-label={`Colecciones de ${prompt.title}`} title="Colecciones" onClick={() => onCollections(prompt)}><AppIcon glyph={Folder} /></button>
        <button type="button" className="icon-button" disabled={busy} aria-label={`Compartir ${prompt.title}`} title="Compartir" onClick={() => onShare(prompt)}><AppIcon glyph={Share2} /></button>
        <button type="button" className="icon-button danger" disabled={busy} aria-label={`Eliminar: ${prompt.title}`} title="Eliminar prompt" onClick={() => onDelete(prompt)}><AppIcon glyph={Trash2} /></button>
      </div>
    </PromptEntry></li>)}
  </ol>;
}
