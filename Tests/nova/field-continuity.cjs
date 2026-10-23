// Real React + same-window execution + local Ollama. Isolated synthetic database.
// Installs nothing and never changes the configured model or Ollama server settings.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { createHash } = require('node:crypto');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const { openAssistant } = require('./ui-settings.cjs');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
const temp = path.join(root, '.playwright', 'field-continuity-' + stamp);
const out = path.join(root, 'docs/evidence/field-continuity-20261018', String(stamp));
const build = path.resolve(root, process.env.NOVA_TEST_BUILD || 'backend/QuickPrompt.Api/bin/Debug/net8.0/QuickPrompt.Api.dll');
const port = Number(process.env.NOVA_QA_PORT || 5236), origin = 'http://127.0.0.1:' + port;
assert.ok(fs.existsSync(build), 'Build API first.');
fs.mkdirSync(temp, { recursive: true }); fs.mkdirSync(out, { recursive: true });
let server, browser, page, serverLog = '';
const report = { status: 'RUNNING', syntheticOnly: true, build, origin, results: [], errors: [] };
report.artifacts = Object.fromEntries(['QuickPrompt.Api.dll', 'QuickPrompt.Application.dll', 'QuickPrompt.Infrastructure.dll', 'QuickPrompt.Domain.dll']
  .map(name => [name, createHash('sha256').update(fs.readFileSync(path.join(path.dirname(build), name))).digest('hex')]));
report.webIndexHash = createHash('sha256').update(fs.readFileSync(path.join(root, 'apps/web/dist/index.html'))).digest('hex');
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2));
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
async function api(route, body) {
  const response = await fetch(origin + '/api/v1' + route, { method: body ? 'POST' : 'GET',
    headers: { 'Content-Type': 'application/json', Origin: origin, 'X-QuickPrompt-Client': 'nova-v1' }, body: body ? JSON.stringify(body) : undefined,
    signal: AbortSignal.timeout(10000) });
  if (!response.ok) throw Error(response.status + ': ' + await response.text());
  return response.status === 204 ? null : response.json();
}
async function poll(check, milliseconds = 150000) {
  const deadline = Date.now() + milliseconds;
  while (Date.now() < deadline) { const value = await check(); if (value) return value; await wait(300); }
  throw Error('QA_TIMEOUT');
}
async function submitted(click) {
  const [response] = await Promise.all([page.waitForResponse(r => r.url() === origin + '/api/v1/nova/live' && r.request().method() === 'POST'), click()]);
  assert.equal(response.status(), 200); return (await response.json()).runId;
}
async function terminal(id) {
  return poll(async () => { const view = await api('/nova/live/' + id);
    assert.equal(view.run.pauseRequested, false);
    return ['COMPLETED', 'FAILED', 'BLOCKED', 'CANCELLED'].includes(view.run.status) ? view : null; });
}
async function moreAction(name) {
  await openAssistant(page);
  await page.getByRole('button', { name: 'Más acciones de NOVA', exact: true }).click();
  await page.getByRole('button', { name, exact: true }).click();
}
(async () => {
  server = spawn('dotnet', [build, '--QuickPrompt:Port=' + port,
    '--QuickPrompt:DataPath=' + path.join(temp, 'synthetic.sqlite'), '--QuickPrompt:WebRoot=' + path.join(root, 'apps/web/dist'),
    '--Logging:LogLevel:Microsoft=Warning', '--Logging:LogLevel:Microsoft.Hosting.Lifetime=Information'],
    { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', data => { serverLog += data; }); server.stderr.on('data', data => { serverLog += data; });
  await poll(() => { if (server.exitCode !== null) throw Error('API_EXITED'); return serverLog.includes('Now listening on: ' + origin); }, 25000);
  report.initialTotal = (await api('/prompts')).total; assert.equal(report.initialTotal, 0);
  const models = await (await fetch('http://127.0.0.1:11434/api/tags', { signal: AbortSignal.timeout(5000) })).json();
  const model = models.models.find(item => item.name === 'qwen3:8b');
  assert.ok(model, 'This evaluation requires the already installed qwen3:8b.');
  report.model = { name: model.name, digest: model.digest, details: model.details };
  const source = await api('/prompts', { title: 'QA · Continuidad de campos',
    templateText: 'Responde exactamente LISTO. No añadas explicaciones. Datos de referencia: <texto>. Destinatario: <publico>.' });
  report.source = source;
  browser = await chromium.launch({ channel: 'msedge', headless: process.env.NOVA_QA_HEADLESS === '1' });
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } }); page = await context.newPage();
  page.on('pageerror', error => { report.errors.push(String(error)); save(); });
  await page.goto(origin); await page.bringToFront();
  await page.getByRole('button', { name: source.title, exact: true }).click();
  await moreAction('Completar variables');
  const fill = page.getByRole('dialog', { name: 'Completar variables', exact: true });
  await fill.getByRole('textbox', { name: 'texto', exact: true }).fill('Datos sintéticos de prueba.');
  const fillId = await submitted(() => fill.getByRole('button', { name: 'Completar con NOVA', exact: true }).click());
  const filled = await terminal(fillId), fillPerformance = await api('/nova/live/' + fillId + '/performance');
  report.results.push({ id: 'partial-fill', view: filled, performance: fillPerformance }); save();
  assert.equal(filled.run.status, 'COMPLETED'); assert.equal(filled.observation.values.texto, 'Datos sintéticos de prueba.');
  assert.equal(filled.observation.values.publico || '', ''); assert.equal(fillPerformance.llmCalls, 0);
  assert.equal(await page.locator('[data-variable-name="texto"]').inputValue(), filled.observation.values.texto);
  assert.equal(await page.locator('[data-variable-name="publico"]').inputValue(), '');
  await page.screenshot({ path: path.join(out, 'partial-fill.png'), fullPage: true });
  await moreAction('Ejecutar con IA local');
  const execute = page.getByRole('dialog', { name: 'Ejecutar con IA local', exact: true });
  const pendingId = await submitted(() => execute.getByRole('button', { name: 'Ejecutar ahora', exact: true }).click());
  const pending = await terminal(pendingId), pendingPerformance = await api('/nova/live/' + pendingId + '/performance');
  report.results.push({ id: 'missing-input', view: pending, performance: pendingPerformance }); save();
  assert.equal(pending.run.status, 'BLOCKED'); assert.ok(pending.question.includes('publico'));
  assert.equal(pendingPerformance.llmCalls, 0); assert.equal(pending.observation.responseComplete, false);
  // A data-only reply resumes the original execution, not a newly classified fill task.
  await openAssistant(page);
  await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).fill('publico: equipo de pruebas');
  const resumedId = await submitted(() => page.getByRole('button', { name: 'Preparar prompt', exact: true }).click());
  const resumed = await terminal(resumedId), resumedPerformance = await api('/nova/live/' + resumedId + '/performance');
  report.results.push({ id: 'continue-execution', view: resumed, performance: resumedPerformance }); save();
  assert.equal(resumed.run.status, 'COMPLETED'); assert.equal(resumed.semantic.continuesPendingTask, true);
  assert.equal(resumed.observation.values.texto, 'Datos sintéticos de prueba.'); assert.equal(resumed.observation.values.publico, 'equipo de pruebas');
  assert.equal(resumed.observation.responseComplete, true); assert.equal(resumed.observation.response.trim(), 'LISTO');
  assert.equal(resumedPerformance.llmCalls, 1, 'Only the actual response generation should call the local model.');
  assert.equal(await page.locator('[data-variable-name="publico"]').inputValue(), 'equipo de pruebas');
  assert.equal(context.pages().length, 1); assert.equal((await api('/prompts/' + source.id)).revision, source.revision);
  await page.screenshot({ path: path.join(out, 'continued.png'), fullPage: true });
  const destination = page.getByRole('dialog', { name: 'Usa tu prompt preparado', exact: true });
  if (await destination.count()) {
    const close = destination.getByRole('button', { name: 'Cerrar diálogo', exact: true });
    await close.click(); await destination.waitFor({ state: 'hidden' });
  }
  // Desktop controls remain outside the open dock footprint.
  await openAssistant(page);
  report.desktop = await page.evaluate(() => {
    const panel = document.querySelector('.nova-dock-panel').getBoundingClientRect();
    const controls = [...document.querySelectorAll('main button, main textarea, main input')].filter(node => !node.closest('.nova-dock'));
    return { overflow: document.documentElement.scrollWidth > innerWidth, overlaps: controls.filter(node => {
      const r = node.getBoundingClientRect(); return r.width > 0 && r.height > 0 && r.left < panel.right && r.right > panel.left && r.top < panel.bottom && r.bottom > panel.top;
    }).map(node => node.getAttribute('aria-label') || node.id || node.textContent) };
  });
  assert.equal(report.desktop.overflow, false); assert.deepEqual(report.desktop.overlaps, []);
  await page.setViewportSize({ width: 390, height: 844 }); await page.waitForTimeout(350);
  await page.locator('[data-variable-name="publico"]').focus();
  report.mobileFocus = await page.locator('[data-variable-name="publico"]').evaluate(node => ({ active: document.activeElement.outerHTML.slice(0, 400), focused: document.activeElement === node,
    modal: node.closest('[role="dialog"]')?.outerHTML.slice(0, 250), insideDock: document.querySelector('.nova-dock').contains(node), field: node.outerHTML.slice(0, 400) })); save();
  await page.waitForFunction(() => {
    const field = document.querySelector('[data-variable-name="publico"]').getBoundingClientRect();
    const panel = document.querySelector('.nova-dock-panel').getBoundingClientRect();
    return !panel.width || !panel.height || field.left >= panel.right || field.right <= panel.left || field.top >= panel.bottom || field.bottom <= panel.top;
  }, null, { timeout: 3000 });
  report.mobile = await page.locator('[data-variable-name="publico"]').evaluate(node => {
    const field = node.getBoundingClientRect(), panel = document.querySelector('.nova-dock-panel').getBoundingClientRect();
    return { expanded: document.querySelector('.nova-dock').dataset.expanded, field: field.toJSON(), panel: panel.toJSON(),
      obscured: panel.width > 0 && panel.height > 0 && field.left < panel.right && field.right > panel.left && field.top < panel.bottom && field.bottom > panel.top };
  });
  assert.equal(report.mobile.obscured, false, 'The actual focused field must remain unobscured.');
  assert.equal(await page.locator('[data-variable-name="publico"]').evaluate(node => document.activeElement === node), true);
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
  await page.screenshot({ path: path.join(out, 'mobile-focused.png'), fullPage: true });
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.locator('[data-variable-name="publico"]').fill('');
  await openAssistant(page);
  await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).fill(
    process.env.NOVA_QA_NATURAL_REQUEST || 'Completa los datos indicados en el prompt abierto; conserva los otros campos y no ejecutes ni guardes.\ntexto: Segundo dato sintético.');
  const naturalId = await submitted(() => page.getByRole('button', { name: 'Preparar prompt', exact: true }).click());
  const natural = await terminal(naturalId), naturalPerformance = await api('/nova/live/' + naturalId + '/performance');
  report.results.push({ id: 'natural-partial-fill', view: natural, performance: naturalPerformance }); save();
  assert.equal(natural.run.status, 'COMPLETED'); assert.equal(natural.observation.values.texto, 'Segundo dato sintético.');
  assert.equal(natural.observation.values.publico || '', '');
  assert.ok(natural.decisions.every(decision => decision.toolName !== 'ui_execute_local'));
  assert.equal((await api('/prompts/' + source.id)).revision, source.revision);
  await page.screenshot({ path: path.join(out, 'natural-partial-fill.png'), fullPage: true });
  assert.deepEqual(report.errors, []); report.status = 'PASS'; save(); console.log('Evidence:', out);
})().catch(async error => {
  report.status = 'FAIL'; report.failure = String(error);
  if (page) { report.lastPage = await page.locator('body').innerText().catch(() => ''); await page.screenshot({ path: path.join(out, 'failure.png'), fullPage: true }).catch(() => {}); }
  save(); console.error(error); process.exitCode = 1;
}).finally(async () => {
  if (browser) await browser.close(); if (server) server.kill();
  fs.writeFileSync(path.join(out, 'server.log'), serverLog); save();
});
