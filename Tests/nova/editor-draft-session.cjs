// Real manual editor/cache flows. No inference; owned API and synthetic SQLite only.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict'), crypto = require('node:crypto');
const { spawn } = require('node:child_process');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
const apiBuild = path.resolve(root, process.env.NOVA_TEST_BUILD || 'backend/QuickPrompt.Api/bin/Debug/net8.0/QuickPrompt.Api.dll');
const webRoot = path.resolve(root, process.env.NOVA_TEST_WEBROOT || 'apps/web/dist');
const temporary = path.join(root, '.playwright/editor-draft-ui-' + stamp), out = path.join(root, 'docs/evidence/editor-draft-ui-' + stamp);
const origin = 'http://127.0.0.1:5272', headers = { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' };
const contextOnly = process.argv.includes('--context-only');
fs.mkdirSync(temporary, { recursive: true }); fs.mkdirSync(out, { recursive: true });
let server, browser, page, log = ''; const checks = [], errors = [];
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const apiSha256 = hash(apiBuild), webIndexSha256 = hash(path.join(webRoot, 'index.html'));
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(read) { for (let i = 0; i < 200; i++) { if (await read()) return; await sleep(100); } throw Error('QA_TIMEOUT'); }
async function api(route, body, method = body ? 'POST' : 'GET') {
  const response = await fetch(origin + '/api/v1' + route, { method, headers, body: body ? JSON.stringify(body) : undefined });
  assert.ok(response.ok, await response.clone().text()); return response.status === 204 ? null : response.json();
}
async function library() { await page.getByRole('button', { name: 'QuickPrompt NOVA, biblioteca', exact: true }).click(); await page.getByTestId('library-ready').waitFor(); }
async function leaveEditor() {
  await page.getByRole('button', { name: 'Volver', exact: true }).click();
  await page.getByRole('button', { name: 'Salir del editor', exact: true }).click();
  await page.getByTestId('prompt-editor').waitFor({ state: 'hidden' });
}
async function editor() { await page.getByTestId('prompt-editor').waitFor(); await page.getByTestId('variables-inspected').waitFor(); }
function save(status, failure) {
  fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify({ status, scope: contextOnly ? 'EDITOR_CONTEXT_UI_API_SQLITE_NOT_AGENT_ACCEPTANCE' : 'EDITOR_SNAPSHOT_UI_API_SQLITE_NOT_AGENT_ACCEPTANCE', headed: true,
    inference: false, syntheticDataOnly: true, apiSha256, webIndexSha256, checks, browserErrors: errors, failure: failure || null }, null, 2));
}
function verifiedArtifacts() {
  assert.equal(hash(apiBuild), apiSha256); assert.equal(hash(path.join(webRoot, 'index.html')), webIndexSha256); assert.deepEqual(errors, []);
}
async function contextFlow(source) {
  const values = { tema: 'React 🚀', audiencia: 'Equipo α' }, title = 'Contexto actual sin guardar';
  const templateText = 'Redacta sobre <tema> para <audiencia>.', compiledText = 'Redacta sobre React 🚀 para Equipo α.';
  const contextResponse = () => page.waitForResponse(response => response.request().method() === 'POST' && response.url() === origin + '/api/v1/nova/editor-drafts');
  await page.getByRole('button', { name: 'Editar: ' + source.title, exact: true }).click(); await editor();
  await page.getByLabel('Título', { exact: true }).fill(title); await page.getByLabel('Contenido', { exact: true }).fill(templateText);
  for (const [name, value] of Object.entries(values)) await page.getByLabel(name, { exact: true }).fill(value);
  await until(async () => await page.getByTestId('compile-ready').count() === 1 && await page.getByTestId('compiled-preview').textContent() === compiledText);
  const using = contextResponse(); await page.getByRole('button', { name: 'Usar en…', exact: true }).click();
  const usedResponse = await using; assert.ok(usedResponse.ok()); const input = usedResponse.request().postDataJSON(), prepared = await usedResponse.json();
  const expected = { promptId: source.id, revision: source.revision, title, templateText, values, sessionId: input.sessionId };
  assert.equal(typeof input.sessionId, 'string'); assert.deepEqual(input, expected); assert.equal(prepared.compiledText, compiledText);
  await page.getByRole('dialog', { name: 'Usa tu prompt preparado', exact: true }).waitFor();
  assert.equal(await page.getByTestId('prepared-provider-dialog').locator('pre').textContent(), compiledText);
  await page.getByRole('button', { name: 'Cerrar diálogo', exact: true }).click();
  checks.push('editor-use-json-matches-visible-source-values-and-compiled-text'); save('RUNNING');
  const leaving = contextResponse(); await leaveEditor(); const leaveResponse = await leaving; assert.ok(leaveResponse.ok());
  assert.deepEqual(leaveResponse.request().postDataJSON(), expected);
  assert.equal((await api('/prompts/' + source.id)).templateText, source.templateText);
  checks.push('leave-editor-sends-identical-context-without-persisting-prompt'); save('RUNNING');
  await page.getByRole('button', { name: 'Editar', exact: true }).click(); await editor();
  const savedTitle = 'Contexto guardado comprobado'; await page.getByLabel('Título', { exact: true }).fill(savedTitle);
  const saving = contextResponse();
  await page.getByRole('button', { name: 'Guardar versión', exact: true }).click(); const savedResponse = await saving; assert.ok(savedResponse.ok());
  await page.getByTestId('prompt-focus').waitFor(); const saved = await api('/prompts/' + source.id);
  assert.equal(saved.revision, source.revision + 1); assert.equal(saved.templateText, templateText);
  assert.deepEqual(savedResponse.request().postDataJSON(), { ...expected, title: savedTitle, revision: saved.revision });
  checks.push('post-save-context-uses-actual-saved-revision-and-title'); save('RUNNING');
  await library(); await page.getByRole('button', { name: 'Nuevo prompt', exact: true }).click(); await editor();
  await page.getByLabel('Título', { exact: true }).fill('Nuevo contexto sin origen');
  await page.getByLabel('Contenido', { exact: true }).fill('Ayuda con <objetivo>.');
  await page.getByLabel('objetivo', { exact: true }).fill('Revisión contractual');
  await until(async () => await page.getByTestId('compile-ready').count() === 1 && await page.getByTestId('compiled-preview').textContent() === 'Ayuda con Revisión contractual.');
  const creating = contextResponse(); await page.getByRole('button', { name: 'Usar en…', exact: true }).click(); const createdResponse = await creating;
  assert.ok(createdResponse.ok()); assert.deepEqual(createdResponse.request().postDataJSON(), { promptId: null, revision: null, title: 'Nuevo contexto sin origen',
    templateText: 'Ayuda con <objetivo>.', values: { objetivo: 'Revisión contractual' }, sessionId: input.sessionId });
  assert.equal((await createdResponse.json()).compiledText, 'Ayuda con Revisión contractual.');
  await page.getByRole('dialog', { name: 'Usa tu prompt preparado', exact: true }).waitFor();
  checks.push('new-context-keeps-null-source-instead-of-previous-selected-prompt');
  await page.screenshot({ path: path.join(out, 'verified-editor-context.png'), animations: 'disabled' });
}
(async () => {
  server = spawn('dotnet', [apiBuild, '--QuickPrompt:Port=5272', '--QuickPrompt:DataPath=' + path.join(temporary, 'synthetic.sqlite'),
    '--QuickPrompt:WebRoot=' + webRoot, '--QuickPrompt:Ollama=http://127.0.0.1:1'], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', data => { log += data; }); server.stderr.on('data', data => { log += data; });
  await until(() => { assert.equal(server.exitCode, null, 'Owned API exited'); return log.includes('Now listening on: ' + origin); });
  const source = await api('/prompts', contextOnly
    ? { title: 'Contexto sintético del editor', templateText: 'Explica <tema> a <audiencia>.', defaults: { tema: 'Valor por defecto', audiencia: 'Lectores' }, description: '', categoryId: 0 }
    : { title: 'Sesión sintética de borradores', templateText: 'Contenido guardado inicial.', description: '', categoryId: 0 });
  browser = await chromium.launch({ channel: 'msedge', headless: false });
  page = await browser.newPage({ viewport: { width: 1280, height: 900 } }); page.setDefaultTimeout(10000);
  page.on('pageerror', error => errors.push(error.message));
  await page.goto(origin); await page.getByTestId('library-ready').waitFor();
  if (contextOnly) { await contextFlow(source); verifiedArtifacts(); save('PASS'); console.log('PASS', out); return; }
  await page.getByRole('button', { name: 'Editar: ' + source.title, exact: true }).click(); await editor();
  await page.getByLabel('Contenido', { exact: true }).fill('Borrador pendiente de la revisión inicial.');
  await leaveEditor(); await library();
  const fresh = await api('/prompts/' + source.id, { title: source.title, templateText: 'Fuente vigente después del cambio externo.', description: '', categoryId: 0, revision: source.revision }, 'PUT');
  await page.getByRole('button', { name: source.title, exact: true }).click(); await page.getByTestId('prompt-focus').waitFor();
  assert.equal(await page.getByTestId('prompt-focus').getAttribute('data-revision'), String(fresh.revision));
  await page.getByRole('button', { name: 'Editar', exact: true }).click(); await editor();
  assert.equal(await page.getByLabel('Contenido', { exact: true }).inputValue(), fresh.templateText, 'Stale cached draft replaced the current source');
  assert.equal(await page.getByTestId('prompt-editor').getAttribute('data-revision'), String(fresh.revision));
  checks.push('stale-snapshot-cannot-replace-fresh-source-from-detail'); save('RUNNING');
  const pendingText = 'Borrador compatible con Unicode: C# & .NET 🚀.';
  await page.getByLabel('Contenido', { exact: true }).fill(pendingText); await leaveEditor();
  await page.getByRole('button', { name: 'Editar', exact: true }).click(); await editor();
  assert.equal(await page.getByLabel('Contenido', { exact: true }).inputValue(), pendingText);
  await leaveEditor(); await library();
  await page.getByRole('button', { name: 'Editar: ' + source.title, exact: true }).click(); await editor();
  assert.equal(await page.getByLabel('Contenido', { exact: true }).inputValue(), pendingText);
  assert.equal((await api('/prompts/' + source.id)).templateText, fresh.templateText, 'Unsaved cache wrote persistent content');
  checks.push('matching-snapshot-preserved-from-detail-and-library-without-persistence'); save('RUNNING');
  await leaveEditor(); await library();
  await page.getByRole('button', { name: 'Nuevo prompt', exact: true }).click(); await editor();
  await page.getByLabel('Título', { exact: true }).fill('Nuevo borrador independiente');
  await page.getByLabel('Contenido', { exact: true }).fill('Nueva instrucción todavía sin guardar.');
  await leaveEditor();
  await page.getByRole('button', { name: 'Editar: ' + source.title, exact: true }).click(); await editor();
  assert.equal(await page.getByLabel('Contenido', { exact: true }).inputValue(), pendingText);
  await leaveEditor(); await library();
  await page.getByRole('button', { name: 'Nuevo prompt', exact: true }).click(); await editor();
  assert.equal(await page.getByLabel('Título', { exact: true }).inputValue(), 'Nuevo borrador independiente');
  assert.equal(await page.getByLabel('Contenido', { exact: true }).inputValue(), 'Nueva instrucción todavía sin guardar.');
  checks.push('new-and-existing-snapshots-remain-independent'); save('RUNNING');
  const savedResponse = page.waitForResponse(response => response.url() === origin + '/api/v1/prompts' && response.request().method() === 'POST');
  await page.getByRole('button', { name: 'Guardar prompt', exact: true }).click(); const response = await savedResponse; assert.ok(response.ok());
  const saved = await response.json(); await page.getByTestId('prompt-focus').waitFor();
  assert.equal((await api('/prompts/' + saved.id)).templateText, 'Nueva instrucción todavía sin guardar.');
  await library(); await page.getByRole('button', { name: 'Nuevo prompt', exact: true }).click(); await editor();
  assert.equal(await page.getByLabel('Título', { exact: true }).inputValue(), '');
  assert.equal(await page.getByLabel('Contenido', { exact: true }).inputValue(), '');
  checks.push('successful-save-forgets-only-the-new-snapshot');
  await leaveEditor(); await page.getByRole('button', { name: 'Editar: ' + source.title, exact: true }).click(); await editor();
  assert.equal(await page.getByLabel('Contenido', { exact: true }).inputValue(), pendingText);
  const versionResponse = page.waitForResponse(result => result.url() === origin + '/api/v1/prompts/' + source.id && result.request().method() === 'PUT');
  await page.getByRole('button', { name: 'Guardar versión', exact: true }).click(); assert.ok((await versionResponse).ok());
  await page.getByTestId('prompt-focus').waitFor();
  const version = await api('/prompts/' + source.id); assert.equal(version.revision, fresh.revision + 1); assert.equal(version.templateText, pendingText);
  checks.push('existing-snapshot-saves-as-a-revision-through-normal-crud');
  await page.screenshot({ path: path.join(out, 'verified-saved-version.png'), animations: 'disabled' });
  verifiedArtifacts();
  save('PASS'); console.log('PASS', out);
})().catch(async error => { save('FAIL', error.message); fs.writeFileSync(path.join(out, 'failure.txt'), String(error.stack));
  await page?.screenshot({ path: path.join(out, 'failure.png') }).catch(() => {}); console.error(error); process.exitCode = 1;
}).finally(async () => { await browser?.close(); if (server?.exitCode === null) { server.kill(); await new Promise(resolve => server.once('exit', resolve)); }
  fs.writeFileSync(path.join(out, 'api.log'), log); });
