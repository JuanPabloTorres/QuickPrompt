import type { ReactNode } from 'react';
import { Boxes, BrainCircuit, Check, Circle, Cpu, Layers, ShieldCheck } from 'lucide-react';
import { AppIcon } from '../../components/ui/Icon';
import { NovaLogo } from '../../design/NovaLogo';
import { visualState } from '../../design/nova-state';
import { terminalLive, type LiveRuntime, type LiveView } from '../../services/live';

const modelLabels: Record<LiveRuntime['modelState'], string> = { ready: 'Listo', interpreting: 'Interpretando', generating: 'Generando', waiting: 'En espera', error: 'Necesita atención' };
const toolLabels = { available: 'Disponible', selected: 'Seleccionada', running: 'En curso', completed: 'Completada', blocked: 'Bloqueada', paused: 'En pausa', unavailable: 'No habilitada' };
const stepLabels = { pending: 'Sin observar', active: 'En curso', completed: 'Completado', stopped: 'Interrumpido', paused: 'En pausa', waiting: 'Espera tu decisión' };
const seconds = (value: number) => `${(value / 1000).toFixed(1)} s`;

export function AgentRuntimePanel({ view, status, controls, disconnected }: { view: LiveView; status: string; controls: ReactNode; disconnected: boolean }) {
  const runtime = view.runtime;
  if (!runtime || runtime.runId !== view.run.runId || runtime.sequence !== view.run.sequence) return <><p className="activity-help">El resumen de runtime aún no está disponible.</p>{controls}</>;
  const paused = view.run.pauseRequested || view.run.status === 'PAUSED';
  const terminal = terminalLive(view.run.status);
  const waiting = view.run.status === 'AWAITING_APPROVAL';
  const activity = terminal ? 'idle' : disconnected ? 'reconnecting' : paused ? 'paused' : waiting ? 'waiting' : 'working';
  const working = activity === 'working';
  const modelActive = working && ['interpreting', 'generating'].includes(runtime.modelState);
  const activeTool = runtime.tools.find(tool => tool.id === runtime.activeTool);
  return <section className="agent-runtime" data-testid="agent-runtime" aria-label="Runtime de NOVA" data-connected={!disconnected} data-activity={activity}>
    <div className="runtime-caption"><span>Un sistema conectado</span><span>RUNTIME / 01</span></div>
    <div className="runtime-map">
      <svg className="runtime-connections" viewBox="0 0 600 300" preserveAspectRatio="none" aria-hidden="true"><path d="M180 65 H225 Q245 65 245 100 V150 H300 M180 235 H225 Q245 235 245 200 V150 M420 65 H375 Q355 65 355 100 V150 H300 M420 235 H375 Q355 235 355 200 V150" /></svg>
      <article className="runtime-card runtime-context" data-active={working && !runtime.contextResolved}><div className="runtime-card-title"><AppIcon glyph={Layers} size={20} /><h3>Contexto</h3></div><strong>{runtime.objective}</strong><p>{runtime.contextSource}</p><small>{runtime.contextResolved ? 'Contexto resuelto' : 'Resolviendo contexto'}</small></article>
      <article className="runtime-card runtime-model" data-active={modelActive}><div className="runtime-card-title"><AppIcon glyph={Cpu} size={20} /><h3>Modelo local</h3></div><strong>{runtime.model}</strong><p>{disconnected && !terminal ? 'Estado sin confirmar' : modelLabels[runtime.modelState]}</p>{runtime.lastInferenceMs != null && <small>Última etapa de IA · {seconds(runtime.lastInferenceMs)}</small>}</article>
      <div className="runtime-center"><div className="runtime-core"><NovaLogo size={64} state={activity === 'reconnecting' ? 'idle' : visualState(view.run.status, undefined, runtime.activeTool, paused)} /></div><strong>NOVA</strong><span>{activity === 'reconnecting' ? 'Reconectando · estado sin confirmar' : status}</span><small>{seconds(runtime.elapsedMs)} observados</small></div>
      <article className="runtime-card runtime-tools" data-active={working && activeTool?.state === 'running'}><div className="runtime-card-title"><AppIcon glyph={Boxes} size={20} /><h3>Herramientas</h3></div><strong>{activeTool?.label ?? 'Ninguna en ejecución'}</strong><p>{runtime.tools.filter(tool => tool.state === 'available').length} disponibles ahora</p><details><summary>Ver herramientas ({runtime.tools.length})</summary><ul aria-label="Herramientas del runtime">{runtime.tools.map(tool => <li key={tool.id} data-state={tool.state}><span>{tool.label}</span><small>{disconnected && ['running', 'selected'].includes(tool.state) ? 'Sin confirmar' : toolLabels[tool.state]}</small></li>)}</ul>{!runtime.tools.length && <p>El catálogo aparecerá al resolver la tarea.</p>}</details></article>
      <article className="runtime-card runtime-human"><div className="runtime-card-title"><AppIcon glyph={ShieldCheck} size={20} /><h3>Control humano</h3></div><strong>{paused ? 'En pausa' : view.run.approval === 'PENDING' || view.deletion && view.run.status === 'AWAITING_APPROVAL' ? 'Esperando tu aprobación' : 'Tú mantienes el control'}</strong><p>Escrituras con aprobación</p>{controls}</article>
    </div>
    <article className="runtime-memory"><div className="runtime-card-title"><AppIcon glyph={BrainCircuit} size={20} /><h3>Memoria activa</h3></div><div className="runtime-memory-facts"><span>{runtime.memory.sourceBound ? 'Prompt vinculado' : 'Sin prompt de origen'}</span><span>{runtime.memory.draftActive ? 'Borrador activo' : 'Sin borrador preparado'}</span><span>{runtime.memory.resolvedVariables}/{runtime.memory.totalVariables} variables resueltas</span>{runtime.memory.reusesDraft && <span>Borrador recuperado</span>}</div><p>{runtime.memory.constraints.join(' · ')}</p></article>
    <ol className="runtime-flow" aria-label="Flujo observado de NOVA">{runtime.flow.map(step => <li key={step.id} data-state={step.state} aria-current={step.state === 'active' || step.state === 'waiting' ? 'step' : undefined}><AppIcon glyph={step.state === 'completed' ? Check : Circle} size={16} /><strong>{step.label}</strong><small>{stepLabels[step.state]}</small></li>)}</ol>
    <details className="runtime-identity"><summary>Identidad de la ejecución</summary><p>RunId <code>{runtime.runId}</code> · evento {runtime.sequence}</p></details>
    {runtime.recoverableOnly && <p className="activity-help">Historial recuperado. La memoria temporal y el catálogo actual ya no están disponibles.</p>}
  </section>;
}
