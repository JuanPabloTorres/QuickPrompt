// Deterministic lifecycle through real HTTP/SQLite; no model or personal database.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict'), crypto = require('node:crypto');
const { spawn } = require('node:child_process');
assert.ok(process.env.NOVA_TEST_BUILD && process.env.NOVA_TEST_WEBROOT, 'Provide owned frozen API and web assets.');
const root = path.resolve(__dirname, '../..'), stamp = Date.now(), origin = 'http://127.0.0.1:5276';
const out = path.join(root, 'docs/evidence/prepared-draft-lifecycle-' + stamp), temp = path.join(root, '.playwright/prepared-draft-lifecycle-' + stamp);
fs.mkdirSync(out); fs.mkdirSync(temp);
const build = path.resolve(root, process.env.NOVA_TEST_BUILD), web = path.resolve(root, process.env.NOVA_TEST_WEBROOT);
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const report = { status: 'RUNNING', scope: 'PREPARED_DRAFT_HTTP_SQLITE_NOT_AGENT_ACCEPTANCE', inference: false, syntheticOnly: true,
  apiSha256: hash(build), webIndexSha256: hash(path.join(web, 'index.html')), checks: [] };
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2));
const checked = name => { report.checks.push(name); save(); };
let child, log = '', stage = 'startup';
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
async function api(route, body, expected = 200, method = body ? 'POST' : 'GET') {
  stage = route;
  const response = await fetch(origin + '/api/v1' + route, { method, headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' }, body: body ? JSON.stringify(body) : undefined });
  const text = await response.text(), value = text ? JSON.parse(text) : null;
  if (response.status !== expected) report.lastResponse = { route, expected, actual: response.status, body: value };
  assert.equal(response.status, expected, route + ': expected ' + expected + ', got ' + response.status);
  return value;
}
(async () => {
  child = spawn('dotnet', [build, '--QuickPrompt:Port=5276', '--QuickPrompt:DataPath=' + path.join(temp, 'synthetic.sqlite'), '--QuickPrompt:WebRoot=' + web,
    '--QuickPrompt:Ollama=http://127.0.0.1:1'], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  child.stdout.on('data', data => { log += data; }); child.stderr.on('data', data => { log += data; });
  let ready = false;
  for (let i = 0; i < 200; i++) { assert.equal(child.exitCode, null, 'Owned API exited'); if (log.includes('Now listening on: ' + origin)) { ready = true; break; } await sleep(100); }
  assert.ok(ready); save();
  const source = await api('/prompts', { title: 'Origen real de versiones', templateText: 'Explica <tema> a <audiencia>.' }, 201);
  const original = await api('/nova/editor-drafts', { promptId: null, revision: null, title: 'Borrador de completar', templateText: 'Revisa <tema> para <audiencia>.',
    values: {}, sessionId: '11000000-0000-4000-8000-000000000001' });
  assert.equal(original.state, 'NEEDS_INFORMATION'); assert.deepEqual(original.missingVariables, ['tema', 'audiencia']);
  const firstId = crypto.randomUUID(), firstInput = { operationId: firstId, values: { tema: 'C# & .NET 🚀' } };
  const partial = await api('/nova/drafts/' + original.id + '/complete', firstInput);
  assert.equal(partial.state, 'NEEDS_INFORMATION'); assert.equal(partial.compiledText, null); assert.deepEqual(partial.missingVariables, ['audiencia']);
  assert.equal((await api('/nova/runs/' + firstId)).approval, 'SAVE_REQUIRES_REVIEW');
  checked('partial-values-compile-with-missing-fields-and-review-required');
  const sequence = (await api('/nova/live/' + firstId)).run.sequence;
  assert.deepEqual(await api('/nova/drafts/' + original.id + '/complete', firstInput), partial);
  assert.equal((await api('/nova/live/' + firstId)).run.sequence, sequence);
  await api('/nova/drafts/' + original.id + '/complete', { ...firstInput, values: { tema: 'Otro contenido' } }, 409);
  checked('same-operation-replay-is-idempotent-and-content-change-is-rejected');
  const fullId = crypto.randomUUID(), fullInput = { operationId: fullId, values: { audiencia: 'Equipo α' } };
  const full = await api('/nova/drafts/' + partial.id + '/complete', fullInput);
  assert.equal(full.state, 'READY'); assert.equal(full.compiledText, 'Revisa C# & .NET 🚀 para Equipo α.');
  assert.equal((await api('/nova/live/' + fullId)).run.status, 'COMPLETED');
  checked('remaining-values-complete-the-exact-literal-result-without-model');
  await api('/nova/drafts/' + fullId + '/save', { confirmed: false }, 400);
  assert.equal((await api('/prompts?size=12')).total, 1);
  const saved = await api('/nova/drafts/' + fullId + '/save', { confirmed: true });
  assert.equal(saved.templateText, full.templateText); assert.equal((await api('/prompts?size=12')).total, 2);
  assert.equal((await api('/nova/runs/' + fullId)).approval, 'APPROVED_COPY_OR_REUSE');
  assert.equal((await api('/nova/drafts/' + fullId + '/save', { confirmed: true })).id, saved.id);
  assert.equal((await api('/prompts?size=12')).total, 2);
  checked('save-needs-confirmation-and-replay-does-not-create-another-prompt');
  await api('/nova/drafts/' + fullId, undefined, 204, 'DELETE'); await api('/nova/drafts/' + fullId, undefined, 404);
  await api('/nova/drafts/' + partial.id + '/complete', fullInput, 409);
  assert.equal((await api('/nova/live/' + fullId)).run.status, 'COMPLETED');
  await api('/nova/drafts/' + fullId, undefined, 404);
  assert.equal((await api('/nova/drafts/' + fullId + '/save', { confirmed: true })).id, saved.id);
  checked('forgotten-result-with-retained-receipt-is-conflict-not-empty-success-or-reexecution');
  const versionDraft = await api('/nova/editor-drafts', { promptId: source.id, revision: source.revision, title: source.title,
    templateText: 'Resume <tema> ante <audiencia>.', values: { tema: 'Datos', audiencia: 'Equipo' } });
  const version = await api('/nova/drafts/' + versionDraft.id + '/save', { confirmed: true, updateSource: true });
  assert.equal(version.id, source.id); assert.equal(version.revision, source.revision + 1); assert.equal(version.templateText, versionDraft.templateText);
  assert.equal((await api('/nova/drafts/' + versionDraft.id + '/save', { confirmed: true, updateSource: true })).revision, version.revision);
  await api('/nova/drafts/' + versionDraft.id + '/save', { confirmed: true, updateSource: false }, 409);
  checked('version-save-respects-source-and-rejects-changing-the-approved-effect');
  await api('/nova/drafts/' + original.id + '/complete', { operationId: '00000000-0000-0000-0000-000000000000', values: {} }, 400);
  await api('/nova/drafts/99000000-0000-4000-8000-000000000009/complete', { operationId: crypto.randomUUID(), values: {} }, 404);
  assert.equal((await api('/prompts?size=12')).total, 2);
  checked('invalid-or-missing-completion-never-adds-a-prompt');
  assert.equal(hash(build), report.apiSha256); assert.equal(hash(path.join(web, 'index.html')), report.webIndexSha256);
  report.status = 'PASS'; save(); console.log('PASS', out);
})().catch(error => { report.status = 'FAIL'; report.stage = stage; report.error = error.message; save(); console.error(error); process.exitCode = 1;
}).finally(async () => { if (child?.exitCode === null) { child.kill(); await new Promise(resolve => child.once('exit', resolve)); } fs.writeFileSync(path.join(out, 'api.log'), log); });
