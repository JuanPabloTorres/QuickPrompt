import { Library, LogOut, Mail, Monitor, Settings } from 'lucide-react';
import { WorkspaceSidebarFrame, WorkspaceThemeButton } from '../../components/WorkspaceSidebar';
import { AppIcon } from '../../components/ui/Icon';

export type AccountWorkspaceView = 'libraries' | 'invitations' | 'connections' | 'preferences';

/** Account navigation composes shared chrome; session and effects remain page-owned. */
export function AccountWorkspaceSidebar({ view, theme, busy, delegation, onHome, onView, onTheme, onLogout }: {
  view: AccountWorkspaceView; theme: string; busy: boolean; delegation: boolean;
  onHome: () => void; onView: (view: AccountWorkspaceView) => void; onTheme: () => void; onLogout: () => void;
}) {
  return <WorkspaceSidebarFrame onHome={onHome} homeControl={false} workspaceLabel="Tu espacio por cuenta" statusLabel="Cuenta privada"
    navigation={<nav aria-label="Espacios de tu cuenta">
      <button type="button" title="Mis bibliotecas" aria-label="Mis bibliotecas" aria-current={view === 'libraries' ? 'page' : undefined} onClick={() => onView('libraries')}><AppIcon glyph={Library} size={20} /><span>Mis bibliotecas</span></button>
      <button type="button" title="Invitaciones" aria-label="Invitaciones" aria-current={view === 'invitations' ? 'page' : undefined} onClick={() => onView('invitations')}><AppIcon glyph={Mail} size={20} /><span>Invitaciones</span></button>
      {delegation && <button type="button" title="Conexiones locales" aria-label="Conexiones locales" aria-current={view === 'connections' ? 'page' : undefined} onClick={() => onView('connections')}><AppIcon glyph={Monitor} size={20} /><span>Conexiones locales</span></button>}
    </nav>}
    utilities={<>
      <WorkspaceThemeButton theme={theme} onTheme={onTheme} />
      <button type="button" title="Mis preferencias" aria-label="Mis preferencias" aria-current={view === 'preferences' ? 'page' : undefined} onClick={() => onView('preferences')}><AppIcon glyph={Settings} size={20} /><span>Mis preferencias</span></button>
      <button type="button" title="Cerrar sesión" aria-label="Cerrar sesión" disabled={busy} onClick={onLogout}><AppIcon glyph={LogOut} size={20} /><span>Cerrar sesión</span></button>
    </>} />;
}
