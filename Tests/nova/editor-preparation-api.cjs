// Same HTTP contract matrix against two owned builds; no inference or personal data.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict'), crypto = require('node:crypto');
const { spawn } = require('node:child_process');
assert.ok(process.env.EDITOR_BASELINE_BUILD && process.env.EDITOR_CANDIDATE_BUILD && process.env.NOVA_TEST_WEBROOT, 'Provide owned API builds and frozen web assets.');
const root = path.resolve(__dirname, '../..'), stamp = Date.now(), origin = 'http://127.0.0.1:5274';
const out = path.join(root, 'docs/evidence/editor-preparation-http-' + stamp);
fs.mkdirSync(out, { recursive: true });
const headers = { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' };
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const report = { status: 'RUNNING', scope: 'EDITOR_HTTP_COMPATIBILITY_NOT_AGENT_ACCEPTANCE', syntheticOnly: true, inference: false, profiles: {} };
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2));
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
async function raw(route, body, method = body ? 'POST' : 'GET') {
  const response = await fetch(origin + '/api/v1' + route, { method, headers, body: body ? JSON.stringify(body) : undefined });
  return { status: response.status, body: response.status === 204 ? null : await response.json() };
}
async function api(route, body) { const result = await raw(route, body); assert.equal(result.status, route === '/prompts' && body ? 201 : 200, route); return result.body; }
async function run(name, entry) {
  const build = path.resolve(root, entry), temporary = path.join(root, '.playwright/editor-preparation-http-' + stamp + '-' + name);
  fs.mkdirSync(temporary); let log = '';
  const child = spawn('dotnet', [build, '--QuickPrompt:Port=5274', '--QuickPrompt:DataPath=' + path.join(temporary, 'synthetic.sqlite'),
    '--QuickPrompt:WebRoot=' + path.resolve(root, process.env.NOVA_TEST_WEBROOT), '--QuickPrompt:Ollama=http://127.0.0.1:1'], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  child.stdout.on('data', data => { log += data; }); child.stderr.on('data', data => { log += data; });
  const rows = []; report.profiles[name] = { apiSha256: hash(build), rows }; save();
  try {
    let ready = false;
    for (let i = 0; i < 200; i++) { assert.equal(child.exitCode, null, 'Owned API exited'); if (log.includes('Now listening on: ' + origin)) { ready = true; break; } await sleep(100); }
    assert.ok(ready, 'Owned API did not start');
    const source = await api('/prompts', { title: 'Origen sintético', templateText: 'Explica <tema> a <audiencia>.', description: 'Fuente real' });
    const archived = await api('/prompts', { title: 'Origen archivado', templateText: 'Explica <tema>.' });
    await api('/prompts/' + archived.id + '/archive', { revision: archived.revision });
    const aliases = new Map([[source.id, 'SOURCE'], [archived.id, 'ARCHIVED']]);
    function normalized(value) {
      if (Array.isArray(value)) return value.map(normalized);
      if (value && typeof value === 'object') return Object.fromEntries(Object.entries(value).filter(([key]) => !['createdAt', 'updatedAt', 'traceId', 'correlationId'].includes(key)).map(([key, item]) => [key, normalized(item)]));
      if (typeof value === 'string') { for (const [id, alias] of aliases) value = value.replaceAll(id, alias); }
      return value;
    }
    const base = { promptId: null, revision: null, title: 'Borrador explícito', templateText: 'Explica <tema>.', values: { tema: 'α\nβ <literal> 🚀' }, sessionId: '11000000-0000-4000-8000-000000000001' };
    const missingId = '99000000-0000-4000-8000-000000000009';
    const cases = [
      ['preview-complete', '/templates/compile', { templateText: base.templateText, values: base.values }, 200],
      ['preview-missing', '/templates/compile', { templateText: '<tema> para <audiencia>', values: { tema: ' ' } }, 200],
      ['preview-unknown-value', '/templates/compile', { templateText: '<tema>', values: { otro: 'Dato' } }, 400],
      ['draft-new-complete', '/nova/editor-drafts', base, 200],
      ['draft-new-missing', '/nova/editor-drafts', { ...base, values: {} }, 200],
      ['draft-without-session', '/nova/editor-drafts', { ...base, sessionId: null }, 200],
      ['draft-existing-source', '/nova/editor-drafts', { ...base, promptId: source.id, revision: source.revision }, 200],
      ['draft-id-only', '/nova/editor-drafts', { ...base, promptId: source.id }, 400],
      ['draft-revision-only', '/nova/editor-drafts', { ...base, revision: 1 }, 400],
      ['draft-empty-session', '/nova/editor-drafts', { ...base, sessionId: '00000000-0000-0000-0000-000000000000' }, 400],
      ['draft-unknown-value', '/nova/editor-drafts', { ...base, values: { otro: 'Dato' } }, 400],
      ['draft-null-values', '/nova/editor-drafts', { ...base, values: null }, 400],
      ['draft-long-template', '/nova/editor-drafts', { ...base, templateText: 'x'.repeat(32001), values: {} }, 400],
      ['draft-long-value', '/nova/editor-drafts', { ...base, values: { tema: 'x'.repeat(8001) } }, 400],
      ['draft-stale-revision', '/nova/editor-drafts', { ...base, promptId: source.id, revision: source.revision + 1 }, 409],
      ['draft-archived-source', '/nova/editor-drafts', { ...base, promptId: archived.id, revision: archived.revision + 1 }, 409],
      ['draft-missing-source', '/nova/editor-drafts', { ...base, promptId: missingId, revision: 1 }, 404],
      ['draft-invalid-title', '/nova/editor-drafts', { ...base, title: '' }, 400],
    ];
    let prepared;
    for (const [label, route, input, expectedStatus] of cases) {
      const response = await raw(route, input); assert.equal(response.status, expectedStatus, label + ':' + JSON.stringify(response.body));
      if (route === '/nova/editor-drafts' && response.status === 200) aliases.set(response.body.id, 'PREPARED:' + label);
      if (label === 'draft-new-complete') { prepared = response.body; assert.equal(prepared.compiledText, 'Explica α\nβ <literal> 🚀.'); }
      if (label === 'preview-missing') assert.deepEqual(response.body, { text: null, missingVariables: ['tema', 'audiencia'] });
      rows.push({ label, ...response, body: normalized(response.body) }); save();
    }
    assert.deepEqual(await api('/nova/drafts/' + prepared.id), prepared); rows.push({ label: 'prepared-snapshot-can-be-read', status: 200 });
    assert.deepEqual(await api('/nova/drafts'), []); rows.push({ label: 'content-not-implicitly-remembered', status: 200 });
    const unchanged = await api('/prompts/' + source.id); assert.equal(unchanged.templateText, source.templateText); assert.equal(unchanged.revision, source.revision);
    rows.push({ label: 'source-not-saved-or-revised-by-preparation', status: 200 }); save();
    assert.equal(hash(build), report.profiles[name].apiSha256);
  } finally {
    if (child.exitCode === null) { child.kill(); await new Promise(resolve => child.once('exit', resolve)); }
    fs.writeFileSync(path.join(out, name + '-api.log'), log);
  }
}
(async () => {
  await run('before', process.env.EDITOR_BASELINE_BUILD); await run('after', process.env.EDITOR_CANDIDATE_BUILD);
  assert.deepEqual(report.profiles.after.rows, report.profiles.before.rows, 'HTTP behavior changed');
  report.status = 'PASS'; report.timestampNormalization = 'Only createdAt/updatedAt/traceId/correlationId and fixture UUID aliases differ';
  save(); console.log('PASS', out);
})().catch(error => { report.status = 'FAIL'; report.error = error.message; save(); console.error(error); process.exitCode = 1; });
