// Non-AI UI regression: one owned API/database/browser; no private records or clipboard.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..');
const out = path.join(root, '.playwright', 'modular-fields-' + Date.now());
const build = path.resolve(root, process.env.NOVA_TEST_BUILD || 'backend/QuickPrompt.Api/bin/Debug/net8.0/QuickPrompt.Api.dll');
const web = path.resolve(root, process.env.NOVA_TEST_WEBROOT || 'apps/web/dist');
const origin = 'http://127.0.0.1:5281';
fs.mkdirSync(out, { recursive: true });
let server, browser, log = '';
const report = { status: 'RUNNING', scope: 'NON_AI_MODULAR_FIELD_CONSUMERS', checks: [], errors: [], build, web };
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2));
const check = label => { report.checks.push(label); save(); console.log('PASS', label); };
async function api(route, body) {
  const response = await fetch(origin + '/api/v1' + route, { method: body ? 'POST' : 'GET',
    headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' }, body: body && JSON.stringify(body) });
  assert.ok(response.ok, route + ':' + response.status); return response.json();
}
(async () => {
  assert.ok(fs.existsSync(build) && fs.existsSync(path.join(web, 'index.html')), 'Build API and web first');
  server = spawn('dotnet', [build, '--QuickPrompt:Port=5281', '--QuickPrompt:DataPath=' + path.join(out, 'synthetic.sqlite'), '--QuickPrompt:WebRoot=' + web], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', bytes => { log += bytes; }); server.stderr.on('data', bytes => { log += bytes; });
  const deadline = Date.now() + 25000;
  while (!log.includes('Now listening on: ' + origin)) {
    assert.equal(server.exitCode, null, 'Owned API exited before becoming ready');
    assert.ok(Date.now() < deadline, 'Owned API startup timed out');
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  const source = await api('/prompts', { title: 'QA · Campos modulares', templateText: 'Explica <tema> para <publico>.' });
  browser = await chromium.launch({ channel: 'msedge', headless: true });
  const page = await browser.newPage({ viewport: { width: 1366, height: 950 } });
  page.on('pageerror', error => { report.errors.push(String(error)); });
  await page.goto(origin); await page.getByRole('button', { name: source.title, exact: true }).click();
  const focus = page.getByTestId('prompt-focus');
  await focus.getByLabel('tema', { exact: true }).fill('energía α');
  await focus.getByLabel('publico', { exact: true }).fill('equipo');
  await focus.getByTestId('compile-ready').waitFor();
  assert.equal(await focus.getByTestId('compiled-preview').innerText(), 'Explica energía α para equipo.');
  check('detail-labels-and-literal-values-produce-expected-preview');
  await focus.getByRole('button', { name: 'Editar', exact: true }).click();
  const preparation = page.getByTestId('editor-preparation');
  await preparation.getByLabel('tema', { exact: true }).fill('editor β');
  await preparation.getByLabel('publico', { exact: true }).fill('lectores');
  await preparation.getByTestId('compile-ready').waitFor();
  assert.equal(await preparation.getByTestId('compiled-preview').innerText(), 'Explica editor β para lectores.');
  check('editor-reuses-fields-and-compiles-own-values-without-saving');
  await page.goto(origin); await page.getByRole('button', { name: source.title, exact: true }).click();
  await page.getByRole('button', { name: 'Abrir asistente NOVA', exact: true }).click();
  const actions = page.getByRole('region', { name: 'Acciones rápidas de NOVA', exact: true });
  await actions.getByRole('button', { name: 'Más acciones de NOVA', exact: true }).click();
  await actions.getByRole('button', { name: 'Completar variables', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Completar variables', exact: true });
  await dialog.getByLabel('tema', { exact: true }).fill('sólo diálogo');
  assert.equal(await dialog.getByTestId('variable-field').count(), 0);
  assert.equal(await page.getByTestId('prompt-focus').getByTestId('variable-field').count(), 2);
  check('quick-action-fields-stay-outside-agent-workspace-targets');
  await page.screenshot({ path: path.join(out, 'shared-fields.png'), fullPage: true });
  assert.deepEqual(await api('/prompts/' + source.id), source);
  assert.deepEqual(report.errors, []);
  check('three-consumers-preserve-saved-source-and-have-no-browser-errors');
  report.status = 'PASS'; save();
})().catch(error => { report.status = 'FAIL'; report.errors.push(String(error)); save(); console.error(String(error)); process.exitCode = 1; })
  .finally(async () => { await browser?.close(); if (server?.exitCode === null) { server.kill(); await new Promise(resolve => server.once('exit', resolve)); } fs.writeFileSync(path.join(out, 'server.log'), log); console.log('EVIDENCE', out); });
