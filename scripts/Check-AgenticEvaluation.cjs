const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto'), assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..'), directory = path.resolve(root, process.argv[2] || 'docs/evidence/agentic-2/frozen-20261016');
const version2 = process.argv.includes('--v2');
const bytes = fs.readFileSync(path.join(root, version2 ? 'Tests/evaluation/agentic-2-v2.json' : 'Tests/evaluation/agentic-2-v1.json'));
const dataset = JSON.parse(bytes), thresholds = JSON.parse(fs.readFileSync(path.join(root, version2 ? 'Tests/evaluation/agentic-2-v2-thresholds.json' : 'Tests/evaluation/agentic-2-thresholds.json')));
const references = JSON.parse(fs.readFileSync(path.join(root, 'Tests/evaluation/agentic-2-reference-assertions.json')));
const preHoldout = process.argv.includes('--pre-holdout');
assert.equal(crypto.createHash('sha256').update(bytes).digest('hex').toUpperCase(), thresholds.datasetSha256);
const reports = [], checks = [], referenceChecks = [], resolvedPromptBindings = [];
function check(name, passed, actual, expected) { checks.push({ name, passed, actual, expected }); }
for (const split of preHoldout ? ['development', 'validation'] : ['development', 'validation', 'holdout']) {
  const report = JSON.parse(fs.readFileSync(path.join(directory, split + '.json'))), scenarios = dataset.scenarios.filter(c => c.split === split);
  assert.equal(report.datasetSha256, thresholds.datasetSha256); assert.equal(report.completed, scenarios.length); assert.equal(report.total, scenarios.length);
  assert.equal(new Set(report.results.map(r => r.id)).size, scenarios.length); assert.deepEqual(report.results.map(r => r.id).sort(), scenarios.map(c => c.id).sort());
  for (const metric of ['intentAccuracy', 'toolCallAccuracy', 'schemaValidity', 'variableResolutionAccuracy', 'taskCompletionRate', 'recallAt3', 'mrr']) check(split + '.' + metric, typeof report.metrics[metric] === 'number' && report.metrics[metric] >= thresholds[metric], report.metrics[metric], '>=' + thresholds[metric]);
  check(split + '.p95Ms', report.metrics.p95Ms <= thresholds.p95Ms, report.metrics.p95Ms, '<=' + thresholds.p95Ms);
  for (const metric of ['unauthorizedMutationCount', 'unauthorizedExternalSendCount', 'acceptedHallucinatedPromptIdCount', 'dataPreservation']) check(split + '.' + metric, report.metrics[metric] === thresholds[metric], report.metrics[metric], thresholds[metric]);
  for (const result of report.results) {
    check(result.id + '.budget', result.tools.length <= 5, result.tools.length, '<=5');
    check(result.id + '.preservation', result.dataPreserved && result.unauthorizedMutations === 0 && result.acceptedHallucinatedIds === 0, result.dataPreserved, true);
    const scenario = scenarios.find(c => c.id === result.id);
    const diagnostic = path.join(directory, split + '-synthetic-drafts', result.id + '.json');
    const draft = fs.existsSync(diagnostic) ? (() => { const value = JSON.parse(fs.readFileSync(diagnostic)); return value.revised || value.initial; })() : null;
    const createConcepts = version2 ? ['compar|contrast|evalu|elig|eleg|choose|seleccion|decid', 'empleo|oferta|laboral|job|offer'] : references.families.create;
    const concepts = scenario.requiredTemplateConcepts || references.templateConcepts[result.id] || (scenario.family === 'create' ? createConcepts : []);
    const assertions = concepts.map(pattern => ({ name: 'requested-concept:' + pattern, passed: !!draft && new RegExp(pattern, 'iu').test(draft.templateText) }));
    if (draft && scenario.expectedPromptIds.length) {
      const expectedIds = draft.candidates.filter(c => scenario.expectedPromptIds.some(key => references.fixtureTitles[key] === c.prompt.title)).map(c => c.prompt.id);
      resolvedPromptBindings.push({ id: result.id, split, expectedPromptIds: expectedIds, actualSourceId: draft.sourceId, sourceRevision: draft.sourceRevision });
      if (!['ambiguity', 'duplicate'].includes(scenario.family)) assertions.push({ name: 'expected-real-source', passed: expectedIds.length > 0 && expectedIds.includes(draft.sourceId) });
      if (draft.sourceId) assertions.push({ name: 'source-revision-bound', passed: draft.candidates.some(c => c.prompt.id === draft.sourceId && c.prompt.revision === draft.sourceRevision) });
    }
    if (scenario.family === 'known') { const literal = scenario.message.split(/texto:\s*/iu).at(-1); assertions.push({ name: 'exact-provided-value', passed: !!draft?.compiledText?.includes(literal) }); }
    if (scenario.family === 'missing') assertions.push({ name: 'missing-input-not-invented', passed: draft?.state === 'NEEDS_INFORMATION' && !draft.compiledText && draft.missingVariables.includes('texto') });
    if (['ambiguity', 'duplicate'].includes(scenario.family)) assertions.push({ name: 'real-candidate-disambiguation', passed: draft?.decision === 'ASK_CLARIFICATION' && draft.candidates.length >= 2 && !draft.sourceId });
    for (const assertion of assertions) { referenceChecks.push({ id: result.id, split, ...assertion }); check(result.id + '.' + assertion.name, assertion.passed, assertion.passed, true); }
  }
  const fts = report.retrievalBenchmarks;
  function retrieval(method) { const ranks = fts.map(r => r[method + 'Rank']); const times = fts.map(r => r[method + 'Ms']); return { count: ranks.length, recallAt3: ranks.length ? ranks.filter(r => r >= 1 && r <= 3).length / ranks.length : null, mrr: ranks.length ? ranks.reduce((n, r) => n + (r > 0 ? 1 / r : 0), 0) / ranks.length : null, meanMs: times.length ? times.reduce((a, b) => a + b, 0) / times.length : null }; }
  reports.push({ split, metrics: report.metrics, resourceSamples: report.resourceSamples, sampledPeakOllamaRamBytes: report.sampledPeakOllamaRamBytes, sampledPeakModelVramBytes: report.sampledPeakModelVramBytes, tokenUsage: report.tokenUsage, retrievalComparison: { lexical: retrieval('lexical'), fts5: retrieval('fts5'), limits: 'Raw request comparison on small synthetic fixtures; FTS index creation/update cost excluded. Production workflow recall may additionally use a bounded Qwen query interpretation.' }, misses: report.results.filter(r => !r.outcomeCorrect || !r.intentCorrect || !r.toolsCorrect).map(r => ({ id: r.id, intentCorrect: r.intentCorrect, toolsCorrect: r.toolsCorrect, outcomeCorrect: r.outcomeCorrect, error: r.error })) });
}
const summary = { status: checks.every(c => c.passed) ? 'PASS' : 'FAIL', phase: preHoldout ? 'pre-holdout' : 'complete', version: 'agentic-2-acceptance-v1', cases: reports.reduce((count, report) => count + dataset.scenarios.filter(c => c.split === report.split).length, 0), datasetSha256: thresholds.datasetSha256, thresholds, reports, referenceChecks, resolvedPromptBindings, failedChecks: checks.filter(c => !c.passed), checksCount: checks.length, limits: 'Acceptance of this frozen synthetic benchmark only. Real-browser acceptance and deterministic compiler regressions are separate mandatory checks. No commercial or remote-provider submission certification.' };
fs.writeFileSync(path.join(directory, preHoldout ? 'pre-holdout-summary.json' : 'acceptance-summary.json'), JSON.stringify(summary, null, 2));
console.log(JSON.stringify({ status: summary.status, cases: summary.cases, checks: checks.length, failedChecks: summary.failedChecks, reports: reports.map(r => ({ split: r.split, metrics: r.metrics, misses: r.misses })) }, null, 2));
if (summary.status !== 'PASS') process.exitCode = 1;
