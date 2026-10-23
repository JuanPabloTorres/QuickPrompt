import { AppIcon } from '../../components/ui/Icon';
import { Download } from 'lucide-react';
import type { LiveEvent, LiveView } from '../../services/live';
import { activityStatus, operationLabels, stageLabels, type ActionActivity } from './activity';

const time = (value: string) => new Date(value).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
function milestones(events: LiveEvent[]) {
  return events.flatMap(event => {
    const operation = event.toolName && operationLabels[event.toolName];
    const phase = event.status === 'UI_UPDATED' ? 'Resultado comprobado' : event.status === 'TOOL_PROPOSED' ? 'Acción elegida' : null;
    const label = stageLabels[event.stage] ?? (operation && phase ? `${operation} · ${phase}` : null);
    return label ? [{ ...event, label }] : [];
  }).filter((item, index, items) => index === 0 || item.label !== items[index - 1].label);
}

export function ActivityLog({ view, events, actions }: { view: LiveView; events: LiveEvent[]; actions: readonly ActionActivity[] }) {
  const stages = milestones(events);
  function download() {
    // Explicit local download. Do not serialize view/transport objects or surface capabilities.
    const report = { version: 1, exportedAt: new Date().toISOString(), runId: view.run.runId,
      status: view.run.status, errorCode: view.run.errorCode, request: view.request,
      scope: 'Últimas 100 acciones observadas en este documento; los valores no sobreviven a una recarga.',
      actions, stages: stages.map(({ sequence, timestamp, label, status, durationMs, errorCode }) => ({ sequence, timestamp, label, status, durationMs, errorCode })) };
    const url = URL.createObjectURL(new Blob([JSON.stringify(report, null, 2)], { type: 'application/json' }));
    const link = document.createElement('a'); link.href = url; link.download = `nova-actividad-${view.run.runId}.json`; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  return <div className="nova-activity-log">
    {view.request && <details className="activity-request"><summary>Tu petición</summary><p>{view.request}</p></details>}
    <h3>Acciones en pantalla</h3>
    {actions.length ? <ol className="activity-actions" aria-label="Acciones en pantalla de NOVA">{[...actions].reverse().map(item => <li key={item.actionId} data-action-status={item.status}>
      <div className="activity-action-heading"><strong>{item.label}</strong><time dateTime={item.updatedAt}>{time(item.updatedAt)}</time></div>
      <p className="activity-badge">{activityStatus[item.status]}</p>
      {item.subject && <p className="activity-subject">{item.subject}{item.revision ? ` · versión ${item.revision}` : ''}</p>}
      {item.fields.length > 0 && <details className="activity-fields" open><summary>Campos y valores comprobados ({item.fields.length})</summary><dl>{item.fields.map((field, index) => <div key={`${field.name}-${index}`}><dt>{field.name}</dt><dd><pre>{field.value || '(vacío)'}</pre>{field.previous !== undefined && <details><summary>Valor anterior</summary><pre>{field.previous || '(vacío)'}</pre></details>}</dd></div>)}</dl></details>}
      {item.errorCode && <p className="error">Referencia del error: <code>{item.errorCode}</code></p>}
    </li>)}</ol> : <p className="activity-help">Todavía no hay acciones comprobadas en este documento. Las etapas de la tarea se muestran debajo.</p>}
    {actions.length === 100 && <p className="activity-help">Se muestran las últimas 100 acciones.</p>}
    {stages.length > 0 && <details className="activity-stages"><summary>Etapas de la tarea ({stages.length})</summary><ol className="live-steps" aria-label="Actividad reciente de NOVA">{stages.map(item => <li key={item.eventId}><time dateTime={item.timestamp}>{time(item.timestamp)}</time> {item.label}{item.durationMs != null && <small> · {(item.durationMs / 1000).toFixed(1)} s</small>}</li>)}</ol></details>}
    <button type="button" className="text-button" onClick={download}><AppIcon glyph={Download} size={16} aria-hidden="true"  />Descargar actividad</button>
    <p className="activity-help">El informe incluye estos valores. El detalle se conserva solo en esta pestaña hasta recargarla.</p>
  </div>;
}
