const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), origin = 'http://127.0.0.1:5225';
const out = path.join(root, 'docs/evidence/goal08/baseline-' + Date.now()); fs.mkdirSync(out, { recursive: true });
const data = path.join(root, '.playwright/goal08-baseline-' + Date.now() + '.sqlite');
let server, browser, log = ''; const events = [];
async function api(route, body) { const r = await fetch(origin + '/api/v1' + route, { method: body ? 'POST' : 'GET', headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' }, body: body ? JSON.stringify(body) : undefined }); assert.ok(r.ok); return r.json(); }
(async () => {
  server = spawn('dotnet', [path.join(root, '.playwright/agentic-final-build/Debug/net8.0/QuickPrompt.Api.dll'), '--QuickPrompt:Port=5225', '--QuickPrompt:DataPath=' + data, '--QuickPrompt:WebRoot=' + path.join(root, 'apps/web/dist')], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', x => log += x); server.stderr.on('data', x => log += x);
  for (let i = 0; i < 100; i++) { if (server.exitCode !== null) throw Error('Server exited'); if (log.includes('Now listening on: ' + origin)) break; await new Promise(r => setTimeout(r, 100)); }
  const source = await api('/prompts', { title: 'Escribir un correo profesional', templateText: 'Redacta un correo dirigido a <destinatario> para <objetivo>. Usa estos datos: <contexto>. Incluye asunto, saludo, mensaje y un cierre con el siguiente paso. Sé claro, cordial y breve. Conserva nombres, fechas y compromisos tal como se proporcionan; no inventes información.' });
  browser = await chromium.launch({ channel: 'msedge', headless: false }); const page = await browser.newPage();
  page.on('response', async r => { if (r.url().endsWith('/nova/prepare')) { try { events.push(await r.text()); } catch {} } });
  await page.goto(origin);
  const input = 'Busca el prompt Escribir un correo profesional y prepara una versión más breve.';
  await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).fill(input); await page.getByRole('button', { name: 'Preparar prompt', exact: true }).click();
  await page.getByRole('dialog').first().waitFor({ timeout: 180000 });
  await page.screenshot({ path: path.join(out, 'result.png'), fullPage: true });
  const after = await api('/prompts/' + source.id); assert.deepEqual(after, source);
  fs.writeFileSync(path.join(out, 'audit.json'), JSON.stringify({ input, model: 'qwen3:8b', browser: 'msedge', headed: true, sourceUnchanged: true, sourceId: source.id, visibleResult: await page.getByRole('dialog').first().innerText(), rawPreparationSse: events, observation: 'Existing preparation endpoint emits coarse stage/response/complete events; no model-selected DOM actions, pause or resumable execution event IDs.' }, null, 2));
  console.log(JSON.stringify({ status: 'REPRODUCED', out, sourceUnchanged: true }));
})().catch(e => { console.error(e); fs.writeFileSync(path.join(out, 'failure.json'), JSON.stringify({ error: e.message })); process.exitCode = 1; }).finally(async () => { await browser?.close(); if (server?.exitCode === null) server.kill(); });
