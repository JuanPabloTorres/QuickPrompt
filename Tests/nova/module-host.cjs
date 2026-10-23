// Actual API host factory and React UI with absent/replaced inference ports.
// Synthetic module composition only: no LLM calls or semantic accuracy claims.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict'), http = require('node:http');
const { spawn } = require('node:child_process'), { createHash } = require('node:crypto');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
assert.ok(process.env.MODULE_TEST_BUILD && process.env.MODULE_TEST_WEB && process.env.MODULE_NORMAL_BUILD, 'Provide owned frozen API/evaluator and web assets.');
const build = path.resolve(root, process.env.MODULE_TEST_BUILD), web = path.resolve(root, process.env.MODULE_TEST_WEB);
const out = path.join(root, 'docs/evidence/module-host-ui-' + stamp); fs.mkdirSync(out);
const hash = p => createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const report = { status: 'RUNNING', scope: 'HOST_MODULE_SUBSTITUTION_REAL_HTTP_UI_NOT_MODEL_COMPREHENSION', syntheticOnly: true,
  builds: Object.fromEntries(['QuickPrompt.Api.dll', 'QuickPrompt.Evaluation.dll', 'QuickPrompt.Application.dll', 'QuickPrompt.Infrastructure.dll', 'QuickPrompt.Domain.dll'].map(file => [file, hash(path.join(path.dirname(build), file))])),
  webIndexSHA256: hash(path.join(web, 'index.html')), checks: [], responses: [], browserErrors: [], externalRequests: [] };
const processes = [], logs = []; let browser, context, page;
const headers = { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' };
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2));
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(read, timeout = 20000) { const end = Date.now() + timeout; while (Date.now() < end) { const value = await read(); if (value) return value; await sleep(100); } throw Error('MODULE_HOST_QA_TIMEOUT'); }
const check = name => { report.checks.push({ name, status: 'PASS' }); save(); console.log('PASS', name); };
async function start(mode, port) {
  const directory = path.join(root, '.playwright/module-host-ui-' + stamp + '-' + mode); fs.mkdirSync(directory);
  const entry = mode === 'normal' ? [path.resolve(root, process.env.MODULE_NORMAL_BUILD)] : [build, '--module-host', mode];
  const child = spawn('dotnet', [...entry, '--QuickPrompt:Port=' + port, '--QuickPrompt:DataPath=' + path.join(directory, 'synthetic.sqlite'), '--QuickPrompt:WebRoot=' + web, '--QuickPrompt:Knowledge:Enabled=false'], { cwd: root, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] }); processes.push(child);
  const log = { name: mode, text: '' }; logs.push(log); child.stdout.on('data', data => { log.text += data; }); child.stderr.on('data', data => { log.text += data; });
  await until(() => { if (child.exitCode !== null) throw Error('OWNED_MODULE_HOST_EXITED:' + mode); return log.text.includes('Now listening on: http://127.0.0.1:' + port); });
  return 'http://127.0.0.1:' + port;
}
async function raw(origin, route, body, method = body ? 'POST' : 'GET', supplied = headers) {
  return fetch(origin + '/api/v1' + route, { method, headers: supplied, body: body ? JSON.stringify(body) : undefined });
}
async function api(origin, route, body, method) { const response = await raw(origin, route, body, method); assert.ok(response.ok, route + ':' + response.status); return response.status === 204 ? null : response.json(); }
async function badHost(origin) {
  return new Promise((resolve, reject) => { const request = http.get(origin + '/health', { headers: { Host: 'outside.invalid' } }, response => { response.resume(); response.on('end', () => resolve(response.statusCode)); }); request.on('error', reject); request.setTimeout(5000, () => request.destroy(Error('LOCAL_HOST_CHECK_TIMEOUT'))); });
}
async function protections(origin, mode) {
  const health = await fetch(origin + '/health'); assert.equal(health.status, 200); assert.equal((await health.json()).status, 'ok');
  assert.ok(health.headers.get('content-security-policy').includes("frame-ancestors 'none'"));
  assert.equal(await badHost(origin), 403);
  assert.equal((await raw(origin, '/prompts', { title: 'Must not save', templateText: 'Owned negative test.' }, 'POST', { 'Content-Type': 'application/json' })).status, 403);
  assert.equal((await raw(origin, '/prompts', { title: 'Must not save', templateText: 'Owned negative test.' }, 'POST', { ...headers, Origin: 'https://outside.invalid' })).status, 403);
  assert.equal((await raw(origin, '/prompts', undefined, 'GET', { 'Sec-Fetch-Site': 'cross-site' })).status, 403);
  assert.equal((await api(origin, '/prompts')).total, 0); check(mode + ':actual-host-protections-remain-and-rejected-writes-have-no-effect');
}
async function crud(origin, mode) {
  context = await browser.newContext({ viewport: { width: 1440, height: 1000 } }); page = await context.newPage(); page.setDefaultTimeout(12000);
  page.on('pageerror', error => { report.browserErrors.push(String(error)); save(); });
  page.on('response', response => { if (response.url().startsWith(origin + '/api/v1/')) report.responses.push({ mode, path: new URL(response.url()).pathname, status: response.status() }); });
  await context.route('**/*', route => { const url = route.request().url(); if (url.startsWith(origin) || /^(about|data|blob):/.test(url)) return route.continue(); report.externalRequests.push(url); return route.abort(); });
  await page.goto(origin); await page.getByTestId('library-ready').waitFor(); await page.getByRole('button', { name: 'Nuevo prompt', exact: true }).click();
  const title = 'QA · Módulo ' + mode;
  await page.locator('#prompt-title').fill(title); await page.locator('#prompt-content').fill('Organiza <tema>.'); await page.getByRole('button', { name: 'Guardar prompt', exact: true }).click();
  await page.getByTestId('prompt-focus').waitFor(); let source = (await api(origin, '/prompts')).items[0]; assert.equal(source.title, title); assert.equal(source.revision, 1);
  check(mode + ':create-and-read-real-form-without-model-implementation');
  if (mode === 'disabled') {
    const before = await api(origin, '/library/export');
    await page.getByRole('button', { name: 'Abrir asistente NOVA', exact: true }).click(); await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).fill('Crea un prompt sintético para comprobar el módulo.');
    await page.getByRole('button', { name: 'Preparar prompt', exact: true }).click();
    await page.getByText('El módulo de inferencia está desactivado en esta prueba sintética.', { exact: true }).waitFor();
    assert.deepEqual(await api(origin, '/library/export'), before);
    assert.equal(await page.evaluate(() => sessionStorage.getItem('nova-live-run')), null);
    assert.ok(!report.responses.some(response => response.mode === mode && /\/(?:actions|events)(?:$|\/)/.test(response.path)), 'Absent run started a UI polling/event loop');
    await page.screenshot({ path: path.join(out, 'disabled-agent-visible-error.png') });
    await page.getByRole('button', { name: 'Cerrar panel de NOVA', exact: true }).click();
    check('disabled:visible-agent-error-leaves-prompt-and-version-data-unchanged');
  }
  await page.getByRole('button', { name: 'Editar', exact: true }).click(); await page.locator('#prompt-title').fill(title + ' revisado'); await page.locator('#prompt-content').fill('Compara <tema>.');
  await page.getByRole('button', { name: 'Guardar versión', exact: true }).click(); await page.getByTestId('prompt-focus').waitFor();
  await until(async () => (await api(origin, '/prompts/' + source.id)).revision === 2); source = await api(origin, '/prompts/' + source.id);
  assert.equal(source.templateText, 'Compara <tema>.'); assert.equal((await api(origin, '/prompts/' + source.id + '/versions')).length, 2);
  check(mode + ':update-through-ui-preserves-revision-and-both-versions');
  const beforeCancel = await api(origin, '/library/export'); await page.getByRole('button', { name: 'Eliminar prompt', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: '¿Eliminar este prompt?', exact: true }); await dialog.getByRole('button', { name: 'Cancelar', exact: true }).click();
  assert.deepEqual(await api(origin, '/library/export'), beforeCancel); await page.getByTestId('prompt-focus').waitFor();
  check(mode + ':cancel-delete-keeps-data-and-current-detail');
  await page.getByRole('button', { name: 'Eliminar prompt', exact: true }).click(); await dialog.getByRole('button', { name: 'Eliminar definitivamente', exact: true }).click();
  await until(async () => (await api(origin, '/prompts')).total === 0); assert.equal((await api(origin, '/library/export')).templates.length, 0);
  check(mode + ':confirmed-delete-through-ui-removes-only-reviewed-synthetic-prompt');
  await page.screenshot({ path: path.join(out, mode + '-crud-complete.png') }); await context.close(); context = null;
}
(async () => {
  browser = await chromium.launch({ channel: 'msedge', headless: false });
  const normal = await start('normal', 5260); await protections(normal, 'normal');
  let normalSource = await api(normal, '/prompts', { title: 'QA · Arranque normal', templateText: 'Explica un tema.' });
  assert.equal((await api(normal, '/prompts/' + normalSource.id)).revision, 1);
  normalSource = await api(normal, '/prompts/' + normalSource.id, { title: 'QA · Arranque normal revisado', templateText: 'Compara un tema.', revision: 1 }, 'PUT');
  assert.equal(normalSource.revision, 2); await api(normal, '/prompts/' + normalSource.id, { revision: 2, confirmed: true }, 'DELETE');
  assert.equal((await api(normal, '/prompts')).total, 0); check('normal:production-entry-point-serves-real-crud-without-test-hook');
  const disabled = await start('disabled', 5258); await protections(disabled, 'disabled');
  const unavailable = await raw(disabled, '/nova/semantic/interpret', { message: 'Petición sintética de composición.' }); assert.equal(unavailable.status, 503);
  assert.ok((await unavailable.json()).detail.includes('desactivado')); check('disabled:agent-endpoint-fails-explicitly-without-fallback');
  await crud(disabled, 'disabled');
  const probe = await start('probe', 5259); await protections(probe, 'probe');
  for (const message of ['Primera petición sintética.', 'Otra entrada para comprobar sustitución, no comprensión.']) {
    const plan = await api(probe, '/nova/semantic/interpret', { message }); assert.deepEqual(plan.intentions, ['ASK_CLARIFICATION']);
    assert.equal(plan.explanation, 'Módulo sintético: ¿qué información falta?'); assert.equal((await api(probe, '/prompts')).total, 0);
  }
  check('probe:existing-task-planner-consumes-replacement-interface-without-legacy-model');
  await crud(probe, 'probe');
  assert.deepEqual(report.browserErrors, []); assert.deepEqual(report.externalRequests, []);
  assert.ok(report.responses.every(response => response.status < 500 || response.status === 503), 'Unexpected host/legacy construction failure');
  report.status = 'PASS'; save(); console.log('EVIDENCE', out);
})().catch(async error => { report.status = 'FAIL'; report.failure = String(error.stack); if (page && !page.isClosed()) { await page.screenshot({ path: path.join(out, 'failure.png') }).catch(() => {}); report.visibleText = await page.locator('body').innerText().catch(() => ''); } save(); console.error(error); process.exitCode = 1; }).finally(async () => {
  await context?.close().catch(() => {}); await browser?.close().catch(() => {});
  for (const child of processes) if (child.exitCode === null) { child.kill(); await new Promise(resolve => { child.once('exit', resolve); setTimeout(resolve, 3000).unref(); }); }
  for (const log of logs) fs.writeFileSync(path.join(out, log.name + '.log'), log.text);
});
