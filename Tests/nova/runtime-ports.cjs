// Real JS interop, bridge fallback and optional installed-model Quick Assist.
// Two owned hosts share synthetic SQLite; external destinations are blocked.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process'), { createHash } = require('node:crypto');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
assert.ok(process.env.NOVA_TEST_BUILD && process.env.LEGACY_TEST_BUILD && process.env.LEGACY_TEST_CONTENT, 'Provide frozen owned builds/content.');
const apiBuild = path.resolve(root, process.env.NOVA_TEST_BUILD), oldBuild = path.resolve(root, process.env.LEGACY_TEST_BUILD);
const temporary = path.join(root, '.playwright/runtime-ui-' + stamp), out = path.join(root, 'docs/evidence/runtime-ui-' + stamp);
fs.mkdirSync(temporary); fs.mkdirSync(out);
const apiOrigin = 'http://127.0.0.1:5253', origin = 'http://127.0.0.1:5252', database = path.join(temporary, 'synthetic.sqlite');
const headers = { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' }, assemblies = ['QuickPrompt.Domain.dll', 'QuickPrompt.Application.dll', 'QuickPrompt.Infrastructure.dll'];
const hash = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const report = { status: 'RUNNING', scope: 'RUNTIME_PORTS_AND_JS_INTEROP_NOT_GENERAL_AGENT_RELIABILITY', syntheticOnly: true,
  apiHashes: Object.fromEntries([...assemblies, 'QuickPrompt.Api.dll'].map(name => [name, hash(path.join(path.dirname(apiBuild), name))])),
  legacyHashes: Object.fromEntries([...assemblies, 'QuickPrompt.Web.dll'].map(name => [name, hash(path.join(path.dirname(oldBuild), name))])), checks: [], browserErrors: [], blockedDestinations: [], quickAssist: 'NOT_RUN' };
for (const name of assemblies) assert.equal(report.apiHashes[name], report.legacyHashes[name]);
const processes = [], logs = []; let browser, context, page;
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2));
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(read, timeout = 20000) { const end = Date.now() + timeout; while (Date.now() < end) { const result = await read(); if (result) return result; await wait(100); } throw Error('RUNTIME_QA_TIMEOUT'); }
async function start(build, port, name, extra) {
  const child = spawn('dotnet', [build, '--QuickPrompt:Port=' + port, '--QuickPrompt:DataPath=' + database, ...extra], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] }); processes.push(child);
  const capture = { name, text: '' }; logs.push(capture); child.stdout.on('data', data => { capture.text += data; }); child.stderr.on('data', data => { capture.text += data; });
  await until(() => { if (child.exitCode !== null) throw Error('OWNED_HOST_EXITED:' + name); return capture.text.includes('Now listening on: http://127.0.0.1:' + port); });
}
async function api(route, body, method = body ? 'POST' : 'GET') { const response = await fetch(apiOrigin + '/api/v1' + route, { method, headers, body: body ? JSON.stringify(body) : undefined }); assert.ok(response.ok, route + ':' + response.status); return response.status === 204 ? null : response.json(); }
const archive = () => api('/library/export');
const check = name => { report.checks.push({ name, status: 'PASS' }); save(); console.log('PASS', name); };
async function panel(input) {
  return page.evaluate(input => new Promise((resolve, reject) => {
    const id = crypto.randomUUID();
    const timer = setTimeout(() => { window.removeEventListener('message', onResult); reject(Error('PANEL_RESULT_TIMEOUT')); }, 12000);
    function onResult(event) { if (event.origin === location.origin && event.data?.source === 'qp.panel.result' && event.data.id === id) { clearTimeout(timer); window.removeEventListener('message', onResult); resolve(event.data); } }
    window.addEventListener('message', onResult); window.postMessage({ source: 'qp.panel.request', id, ...input }, location.origin);
  }), input);
}
(async () => {
  await start(apiBuild, 5253, 'api', []); await start(oldBuild, 5252, 'web', ['--contentRoot=' + path.resolve(root, process.env.LEGACY_TEST_CONTENT)]);
  let source = await api('/prompts', { title: 'QA · Puente local', templateText: 'Explica <tema>.', defaults: { tema: 'PRIVATE_DEFAULT_SYNTHETIC' } });
  browser = await chromium.launch({ channel: 'msedge', headless: false }); context = await browser.newContext({ viewport: { width: 1440, height: 1050 }, permissions: ['clipboard-read', 'clipboard-write'] });
  await context.route('**/*', route => { const url = route.request().url(); if (url.startsWith(origin) || /^(about|data|blob):/.test(url)) return route.continue(); report.blockedDestinations.push(url); save(); return route.abort(); });
  page = await context.newPage(); page.setDefaultTimeout(12000); page.on('pageerror', error => { report.browserErrors.push(String(error)); save(); });
  await page.goto(origin); await page.waitForFunction(() => Boolean(window.qp?.panelReference));
  const listed = await panel({ kind: 'library', search: source.title, favorites: false, page: 0 }); assert.ok(listed.result); assert.equal(listed.result.total, 1); assert.ok(!JSON.stringify(listed.result).includes('PRIVATE_DEFAULT')); assert.deepEqual(Object.keys(listed.result.items[0]).sort(), ['category', 'favorite', 'id', 'revision', 'text', 'title', 'variables']); assert.equal((await archive()).history.length, 0); check('real-panel-js-interop-projects-library-without-defaults-or-history');
  const compiled = await panel({ kind: 'compile', templateId: source.id, revision: source.revision, values: { tema: 'SQLite & 李' } }); assert.equal(compiled.result.text, 'Explica SQLite & 李.'); assert.equal((await archive()).history.length, 1); assert.equal((await api('/prompts/' + source.id)).uses, 1); check('panel-explicit-compile-retains-authoritative-text-and-one-history-entry');
  source = await api('/prompts/' + source.id, { title: source.title, templateText: 'Resume <tema>.', revision: source.revision, defaults: { tema: 'PRIVATE_DEFAULT_SYNTHETIC' } }, 'PUT'); const beforeStale = await archive();
  const stale = await panel({ kind: 'compile', templateId: source.id, revision: source.revision - 1, values: { tema: 'No guardar' } }); assert.ok(stale.error); assert.deepEqual(await archive(), beforeStale);
  const invalid = await panel({ kind: 'compile', templateId: source.id, revision: source.revision, values: { extra: 'No guardar' } }); assert.ok(invalid.error); assert.deepEqual(await archive(), beforeStale); check('panel-rejects-stale-and-unknown-field-requests-without-effects');
  await page.reload(); await page.locator('.card-open').filter({ has: page.getByRole('heading', { name: source.title, exact: true }) }).click(); await page.locator('#v-74656D61').fill('Síntesis');
  await page.getByRole('button', { name: 'Usar en…', exact: true }).click(); await page.getByRole('region', { name: 'Elegir destino', exact: true }).getByRole('button', { name: 'ChatGPT', exact: true }).click();
  const actions = page.getByTestId('provider-actions'); await actions.waitFor(); const popupEvent = context.waitForEvent('page'); await actions.getByRole('link', { name: 'Copiar y abrir ChatGPT', exact: true }).click(); const popup = await popupEvent;
  await until(async () => (await archive()).webTransfers.length === 1); await until(() => report.blockedDestinations.length === 1); await popup.close(); await page.bringToFront(); assert.equal(await page.evaluate(() => navigator.clipboard.readText()), 'Resume Síntesis.');
  const audit = (await archive()).webTransfers[0]; assert.equal(audit.code, 'copied-manual-paste'); assert.ok(!JSON.stringify(audit).includes('Síntesis')); assert.ok(!JSON.stringify(audit).includes('PRIVATE_DEFAULT')); assert.equal(report.blockedDestinations[0], 'https://chatgpt.com/'); check('real-manual-fallback-copies-text-and-persists-metadata-only-audit-through-port');
  await page.getByRole('button', { name: 'Configuración', exact: true }).click(); await page.locator('details.settings-advanced > summary').click(); await page.getByRole('button', { name: 'Verificar consistencia', exact: true }).click(); await page.getByText('SQLite integrity_check: ok', { exact: true }).waitFor(); check('diagnostics-port-is-resolved-and-operational-in-ui');
  if (process.env.RUN_REAL_QUICK_ASSIST === '1') {
    await page.getByRole('button', { name: 'Volver a la biblioteca', exact: true }).click(); await page.locator('.card-open').filter({ has: page.getByRole('heading', { name: source.title, exact: true }) }).click();
    await page.getByRole('button', { name: 'Quick Assist', exact: true }).click(); const assist = page.locator('#assist-dialog'); await assist.getByRole('textbox', { name: '¿Qué necesitas?', exact: true }).fill('Añade el prompt seleccionado a mis favoritos.');
    report.quickAssist = { status: 'REQUESTED', model: 'qwen3:8b' }; save(); const started = Date.now(); await assist.getByRole('button', { name: 'Interpretar con Qwen', exact: true }).click(); console.log('RUN real-quick-assist-model');
    await assist.getByRole('button', { name: 'Aprobar y aplicar plan', exact: true }).waitFor({ timeout: 180000 });
    const steps = await assist.locator('ol li').allTextContents(); report.quickAssist = { status: 'PLAN_OBSERVED', model: 'qwen3:8b', elapsedMs: Date.now() - started, steps }; save(); assert.equal(steps.length, 1); assert.ok(steps[0].includes('Guardar en favoritos')); assert.equal((await api('/prompts/' + source.id)).isFavorite, false);
    await assist.getByRole('button', { name: 'Aprobar y aplicar plan', exact: true }).click(); await until(async () => (await api('/prompts/' + source.id)).isFavorite === true); report.quickAssist.status = 'PASS_ONE_SYNTHETIC_TYPED_FAVORITE_NOT_GENERAL_SEMANTICS'; save(); check('real-quick-assist-through-moved-inference-contract-waits-for-approval-before-effect');
  }
  await page.screenshot({ path: path.join(out, 'runtime-final.png') }); assert.deepEqual(report.browserErrors, []); report.status = 'PASS'; save(); console.log('EVIDENCE', out);
})().catch(async error => { report.status = 'FAIL'; report.failure = String(error.stack); if (page && !page.isClosed()) { await page.screenshot({ path: path.join(out, 'failure.png') }).catch(() => {}); report.visibleText = await page.locator('body').innerText().catch(() => ''); } save(); console.error(error); process.exitCode = 1; }).finally(async () => {
  await context?.close().catch(() => {}); await browser?.close().catch(() => {}); for (const child of processes) if (child.exitCode === null) { child.kill(); await new Promise(resolve => { child.once('exit', resolve); setTimeout(resolve, 3000).unref(); }); } for (const log of logs) fs.writeFileSync(path.join(out, log.name + '.log'), log.text);
});
