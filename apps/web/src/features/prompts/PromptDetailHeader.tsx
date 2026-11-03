import type { Ref } from 'react';
import { Archive, ArrowLeft, Braces, Copy, FileText, Folder, History, Pencil, Share2, Star, Trash2, Undo2 } from 'lucide-react';
import { AppIcon, IconAction } from '../../components/ui/Icon';
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
    <div className="detail-navigation"><IconAction data-nova-control={homeControl ? 'library.home' : undefined} className="secondary" onClick={onBack} label={backLabel} glyph={ArrowLeft} /><span>Biblioteca <span aria-hidden="true">/</span> Detalle</span></div>
    <header className="detail-header">
      <div className="detail-identity"><span className="detail-document-icon" aria-hidden="true"><AppIcon glyph={FileText} size={24} /></span><div><p className="eyebrow">{prompt.isArchived ? 'EN EL ARCHIVO' : 'INSTRUCCIÓN REUTILIZABLE'}</p><h1 id="focus-heading" data-testid="focus-title" ref={titleRef} tabIndex={-1}>{prompt.title}</h1>{prompt.description && <p className="focus-description">{prompt.description}</p>}</div></div>
      <div className="detail-meta"><span className="detail-version">Versión {prompt.revision.toString().padStart(2, '0')}</span>{prompt.variableNames && <span><AppIcon glyph={Braces} size={16} />{prompt.variableNames.length ? `${prompt.variableNames.length} ${prompt.variableNames.length === 1 ? 'variable' : 'variables'}` : 'Sin variables'}</span>}{updated && <span>Actualizado <time dateTime={updated}>{new Date(updated).toLocaleDateString('es', { day: 'numeric', month: 'short', year: 'numeric' })}</time></span>}</div>
      <div className="detail-management"><div className="detail-main-actions">
        <IconAction data-nova-control="prompt.edit" className="secondary" disabled={busy} onClick={onEdit} label={editLabel} glyph={Pencil} />
        {onVersions && <IconAction className="secondary" disabled={busy} onClick={onVersions} label={versionsLabel} glyph={History} />}
        {onAction && <IconAction className="detail-favorite" disabled={busy} onClick={() => onAction('favorite')} aria-pressed={!!prompt.isFavorite} label="Favorito" glyph={Star} />}
      </div><span className="detail-storage"><i aria-hidden="true" />{storageLabel}</span></div>
      <div className="detail-secondary-actions" role="group" aria-label="Organizar y compartir prompt">
        {onGroups && <IconAction disabled={busy} onClick={onGroups} label="Colecciones" glyph={Folder} />}
        {onShare && <IconAction disabled={busy} onClick={onShare} label="Compartir plantilla" glyph={Share2} />}
        {onAction && <>
          <IconAction disabled={busy} onClick={() => onAction('duplicate')} label="Duplicar" glyph={Copy} />
          <IconAction disabled={busy} onClick={() => onAction(prompt.isArchived ? 'restore' : 'archive')} label={prompt.isArchived ? 'Recuperar' : 'Archivar'} glyph={prompt.isArchived ? Undo2 : Archive} />
        </>}
        {onDelete && <IconAction className="danger" disabled={busy} onClick={onDelete} label="Eliminar prompt" glyph={Trash2} />}
      </div>
    </header>
  </>;
}
