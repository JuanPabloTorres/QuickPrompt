// The normal library/editor must work with an unreachable inference provider.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), stamp = Date.now(), port=Number(process.env.NOVA_QA_PORT||5267), origin = 'http://127.0.0.1:'+port;
const data = path.join(root, '.playwright', 'runtime-offline-' + stamp), out = path.join(root, 'docs/evidence/runtime-view', 'offline-' + stamp);
fs.mkdirSync(data, { recursive: true }); fs.mkdirSync(out, { recursive: true });
let server, browser, page, runId, log = ''; const checks = [];
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(check, ms = 30000) { const end = Date.now() + ms; while (Date.now() < end) { const value = await check(); if (value) return value; await delay(100); } throw Error('QA_TIMEOUT'); }
async function api(route) { const r = await fetch(origin + '/api/v1' + route); assert.ok(r.ok); return r.json(); }
(async () => {
  try {
    server = spawn('dotnet', [path.resolve(root, process.env.NOVA_RUNTIME_BUILD || '.playwright/runtime-build/Debug/net8.0/QuickPrompt.Api.dll'), '--QuickPrompt:Port='+port, '--QuickPrompt:DataPath=' + path.join(data, 'synthetic.sqlite'), '--QuickPrompt:WebRoot=' + path.resolve(root, process.env.NOVA_TEST_WEBROOT || 'apps/web/dist'), '--QuickPrompt:Ollama=http://127.0.0.1:1', ...JSON.parse(process.env.NOVA_AGENT_OPTIONS || '[]')], { cwd: root, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
    server.stdout.on('data', chunk => log += chunk); server.stderr.on('data', chunk => log += chunk);
    await until(() => { if (server.exitCode !== null) throw Error('QA API exited'); return log.includes('Now listening on: ' + origin); });
    browser = await chromium.launch({ channel: 'msedge', headless: true }); const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, reducedMotion: 'reduce' }); page = await context.newPage();
    await page.goto(origin); await page.getByTestId('library-ready').waitFor();
    assert.equal(await page.getByTestId('nova-live').count(), 0);
    await page.getByRole('button', { name: 'Nuevo prompt', exact: true }).click();
    await page.getByLabel('Título', { exact: true }).fill('Borrador manual sin IA');
    await page.getByLabel('Contenido', { exact: true }).fill('Explica <tema> con un ejemplo.');
    await page.getByRole('button', { name: 'Guardar prompt', exact: true }).click(); await page.getByTestId('prompt-focus').waitFor();
    const library = await api('/prompts'); assert.equal(library.total, 1); const saved = library.items[0];
    assert.equal(saved.title, 'Borrador manual sin IA'); assert.equal(saved.templateText, 'Explica <tema> con un ejemplo.'); checks.push('manual-create-with-provider-unreachable');
    assert.equal(await page.getByTestId('nova-live').count(), 0); checks.push('no-phantom-runtime-for-manual-work');
    await page.getByRole('button', { name: 'Abrir asistente NOVA', exact: true }).click();
    await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).fill('Abre el prompt y explícame para qué sirve.');
    const [response] = await Promise.all([page.waitForResponse(r => r.url() === origin + '/api/v1/nova/live' && r.request().method() === 'POST'), page.getByRole('button', { name: 'Preparar prompt', exact: true }).click()]);
    runId = (await response.json()).runId;
    const failed = await until(async () => { const view = await api('/nova/live/' + runId); return ['FAILED', 'BLOCKED'].includes(view.run.status) ? view : null; });
    assert.equal(failed.runtime.modelState, 'error'); assert.equal(failed.run.effectCommitted, false); checks.push('provider-failure-visible-no-effects');
    assert.equal((await api('/prompts/' + saved.id)).templateText, saved.templateText);
    const launcher = page.getByRole('button', { name: 'Abrir asistente NOVA', exact: true }); if (await launcher.count()) await launcher.click();
    await page.getByRole('button', { name: 'Ver última actividad de NOVA', exact: true }).click(); await page.getByTestId('agent-runtime').waitFor();
    assert.equal(await page.getByRole('button', { name: 'Pausar', exact: true }).count(), 0); await page.screenshot({ path: path.join(out, 'provider-unavailable.png') });
    checks.push('failure-history-without-obsolete-controls');
    fs.writeFileSync(path.join(out, 'acceptance.json'), JSON.stringify({ status: 'PASS', providerEndpoint: 'http://127.0.0.1:1', checks }, null, 2)); console.log(JSON.stringify({ status: 'PASS', out, checks }));
  } catch (error) {
    await page?.screenshot({ path: path.join(out, 'failure.png') }).catch(() => {});
    fs.writeFileSync(path.join(out, 'acceptance.json'), JSON.stringify({ status: 'FAIL', checks, error: error.stack }, null, 2)); console.error(error.stack); process.exitCode = 1;
  } finally { await browser?.close(); if (server?.exitCode === null) server.kill(); fs.writeFileSync(path.join(out, 'server.log'), log); }
})();
