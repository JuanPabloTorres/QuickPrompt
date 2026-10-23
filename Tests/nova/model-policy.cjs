// Actual HTTP admission/defaults over an owned synthetic database. No inference requests.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
assert.ok(process.env.MODEL_POLICY_API_BUILD, 'Provide the frozen owned API build.');
const build = path.resolve(root, process.env.MODEL_POLICY_API_BUILD);
assert.ok(build.startsWith(path.join(root, '.playwright') + path.sep) && fs.existsSync(build));
const temporary = path.join(root, '.playwright/model-policy-http-' + stamp), out = path.join(root, 'docs/evidence/model-policy-http-' + stamp);
fs.mkdirSync(temporary); fs.mkdirSync(out);
const origin = 'http://127.0.0.1:5264', headers = { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' };
const report = { status: 'RUNNING', scope: 'REAL_HTTP_MODEL_ADMISSION_NOT_INFERENCE_ACCURACY', syntheticOnly: true, checks: [] };
let server, log = '';
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2));
const check = name => { report.checks.push({ name, status: 'PASS' }); save(); console.log('PASS', name); };
async function raw(route, body, method = body ? 'POST' : 'GET', extra = {}) {
  return fetch(origin + '/api/v1' + route, { method, headers: { ...headers, ...extra }, body: body ? JSON.stringify(body) : undefined });
}
async function api(route, body, method) { const response = await raw(route, body, method); assert.ok(response.ok, route + ':' + response.status); return response.status === 204 ? null : response.json(); }
(async () => {
  save();
  server = spawn('dotnet', [build, '--QuickPrompt:Port=5264', '--QuickPrompt:DataPath=' + path.join(temporary, 'synthetic.sqlite'), '--QuickPrompt:Knowledge:Enabled=false'],
    { cwd: root, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', data => { log += data; }); server.stderr.on('data', data => { log += data; });
  const deadline = Date.now() + 20000;
  while (!log.includes('Now listening on: ' + origin)) {
    if (server.exitCode !== null) throw Error('Owned API exited.');
    if (Date.now() > deadline) throw Error('Owned API readiness timeout.');
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  for (const model of [undefined, 'qwen3:8b', 'qwen2.5:7b-instruct']) {
    const id = crypto.randomUUID(); const run = await api('/nova/live/observe', {
      runId: id, sessionId: crypto.randomUUID(), message: 'Observación sintética sin ejecución.', mode: 'LIVE', ...(model ? { model } : {}),
    });
    assert.equal(run.model, model ?? 'qwen3:8b'); assert.equal(run.mode, 'LIVE');
    assert.equal((await api('/nova/live/' + id)).run.model, run.model);
    await api('/nova/live/' + id + '/cancel', {});
  }
  check('observation-retains-default-and-both-previously-admitted-models');
  for (const model of ['qwen2.5:7b-instruct', 'unknown:model']) {
    const id = crypto.randomUUID(); const response = await raw('/nova/live', {
      runId: id, sessionId: crypto.randomUUID(), message: 'Solicitud sintética.', mode: 'SAME_TAB', model,
    }, 'POST', { 'X-Nova-Surface': crypto.randomUUID() + crypto.randomUUID() });
    assert.equal(response.status, 400); assert.ok((await response.json()).detail.includes('Modelo no permitido'));
    assert.equal((await raw('/nova/live/' + id)).status, 404);
  }
  check('same-tab-rejects-unqualified-and-unknown-models-before-persisting-a-run');
  const invalidPrepare = await raw('/nova/prepare', { operationId: crypto.randomUUID(), message: 'Solicitud sintética.', model: 'unknown:model' });
  assert.equal(invalidPrepare.status, 400); assert.ok((await invalidPrepare.json()).detail.includes('Solicitud de preparación inválida'));
  check('preparation-rejects-unknown-model-before-stream-or-generation');
  const source = await api('/prompts', { title: 'QA · Política', templateText: 'Explica el procedimiento.' });
  const draft = await api('/nova/editor-drafts', { promptId: source.id, revision: source.revision, title: source.title,
    templateText: source.templateText, values: {}, sessionId: crypto.randomUUID() });
  assert.equal(draft.state, 'READY'); assert.equal(draft.compiledText, source.templateText);
  const before = await api('/library/export'), operationId = crypto.randomUUID();
  const execution = await raw('/nova/drafts/' + draft.id + '/execute', { confirmed: true, operationId, expectedText: draft.compiledText, model: 'unknown:model' });
  assert.equal(execution.status, 400); assert.ok((await execution.json()).detail.includes('Revisa y confirma el prompt final'));
  assert.deepEqual(await api('/library/export'), before); assert.equal((await raw('/nova/runs/' + operationId)).status, 404);
  check('execution-rejects-unknown-model-without-history-or-operation-write');
  report.status = 'PASS'; save(); console.log('EVIDENCE', out);
})().catch(error => { report.status = 'FAIL'; report.failure = String(error.stack); save(); console.error(error); process.exitCode = 1; }).finally(async () => {
  if (server && server.exitCode === null) { server.kill(); await new Promise(resolve => { server.once('exit', resolve); setTimeout(resolve, 3000).unref(); }); }
  fs.writeFileSync(path.join(out, 'api.log'), log);
});
