import { Archive, Clock3, FolderPlus, LayoutGrid, Library, List, Plus, Search, Star } from 'lucide-react';
import { AppIcon } from '../../components/ui/Icon';
import type { Group } from '../../services/api';
import type { LibraryFilter } from '../../services/library';

/** Controlled presentation; query state and navigation stay with their owners. */
export function LibraryToolbar({ total, search, filter, groupId, groups, view, onSearch, onFilter, onGroup, onView, onCreate, onGroups, title = 'Tu biblioteca', createLabel = 'Nuevo prompt', collectionLabel = 'Colección', includeUngrouped = false, busy = false }: {
  total: number; search: string; filter: LibraryFilter; groupId: string; groups: Group[]; view: 'cards' | 'rows';
  onSearch: (value: string) => void; onFilter: (value: LibraryFilter) => void; onGroup: (value: string) => void;
  onView: (value: 'cards' | 'rows') => void; onCreate: () => void; onGroups: () => void;
  title?: string; createLabel?: string; collectionLabel?: string; includeUngrouped?: boolean; busy?: boolean;
}) {
  return <div className="library-toolbar">
    <div className="library-heading-row"><div><h2 id="library-heading">{title} <span className="count">{total}</span></h2><p>Una buena instrucción merece volver a usarse.</p></div><button data-nova-control="prompt.new" className="secondary" disabled={busy} onClick={onCreate}><AppIcon glyph={Plus} size={20} />{createLabel}</button></div>
    <div className="library-command-row"><label className="library-search" htmlFor="library-search"><span>Buscar en la biblioteca</span><span className="library-search-field"><AppIcon glyph={Search} size={20} /><input id="library-search" value={search} maxLength={200} placeholder="Encuentra tu próximo punto de partida…" onChange={event => onSearch(event.target.value)} /></span></label>
      <div className="view-switch" role="group" aria-label="Vista de la biblioteca"><button type="button" className="icon-button" aria-label="Ver tarjetas" title="Ver tarjetas" aria-pressed={view === 'cards'} onClick={() => onView('cards')}><AppIcon glyph={LayoutGrid} size={20} /></button><button type="button" className="icon-button" aria-label="Ver filas" title="Ver filas" aria-pressed={view === 'rows'} onClick={() => onView('rows')}><AppIcon glyph={List} size={20} /></button></div>
    </div>
    <div className="library-scope-row"><div className="filter-tabs" role="group" aria-label="Filtrar biblioteca">{([['all', 'Todos', Library], ['favorites', 'Favoritos', Star], ['recent', 'Recientes', Clock3], ['archived', 'Archivo', Archive]] as const).map(([value, label, Glyph]) => <button key={value} aria-pressed={filter === value} onClick={() => onFilter(value)}><AppIcon glyph={Glyph} size={16} />{label}</button>)}</div>
      <div className="collection-filter"><label className="group-filter"><span className="sr-only">{collectionLabel}</span><select aria-label={collectionLabel} value={groupId} onChange={event => onGroup(event.target.value)}><option value="">Todas las colecciones</option>{includeUngrouped && <option value="ungrouped">Sin colección</option>}{groupId && groupId !== 'ungrouped' && !groups.some(item => item.group.id === groupId) && <option value={groupId} disabled>Colección no disponible</option>}{groups.map(({ group }) => <option key={group.id} value={group.id}>{group.name}</option>)}</select></label><button className="icon-button" aria-label="Gestionar colecciones" title="Gestionar colecciones" disabled={busy} onClick={onGroups}><AppIcon glyph={FolderPlus} size={20} /></button></div>
    </div>
  </div>;
}
