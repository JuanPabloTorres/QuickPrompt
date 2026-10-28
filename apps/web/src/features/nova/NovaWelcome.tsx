import { ArrowUpRight, CircleHelp, ShieldCheck, Sparkles } from 'lucide-react';
import { AppIcon } from '../../components/ui/Icon';
import type { NovaRuntimeSnapshot } from './NovaLive';
/** Presentation only: real activity always comes from NovaLive's bound snapshot. */
export function NovaWelcome({ onOpen, onHelp, runtime, home, controlling }: {
  onOpen: () => void; onHelp: () => void; runtime: NovaRuntimeSnapshot | null; home: boolean; controlling: boolean;
}) {
  // The control bar owns the compact live map. Do not occupy the driver's workspace.
  if (controlling || runtime) return null;
  return <section className="nova-mission" aria-labelledby="discover-heading">
    <div className="nova-idle-studio">
      <div className="nova-studio-intro"><p className="nova-studio-label"><AppIcon glyph={Sparkles} size={16} />NOVA, tu agente de prompts</p><h1 id="discover-heading">Tus prompts, listos para trabajar.</h1><p className="nova-studio-description">Guarda lo que funciona. Encuentra y mejora tus prompts con ayuda de NOVA.</p>
        {home && <div className="nova-welcome-actions"><button type="button" className="button primary" onClick={onOpen}>Trabajar con NOVA<AppIcon glyph={ArrowUpRight} size={20} /></button><button type="button" className="text-button" title="Guía y videos" onClick={onHelp}><AppIcon glyph={CircleHelp} size={20} />Guía y videos</button></div>}
        <p className="nova-studio-assurance"><AppIcon glyph={ShieldCheck} size={16} />Tú apruebas los cambios de NOVA.</p>
      </div>
    </div>
  </section>;
}
