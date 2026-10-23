import { ArrowUpRight, Boxes, CircleHelp, FileCheck2, Layers, ShieldCheck, Sparkles, Target } from 'lucide-react';
import { AppIcon } from '../../components/ui/Icon';
import { liveLabels } from '../../services/live';
import { AgentRuntimePanel } from './AgentRuntimePanel';
import type { NovaRuntimeSnapshot } from './NovaLive';

const plannedSteps = [
  { label: 'Tu objetivo', detail: 'Tú eliges la acción.', glyph: Target },
  { label: 'Contexto y prompt', detail: 'Tus instrucciones y tus datos.', glyph: Layers },
  { label: 'IA y herramientas', detail: 'NOVA prepara el contenido.', glyph: Boxes },
  { label: 'Tu revisión', detail: 'Aprueba o rechaza la propuesta.', glyph: ShieldCheck },
  { label: 'Resultado', detail: 'Guarda, aplica y vuelve a usar.', glyph: FileCheck2 },
];

/** Presentation only: real activity always comes from NovaLive's bound snapshot. */
export function NovaWelcome({ onOpen, onHelp, runtime, home, controlLost, controlling }: {
  onOpen: () => void; onHelp: () => void; runtime: NovaRuntimeSnapshot | null; home: boolean; controlLost: boolean; controlling: boolean;
}) {
  return <section className="nova-mission" aria-labelledby="discover-heading" data-controlling={controlling} data-runtime={!!runtime}>
    {runtime ? <>
      <div className="nova-mission-heading"><div><p className="eyebrow">Mapa de NOVA</p><h1 id="discover-heading">Tu tarea, paso a paso.</h1></div><button type="button" className="text-button" data-nova-user-control="true" onClick={() => window.dispatchEvent(new Event('nova-open-activity'))}>Ver actividad<AppIcon glyph={ArrowUpRight} size={16} /></button></div>
      <div data-nova-user-control="true"><AgentRuntimePanel view={runtime.view} status={liveLabels[runtime.view.run.status] ?? runtime.view.run.status} disconnected={runtime.disconnected} controlLost={controlLost} controlled={controlling} controls={null} /></div>
    </> : controlling ? <><h1 id="discover-heading">Conectando con NOVA</h1><p className="nova-map-idle" role="status">Esperando el primer estado confirmado de tu tarea.</p></> : <div className="nova-idle-studio">
      <div className="nova-studio-intro"><p className="nova-studio-label"><AppIcon glyph={Sparkles} size={16} />NOVA, tu agente de prompts</p><h1 id="discover-heading">Tus prompts, listos para trabajar.</h1><p className="nova-studio-description">Guarda lo que funciona. Encuentra y mejora tus prompts con ayuda de NOVA.</p>
        {home && <div className="nova-welcome-actions"><button type="button" className="button primary" onClick={onOpen}>Trabajar con NOVA<AppIcon glyph={ArrowUpRight} size={20} /></button><button type="button" className="text-button" title="Guía y videos" onClick={onHelp}><AppIcon glyph={CircleHelp} size={20} />Guía y videos</button></div>}
        <p className="nova-studio-assurance"><AppIcon glyph={ShieldCheck} size={16} />Tú apruebas los cambios de NOVA.</p>
      </div>
      <div className="nova-plan"><div className="nova-plan-heading"><span>Así trabajará NOVA</span><span>Recorrido previsto</span></div><ol className="nova-idle-map" aria-label="Recorrido previsto del agente">{plannedSteps.map(({ label, detail, glyph }, index) => <li key={label}><span className="nova-plan-icon"><AppIcon glyph={glyph} size={20} /></span><div><strong>{label}</strong><small>{detail}</small></div><span className="nova-plan-number">{String(index + 1).padStart(2, '0')}</span></li>)}</ol></div>
    </div>}
  </section>;
}
