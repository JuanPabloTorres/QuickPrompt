import { CircleHelp, ShieldCheck } from 'lucide-react';
import { AppIcon, IconAction } from '../../components/ui/Icon';
import type { NovaRuntimeSnapshot } from './NovaLive';
/** Presentation only: real activity always comes from NovaLive's bound snapshot. */
export function NovaWelcome({ onHelp, runtime, home, controlling }: {
  onHelp: () => void; runtime: NovaRuntimeSnapshot | null; home: boolean; controlling: boolean;
}) {
  // The control bar owns the compact live map. Do not occupy the driver's workspace.
  if (controlling || runtime) return null;
  return <section className="nova-mission" aria-labelledby="discover-heading">
    <div className="nova-idle-studio">
      <div className="nova-studio-intro"><h1 id="discover-heading">Tus prompts, listos para trabajar.</h1>
        <p className="nova-studio-assurance"><AppIcon glyph={ShieldCheck} size={16} />Tú apruebas los cambios de NOVA.</p>
      </div>
      {home && <IconAction className="secondary" label="Guía y videos" glyph={CircleHelp} onClick={onHelp} />}
    </div>
  </section>;
}
