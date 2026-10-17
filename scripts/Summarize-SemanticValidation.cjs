// Exports measured results. Missing/failed gates never become an acceptance claim.
const fs = require('node:fs'), path = require('node:path'), {createHash}=require('node:crypto');
const root = path.resolve(__dirname, '..');
const reportDirectory = path.resolve(process.argv[2] || 'docs/evidence/semantic/final-independent');
const matrixDirectories = process.argv.slice(3).map(p => path.resolve(p));
const read = p => fs.existsSync(p) ? JSON.parse(fs.readFileSync(p, 'utf8')) : null;
const relative = p => path.relative(root, p).replaceAll('\\', '/');
const reports = ['development', 'validation', 'holdout'].map(split => {
  const file = path.join(reportDirectory, split + '.json'), r = read(file);
  return r ? { split, evidence: relative(file), reportSha256: createHash('sha256').update(fs.readFileSync(file)).digest('hex'), datasetSha256: r.datasetSha256, status: r.status, count: r.count, semanticAccuracy: r.semanticAccuracy,
    modelDigest: r.modelDigest, runtimeHashes: r.runtimeHashes, validPlanRate: r.validPlanRate, criticalErrors: r.criticalErrors, metamorphicEquivalence: r.metamorphicEquivalence,
    p50Ms: r.p50Ms, p95Ms: r.p95Ms, modelCalls: r.modelCalls, metrics: r.metrics,
    failures: r.rows.filter(x => !x.correct || x.critical).map(x => ({ id: x.id, family: x.family, input: x.message, actual: x.interpretation?.intentions, error: x.error })) }
    : { split, status: 'NOT_RUN' };
});
const matrices = matrixDirectories.map(directory => ({ directory, data: read(path.join(directory, 'matrix.json')), environment: read(path.join(directory, 'environment.json')) }));
const rows = matrices.flatMap(m => (m.data?.rows ?? []).map(row => ({ ...row, evidence: relative(m.directory) })));
const required = ['compound-adaptation', 'read-working-draft', 'contextual-paraphrase', 'explicit-human-save', 'post-save-context', 'read-only', 'fill-authorized-values',
  'prepare-is-not-run', 'explicit-execution', 'ambiguity', 'selected-search-candidate', 'correction-to-other-result', 'pending-source-question', 'pending-task-continued', 'unsaved-editor-context',
  'unsaved-copy-context', 'stale-save-rejected', 'visible-pending-source-question', 'visible-pending-task-continued', 'visible-adaptation', 'visible-change-before-execution'];
const missing = required.filter(name => !rows.some(r => r.name === name && r.status === 'PASS'));
const checked = read(path.join(reportDirectory, 'checked-summary.json'));
const runtimeMatches = checked?.runtimeHashes && matrices.every(m => m.environment?.model?.digest === checked.modelDigest && ['QuickPrompt.Infrastructure.dll','QuickPrompt.Application.dll','QuickPrompt.Domain.dll'].every(name => /^[a-f0-9]{64}$/i.test(checked.runtimeHashes[name] ?? '') && m.environment?.hashes?.[name]?.toLowerCase() === checked.runtimeHashes[name]?.toLowerCase()));
const reportHashesMatch = checked?.reports?.length === 3 && reports.every(r => checked.reports.find(c => c.split === r.split)?.reportSha256 === r.reportSha256);
const accepted = reportHashesMatch && runtimeMatches && checked?.status === 'PASS' && reports.every(r => r.status === 'PASS') && missing.length === 0
  && matrices.length > 0 && matrices.every(m => m.data?.status === 'PASS' && m.data.headed === true && m.data.errors.length === 0);
const summary = { reportHashesMatch: !!reportHashesMatch, runtimeMatches: !!runtimeMatches, status: accepted ? 'PASS' : 'NOT_ACCEPTED', datasetSha256: checked?.datasetSha256 ?? read(path.join(reportDirectory, 'development.json'))?.datasetSha256,
  reports, relations: checked?.relations ?? [], headed: matrices.map(m => ({ evidence: relative(m.directory), status: m.data?.status ?? 'NOT_RUN',
    error: m.data?.error, environment: m.environment, cases: m.data?.rows.map(r => ({ name: r.name, status: r.status })) ?? [] })), missingHeadedCases: missing,
  limits: ['Measured synthetic fixtures on the installed local Qwen3 model; no population-accuracy guarantee.',
    'Controlled fault tests are separate from real-model semantic quality.', 'No installations, private library mutations or publication.',
    'Headed mode operates saved prompts; independent new drafts use the main canvas.'] };
fs.writeFileSync(path.join(root, 'docs/nova/SEMANTIC_VALIDATION_SUMMARY.json'), JSON.stringify(summary, null, 2) + '\n');
const escape = value => String(value ?? '—').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('|', '\\|').replaceAll('\n', '<br>');
function context(row) {
  const c = row.context;
  if (!c) return typeof row.interpretation === 'string' ? 'Control tipado de UI/API' : 'Biblioteca sintética; ver traza real';
  return JSON.stringify({ currentSubject: c.currentSubject, sourceProposal: c.sourceProposal, pendingPlan: c.pendingPlan,
    sourceId: c.sourceId, revision: c.revision, activeDraft: c.currentSubject?.workingDraftExists });
}
function interpretation(row) {
  if (typeof row.interpretation === 'string') return row.interpretation;
  const actual = [row.qwenSourceRole?.sourceRole, row.qwenTextGoal?.requestType ?? row.qwenTextGoal?.resultKind, row.qwenTextGoal?.authoringRequestEvidence ? 'userEvidence='+JSON.stringify(row.qwenTextGoal.authoringRequestEvidence) : null, row.qwenArtifactIdentity ? 'additionalArtifact='+row.qwenArtifactIdentity.additionalArtifactRequested : null, row.qwenObjectives?.access, row.qwenObjectives?.execution].filter(Boolean).join('; ');
  return actual || row.interpretation?.explanation || 'Control tipado; no se atribuye una decisión a Qwen';
}
const table = rows.map(r => [r.input, context(r), interpretation(r), (r.plan ?? []).map(s => `${s.id}:${s.operation}←[${s.dependsOn}]`).join('; '),
  (r.tools ?? []).map(t => `${t.tool ?? t.toolName}:${t.outcome ?? 'ver traza'}`).join('; '),
  JSON.stringify({ state: r.result?.state, sourceId: r.result?.sourceId ?? r.result?.promptId ?? r.result?.id, revision: r.result?.sourceRevision ?? r.result?.revision,
    templateText: r.result?.templateText, localResponse: r.result?.localResponse ?? r.result?.response, httpStatus: r.result?.httpStatus }),
  `${r.status}: ${(r.validation ?? []).join('; ')} (${r.evidence})`].map(escape).join(' | '));
fs.writeFileSync(path.join(root, 'docs/nova/SEMANTIC_MATRIX.md'), `# Matriz de verificación semántica\n\nEstado medido: **${summary.status}**. La matriz conserva resultados reales; no convierte una prueba incompleta en aceptación.\n\n` +
  '| Entrada | Contexto | Interpretación Qwen / control tipado | Plan y dependencias | Herramientas | Resultado real | Validación |\n|---|---|---|---|---|---|---|\n' +
  table.map(line => '| ' + line + ' |').join('\n') + '\n\nCasos requeridos pendientes: ' + (missing.join(', ') || 'ninguno') + '.\n');
console.log(summary.status, 'reports:', reports.map(r => `${r.split}=${r.status}`).join(', '), 'headed rows:', rows.length);
if (!accepted) process.exitCode = 1;
