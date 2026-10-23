// Exercises the real C# exporter and unchanged OpenAPI writer; no model/runtime calls.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict'), crypto = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const bridge = require(path.join(root, 'scripts/nova-contract-metadata.cjs'));
const generator = path.join(root, 'scripts/nova-openapi.cjs'), artifact = path.join(root, 'contracts/openapi/nova-v1.json');
const output = path.resolve(root, process.argv[2] || 'docs/evidence/contract-metadata-20261020/verification.json');
assert.ok(output.startsWith(path.join(root, 'docs/evidence/contract-metadata-20261020') + path.sep), 'Use an owned versioned evidence path.');
assert.ok(!fs.existsSync(output), 'Preserve evidence.');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const before = fs.readFileSync(artifact), metadata = bridge.load();
const checks = [], check = name => checks.push({ name, status: 'PASS' });
assert.equal(metadata.contractVersion, 1); assert.ok(metadata.models.some(model => model.name === metadata.defaultModel));
check('real-exporter-returns-production-policy-with-default-and-capabilities');
const clone = () => JSON.parse(JSON.stringify(metadata));
for (const mutate of [v => { v.contractVersion = 2; }, v => { v.defaultModel = 'not-in-policy'; },
  v => { v.models.push({ ...v.models[0] }); }, v => { v.models[0].thinking = 'invalid'; },
  v => { v.enums.NovaIntent.push(v.enums.NovaIntent[0]); }, v => { v.enums.Unknown = ['invented']; },
  v => { v.unexpected = true; }]) {
  const invalid = clone(); mutate(invalid); assert.throws(() => bridge.validate(invalid));
}
check('seven-malformed-metadata-variants-reject-without-changing-artifact'); assert.equal(hash(fs.readFileSync(artifact)), hash(before));
const load = bridge.load;
try {
  bridge.load = () => { throw Error('SYNTHETIC_EXPORT_FAILURE'); };
  assert.throws(() => require(generator), /SYNTHETIC_EXPORT_FAILURE/);
  assert.equal(hash(fs.readFileSync(artifact)), hash(before));
  check('actual-generator-stops-before-writing-when-export-boundary-fails');
} finally { bridge.load = load; }
require(generator);
const first = fs.readFileSync(artifact), doc = JSON.parse(first);
assert.deepEqual(doc.components.schemas.NovaRequest.properties.model.enum, metadata.models.map(model => model.name));
assert.deepEqual(doc.components.schemas.PrepareInput.properties.model.enum, metadata.models.map(model => model.name));
assert.deepEqual(doc.components.schemas.LiveStart.properties.model.enum, metadata.models.map(model => model.name));
assert.equal(doc.components.schemas.LiveStart.properties.model.default, metadata.defaultModel);
assert.deepEqual(doc.components.schemas.NovaAction.properties.intent.enum, metadata.enums.NovaIntent);
assert.deepEqual(doc.components.schemas.PreparedDraft.properties.decision.enum, metadata.enums.PreparationDecision);
check('five-schema-positions-and-live-default-match-real-csharp-metadata');
delete require.cache[generator]; require(generator); const second = fs.readFileSync(artifact);
assert.equal(hash(first), hash(second)); assert.equal(hash(before), hash(second));
const original = fs.readFileSync(path.join(root, '.playwright/contract-metadata-20261020/before/contracts/openapi/nova-v1.json'));
assert.equal(hash(original), hash(second)); check('two-generations-are-byte-identical-to-original-public-contract');
// Substitution proves every schema consumes its metadata owner, instead of only
// comparing currently equal literals. This synthetic policy is never installed.
try {
  const alternate = bridge.validate({ contractVersion: 1, defaultModel: 'synthetic-model',
    models: [{ name: 'synthetic-model', thinking: false }],
    enums: { NovaIntent: ['SYNTHETIC_INTENT'], PreparationDecision: ['SYNTHETIC_DECISION'] } });
  bridge.load = () => alternate;
  delete require.cache[generator]; require(generator);
  const swapped = JSON.parse(fs.readFileSync(artifact));
  for (const name of ['NovaRequest', 'PrepareInput', 'LiveStart'])
    assert.deepEqual(swapped.components.schemas[name].properties.model.enum, ['synthetic-model']);
  assert.equal(swapped.components.schemas.LiveStart.properties.model.default, alternate.defaultModel);
  assert.deepEqual(swapped.components.schemas.NovaAction.properties.intent.enum, alternate.enums.NovaIntent);
  assert.deepEqual(swapped.components.schemas.PreparedDraft.properties.decision.enum, alternate.enums.PreparationDecision);
  check('substituted-metadata-reaches-all-five-schema-positions-and-default');
} finally {
  bridge.load = load;
  delete require.cache[generator]; require(generator);
  assert.equal(hash(fs.readFileSync(artifact)), hash(original));
}
const project = fs.readFileSync(path.join(root, 'backend/QuickPrompt.ContractExport/QuickPrompt.ContractExport.csproj'), 'utf8');
assert.ok(project.includes('QuickPrompt.Application') && !project.includes('QuickPrompt.Infrastructure') && !project.includes('QuickPrompt.Api') && !project.includes('QuickPrompt.Evaluation'));
const assets = JSON.parse(fs.readFileSync(path.join(root, 'backend/QuickPrompt.ContractExport/obj/project.assets.json'), 'utf8'));
assert.deepEqual(Object.keys(assets.libraries).sort(), ['QuickPrompt.Application/1.0.0', 'QuickPrompt.Domain/1.0.0']);
check('exporter-depends-only-on-application-and-domain-without-provider-or-external-packages');
fs.writeFileSync(output, JSON.stringify({ status: 'PASS', scope: 'REAL_METADATA_EXPORT_AND_OPENAPI_GENERATION_NOT_MODEL_SEMANTICS', checks,
  metadata, contractSHA256: hash(second), originalContractSHA256: hash(original), publicContractChanged: false,
  operations: Object.values(doc.paths).reduce((sum, methods) => sum + Object.keys(methods).length, 0), modelDiscoveryOrInference: 'NOT_REQUESTED',
  incidents: ['Initial source-edit assertion stopped before editing because the two declarations had different source forms.',
    'The first static boundary check found a third model list and default in LiveStart. Original failure retained; that declaration now consumes the same owner and a substitution test covers every position.'] }, null, 2));
console.log(JSON.stringify({ status: 'PASS', checks: checks.length, malformedInputs: 7, publicContractChanged: false, artifactSHA256: hash(second) }));
