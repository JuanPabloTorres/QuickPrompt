import type { Ref } from 'react';
import { Archive, ArrowLeft, Braces, Copy, FileText, Folder, History, Pencil, Share2, Star, Trash2, Undo2 } from 'lucide-react';
import { AppIcon } from '../../components/ui/Icon';
import type { Prompt } from '../../services/api';
import type { PromptAction } from '../../services/library';

/** Presentation only. The parent retains navigation and all domain operations. */
export function PromptDetailHeader({ prompt, titleRef, onBack, onEdit, onAction, onDelete, onGroups, onVersions, onShare }: {
  prompt: Prompt; titleRef: Ref<HTMLHeadingElement>; onBack: () => void; onEdit: () => void;
  onAction: (action: PromptAction) => void; onDelete: () => void;
  onGroups?: () => void; onVersions?: () => void; onShare?: () => void;
}) {
  return <>
    <div className="detail-navigation"><button type="button" className="text-button" onClick={onBack}><AppIcon glyph={ArrowLeft} size={20} />Biblioteca</button><span>Tu espacio de prompts<span aria-hidden="true"> / </span>Detalle</span></div>
    <header className="detail-header">
      <div className="detail-identity"><span className="detail-document-icon" aria-hidden="true"><AppIcon glyph={FileText} size={24} /></span><div><p className="eyebrow">{prompt.isArchived ? 'EN EL ARCHIVO' : 'INSTRUCCIÓN REUTILIZABLE'}</p><h1 id="focus-heading" data-testid="focus-title" ref={titleRef} tabIndex={-1}>{prompt.title}</h1>{prompt.description && <p className="focus-description">{prompt.description}</p>}</div></div>
      <div className="detail-meta"><span className="detail-version">Versión {prompt.revision.toString().padStart(2, '0')}</span><span><AppIcon glyph={Braces} size={16} />{prompt.variableNames.length ? `${prompt.variableNames.length} ${prompt.variableNames.length === 1 ? 'variable' : 'variables'}` : 'Sin variables'}</span><span>Actualizado <time dateTime={prompt.updatedAt}>{new Date(prompt.updatedAt).toLocaleDateString('es', { day: 'numeric', month: 'short', year: 'numeric' })}</time></span></div>
      <div className="detail-management"><div className="detail-main-actions"><button type="button" data-nova-control="prompt.edit" className="secondary" onClick={onEdit}><AppIcon glyph={Pencil} size={20} />Editar</button>{onVersions && <button type="button" className="secondary" onClick={onVersions}><AppIcon glyph={History} size={20} />Versiones</button>}<button type="button" className="text-button detail-favorite" onClick={() => onAction('favorite')} aria-pressed={prompt.isFavorite}><AppIcon glyph={Star} size={20} />Favorito</button></div><span className="detail-storage"><i aria-hidden="true" />Guardado en tu equipo</span></div>
      <div className="detail-secondary-actions" role="group" aria-label="Organizar y compartir prompt">{onGroups && <button type="button" className="text-button" onClick={onGroups}><AppIcon glyph={Folder} size={16} />Colecciones</button>}{onShare && <button type="button" className="text-button" onClick={onShare}><AppIcon glyph={Share2} size={16} />Compartir plantilla</button>}<button type="button" className="text-button" onClick={() => onAction('duplicate')}><AppIcon glyph={Copy} size={16} />Duplicar</button><button type="button" className="text-button" onClick={() => onAction(prompt.isArchived ? 'restore' : 'archive')}><AppIcon glyph={prompt.isArchived ? Undo2 : Archive} size={16} />{prompt.isArchived ? 'Recuperar' : 'Archivar'}</button><button type="button" className="icon-button danger" onClick={onDelete} aria-label="Eliminar prompt" title="Eliminar prompt"><AppIcon glyph={Trash2} size={20} /></button></div>
    </header>
  </>;
}
