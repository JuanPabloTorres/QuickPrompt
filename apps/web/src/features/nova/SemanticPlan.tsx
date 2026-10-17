import type { z } from 'zod';
import type { semanticSchema } from '../../services/semantic';

const labels: Record<string, string> = { SEARCH_PROMPT: 'Buscar en tu biblioteca', RESOLVE_PROMPT: 'Comprobar el prompt de origen', OPEN_PROMPT: 'Abrir el prompt', PROPOSE_ADAPTATION: 'Preparar una adaptación', CREATE_DRAFT: 'Crear un borrador nuevo', VALIDATE_DRAFT: 'Comprobar la propuesta', FILL_VARIABLES: 'Completar tus datos', COMPILE_PROMPT: 'Preparar la vista previa', PREPARE_LOCAL_EXECUTION: 'Dejar listo para Qwen', EXECUTE_LOCAL: 'Ejecutar con Qwen local', PREPARE_PROVIDER: 'Preparar el destino', COPY_PROMPT: 'Dejar listo para copiar', EXPLAIN: 'Explicar las opciones', ASK_CLARIFICATION: 'Pedir el dato que falta', REQUEST_SAVE_VERSION: 'Pedir revisión para guardar versión', REQUEST_SAVE_COPY: 'Pedir revisión para guardar una copia', CANCEL: 'Cancelar la preparación', PAUSE: 'Pausar', RESUME: 'Continuar' };
export function SemanticPlan({ plan }: { plan?: z.infer<typeof semanticSchema> | null }) {
  if (!plan) return null;
  return <details className="semantic-plan"><summary>Lo que entendió NOVA</summary><p>{plan.explanation}</p><ol>{plan.proposedSteps.map(step => <li key={step.id}>{labels[step.operation] ?? 'Comprobar la solicitud'}</li>)}</ol>{plan.constraints.preserveOriginal && <p className="muted">El original se conserva. Tú decides qué guardar.</p>}</details>;
}
