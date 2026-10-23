// Collection UI -> read/write ports -> existing transactional store.
// Own process/database only. No model inference, mocks, browser downloads or user data.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process'), { createHash } = require('node:crypto');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
assert.ok(process.env.NOVA_TEST_BUILD, 'Provide the specific owned API build.');
const build = path.resolve(root, process.env.NOVA_TEST_BUILD), web = path.resolve(root, process.env.NOVA_TEST_WEBROOT || 'apps/web/dist');
const temporary = path.join(root, '.playwright/collection-ports-' + stamp), out = path.join(root, 'docs/evidence/collection-ports-' + stamp);
fs.mkdirSync(temporary); fs.mkdirSync(out);
const origin = 'http://127.0.0.1:5244', headers = { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' };
const hash = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const report = { status: 'RUNNING', scope: 'HEADED_COLLECTION_CRUD_AND_REVISION_PORT_INTEGRATION_NOT_AGENT_SEMANTICS', syntheticOnly: true,
  hashes: Object.fromEntries(['QuickPrompt.Api.dll', 'QuickPrompt.Application.dll', 'QuickPrompt.Infrastructure.dll', 'QuickPrompt.Domain.dll'].map(name => [name, hash(path.join(path.dirname(build), name))])),
  webIndexSHA256: hash(path.join(web, 'index.html')), checks: [], browserErrors: [], externalRequests: [] };
let server, browser, context, page, log = '';
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2));
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(read, timeout = 20000) { const end = Date.now() + timeout; while (Date.now() < end) { const result = await read(); if (result) return result; await wait(100); } throw Error('COLLECTION_QA_TIMEOUT'); }
async function raw(route, body, method = body ? 'POST' : 'GET') { return fetch(origin + '/api/v1' + route, { method, headers, body: body ? JSON.stringify(body) : undefined }); }
async function api(route, body, method) { const response = await raw(route, body, method); assert.ok(response.ok, route + ':' + response.status); return response.status === 204 ? null : response.json(); }
const check = name => { report.checks.push({ name, status: 'PASS' }); save(); console.log('PASS', name); };
(async () => {
  server = spawn('dotnet', [build, '--QuickPrompt:Port=5244', '--QuickPrompt:DataPath=' + path.join(temporary, 'synthetic.sqlite'), '--QuickPrompt:WebRoot=' + web], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', chunk => { log += chunk; }); server.stderr.on('data', chunk => { log += chunk; });
  await until(() => { if (server.exitCode !== null) throw Error('OWNED_API_EXITED'); return log.includes('Now listening on: ' + origin); });
  assert.equal((await api('/prompts')).total, 0);
  const source = await api('/prompts', { title: 'QA · Colecciones', templateText: 'Explica <tema> sin inventar datos.' });
  const before = await api('/prompts/' + source.id), versions = await api(`/prompts/${source.id}/versions`);
  browser = await chromium.launch({ channel: 'msedge', headless: false });
  context = await browser.newContext({ viewport: { width: 1280, height: 900 } }); page = await context.newPage(); page.setDefaultTimeout(10000);
  page.on('pageerror', error => { report.browserErrors.push(String(error)); save(); });
  page.on('request', request => { if (!request.url().startsWith(origin) && !request.url().startsWith('data:')) report.externalRequests.push(request.url()); });
  await page.goto(origin); await page.getByTestId('library-ready').waitFor();
  await page.getByRole('button', { name: source.title, exact: true }).click(); await page.getByTestId('prompt-focus').waitFor();
  await page.getByRole('button', { name: 'Colecciones', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Tus colecciones', exact: true }); await dialog.waitFor();
  await dialog.getByLabel('Nueva colección', { exact: true }).fill('Trabajo inicial');
  await dialog.getByRole('button', { name: 'Crear colección', exact: true }).click();
  await dialog.getByLabel('Nombre de colección Trabajo inicial', { exact: true }).waitFor();
  const first = (await api('/groups'))[0].group; assert.equal(first.name, 'Trabajo inicial');
  await dialog.getByRole('checkbox', { name: 'Asignar Trabajo inicial', exact: true }).check();
  await until(async () => (await api(`/prompts/${source.id}/groups`)).includes(first.id));
  check('create-and-assign-through-visible-collection-controls');
  await dialog.getByLabel('Nombre de colección Trabajo inicial', { exact: true }).fill('Trabajo revisado');
  await dialog.getByRole('button', { name: 'Guardar nombre de Trabajo inicial', exact: true }).click();
  await dialog.getByLabel('Nombre de colección Trabajo revisado', { exact: true }).waitFor();
  const renamed = (await api('/groups'))[0].group; assert.equal(renamed.revision, first.revision + 1);
  assert.equal((await raw('/groups/' + first.id, { name: 'No aplicar', revision: first.revision }, 'PUT')).status, 409);
  assert.equal((await api('/groups'))[0].group.name, 'Trabajo revisado');
  check('rename-and-reject-stale-revision-without-changing-current-name');
  const deletes = []; page.on('request', request => { if (request.method() === 'DELETE') deletes.push(request.url()); });
  await dialog.getByRole('button', { name: 'Eliminar colección Trabajo revisado', exact: true }).click();
  await dialog.getByRole('button', { name: 'Cancelar eliminación', exact: true }).click();
  assert.equal(deletes.length, 0); assert.equal((await api('/groups')).length, 1);
  assert.equal(await page.getByTestId('prompt-focus').getAttribute('data-prompt-id'), source.id);
  check('cancel-deletion-does-not-send-delete-or-change-current-prompt');
  await dialog.getByRole('checkbox', { name: 'Asignar Trabajo revisado', exact: true }).uncheck();
  await until(async () => (await api(`/prompts/${source.id}/groups`)).length === 0);
  await dialog.getByRole('checkbox', { name: 'Asignar Trabajo revisado', exact: true }).check();
  await until(async () => (await api(`/prompts/${source.id}/groups`)).includes(first.id));
  await page.screenshot({ path: path.join(out, 'membership.png') });
  await dialog.getByRole('button', { name: 'Eliminar colección Trabajo revisado', exact: true }).click();
  const deleted = page.waitForResponse(response => response.request().method() === 'DELETE' && response.url() === origin + '/api/v1/groups/' + first.id);
  await dialog.getByRole('button', { name: 'Confirmar eliminación', exact: true }).click(); assert.equal((await deleted).status(), 204);
  await until(async () => (await api('/groups')).length === 0);
  assert.deepEqual(await api(`/prompts/${source.id}/groups`), []);
  assert.deepEqual(await api('/prompts/' + source.id), before);
  assert.deepEqual(await api(`/prompts/${source.id}/versions`), versions);
  assert.equal(deletes.length, 1);
  check('confirmed-collection-delete-clears-membership-and-preserves-prompt-and-history');
  assert.equal((await raw(`/prompts/${crypto.randomUUID()}/groups`)).status, 404);
  assert.equal((await raw(`/prompts/${crypto.randomUUID()}/versions`)).status, 404);
  assert.deepEqual(report.browserErrors, []); assert.deepEqual(report.externalRequests, []);
  report.status = 'PASS'; save(); console.log('EVIDENCE', out);
})().catch(error => { report.status = 'FAIL'; report.failure = String(error.stack); save(); console.error(error); process.exitCode = 1; }).finally(async () => {
  await context?.close().catch(() => {}); await browser?.close().catch(() => {});
  if (server && server.exitCode === null) { server.kill(); await new Promise(resolve => { server.once('exit', resolve); setTimeout(resolve, 3000).unref(); }); }
  fs.writeFileSync(path.join(out, 'api.log'), log);
});
