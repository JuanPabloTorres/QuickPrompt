// Read-only release ledger check. --write-report renders its companion Markdown.
// No network, model calls, installations, product mutations, or automatic status changes.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const matrixPath = path.join(root, 'docs/nova/VERSION_CLOSURE_MATRIX.json');
const outputPath = path.join(root, 'docs/nova/VERSION_CLOSURE_MATRIX.md');
const matrix = JSON.parse(fs.readFileSync(matrixPath, 'utf8').replace(/^\uFEFF/, ''));
const failures = [], ids = new Set(), counts = Object.fromEntries(Object.keys(matrix.statuses).map(status => [status, 0]));
if (!['NO_CERRADA', 'LISTA_PARA_REVISION', 'CERRADA'].includes(matrix.releaseState)) failures.push('Estado de versión desconocido.');
if (matrix.columns.join(',') !== 'id,area,requirement,status,priority,evidence,observed,closeWhen') failures.push('Columnas desconocidas.');
for (const fact of matrix.verificationFacts ?? []) if (!fact.text || !Object.hasOwn(matrix.sources, fact.source)) failures.push('Resumen de verificación sin texto o fuente válida.');
for (const row of matrix.rows) {
  if (!Array.isArray(row) || row.length !== matrix.columns.length) { failures.push('Fila incompleta.'); continue; }
  const [id, area, requirement, status, priority, evidence, observed, closeWhen] = row;
  if (!/^[A-Z]+-\d{2}$/.test(id) || ids.has(id)) failures.push('ID inválido/duplicado: ' + id);
  ids.add(id);
  if (!Object.hasOwn(counts, status)) failures.push('Estado inválido: ' + id);
  else counts[status]++;
  if (![area, requirement, observed, closeWhen].every(value => typeof value === 'string' && value.trim())) failures.push('Contrato vacío: ' + id);
  if (!['Bloqueante', 'Alta', 'Media', 'Baja'].includes(priority)) failures.push('Prioridad inválida: ' + id);
  if (!Array.isArray(evidence) || !evidence.length) failures.push('Sin fuente: ' + id);
  else for (const source of evidence) if (!Object.hasOwn(matrix.sources, source)) failures.push('Fuente desconocida: ' + id + '/' + source);
}
for (const [key, relative] of Object.entries(matrix.sources)) {
  const resolved = path.resolve(root, relative);
  if (!resolved.startsWith(root + path.sep) || !fs.existsSync(resolved)) failures.push('Fuente no disponible localmente: ' + key + ' -> ' + relative);
}
const open = matrix.rows.filter(row => row[3] !== 'CERRADO');
if (!matrix.rows.length || !ids.has('REL-10')) failures.push('La matriz no puede estar vacía ni omitir el acta de aceptación.');
if (matrix.releaseState !== 'NO_CERRADA' && open.length) failures.push('No se permite declarar lista/cerrada una versión con requisitos abiertos.');
const state = open.length ? 'NO_CERRADA' : matrix.releaseState === 'CERRADA' ? 'CERRADA' : 'LISTA_PARA_REVISION';
const summary = { date: matrix.reviewDate, status: state, requirements: matrix.rows.length, counts,
  open: open.length, openBlockers: open.filter(row => row[4] === 'Bloqueante').map(row => row[0]),
  matrixValid: failures.length === 0, errors: failures };
const esc = value => String(value).replaceAll('|', '\\|').replaceAll('\n', ' ');
const link = (relative, title) => `[${title}](${path.relative(path.dirname(outputPath), path.resolve(root, relative)).replaceAll('\\', '/')})`;
if (process.argv.includes('--write-report')) {
  const lines = [
    '# Matriz obligatoria de cierre — QuickPrompt NOVA', '',
    `Fecha: ${matrix.reviewDate}, ${matrix.timezone}. **Estado: ${state.replaceAll('_', ' ')}.** Modo ${matrix.mode}: la evidencia de cada actualización se detalla debajo; no implica cierre global.`, '',
    '**Regla del usuario:** no dar esta versión por completada ni cerrada mientras quede algún requisito obligatorio abierto. Todos los registros de esta matriz son obligatorios; la prioridad sólo ordena el trabajo. No se excluye alcance ni se rebajan criterios para conseguir un resultado verde.', '',
    matrix.scope, '',
    `**${matrix.rows.length} requisitos: ${counts.CERRADO} cerrados en su alcance probado y ${open.length} abiertos.** No es un porcentaje de fiabilidad ni una estimación de trabajo restante. Los abiertos se distribuyen en ${counts.FALLA} fallos observados, ${counts.PARCIAL} parciales, ${counts.PENDIENTE} pendientes y ${counts.SIN_VERIFICAR} sin verificar.`, '',
    '## Qué significa cerrado', '',
    'Un cierre acotado conserva el alcance y fecha de su evidencia. Los recorridos manuales probados no prueban el agente; los tests simulados no prueban comprensión de Qwen; un estado COMPLETED no demuestra cumplir la petición. Las pruebas históricas de otro build se identifican como tales y deben repetirse mediante REL-04 antes del cierre de versión. No se borra un fallo histórico al corregirlo: se añade evidencia posterior y se vincula al criterio original.', '',
    ...Object.entries(matrix.statuses).map(([status, meaning]) => `- **${status}:** ${meaning}`), '',
    'Los requisitos de paridad, privacidad y proveedores se incluyeron desde el SOW y la paridad pendiente; no se inventan como nuevas funcionalidades. Los que requieren decidir alcance permanecen abiertos hasta acuerdo explícito o implementación y validación. Datos privados y sesiones externas no quedan autorizados por esta matriz.', '',
    '## Auditoría de esta revisión', '',
    ...(matrix.verificationFacts ?? []).map(fact => `- ${fact.text} ${link(matrix.sources[fact.source], fact.source)}.`),
    '- Hallazgo de entrega: el CI inspeccionado verifica MAUI, no el conjunto API/React NOVA. El comprobador de esta matriz es local y no modifica ese CI.', '',
    `- Comprobador local verificado con seis controles: matriz abierta, cierre falso con pendientes, estado desconocido, falta de evidencia, ID duplicado y matriz vacía. ${link('docs/evidence/version-closure-20261019/checker-validation.json', 'Resultado del comprobador')}. Estas comprobaciones no evalúan el agente.`, '',
    '## Algoritmo de cierre', '',
    '```mermaid', 'flowchart TD',
    '    A[Inventariar requisitos y evidencia] --> B[Registrar estado, prioridad y criterio por ID]',
    '    B --> C{¿Hay requisitos obligatorios abiertos?}',
    '    C -- Sí --> D[NO CERRADA: corregir o verificar sin eliminar fallos]',
    '    D --> B',
    '    C -- No --> E[Revisar evidencia del mismo candidato congelado]',
    '    E --> F{¿Tests, UI, datos, seguridad y umbrales completos?}',
    '    F -- No --> G[Reabrir requisitos afectados]',
    '    G --> B',
    '    F -- Sí --> H[Revisión humana y acta de aceptación]', '```', '',
    '```text',
    'para cada requisito:',
    '    contrastar la petición original, implementación, ejecución y su alcance',
    '    registrar evidencia y criterio; ausencia de evidencia nunca es PASS',
    '    si hay un fallo relevante no cerrado: conservarlo y mantener abierto',
    'tras corregir: ejecutar regresión y añadir evidencia del mismo criterio',
    'si cambia código/config/modelo: reabrir o revalidar los requisitos afectados',
    'si algún obligatorio no está CERRADO: versión NO CERRADA',
    'antes del acta: congelar candidato y comprobar campaña integral + umbrales',
    'la aprobación de un borrador o un commit no sustituye aceptación de versión',
    '```', '',
    '## Matriz por área', ''
  ];
  for (const area of new Set(matrix.rows.map(row => row[1]))) {
    lines.push(`### ${area}`, '', '| ID / prioridad | Requisito | Estado | Evidencia y estado observado | Condición para cerrar o conservar cerrado |', '| --- | --- | --- | --- | --- |');
    for (const [id, , requirement, status, priority, evidence, observed, closeWhen] of matrix.rows.filter(row => row[1] === area))
      lines.push(`| ${id} · ${priority} | ${esc(requirement)} | **${status}** | ${esc(observed)} ${evidence.map(key => link(matrix.sources[key], key)).join(', ')} | ${esc(closeWhen)} |`);
    lines.push('');
  }
  lines.push('## Orden de trabajo y dependencias', '',
    '1. **Autoría e interpretación:** AUT-01/02/03/07 y AI-01/07. Resolver separación de órdenes actuales y tarea futura con contratos evaluables; no acumular modelos ni prompts experimentales rechazados.',
    '2. **Contexto y CRUD:** CTX-05/07, SAV-01/02/03/04, AI-02/05 y EXE-02. La creación fiel habilita probar aprobación/guardado real; el borrado requiere capacidad de dominio y confirmación, no una ruta por palabras.',
    '3. **Producto completo:** paridad PAR-01..06, copia VAR-04, UX, datos, proveedores y seguridad. Los éxitos manuales se conservan. Las decisiones de alcance se resuelven explícitamente y no se marcan como implementadas.',
    '4. **Calidad y recursos antes de optimizar:** PER-01/02/03 con condiciones y muestras comparables; rapidez de un rechazo o contenido incorrecto no cuenta como mejora.',
    '5. **Congelar y validar:** REL-03/04/05/06, desarrollo y validación antes del holdout. No inspeccionar ni ajustar el holdout para corregir el candidato; no reutilizar porcentajes históricos como actuales.',
    '6. **Entrega reproducible:** DAT-04/06, SEC-03/04 y REL-07/08/09; documentación vigente, backups, rollback, pipeline y piloto. Commit/push/publicación sólo si se solicitan.',
    '7. **Acta de cierre:** REL-10 después de cerrar y verificar los anteriores. Ninguna severidad baja ni limitación conocida se considera aceptada por silencio.', '',
    '## Umbrales e invariantes que no se rebajan', '',
    'Se conservan los umbrales de recovery-v1: intención ≥90%; planes válidos, destino, finalización y herramientas ≥95%; fidelidad de adaptación, éxito de tarea y equivalencia metamórfica ≥90%. Cero efectos no autorizados, IDs inventados aceptados, finalización prematura y contaminación. En un split de 16, ≥95% exige 16/16. Las dimensiones ausentes son NOT_TESTED, no 100%. El objetivo de latencia se congela tras tres baselines comparables, conforme al contrato existente.', '',
    'Las variantes de creación deben añadir criterios de título/idioma/entradas, restricciones futuras, controles actuales y datos reales frente a declaraciones; no sustituyen ni reetiquetan el corpus congelado. Una corrección necesita nuevos casos independientes de validación, no sólo las frases que fallaron.', '',
    '## Exclusiones explícitas existentes', '',
    ...matrix.explicitNonGoals.map(item => `- ${item.scope}. ${item.basis} ${link(matrix.sources[item.source], item.source)}.`), '',
    '## Cómo mantener y comprobar la matriz', '',
    `La fuente canónica es ${link('docs/nova/VERSION_CLOSURE_MATRIX.json', 'VERSION_CLOSURE_MATRIX.json')}. Cada cambio de estado requiere evidencia y motivo, conservando el historial en los informes y en Git cuando se solicite el commit. CERRADO no se asigna automáticamente a partir de un test count.`, '',
    '```powershell', 'node scripts/Check-VersionClosure.cjs --write-report', '```', '',
    'El comando valida IDs, estados, criterios y existencia local de fuentes; regenera este informe sólo con --write-report. Retorna **2 mientras haya requisitos abiertos**, **1 ante una matriz inválida**, y no cambia estados ni producto. La evidencia es local e ignorada por Git: en otro clon debe reproducirse o entregarse como artefacto revisado; la falta de archivos invalida la comprobación local, no demuestra un fallo del producto. Su salida no prueba fidelidad ni sustituye una revisión humana de las evidencias. No está conectado a CI ni puede impedir por sí solo que alguien publique.', '',
    `**Estado de esta revisión: ${state.replaceAll('_', ' ')}.** Se preservan los éxitos acotados y todos los bloqueos; ningún paso no ejecutado se declara aprobado.`, ''
  );
  fs.writeFileSync(outputPath, lines.join('\n'));
}
console.log(JSON.stringify(summary, null, 2));
process.exitCode = failures.length ? 1 : open.length ? 2 : 0;
