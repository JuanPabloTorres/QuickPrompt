import { CircleHelp, Clock3, Library, Moon, Plus, Settings, Sun } from 'lucide-react';
import { AppBrand } from './ui/AppBrand';
import { AppIcon } from './ui/Icon';

export function WorkspaceSidebar({ mode, theme, busy, onHome, onCreate, onHistory, onTheme, onSettings, onHelp }: {
  mode: 'discover' | 'focus' | 'create'; theme: string; busy: boolean;
  onHome: () => void; onCreate: () => void; onHistory: () => void; onTheme: () => void; onSettings: () => void; onHelp: () => void;
}) {
  const themeLabel = theme === 'dark' ? 'Activar tema claro' : 'Activar tema oscuro';
  return <aside className="workspace-sidebar" aria-label="Barra lateral de QuickPrompt">
    <AppBrand onClick={onHome} />
    <div className="sidebar-workspace-label">Tu espacio de trabajo</div>
    <nav aria-label="Navegación principal">
      <button type="button" title="Mi biblioteca" aria-label="Mi biblioteca" aria-current={mode === 'discover' ? 'page' : undefined} onClick={onHome}><AppIcon glyph={Library} size={20} /><span>Mi biblioteca</span></button>
      <button type="button" title="Crear un prompt" aria-label="Crear un prompt" aria-current={mode === 'create' ? 'page' : undefined} onClick={onCreate}><AppIcon glyph={Plus} size={20} /><span>Crear un prompt</span></button>
      <button type="button" title="Historial de resultados" aria-label="Historial de resultados" disabled={busy} onClick={onHistory}><AppIcon glyph={Clock3} size={20} /><span>Historial</span></button>
    </nav>
    <div className="sidebar-utilities">
      <button type="button" title={themeLabel} aria-label={themeLabel} onClick={onTheme}><AppIcon glyph={theme === 'dark' ? Sun : Moon} size={20} /><span>{theme === 'dark' ? 'Tema claro' : 'Tema oscuro'}</span></button>
      <button type="button" title="Configuración" aria-label="Configuración" onClick={onSettings}><AppIcon glyph={Settings} size={20} /><span>Configuración</span></button>
      <button type="button" title="Ayuda" aria-label="Ayuda" onClick={onHelp}><AppIcon glyph={CircleHelp} size={20} /><span>Ayuda</span></button>
      <p className="sidebar-local"><i aria-hidden="true" /><span>Local y privado</span></p>
    </div>
  </aside>;
}
