// Owned React/API/SQLite consumers. External pages are blocked before navigation;
// optional local execution uses the installed model, never a simulated SSE response.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process'), { createHash } = require('node:crypto');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
assert.ok(process.env.NOVA_TEST_BUILD, 'Specify the owned frozen API build.');
const build = path.resolve(root, process.env.NOVA_TEST_BUILD), web = path.resolve(root, process.env.NOVA_TEST_WEBROOT);
const temporary = path.join(root, '.playwright/provider-ui-' + stamp), out = path.join(root, 'docs/evidence/provider-ui-' + stamp);
fs.mkdirSync(temporary); fs.mkdirSync(out);
const origin = 'http://127.0.0.1:5247', headers = { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' };
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const report = { status: 'RUNNING', scope: 'PROVIDER_PREFERENCES_EXECUTION_PORT_INTEGRATION_NOT_AGENT_PLANNING', syntheticOnly: true,
  hashes: Object.fromEntries(['QuickPrompt.Api.dll', 'QuickPrompt.Application.dll', 'QuickPrompt.Infrastructure.dll', 'QuickPrompt.Domain.dll'].map(name => [name, hash(fs.readFileSync(path.join(path.dirname(build), name)))])),
  webIndexSHA256: hash(fs.readFileSync(path.join(web, 'index.html'))), checks: [], browserErrors: [], blockedDestinations: [], modelExecution: 'NOT_RUN' };
let server, browser, context, page, log = ''; const effects = [];
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2));
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(read, timeout = 20000) { const end = Date.now() + timeout; while (Date.now() < end) { const result = await read(); if (result) return result; await wait(100); } throw Error('PROVIDER_QA_TIMEOUT'); }
async function raw(route, body, method = body ? 'POST' : 'GET') { return fetch(origin + '/api/v1' + route, { method, headers, body: body ? JSON.stringify(body) : undefined }); }
async function api(route, body, method) { const response = await raw(route, body, method); assert.ok(response.ok, route + ':' + response.status); return response.status === 204 ? null : response.json(); }
const check = name => { report.checks.push({ name, status: 'PASS' }); save(); console.log('PASS', name); };
(async () => {
  server = spawn('dotnet', [build, '--QuickPrompt:Port=5247', '--QuickPrompt:DataPath=' + path.join(temporary, 'synthetic.sqlite'), '--QuickPrompt:WebRoot=' + web], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', data => { log += data; }); server.stderr.on('data', data => { log += data; });
  await until(() => { if (server.exitCode !== null) throw Error('OWNED_HOST_EXITED'); return log.includes('Now listening on: ' + origin); });
  const source = await api('/prompts', { title: 'QA · Destino y ejecución', templateText: 'Responde con una sola palabra: LISTO.' });
  const before = await api('/prompts/' + source.id), versions = await api(`/prompts/${source.id}/versions`);
  browser = await chromium.launch({ channel: 'msedge', headless: false }); context = await browser.newContext({ viewport: { width: 1280, height: 900 }, permissions: ['clipboard-read', 'clipboard-write'] });
  await context.route('**/*', route => {
    const url = route.request().url();
    if (url.startsWith(origin) || /^(about|data|blob):/.test(url)) return route.continue();
    report.blockedDestinations.push(url); save(); return route.abort();
  });
  page = await context.newPage(); page.setDefaultTimeout(12000); page.on('pageerror', error => { report.browserErrors.push(String(error)); save(); });
  page.on('request', request => { if (request.method() !== 'GET' && request.url().startsWith(origin + '/api/v1/')) effects.push({ path: new URL(request.url()).pathname, body: request.postDataJSON() }); });
  await page.goto(origin); await page.getByTestId('library-ready').waitFor();
  await page.getByRole('button', { name: 'Configuración', exact: true }).click(); const settings = page.getByRole('dialog', { name: 'Configuración', exact: true });
  await settings.locator('summary', { hasText: 'Preferencias de NOVA' }).click();
  await settings.getByLabel('Idioma de respuesta', { exact: true }).selectOption('en'); await settings.getByLabel('Formato preferido', { exact: true }).selectOption('JSON');
  assert.deepEqual(await api('/nova/preferences'), { language: null, format: null, providerId: null });
  await settings.getByRole('button', { name: 'Confirmar preferencias', exact: true }).click(); await settings.getByText('Preferencias confirmadas en este equipo.', { exact: true }).waitFor();
  assert.deepEqual(await api('/nova/preferences'), { language: 'en', format: 'JSON', providerId: null }); check('preferences-edit-does-not-persist-until-confirmed');
  await settings.getByRole('button', { name: 'Cerrar', exact: true }).click(); await page.reload(); await page.getByTestId('library-ready').waitFor();
  await page.getByRole('button', { name: 'Configuración', exact: true }).click(); await settings.locator('summary', { hasText: 'Preferencias de NOVA' }).click();
  await until(async () => await settings.getByLabel('Idioma de respuesta', { exact: true }).inputValue() === 'en'); assert.equal(await settings.getByLabel('Formato preferido', { exact: true }).inputValue(), 'JSON');
  await settings.getByRole('button', { name: 'Eliminar preferencias', exact: true }).click(); await settings.getByText('Preferencias eliminadas.', { exact: true }).waitFor();
  assert.deepEqual(await api('/nova/preferences'), { language: null, format: null, providerId: null }); assert.deepEqual(await api('/prompts/' + source.id), before); check('preferences-reload-and-forget-preserve-library'); await settings.getByRole('button', { name: 'Cerrar', exact: true }).click();
  await page.getByRole('button', { name: source.title, exact: true }).click(); await page.getByTestId('prompt-focus').waitFor();
  const open = page.getByRole('button', { name: 'Usar en…', exact: true }); await until(async () => await open.isEnabled()); await open.click();
  const picker = page.getByRole('dialog', { name: 'Lleva tu instrucción más lejos', exact: true }); await picker.getByRole('button', { name: 'ChatGPT', exact: true }).click();
  assert.equal(await picker.locator('.transfer-preview pre').textContent(), source.templateText); assert.equal(effects.filter(item => item.path.includes('/execute') || item.path.includes('/transfers/prepare') || item.path.endsWith('/usage')).length, 0); check('provider-discovery-and-preview-have-no-execution-or-transfer-effect');
  await picker.getByRole('button', { name: 'Cerrar diálogo', exact: true }).click(); assert.equal((await api('/prompts/' + source.id)).uses, 0); check('closing-destination-picker-does-not-record-use'); await open.click();
  await picker.getByRole('button', { name: 'ChatGPT', exact: true }).click(); const popupEvent = context.waitForEvent('page');
  await picker.getByRole('button', { name: 'Copiar y abrir', exact: true }).click(); const popup = await popupEvent;
  await until(async () => (await api('/prompts/' + source.id)).uses === 1); await popup.close(); await page.bringToFront(); assert.equal(await page.evaluate(() => navigator.clipboard.readText()), source.templateText); await until(() => report.blockedDestinations.length > 0);
  const transfer = effects.filter(item => item.path === '/api/v1/transfers/prepare'); assert.equal(transfer.length, 1); assert.equal(transfer[0].body.confirmed, true); assert.equal(transfer[0].body.revision, source.revision); assert.ok(report.blockedDestinations.every(url => url === 'https://chatgpt.com/')); check('confirmed-copy-prepares-approved-destination-and-records-exact-use-once');
  assert.equal((await raw('/transfers/prepare', { promptId: source.id, revision: source.revision, values: {}, providerId: 'chatgpt', confirmed: false })).status, 400);
  assert.equal((await raw('/ai/local/execute', { promptId: source.id, revision: source.revision, values: {}, model: 'qwen3:8b', confirmed: true, expectedText: 'Texto distinto' })).status, 409); check('backend-ports-reject-unapproved-transfer-and-unreviewed-execution-before-stream');
  if (process.env.RUN_REAL_LOCAL_EXECUTION === '1') {
    await picker.getByRole('button', { name: 'Ollama local', exact: true }).click(); const execute = picker.getByRole('button', { name: 'Ejecutar en este equipo', exact: true }); await until(async () => await execute.isEnabled());
    const started = Date.now(), pending = page.waitForResponse(response => response.url() === origin + '/api/v1/ai/local/execute', { timeout: 180000 });
    report.modelExecution = { status: 'REQUESTED', model: 'qwen3:8b' }; save(); await execute.click(); console.log('RUN real-installed-model-execution'); const response = await pending; assert.equal(response.status(), 200);
    // readLocalStream deliberately cancels its reader after the terminal event.
    // CDP response.text() is not a reliable observer for that consumed SSE request.
    await picker.getByRole('button', { name: 'Copiar respuesta', exact: true }).waitFor({ timeout: 180000 });
    const text = await picker.locator('.local-response pre').textContent(); assert.ok(text.trim());
    const executions = effects.filter(item => item.path === '/api/v1/ai/local/execute'); assert.equal(executions.length, 1); assert.equal(executions[0].body.expectedText, source.templateText); assert.equal(executions[0].body.revision, source.revision); assert.equal(executions[0].body.confirmed, true);
    await picker.getByRole('button', { name: 'Copiar respuesta', exact: true }).click(); assert.equal(await page.evaluate(() => navigator.clipboard.readText()), text);
    await until(async () => (await api('/prompts/' + source.id)).uses === 2); report.modelExecution = { status: 'PASS_REAL_UI_COMPLETION_AND_CLIPBOARD_NOT_AGENT_PLANNING', model: 'qwen3:8b', elapsedMs: Date.now() - started, responseLength: text.length, responseSHA256: hash(text), literalInstructionSatisfied: text.trim() === 'LISTO' }; save(); check('real-installed-model-completes-visible-response-copies-exactly-and-records-use-once');
  }
  await page.screenshot({ path: path.join(out, 'provider-result.png') });
  await picker.getByRole('button', { name: 'Cerrar diálogo', exact: true }).click(); await page.getByTestId('prompt-focus').getByRole('button', { name: 'Editar', exact: true }).click();
  await page.getByTestId('prompt-focus').waitFor({ state: 'hidden' });
  await until(async () => await page.getByRole('button', { name: 'Usar en…', exact: true }).isEnabled()); await page.getByRole('button', { name: 'Usar en…', exact: true }).click();
  const prepared = page.getByRole('dialog', { name: 'Usa tu prompt preparado', exact: true }); await prepared.getByRole('option', { name: 'ChatGPT', exact: true }).waitFor({ state: 'attached' });
  assert.equal(await prepared.locator('.prepared-text').textContent(), source.templateText); check('editor-prepared-use-renders-destinations-from-real-catalog');
  const preparedPopup = context.waitForEvent('page'); await prepared.getByRole('button', { name: 'Copiar y abrir ChatGPT', exact: true }).click(); const openedPrepared = await preparedPopup;
  await prepared.getByText('Texto copiado. Pégalo y envíalo cuando decidas.', { exact: false }).waitFor(); await openedPrepared.close(); await page.bringToFront(); assert.equal(await page.evaluate(() => navigator.clipboard.readText()), source.templateText);
  assert.equal(effects.filter(item => item.path === '/api/v1/ai/local/execute').length, process.env.RUN_REAL_LOCAL_EXECUTION === '1' ? 1 : 0); check('prepared-draft-copy-revalidates-catalog-and-does-not-execute-or-save-template');
  assert.deepEqual(await api(`/prompts/${source.id}/versions`), versions); assert.deepEqual(report.browserErrors, []); report.status = 'PASS'; save(); console.log('EVIDENCE', out);
})().catch(async error => { report.status = 'FAIL'; report.failure = String(error.stack); if (page && !page.isClosed()) { await page.screenshot({ path: path.join(out, 'failure.png') }).catch(() => {}); report.visibleDialogs = await page.getByRole('dialog').allTextContents().catch(() => []); } save(); console.error(error); process.exitCode = 1; }).finally(async () => {
  await context?.close().catch(() => {}); await browser?.close().catch(() => {});
  if (server && server.exitCode === null) { server.kill(); await new Promise(resolve => { server.once('exit', resolve); setTimeout(resolve, 3000).unref(); }); }
  fs.writeFileSync(path.join(out, 'api.log'), log);
});
