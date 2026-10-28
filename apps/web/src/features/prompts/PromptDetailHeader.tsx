import type { Ref } from 'react';
import { Archive, ArrowLeft, Braces, Copy, FileText, Folder, History, Pencil, Share2, Star, Trash2, Undo2 } from 'lucide-react';
import { AppIcon } from '../../components/ui/Icon';
import type { PromptAction } from '../../services/library';

type DetailIdentity = { title: string; revision: number; description?: string; isArchived?: boolean; isFavorite?: boolean; variableNames?: string[]; updatedAt?: string | null };

/** Presentation only. The parent retains navigation and all domain operations. */
export function PromptDetailHeader({ prompt, titleRef, onBack, onEdit, onAction, onDelete, onGroups, onVersions, onShare,
  storageLabel = 'Guardado en tu equipo', backLabel = 'Biblioteca', editLabel = 'Editar', versionsLabel = 'Versiones', homeControl = false, busy = false }: {
  prompt: DetailIdentity; titleRef: Ref<HTMLHeadingElement>; onBack: () => void; onEdit: () => void;
  onAction?: (action: PromptAction) => void; onDelete?: () => void;
  onGroups?: () => void; onVersions?: () => void; onShare?: () => void;
  storageLabel?: string; backLabel?: string; editLabel?: string; versionsLabel?: string; homeControl?: boolean; busy?: boolean;
}) {
  const updated = prompt.updatedAt && !Number.isNaN(Date.parse(prompt.updatedAt)) ? prompt.updatedAt : null;
  return <>
    <div className="detail-navigation"><button type="button" data-nova-control={homeControl ? 'library.home' : undefined} className="text-button" onClick={onBack}><AppIcon glyph={ArrowLeft} size={20} />{backLabel}</button><span>Tu espacio de prompts<span aria-hidden="true"> / </span>Detalle</span></div>
    <header className="detail-header">
      <div className="detail-identity"><span className="detail-document-icon" aria-hidden="true"><AppIcon glyph={FileText} size={24} /></span><div><p className="eyebrow">{prompt.isArchived ? 'EN EL ARCHIVO' : 'INSTRUCCIÓN REUTILIZABLE'}</p><h1 id="focus-heading" data-testid="focus-title" ref={titleRef} tabIndex={-1}>{prompt.title}</h1>{prompt.description && <p className="focus-description">{prompt.description}</p>}</div></div>
      <div className="detail-meta"><span className="detail-version">Versión {prompt.revision.toString().padStart(2, '0')}</span>{prompt.variableNames && <span><AppIcon glyph={Braces} size={16} />{prompt.variableNames.length ? `${prompt.variableNames.length} ${prompt.variableNames.length === 1 ? 'variable' : 'variables'}` : 'Sin variables'}</span>}{updated && <span>Actualizado <time dateTime={updated}>{new Date(updated).toLocaleDateString('es', { day: 'numeric', month: 'short', year: 'numeric' })}</time></span>}</div>
      <div className="detail-management"><div className="detail-main-actions"><button type="button" data-nova-control="prompt.edit" className="secondary" disabled={busy} onClick={onEdit}><AppIcon glyph={Pencil} size={20} />{editLabel}</button>{onVersions && <button type="button" className="secondary" disabled={busy} onClick={onVersions}><AppIcon glyph={History} size={20} />{versionsLabel}</button>}{onAction && <button type="button" className="text-button detail-favorite" disabled={busy} onClick={() => onAction('favorite')} aria-pressed={!!prompt.isFavorite}><AppIcon glyph={Star} size={20} />Favorito</button>}</div><span className="detail-storage"><i aria-hidden="true" />{storageLabel}</span></div>
      <div className="detail-secondary-actions" role="group" aria-label="Organizar y compartir prompt">{onGroups && <button type="button" className="text-button" disabled={busy} onClick={onGroups}><AppIcon glyph={Folder} size={16} />Colecciones</button>}{onShare && <button type="button" className="text-button" disabled={busy} onClick={onShare}><AppIcon glyph={Share2} size={16} />Compartir plantilla</button>}{onAction && <><button type="button" className="text-button" disabled={busy} onClick={() => onAction('duplicate')}><AppIcon glyph={Copy} size={16} />Duplicar</button><button type="button" className="text-button" disabled={busy} onClick={() => onAction(prompt.isArchived ? 'restore' : 'archive')}><AppIcon glyph={prompt.isArchived ? Undo2 : Archive} size={16} />{prompt.isArchived ? 'Recuperar' : 'Archivar'}</button></>}{onDelete && <button type="button" className="icon-button danger" disabled={busy} onClick={onDelete} aria-label="Eliminar prompt" title="Eliminar prompt"><AppIcon glyph={Trash2} size={20} /></button>}</div>
    </header>
  </>;
}
