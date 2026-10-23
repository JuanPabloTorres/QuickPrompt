// Real React/API and server-rendered archive consumers. Owned data/processes only.
// No model inference, provider mocks, downloads of dependencies or user database access.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process'), { createHash } = require('node:crypto');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
assert.ok(process.env.NOVA_TEST_BUILD && process.env.LEGACY_TEST_BUILD && process.env.LEGACY_TEST_CONTENT, 'Provide frozen builds and server-rendered content root.');
assert.ok(process.env.LEGACY_SQLITE_FIXTURE, 'Provide the owned synthetic SQLite fixture.');
const fixture = path.resolve(root, process.env.LEGACY_SQLITE_FIXTURE);
assert.ok(fixture.startsWith(path.join(root, '.playwright') + path.sep), 'Synthetic fixture must stay in the owned QA root.');
const apiBuild = path.resolve(root, process.env.NOVA_TEST_BUILD), oldBuild = path.resolve(root, process.env.LEGACY_TEST_BUILD);
const web = path.resolve(root, process.env.NOVA_TEST_WEBROOT || 'apps/web/dist');
const temporary = path.join(root, '.playwright/archive-ui-' + stamp), out = path.join(root, 'docs/evidence/archive-ui-' + stamp);
fs.mkdirSync(temporary); fs.mkdirSync(out);
const origin = 'http://127.0.0.1:5245', legacyOrigin = 'http://127.0.0.1:5246';
const headers = { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' };
const hash = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const report = { status: 'RUNNING', scope: 'HEADED_ARCHIVE_IMPORT_RESTORE_SHARING_PORTS_NOT_AGENT_SEMANTICS', syntheticOnly: true,
  apiHashes: Object.fromEntries(['QuickPrompt.Api.dll', 'QuickPrompt.Application.dll', 'QuickPrompt.Infrastructure.dll', 'QuickPrompt.Domain.dll'].map(name => [name, hash(path.join(path.dirname(apiBuild), name))])),
  legacyHashes: Object.fromEntries(['QuickPrompt.Web.dll', 'QuickPrompt.Application.dll', 'QuickPrompt.Infrastructure.dll', 'QuickPrompt.Domain.dll'].map(name => [name, hash(path.join(path.dirname(oldBuild), name))])),
  webIndexSHA256: hash(path.join(web, 'index.html')), legacyFixtureSHA256: hash(fixture), checks: [], browserErrors: [], externalRequests: [] };
const processes = [], logs = []; let browser, context, page;
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2));
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(read, timeout = 20000) { const end = Date.now() + timeout; while (Date.now() < end) { const result = await read(); if (result) return result; await wait(100); } throw Error('ARCHIVE_QA_TIMEOUT'); }
async function start(build, port, name, extra) {
  const child = spawn('dotnet', [build, '--QuickPrompt:Port=' + port, '--QuickPrompt:DataPath=' + path.join(temporary, name + '.sqlite'), ...extra], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'], cwd: root });
  processes.push(child); const capture = { name, text: '' }; logs.push(capture);
  child.stdout.on('data', data => { capture.text += data; }); child.stderr.on('data', data => { capture.text += data; });
  await until(() => { if (child.exitCode !== null) throw Error('OWNED_HOST_EXITED:' + name); return capture.text.includes('Now listening on: http://127.0.0.1:' + port); });
}
async function raw(route, body, method = body ? 'POST' : 'GET') { return fetch(origin + '/api/v1' + route, { method, headers, body: body ? JSON.stringify(body) : undefined }); }
async function api(route, body, method) { const response = await raw(route, body, method); assert.ok(response.ok, route + ':' + response.status); return response.status === 204 ? null : response.json(); }
const check = name => { report.checks.push({ name, status: 'PASS' }); save(); console.log('PASS', name); };
async function download(button, name) { const event = page.waitForEvent('download'); await button.click(); const file = path.join(temporary, name); await (await event).saveAs(file); return JSON.parse(fs.readFileSync(file, 'utf8')); }
const upload = (input, name, data) => input.setInputFiles({ name, mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(data)) });
async function reactSettings() { await page.getByRole('button', { name: 'Configuración', exact: true }).click(); const dialog = page.getByRole('dialog', { name: 'Configuración', exact: true }); await dialog.waitFor(); await dialog.locator('summary', { hasText: 'Importar o restaurar un archivo' }).click(); return dialog; }
async function legacyExport(name) { return download(page.getByRole('button', { name: 'Exportar / respaldo JSON', exact: true }), name); }
(async () => {
  await start(apiBuild, 5245, 'react', ['--QuickPrompt:WebRoot=' + web, ...JSON.parse(process.env.NOVA_AGENT_OPTIONS || '[]')]);
  const source = await api('/prompts', { title: 'QA · Archivo privado', templateText: 'Explica <tema>.', defaults: { tema: 'PRIVATE_DEFAULT_SYNTHETIC' } });
  const compiled = await api(`/prompts/${source.id}/compile`, { revision: source.revision, values: { tema: 'PRIVATE_HISTORY_SYNTHETIC' } });
  await api(`/prompts/${source.id}/usage`, { revision: source.revision, values: { tema: 'PRIVATE_HISTORY_SYNTHETIC' }, operationId: crypto.randomUUID(), expectedText: compiled.text });
  browser = await chromium.launch({ channel: 'msedge', headless: false }); context = await browser.newContext({ viewport: { width: 1280, height: 900 }, acceptDownloads: true }); page = await context.newPage(); page.setDefaultTimeout(10000);
  page.on('pageerror', error => { report.browserErrors.push(String(error)); save(); });
  page.on('request', request => { if (![origin, legacyOrigin].some(host => request.url().startsWith(host)) && !/^(data|blob):/.test(request.url())) report.externalRequests.push(request.url()); });
  await page.goto(origin); await page.getByTestId('library-ready').waitFor();
  await page.getByRole('button', { name: source.title, exact: true }).click(); await page.getByTestId('prompt-focus').waitFor();
  await page.getByRole('button', { name: 'Compartir plantilla', exact: true }).click();
  const shared = page.getByRole('dialog', { name: `Compartir «${source.title}»`, exact: true });
  const packet = await download(shared.getByRole('button', { name: 'Descargar plantilla (.json)', exact: true }), 'portable.json');
  const serialized = JSON.stringify(packet); assert.ok(!serialized.includes('PRIVATE_')); assert.ok(!serialized.includes(source.id));
  assert.deepEqual(Object.keys(packet.prompt).sort(), ['category', 'description', 'template', 'title']); await shared.getByRole('button', { name: 'Cancelar', exact: true }).click(); check('react-portable-share-excludes-identity-defaults-history');
  const dialog = await reactSettings(); const archive = await download(dialog.getByRole('button', { name: 'Exportar respaldo JSON', exact: true }), 'react-backup.json');
  assert.ok(JSON.stringify(archive).includes('PRIVATE_DEFAULT_SYNTHETIC')); assert.ok(JSON.stringify(archive).includes('PRIVATE_HISTORY_SYNTHETIC')); check('react-full-archive-retains-owned-private-values-and-history');
  const portable = { ...packet, prompt: { ...packet.prompt, title: 'QA · Importación', template: 'Resume <texto>.' } };
  await upload(dialog.locator('#library-file'), 'portable.json', portable); await dialog.getByRole('button', { name: 'Cancelar importación', exact: true }).click(); assert.equal((await api('/prompts')).total, 1); check('react-cancel-preview-does-not-import');
  await upload(dialog.locator('#library-file'), 'portable.json', portable); await dialog.getByRole('button', { name: 'Confirmar importación', exact: true }).click(); await until(async () => (await api('/prompts')).total === 2);
  await upload(dialog.locator('#library-file'), 'portable.json', portable); await dialog.getByRole('button', { name: 'Confirmar importación', exact: true }).click(); await dialog.getByText('0 prompts procesados.', { exact: false }).waitFor(); assert.equal((await api('/prompts')).total, 2); check('react-confirmed-import-is-additive-and-idempotent');
  await dialog.getByRole('checkbox', { name: 'Reemplazar toda la biblioteca desde un respaldo', exact: true }).check(); await upload(dialog.locator('#library-file'), 'backup.json', archive);
  const restore = dialog.getByRole('button', { name: 'Restaurar biblioteca', exact: true }); assert.equal(await restore.isEnabled(), false);
  const later = await api('/prompts', { title: 'QA · Posterior', templateText: 'Conservar hasta revisar.' });
  await dialog.getByRole('checkbox', { name: 'Confirmo reemplazar todos los datos actuales.', exact: false }).check(); await restore.click(); await dialog.getByRole('alert').filter({ hasText: 'La biblioteca cambió' }).waitFor(); assert.equal((await api('/prompts')).total, 3); assert.ok(await api('/prompts/' + later.id)); check('react-restore-requires-confirmation-and-rejects-stale-review');
  await page.screenshot({ path: path.join(out, 'react-stale-review.png') });
  await upload(dialog.locator('#library-file'), 'backup.json', archive); await dialog.getByRole('checkbox', { name: 'Confirmo reemplazar todos los datos actuales.', exact: false }).check(); await restore.click(); await until(async () => (await api('/prompts')).total === 1);
  assert.equal((await api('/prompts/' + source.id)).templateText, source.templateText); assert.equal(fs.readdirSync(path.join(temporary, 'backups')).filter(name => name.endsWith('.sqlite')).length, 1); check('react-fresh-restore-replaces-library-only-after-verified-backup');
  await dialog.getByRole('button', { name: 'Crear respaldo SQLite', exact: true }).click(); await dialog.getByRole('status').filter({ hasText: 'Respaldo SQLite verificado:' }).waitFor(); assert.equal(fs.readdirSync(path.join(temporary, 'backups')).filter(name => name.endsWith('.sqlite')).length, 2); check('react-explicit-backup-uses-shared-writer');
  await dialog.getByRole('checkbox', { name: 'Reemplazar toda la biblioteca desde un respaldo', exact: true }).uncheck();
  await dialog.locator('#library-file').setInputFiles(fixture); await dialog.getByRole('button', { name: 'Confirmar importación', exact: true }).click(); await until(async () => (await api('/prompts')).total === 2);
  assert.ok((await api('/prompts')).items.some(item => item.title === 'QA · SQLite legado')); assert.equal(hash(fixture), report.legacyFixtureSHA256); check('react-real-sqlite-upload-converts-through-binary-port-without-source-mutation');
  await start(oldBuild, 5246, 'legacy', ['--contentRoot=' + path.resolve(root, process.env.LEGACY_TEST_CONTENT)]);
  await page.goto(legacyOrigin); await page.getByRole('button', { name: 'Configuración', exact: true }).click(); await page.locator('summary', { hasText: 'Configuración avanzada' }).click();
  await upload(page.locator('#import-file'), 'portable.json', packet); const review = page.locator('#import-dialog'); await review.waitFor({ state: 'visible' });
  await review.getByRole('button', { name: 'Cancelar importación', exact: true }).click(); const empty = await legacyExport('legacy-empty.json'); assert.equal(empty.templates.length, 0); check('server-rendered-cancel-preview-does-not-import');
  await upload(page.locator('#import-file'), 'portable.json', packet); await review.getByRole('button', { name: 'Confirmar importación', exact: true }).click(); await review.waitFor({ state: 'hidden' }); const first = await legacyExport('legacy-first.json'); assert.equal(first.templates.length, 1);
  await upload(page.locator('#import-file'), 'portable.json', packet); await review.getByRole('button', { name: 'Confirmar importación', exact: true }).click(); await review.waitFor({ state: 'hidden' }); assert.equal((await legacyExport('legacy-idempotent.json')).templates.length, 1); check('server-rendered-shared-import-is-additive-and-idempotent');
  await upload(page.locator('#import-file'), 'portable.json', portable); await review.getByRole('button', { name: 'Confirmar importación', exact: true }).click(); await review.waitFor({ state: 'hidden' }); assert.equal((await legacyExport('legacy-second.json')).templates.length, 2);
  await upload(page.locator('#restore-file'), 'backup.json', first); page.once('dialog', event => event.dismiss()); await review.getByRole('button', { name: 'Confirmar importación', exact: true }).click(); assert.equal(await review.isVisible(), true); await review.getByRole('button', { name: 'Cancelar importación', exact: true }).click(); assert.equal((await legacyExport('legacy-cancel-restore.json')).templates.length, 2); check('server-rendered-cancel-restore-confirmation-preserves-library');
  await upload(page.locator('#restore-file'), 'backup.json', first); page.once('dialog', event => event.accept()); await review.getByRole('button', { name: 'Confirmar importación', exact: true }).click(); await review.waitFor({ state: 'hidden' }); assert.equal((await legacyExport('legacy-restored.json')).templates.length, 1); assert.equal(fs.readdirSync(path.join(temporary, 'backups')).filter(name => name.endsWith('.sqlite')).length, 3); check('server-rendered-restore-now-uses-confirmed-reviewed-backed-up-writer');
  await page.screenshot({ path: path.join(out, 'server-rendered-restored.png') });
  await page.locator('#import-file').setInputFiles(fixture); await review.getByRole('button', { name: 'Confirmar importación', exact: true }).click(); await review.waitFor({ state: 'hidden' });
  const converted = await legacyExport('legacy-sqlite-imported.json'); assert.equal(converted.templates.length, 2); assert.ok(converted.templates.some(item => item.title === 'QA · SQLite legado')); assert.equal(hash(fixture), report.legacyFixtureSHA256); check('server-rendered-real-sqlite-upload-shares-converter-and-keeps-source');
  await page.getByRole('button', { name: 'Volver a la biblioteca', exact: true }).click();
  await page.locator('.card-open').filter({ has: page.getByRole('heading', { name: source.title, exact: true }) }).click();
  await page.locator('details.prompt-options > summary').click(); await page.locator('summary', { hasText: 'Compartir plantilla' }).click();
  const oldPacket = await download(page.getByRole('button', { name: 'Descargar plantilla JSON', exact: true }), 'legacy-portable.json');
  assert.deepEqual(oldPacket, packet); check('server-rendered-sharing-resolves-shared-port-and-retains-portable-format');
  assert.deepEqual(report.browserErrors, []); assert.deepEqual(report.externalRequests, []); report.status = 'PASS'; save(); console.log('EVIDENCE', out);
})().catch(error => { report.status = 'FAIL'; report.failure = String(error.stack); save(); console.error(error); process.exitCode = 1; }).finally(async () => {
  await context?.close().catch(() => {}); await browser?.close().catch(() => {});
  for (const child of processes) if (child.exitCode === null) { child.kill(); await new Promise(resolve => { child.once('exit', resolve); setTimeout(resolve, 3000).unref(); }); }
  for (const log of logs) fs.writeFileSync(path.join(out, log.name + '.log'), log.text);
});
