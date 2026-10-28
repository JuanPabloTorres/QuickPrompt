import type { UiAction, UiState } from './AgentUiDriver';

export const operationLabels: Record<string, string> = {
  start: 'Preparando tu espacio de trabajo', set_working_context: 'Vinculando el borrador actual',
  ui_navigate_library: 'Abriendo tu biblioteca', ui_search_prompt: 'Buscando en tu biblioteca',
  ui_open_prompt: 'Abriendo el prompt', ui_open_create_prompt: 'Abriendo un nuevo borrador',
  ui_prepare_creation: 'Escribiendo el borrador', ui_load_active_draft: 'Recuperando tu borrador',
  ui_open_editor: 'Abriendo el editor', ui_fill_variable: 'Completando tus datos',
  ui_prepare_update: 'Preparando los cambios', ui_show_compiled_prompt: 'Preparando la vista previa',
  ui_show_template: 'Mostrando la plantilla con marcadores', ui_copy_template: 'Copiando la plantilla con marcadores',
  ui_open_provider_picker: 'Preparando el destino', ui_execute_local: 'Generando la respuesta',
  ui_read_visible_state: 'Revisando el contenido', ui_verify_prompt_state: 'Comprobando el resultado',
  ui_request_save_version: 'Preparando la revisión', prepare_review: 'Preparando la revisión',
  ui_review_deletion: 'Mostrando el prompt que puedes eliminar', ui_enable_deletion_confirmation: 'Esperando tu decisión de eliminación',
  ui_verify_deletion: 'Comprobando que el prompt fue eliminado',
  commit_approved: 'Guardando la versión aprobada', finish: 'Comprobando el resultado',
  ask_clarification: 'Necesito un dato más'
};
export const stageLabels: Record<string, string> = {
  SemanticInterpretation: 'Entendiendo tu petición', SemanticPlan: 'Plan de trabajo preparado',
  InputPrerequisite: 'Revisando los datos que proporcionaste', InputExtraction: 'Relacionando tus datos con los campos',
  Model: 'Decidiendo la siguiente acción', ModelRetry: 'Revisando la siguiente acción',
  Generation: 'Redactando el contenido del prompt', GenerationRetry: 'Revisando el contenido generado',
  Verification: 'Comprobando el resultado', Completion: 'Tarea completada y comprobada',
  Cancellation: 'Ejecución detenida', Explanation: 'Consulta resuelta',
  DeletionReview: 'Revisando la eliminación', DeletionCommit: 'Eliminación confirmada por ti', DeletionVerified: 'Eliminación comprobada'
};
export type ActivityField = { name: string; value: string; previous?: string };
export type ActionActivity = {
  actionId: string; sequence: number; tool: string; label: string; startedAt: string; updatedAt: string;
  status: 'running' | 'target' | 'checking' | 'confirming' | 'verified' | 'interrupted';
  subject?: string; revision?: number; fields: ActivityField[]; errorCode?: string;
};
export const activityStatus = {
  running: 'En curso', target: 'Control localizado', checking: 'Comprobando el resultado',
  confirming: 'Esperando confirmación', verified: 'Comprobado en pantalla', interrupted: 'Resultado sin confirmar'
};

// Values stay in this document's memory: no localStorage, database or telemetry.
// Keep bounded histories for recent runs; retrying a receipt updates the same action.
const histories = new Map<string, readonly ActionActivity[]>();
const listeners = new Set<() => void>();
let notification: ReturnType<typeof setTimeout> | undefined;
const empty: readonly ActionActivity[] = [];
export const getActivity = (runId: string | null) => runId ? histories.get(runId) ?? empty : empty;
export function subscribeActivity(listener: () => void) { listeners.add(listener); return () => { listeners.delete(listener); }; }
function update(runId: string, entry: ActionActivity) {
  const entries = getActivity(runId), index = entries.findIndex(item => item.actionId === entry.actionId);
  if (index >= 0 && entries[index].status === 'verified') return;
  histories.set(runId, (index < 0 ? [...entries, entry] : entries.map((item, at) => at === index ? entry : item)).slice(-100));
  while (histories.size > 3) histories.delete(histories.keys().next().value!);
  notifyLater();
}
/** Clear only the retiring private scope's runs; a new account must not inherit UUID-keyed fields. */
export function clearActivity(runId: string) { if (histories.delete(runId)) notifyLater(); }
function notifyLater() {
  // The journal is immediate; rendering its observers is coalesced and never
  // runs synchronously on the executor's critical path.
  if (notification === undefined) notification = setTimeout(() => {
    notification = undefined; listeners.forEach(listener => listener());
  }, 80);
}
export function beginActivity(action: UiAction) {
  if (getActivity(action.runId).some(item => item.actionId === action.actionId)) return;
  update(action.runId, { actionId: action.actionId, sequence: action.sequence, tool: action.tool,
    label: operationLabels[action.tool] ?? 'Realizando una acción en pantalla', startedAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(), status: 'running', fields: [] });
}
export function progressActivity(action: UiAction, status: ActionActivity['status'], errorCode?: string) {
  const entry = getActivity(action.runId).find(item => item.actionId === action.actionId);
  if (entry) update(action.runId, { ...entry, status, updatedAt: new Date().toISOString(),
    errorCode: errorCode && /^[A-Z][A-Z_0-9]{1,80}$/.test(errorCode) ? errorCode : undefined });
}

export function confirmActivity(action: UiAction, before: UiState, after: UiState) {
  const entry = getActivity(action.runId).find(item => item.actionId === action.actionId);
  if (!entry) return;
  const fields: ActivityField[] = [];
  const field = (name: string, value: string | null, previous?: string | null) => {
    if (value !== null) fields.push({ name, value, ...(previous != null && previous !== value ? { previous } : {}) });
  };
  // Describe observed results only for the operation that actually wrote them.
  // Navigating to a prompt must never attribute its existing values to NOVA.
  if (['ui_prepare_creation', 'ui_prepare_update', 'ui_load_active_draft'].includes(action.tool)) {
    if (after.title !== before.title) field('Título', after.title, before.title);
    if (after.templateText !== before.templateText) field('Contenido', after.templateText, before.templateText);
  }
  if (action.tool === 'ui_fill_variable' && typeof action.arguments.name === 'string') {
    const name = action.arguments.name;
    if (Object.hasOwn(after.values, name)) field(name, after.values[name], before.values[name]);
  }
  if (action.tool === 'ui_load_active_draft') for (const [name, value] of Object.entries(after.values)) {
    if (value !== before.values[name]) field(name, value, before.values[name]);
  }
  if (action.tool === 'ui_search_prompt' && typeof action.arguments.query === 'string') {
    field('Búsqueda comprobada', action.arguments.query); field('Resultados visibles', String(after.candidates.length));
  }
  if (action.tool === 'ui_show_compiled_prompt' && after.compiledReady) field('Vista previa', after.compiledText);
  if (action.tool === 'ui_execute_local' && after.responseComplete) field('Respuesta generada', after.response);
  if (action.tool === 'ui_open_provider_picker' && after.providerOpen) field('Destino seleccionado', after.providerId);
  if (action.tool === 'ui_navigate_library') field('Pantalla', 'Biblioteca');
  if (action.tool === 'commit_approved') field('Versión guardada', String(after.revision));
  update(action.runId, { ...entry, status: 'verified', updatedAt: new Date().toISOString(),
    subject: after.title ?? undefined, revision: after.revision ?? undefined, fields });
}
