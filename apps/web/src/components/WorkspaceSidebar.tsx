import { CircleHelp, Clock3, Library, Moon, Plus, Settings, Sun } from 'lucide-react';
import { AppBrand } from './ui/AppBrand';
import { AppIcon } from './ui/Icon';
import type { ReactNode } from 'react';

/** Shared navigation frame with no route, data or authorization ownership. */
export function WorkspaceSidebarFrame({ onHome, navigation, utilities, workspaceLabel = 'Tu espacio de trabajo', statusLabel = 'Local y privado', homeControl = true }: {
  onHome: () => void; navigation: ReactNode; utilities: ReactNode; workspaceLabel?: string; statusLabel?: string; homeControl?: boolean;
}) {
  return <aside className="workspace-sidebar" aria-label="Barra lateral de QuickPrompt">
    <AppBrand onClick={onHome} homeControl={homeControl} />
    <div className="sidebar-workspace-label">{workspaceLabel}</div>
    {navigation}
    <div className="sidebar-utilities">{utilities}<p className="sidebar-local"><i aria-hidden="true" /><span>{statusLabel}</span></p></div>
  </aside>;
}

export function WorkspaceThemeButton({ theme, onTheme }: { theme: string; onTheme: () => void }) {
  const themeLabel = theme === 'dark' ? 'Activar tema claro' : 'Activar tema oscuro';
  return <button type="button" title={themeLabel} aria-label={themeLabel} onClick={onTheme}><AppIcon glyph={theme === 'dark' ? Sun : Moon} size={20} /><span>{theme === 'dark' ? 'Tema claro' : 'Tema oscuro'}</span></button>;
}

export function WorkspaceSidebar({ mode, theme, busy, onHome, onCreate, onHistory, onTheme, onSettings, onHelp }: {
  mode: 'discover' | 'focus' | 'create'; theme: string; busy: boolean;
  onHome: () => void; onCreate: () => void; onHistory: () => void; onTheme: () => void; onSettings: () => void; onHelp: () => void;
}) {
  return <WorkspaceSidebarFrame onHome={onHome} navigation={<nav aria-label="Navegación principal">
      <button type="button" title="Mi biblioteca" aria-label="Mi biblioteca" aria-current={mode === 'discover' ? 'page' : undefined} onClick={onHome}><AppIcon glyph={Library} size={20} /><span>Mi biblioteca</span></button>
      <button type="button" title="Crear un prompt" aria-label="Crear un prompt" aria-current={mode === 'create' ? 'page' : undefined} onClick={onCreate}><AppIcon glyph={Plus} size={20} /><span>Crear un prompt</span></button>
      <button type="button" title="Historial de resultados" aria-label="Historial de resultados" disabled={busy} onClick={onHistory}><AppIcon glyph={Clock3} size={20} /><span>Historial</span></button>
    </nav>} utilities={<>
      <WorkspaceThemeButton theme={theme} onTheme={onTheme} />
      <button type="button" title="Configuración" aria-label="Configuración" onClick={onSettings}><AppIcon glyph={Settings} size={20} /><span>Configuración</span></button>
      <button type="button" title="Ayuda" aria-label="Ayuda" onClick={onHelp}><AppIcon glyph={CircleHelp} size={20} /><span>Ayuda</span></button>
    </>} />;
}
