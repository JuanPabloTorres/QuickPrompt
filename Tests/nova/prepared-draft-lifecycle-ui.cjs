// Real production UI + completion/save APIs. Only the preparation stream is controlled.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict'), crypto = require('node:crypto');
const { spawn } = require('node:child_process');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const { configurePreparation } = require('./ui-settings.cjs');
assert.ok(process.env.NOVA_TEST_BUILD && process.env.NOVA_TEST_WEBROOT, 'Provide owned frozen API and web assets.');
const root = path.resolve(__dirname, '../..'), stamp = Date.now(), origin = 'http://127.0.0.1:5278';
const out = path.join(root, 'docs/evidence/prepared-lifecycle-ui-' + stamp), temp = path.join(root, '.playwright/prepared-lifecycle-ui-' + stamp);
fs.mkdirSync(out); fs.mkdirSync(temp);
const build = path.resolve(root, process.env.NOVA_TEST_BUILD), web = path.resolve(root, process.env.NOVA_TEST_WEBROOT);
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const report = { status: 'RUNNING', scope: 'CONTROLLED_PREPARATION_REAL_UI_COMPLETION_AND_SAVE_NOT_MODEL_ACCEPTANCE', headed: true,
  syntheticOnly: true, inference: false, planningStreamControlled: true, apiSha256: hash(build), webIndexSha256: hash(path.join(web, 'index.html')), checks: [] };
const errors = [], external = []; let server, browser, page, log = '', controlledRequests = 0;
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify({ ...report, browserErrors: errors, externalRequests: external }, null, 2));
async function api(route, body) {
  const response = await fetch(origin + '/api/v1' + route, { method: body ? 'POST' : 'GET', headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' }, body: body ? JSON.stringify(body) : undefined });
  assert.equal(response.status, 200, route); return response.json();
}
(async () => {
  server = spawn('dotnet', [build, '--QuickPrompt:Port=5278', '--QuickPrompt:DataPath=' + path.join(temp, 'synthetic.sqlite'), '--QuickPrompt:WebRoot=' + web,
    '--QuickPrompt:Ollama=http://127.0.0.1:1'], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', data => { log += data; }); server.stderr.on('data', data => { log += data; });
  let ready = false;
  for (let i = 0; i < 200; i++) { assert.equal(server.exitCode, null); if (log.includes('Now listening on: ' + origin)) { ready = true; break; } await sleep(100); }
  assert.ok(ready); browser = await chromium.launch({ channel: 'msedge', headless: false });
  page = await browser.newPage({ viewport: { width: 1280, height: 900 } }); page.setDefaultTimeout(10000);
  page.on('pageerror', error => errors.push(error.message)); page.on('request', request => { if (/^https?:/.test(request.url()) && !request.url().startsWith(origin + '/')) external.push(request.url()); });
  const title = 'Borrador de ciclo de vida', templateText = 'Audita <tema> para <audiencia>.';
  await page.route(origin + '/api/v1/nova/prepare', async route => {
    controlledRequests++; const input = route.request().postDataJSON();
    const seed = await api('/nova/editor-drafts', { promptId: null, revision: null, title, templateText, values: {}, sessionId: input.sessionId });
    const prepared = await api('/nova/drafts/' + seed.id + '/complete', { operationId: input.operationId, values: {} });
    await route.fulfill({ status: 200, contentType: 'text/event-stream; charset=utf-8', body:
      'event: stage\ndata: ' + JSON.stringify({ state: prepared.state }) + '\n\nevent: complete\ndata: ' + JSON.stringify(prepared) + '\n\n' });
  });
  await page.goto(origin); await page.getByTestId('library-ready').waitFor();
  await configurePreparation(page, { mode: 'LIVE' });
  await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).fill('Fixture controlado de completar y guardar.');
  await page.getByRole('button', { name: 'Preparar prompt', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: title, exact: true }); await dialog.waitFor();
  await dialog.getByLabel('tema', { exact: true }).fill('C# & .NET 🚀'); await dialog.getByLabel('audiencia', { exact: true }).fill('Equipo α');
  assert.equal(await dialog.getByLabel('tema', { exact: true }).inputValue(), 'C# & .NET 🚀');
  assert.equal(await dialog.getByLabel('audiencia', { exact: true }).inputValue(), 'Equipo α');
  const completed = page.waitForResponse(response => response.request().method() === 'POST' && response.url().endsWith('/complete'));
  await dialog.getByRole('button', { name: 'Completar prompt', exact: true }).click(); const response = await completed; assert.equal(response.status(), 200);
  const result = await response.json(); assert.equal(result.compiledText, 'Audita C# & .NET 🚀 para Equipo α.');
  assert.deepEqual(Object.fromEntries(result.values.map(value => [value.name, value.value])), { tema: 'C# & .NET 🚀', audiencia: 'Equipo α' });
  assert.deepEqual(result.missingVariables, []); assert.equal(result.state, 'READY');
  await page.waitForFunction(text => document.querySelector('.preparation-dialog .prepared-text')?.textContent === text, result.compiledText);
  assert.equal(await dialog.getByLabel('tema', { exact: true }).count(), 0);
  assert.equal(await dialog.getByLabel('audiencia', { exact: true }).count(), 0);
  assert.equal((await api('/prompts?size=12')).total, 0);
  report.checks.push('visible-fields-and-preview-equal-real-completion-result-without-saving'); save();
  const savedResponse = page.waitForResponse(value => value.request().method() === 'POST' && value.url().endsWith('/save'));
  await dialog.getByRole('button', { name: 'Guardar', exact: true }).click(); assert.equal((await savedResponse).status(), 200);
  await dialog.getByRole('button', { name: 'Guardado', exact: true }).waitFor();
  const library = await api('/prompts?size=12'); assert.equal(library.total, 1); assert.equal(library.items[0].templateText, templateText);
  assert.equal((await api('/nova/runs/' + result.id)).approval, 'APPROVED_COPY_OR_REUSE');
  report.checks.push('explicit-ui-save-uses-real-lifecycle-and-persists-only-the-template');
  assert.equal(controlledRequests, 1); assert.deepEqual(errors, []); assert.deepEqual(external, []);
  await page.screenshot({ path: path.join(out, 'verified-complete-and-save.png'), animations: 'disabled' });
  assert.equal(hash(build), report.apiSha256); assert.equal(hash(path.join(web, 'index.html')), report.webIndexSha256);
  report.status = 'PASS'; save(); console.log('PASS', out);
})().catch(async error => { report.status = 'FAIL'; report.error = error.message; save(); await page?.screenshot({ path: path.join(out, 'failure.png') }).catch(() => {}); console.error(error); process.exitCode = 1;
}).finally(async () => { await browser?.close(); if (server?.exitCode === null) { server.kill(); await new Promise(resolve => server.once('exit', resolve)); } fs.writeFileSync(path.join(out, 'api.log'), log); });
