// Real UI -> same-tab agent -> receipts -> API/SQLite. Owned synthetic data only.
// Quick buttons provide explicit typed goals; this is not free-language/Qwen accuracy.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process'), { createHash } = require('node:crypto');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), stamp = Date.now(), temporary = path.join(root, '.playwright/ui-surface-' + stamp);
const out = path.join(root, 'docs/evidence/ui-surface-20261019', String(stamp));
const build = path.resolve(root, process.env.NOVA_TEST_BUILD || '.playwright/ui-surface-20261019/api-v3/QuickPrompt.Api.dll');
const web = path.resolve(root, process.env.NOVA_TEST_WEBROOT || '.playwright/ui-surface-20261019/web-v3');
const origin = 'http://127.0.0.1:5242', headers = { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' };
const realQwen = process.argv.includes('--real-qwen');
fs.mkdirSync(temporary, { recursive: true }); fs.mkdirSync(out, { recursive: true });
const hash = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const report = { status: 'RUNNING', syntheticOnly: true, modelInference: realQwen ? 'REAL_QWEN_LOCAL_EXECUTION_TYPED_GOAL' : 'NOT_REQUIRED_TYPED_QUICK_COMMANDS',
  hashes: Object.fromEntries(['QuickPrompt.Api.dll', 'QuickPrompt.Application.dll', 'QuickPrompt.Infrastructure.dll', 'QuickPrompt.Domain.dll'].map(name => [name, hash(path.join(path.dirname(build), name))])),
  webIndexSHA256: hash(path.join(web, 'index.html')), checks: [], runs: [], errors: [], externalRequests: [] };
let server, browser, page, log = '', runId;
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2));
const check = name => { report.checks.push({ name, status: 'PASS' }); save(); console.log('PASS', name); };
async function until(read, timeout = 25000) { const end = Date.now() + timeout; while (Date.now() < end) { const value = await read(); if (value) return value; await new Promise(resolve => setTimeout(resolve, 100)); } throw Error('QA_TIMEOUT'); }
async function api(route, body, method = body ? 'POST' : 'GET') {
  const response = await fetch(origin + '/api/v1' + route, { method, headers, body: body ? JSON.stringify(body) : undefined });
  assert.ok(response.ok, route + ':' + response.status); return response.status === 204 ? null : response.json();
}
async function assistant() { const open = page.getByRole('button', { name: 'Abrir asistente NOVA', exact: true }); if (await open.isVisible()) await open.click(); }
async function quick(name, submit, fill) {
  await assistant(); const target = page.getByRole('button', { name, exact: true });
  if (!await target.isVisible()) await page.getByRole('button', { name: 'Más acciones de NOVA', exact: true }).click();
  await target.click(); const dialog = page.getByRole('dialog', { name, exact: true }); await dialog.waitFor();
  await fill(dialog);
  const reply = page.waitForResponse(response => response.url() === origin + '/api/v1/nova/live' && response.request().method() === 'POST');
  await dialog.getByRole('button', { name: submit, exact: true }).click(); const created = await (await reply).json(); runId = created.runId;
  assert.ok(runId); const view = await until(async () => { const current = await api('/nova/live/' + runId); return ['COMPLETED', 'BLOCKED', 'FAILED', 'CANCELLED', 'AWAITING_APPROVAL'].includes(current.run.status) ? current : null; }, 60000);
  report.runs.push({ runId, action: name, status: view.run.status, errorCode: view.run.errorCode, question: view.question, observation: view.observation }); save();
  assert.equal(view.run.status, 'COMPLETED', JSON.stringify({ action: name, status: view.run.status, error: view.run.errorCode, question: view.question }));
  runId = null; return view;
}
(async () => {
  server = spawn('dotnet', [build, '--QuickPrompt:Port=5242', '--QuickPrompt:DataPath=' + path.join(temporary, 'synthetic.sqlite'), '--QuickPrompt:WebRoot=' + web, ...JSON.parse(process.env.NOVA_AGENT_OPTIONS || '[]')], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', bytes => { log += bytes; }); server.stderr.on('data', bytes => { log += bytes; });
  await until(() => { if (server.exitCode !== null) throw Error('API_EXITED'); return log.includes('Now listening on: ' + origin); });
  assert.equal((await api('/prompts')).total, 0);
  const source = await api('/prompts', { title: 'QA · Superficie de trabajo', templateText: 'Explica <tema> y propone dos acciones. No inventes datos.' });
  const other = await api('/prompts', { title: 'QA · Otro registro', templateText: 'Texto independiente.' });
  const local = realQwen ? await api('/prompts', { title: 'Resultado breve', templateText: 'Responde exactamente LISTO, sin otros caracteres.' }) : null;
  const original = await api('/prompts?size=100');
  browser = await chromium.launch({ channel: 'msedge', headless: false });
  const context = await browser.newContext({ viewport: { width: 1366, height: 950 } }); page = await context.newPage();
  page.on('pageerror', error => { report.errors.push(String(error)); save(); });
  page.on('request', request => { if (!request.url().startsWith(origin) && !request.url().startsWith('data:')) report.externalRequests.push(request.url()); });
  await page.goto(origin); await page.bringToFront();
  for (const view of ['cards', 'rows']) {
    if (view === 'rows') { await page.locator('[data-nova-control="library.home"]').click(); await page.getByTestId('library-ready').waitFor(); await page.getByRole('button', { name: 'Ver filas', exact: true }).click(); }
    const opened = await quick('Buscar y abrir', 'Abrir con NOVA', async dialog => {
      await dialog.getByRole('group', { name: 'Elegir prompt', exact: true }).getByRole('button').filter({ hasText: source.title }).click();
    });
    assert.equal(opened.observation.promptId, source.id); assert.equal(opened.observation.templateText, source.templateText);
    assert.equal(await page.getByTestId('prompt-focus').getAttribute('data-prompt-id'), source.id); check('agent-opens-real-source-in-' + view);
  }
  const filled = await quick('Completar variables', 'Completar con NOVA', dialog => dialog.getByRole('textbox', { name: 'tema', exact: true }).fill('energía solar'));
  assert.deepEqual(filled.observation.values, { tema: 'energía solar' });
  assert.equal(await page.getByTestId('prompt-focus').getByTestId('variable-field').inputValue(), 'energía solar');
  const expected = 'Explica energía solar y propone dos acciones. No inventes datos.';
  assert.equal(await page.getByTestId('prompt-focus').getByTestId('compiled-preview').innerText(), expected); check('agent-fills-real-field-and-compiles-exact-preview');
  await page.screenshot({ path: path.join(out, 'filled.png'), fullPage: true });
  const prepared = await quick('Preparar para una IA', 'Preparar con NOVA', async dialog => {
    await dialog.getByRole('textbox', { name: 'tema', exact: true }).fill('energía eólica');
    await until(async () => await dialog.getByRole('combobox', { name: '¿En qué IA quieres usarlo?', exact: true }).locator('option[value="claude"]').count() > 0);
    await dialog.getByRole('combobox', { name: '¿En qué IA quieres usarlo?', exact: true }).selectOption('claude');
  });
  assert.equal(prepared.observation.providerId, 'claude'); assert.equal(prepared.observation.providerOpen, true);
  const provider = page.getByRole('dialog', { name: 'Usa tu prompt preparado', exact: true });
  assert.equal(await provider.locator('#prepared-provider').inputValue(), 'claude');
  assert.equal(await provider.locator('.prepared-text').innerText(), 'Explica energía eólica y propone dos acciones. No inventes datos.');
  check('agent-selects-real-provider-without-submitting-externally'); await page.screenshot({ path: path.join(out, 'provider.png'), fullPage: true });
  await provider.locator('[data-nova-control="dialog.close"]').click(); await provider.waitFor({ state: 'hidden' });
  assert.deepEqual(await api('/prompts?size=100'), original); check('agent-preparation-does-not-persist-or-change-other-records');
  await page.getByTestId('prompt-focus').locator('[data-nova-control="prompt.edit"]').click();
  await page.getByTestId('prompt-editor').waitFor(); await page.getByLabel('Contenido', { exact: true }).fill('Describe <tema> sin inventar datos.');
  await page.getByRole('button', { name: 'Guardar versión', exact: true }).click(); await page.getByTestId('prompt-focus').waitFor();
  const saved = await api('/prompts/' + source.id); assert.equal(saved.revision, 2); assert.equal(saved.templateText, 'Describe <tema> sin inventar datos.');
  assert.deepEqual(await api('/prompts/' + other.id), other); check('manual-edit-and-save-still-use-the-existing-domain-contract');
  await page.reload(); await page.getByRole('button', { name: source.title, exact: true }).click();
  assert.equal(await page.getByTestId('prompt-focus').getAttribute('data-revision'), '2'); check('saved-version-reopens-with-current-identity');
  if (local) {
    await page.locator('[data-nova-control="library.home"]').click(); await page.getByTestId('library-ready').waitFor();
    await quick('Buscar y abrir', 'Abrir con NOVA', async dialog => { await dialog.getByRole('group', { name: 'Elegir prompt', exact: true }).getByRole('button').filter({ hasText: local.title }).click(); });
    const executed = await quick('Ejecutar con IA local', 'Ejecutar ahora', async () => {});
    assert.equal(executed.observation.promptId, local.id); assert.equal(executed.observation.providerId, 'ollama');
    assert.equal(executed.observation.responseComplete, true); assert.equal(executed.observation.response.trim(), 'LISTO');
    assert.equal((await api('/prompts/' + local.id)).templateText, local.templateText);
    check('agent-executes-exact-prepared-prompt-with-real-qwen'); await page.screenshot({ path: path.join(out, 'local-response.png'), fullPage: true });
  }
  assert.equal(context.pages().length, 1); assert.deepEqual(report.errors, []); assert.deepEqual(report.externalRequests, []);
  report.status = 'PASS'; save();
})().catch(async error => { report.status = 'FAIL'; report.failure = String(error.stack || error); save(); process.exitCode = 1; if (page) await page.screenshot({ path: path.join(out, 'failure.png'), fullPage: true }).catch(() => {}); })
  .finally(async () => { if (runId && server?.exitCode === null) await api('/nova/live/' + runId + '/cancel', {}).catch(() => {}); await browser?.close(); if (server?.exitCode === null) { server.kill(); await new Promise(resolve => server.once('exit', resolve)); } fs.writeFileSync(path.join(out, 'server.log'), log); console.log('EVIDENCE', out); });
