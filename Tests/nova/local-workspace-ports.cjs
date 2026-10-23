// Real server-rendered CRUD/composition through capability ports. The API reads
// the same owned synthetic DB and creates one deliberate stale-edit condition.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process'), { createHash } = require('node:crypto');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
assert.ok(process.env.NOVA_TEST_BUILD && process.env.LEGACY_TEST_BUILD && process.env.LEGACY_TEST_CONTENT, 'Provide owned frozen builds/content.');
const apiBuild = path.resolve(root, process.env.NOVA_TEST_BUILD), oldBuild = path.resolve(root, process.env.LEGACY_TEST_BUILD);
const temporary = path.join(root, '.playwright/local-workspace-ui-' + stamp), out = path.join(root, 'docs/evidence/local-workspace-ui-' + stamp);
fs.mkdirSync(temporary); fs.mkdirSync(out);
const apiOrigin = 'http://127.0.0.1:5249', origin = 'http://127.0.0.1:5248', database = path.join(temporary, 'synthetic.sqlite');
const headers = { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' };
const hash = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const assemblies = ['QuickPrompt.Domain.dll', 'QuickPrompt.Application.dll', 'QuickPrompt.Infrastructure.dll'];
const report = { status: 'RUNNING', scope: 'SERVER_RENDERED_CORE_PORTS_REAL_UI_AND_SQLITE_NOT_MODEL_SEMANTICS', syntheticOnly: true,
  apiHashes: Object.fromEntries([...assemblies, 'QuickPrompt.Api.dll'].map(name => [name, hash(path.join(path.dirname(apiBuild), name))])),
  legacyHashes: Object.fromEntries([...assemblies, 'QuickPrompt.Web.dll'].map(name => [name, hash(path.join(path.dirname(oldBuild), name))])), checks: [], browserErrors: [], externalRequests: [] };
for (const name of assemblies) assert.equal(report.apiHashes[name], report.legacyHashes[name]);
const processes = [], logs = []; let browser, context, page;
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2));
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(read, timeout = 20000) { const end = Date.now() + timeout; while (Date.now() < end) { const result = await read(); if (result) return result; await wait(100); } throw Error('WORKSPACE_QA_TIMEOUT'); }
async function start(build, port, name, extra) {
  const child = spawn('dotnet', [build, '--QuickPrompt:Port=' + port, '--QuickPrompt:DataPath=' + database, ...extra], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] }); processes.push(child);
  const capture = { name, text: '' }; logs.push(capture); child.stdout.on('data', data => { capture.text += data; }); child.stderr.on('data', data => { capture.text += data; });
  await until(() => { if (child.exitCode !== null) throw Error('OWNED_HOST_EXITED:' + name); return capture.text.includes('Now listening on: http://127.0.0.1:' + port); });
}
async function api(route, body, method = body ? 'POST' : 'GET') { const response = await fetch(apiOrigin + '/api/v1' + route, { method, headers, body: body ? JSON.stringify(body) : undefined }); assert.ok(response.ok, route + ':' + response.status); return response.status === 204 ? null : response.json(); }
const archive = () => api('/library/export');
const check = name => { report.checks.push({ name, status: 'PASS' }); save(); console.log('PASS', name); };
const options = () => page.locator('details.prompt-options > summary');
async function visibleOptions() { const detail = page.locator('details.prompt-options'); if (!await detail.evaluate(node => node.open)) await options().click(); }
(async () => {
  await start(apiBuild, 5249, 'api', []); await start(oldBuild, 5248, 'web', ['--contentRoot=' + path.resolve(root, process.env.LEGACY_TEST_CONTENT)]);
  browser = await chromium.launch({ channel: 'msedge', headless: false }); context = await browser.newContext({ viewport: { width: 1440, height: 1050 }, permissions: ['clipboard-read', 'clipboard-write'] }); page = await context.newPage(); page.setDefaultTimeout(12000);
  page.on('pageerror', error => { report.browserErrors.push(String(error)); save(); }); page.on('request', request => { if (!request.url().startsWith(origin) && !/^(about|data|blob):/.test(request.url())) report.externalRequests.push(request.url()); });
  await page.goto(origin); await page.getByRole('button', { name: 'Nueva plantilla', exact: true }).click();
  await page.locator('#title').fill('QA · Puertos locales'); await page.locator('#template').fill('Hola <nombre>.'); await page.getByRole('button', { name: 'Guardar plantilla', exact: true }).click();
  await until(async () => (await api('/prompts')).total === 1); let source = (await api('/prompts')).items[0]; assert.equal(source.revision, 1); assert.equal(source.templateText, 'Hola <nombre>.'); check('create-through-server-rendered-authoring-port');
  await page.locator('textarea[data-variable-name="nombre"],textarea[id="v-6E6F6D627265"]').fill('Ana');
  const preview = page.locator('details.final-preview'); if (!await preview.evaluate(node => node.open)) await preview.locator('summary').click(); await until(async () => await page.getByTestId('live-preview').textContent() === 'Hola Ana.');
  assert.equal((await archive()).history.length, 0); assert.equal((await api('/prompts/' + source.id)).uses, 0); check('live-preview-is-read-only-and-keeps-values-circuit-local');
  const compose = page.locator('details.composition-options'); await page.locator('details.composition-options > summary').click(); await compose.getByRole('button', { name: 'Generar texto final', exact: false }).click();
  await until(async () => (await archive()).history.length === 1); assert.equal(await page.getByTestId('compiled').textContent(), 'Hola Ana.'); assert.equal((await api('/prompts/' + source.id)).uses, 1); assert.deepEqual((await archive()).history[0].values, {}); check('explicit-compilation-persists-one-history-entry-with-opt-in-privacy');
  await visibleOptions(); await page.getByRole('button', { name: 'Editar', exact: true }).click(); await page.locator('#title').fill('QA · Puertos revisados'); await page.getByRole('button', { name: 'Guardar plantilla', exact: true }).click();
  await until(async () => (await api('/prompts/' + source.id)).revision === 2); source = await api('/prompts/' + source.id); assert.equal(source.title, 'QA · Puertos revisados'); assert.equal((await api(`/prompts/${source.id}/versions`)).length, 2); check('update-through-authoring-preserves-version-history');
  await visibleOptions(); page.once('dialog', dialog => dialog.dismiss()); await page.getByRole('button', { name: 'Eliminar', exact: true }).click(); assert.equal((await api('/prompts')).total, 1); check('delete-cancel-has-no-data-or-navigation-effect');
  await options().click(); // Close the still-open menu after cancelling its delete action.
  await page.locator('textarea[id="v-6E6F6D627265"]').fill('Bea'); await page.locator('details.composition-options > summary').click();
  await api('/prompts/' + source.id, { title: source.title, templateText: 'Adiós <nombre>.', revision: source.revision }, 'PUT'); const beforeStale = await archive();
  await page.getByRole('button', { name: 'Generar texto final', exact: false }).click(); await page.getByText('El prompt cambió. Actualiza la biblioteca / Prompt changed. Refresh the library.', { exact: true }).waitFor(); assert.deepEqual(await archive(), beforeStale); check('stale-compilation-rejected-without-history-or-use');
  await visibleOptions(); page.once('dialog', dialog => dialog.accept()); await page.getByRole('button', { name: 'Eliminar', exact: true }).click(); await page.getByText('El prompt cambió. Recarga antes de eliminar.', { exact: true }).waitFor(); assert.deepEqual(await archive(), beforeStale); check('stale-delete-rejected-without-removing-newer-source');
  await page.reload(); await page.locator('.card-open').filter({ has: page.getByRole('heading', { name: source.title, exact: true }) }).click(); await visibleOptions(); page.once('dialog', dialog => dialog.accept()); await page.getByRole('button', { name: 'Eliminar', exact: true }).click();
  await until(async () => (await api('/prompts')).total === 0); assert.equal((await archive()).history.length, 1); check('confirmed-current-delete-removes-template-and-preserves-compiled-history');
  await page.getByRole('button', { name: 'Configuración', exact: true }).click(); await page.locator('#theme').selectOption('dark'); await until(async () => (await archive()).preferences.theme === 'dark'); await page.reload(); await page.getByRole('button', { name: 'Configuración', exact: true }).click(); assert.equal(await page.locator('#theme').inputValue(), 'dark'); check('settings-port-persists-theme-across-circuit-reload');
  await page.screenshot({ path: path.join(out, 'workspace-final.png') }); assert.deepEqual(report.browserErrors, []); assert.deepEqual(report.externalRequests, []); report.status = 'PASS'; save(); console.log('EVIDENCE', out);
})().catch(async error => { report.status = 'FAIL'; report.failure = String(error.stack); if (page && !page.isClosed()) { await page.screenshot({ path: path.join(out, 'failure.png') }).catch(() => {}); report.visibleText = await page.locator('body').innerText().catch(() => ''); } save(); console.error(error); process.exitCode = 1; }).finally(async () => {
  await context?.close().catch(() => {}); await browser?.close().catch(() => {}); for (const child of processes) if (child.exitCode === null) { child.kill(); await new Promise(resolve => { child.once('exit', resolve); setTimeout(resolve, 3000).unref(); }); } for (const log of logs) fs.writeFileSync(path.join(out, log.name + '.log'), log.text);
});
