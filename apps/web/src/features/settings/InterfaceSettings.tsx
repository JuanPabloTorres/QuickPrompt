import { AppIcon } from '../../components/ui/Icon';
import { Bot, Monitor, Palette } from 'lucide-react';
import type { InterfacePreferences } from '../../hooks/useInterfacePreferences';

export function InterfaceSettings({ preferences, onChange, theme, onTheme }: { preferences: InterfacePreferences; onChange: (patch: Partial<InterfacePreferences>) => void; theme: string; onTheme: (theme: 'dark' | 'light') => void }) {
  return <div className="interface-settings">
    <section><h3><AppIcon glyph={Palette} size={20} aria-hidden="true"  />Apariencia</h3>
      <label htmlFor="settings-theme">Tema</label><select id="settings-theme" value={theme} onChange={event => onTheme(event.target.value as 'dark' | 'light')}><option value="dark">Oscuro</option><option value="light">Claro</option></select>
      <label htmlFor="settings-view">Vista de la biblioteca</label><select id="settings-view" value={preferences.view} onChange={event => onChange({ view: event.target.value as InterfacePreferences['view'] })}><option value="cards">Tarjetas</option><option value="rows">Filas</option></select>
      <label className="settings-check"><input type="checkbox" checked={preferences.decorativeMotion} onChange={event => onChange({ decorativeMotion: event.target.checked })} />Animaciones decorativas</label>
      <p className="muted">Se respeta también el movimiento reducido de tu dispositivo. Los estados y controles de NOVA siguen disponibles.</p>
    </section>
    <section><h3><AppIcon glyph={Bot} size={20} aria-hidden="true"  />Preparación con NOVA</h3>
      <label htmlFor="settings-execution">Presentación del resultado</label><select id="settings-execution" value={preferences.executionMode} onChange={event => onChange({ executionMode: event.target.value as InterfacePreferences['executionMode'] })}><option value="SAME_TAB">Ver cómo se prepara en esta pantalla</option><option value="LIVE">Solo mostrar el resultado</option></select>
      <label htmlFor="settings-speed">Velocidad de escritura</label><select id="settings-speed" value={preferences.agentSpeed} onChange={event => onChange({ agentSpeed: event.target.value as InterfacePreferences['agentSpeed'] })}><option value="fast">Ejecución rápida</option><option value="normal">Ejecución demostrativa</option></select>
      <label className="settings-check"><input type="checkbox" checked={preferences.rememberDraft} onChange={event => onChange({ rememberDraft: event.target.checked })} />Conservar borradores y respuestas del modo «Solo mostrar el resultado» en este equipo</label>
      <p className="muted"><AppIcon glyph={Monitor} size={16} aria-hidden="true"  />NOVA trabaja desde tu pantalla actual. Guardar una versión y enviar a otra IA requieren tu confirmación.</p>
    </section>
  </div>;
}
