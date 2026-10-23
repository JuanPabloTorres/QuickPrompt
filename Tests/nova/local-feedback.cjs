// Real local streaming/cancellation in the provider dialog, owned synthetic library only.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict'), { spawn } = require('node:child_process');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), name = 'local-feedback-' + Date.now(), temp = path.join(root, '.playwright', name), out = path.join(root, 'docs/evidence', name), origin = 'http://127.0.0.1:5196';
fs.mkdirSync(temp, { recursive: true }); fs.mkdirSync(out, { recursive: true }); fs.cpSync(path.join(root, 'apps/web/dist'), path.join(temp, 'web'), { recursive: true });
let api, browser, context, page, log = '', errors = [];
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
(async () => {
  api = spawn('dotnet', [path.join(root, 'backend/QuickPrompt.Api/bin/Debug/net8.0/QuickPrompt.Api.dll'), '--QuickPrompt:Port=5196', '--QuickPrompt:DataPath=' + path.join(temp, 'synthetic.sqlite'), '--QuickPrompt:WebRoot=' + path.join(temp, 'web')], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  api.stdout.on('data', c => log += c); api.stderr.on('data', c => log += c);
  for (let i = 0; i < 100 && !log.includes('Now listening on: ' + origin); i++) { if (api.exitCode !== null) throw Error(log); await sleep(200); }
  const seeded = await fetch(origin + '/api/v1/prompts', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' }, body: JSON.stringify({ title: 'Comprobar respuesta local', description: 'Instrucción sintética para observar una respuesta en curso.', templateText: 'Escribe una guía de 500 palabras para organizar un proyecto, con pasos concretos y ejemplos.' }) }); assert.equal(seeded.status, 201);
  browser = await chromium.launch({ channel: 'msedge', headless: false });
  context = await browser.newContext({ viewport: { width: 1024, height: 900 }, permissions: ['clipboard-read', 'clipboard-write'], recordVideo: { dir: path.join(out, 'video') } });
  page = await context.newPage(); page.on('pageerror', error => errors.push(error.message)); page.setDefaultTimeout(15000);
  await page.goto(origin); await page.getByRole('button', { name: 'Comprobar respuesta local', exact: true }).click(); await page.getByTestId('compile-ready').waitFor({ state: 'attached' });
  await page.getByRole('button', { name: 'Usar en…', exact: true }).click();
  const start = page.getByRole('button', { name: 'Ejecutar en este equipo', exact: true });
  const firstRequest = page.waitForRequest(request => request.url().endsWith('/ai/local/execute'));
  await start.click(); await firstRequest; await page.getByRole('button', { name: 'Detener', exact: true }).waitFor(); await sleep(500);
  await page.screenshot({ path: path.join(out, 'real-qwen-loading.png') });
  assert.equal(await page.locator('.status-feedback .nova-logo[data-state="generating"]').isVisible(), true);
  await page.getByRole('button', { name: 'Detener', exact: true }).click(); await start.waitFor();
  await page.getByText('Cancelado. No se guardó ningún resultado parcial.', { exact: true }).waitFor();
  assert.equal(await page.getByRole('button', { name: 'Copiar respuesta', exact: true }).count(), 0);
  assert.equal(await page.locator('.nova-logo[data-state="generating"]').count(), 0);
  await page.screenshot({ path: path.join(out, 'cancelled-cleanly.png') });
  const completed = page.waitForRequest(request => request.url().endsWith('/ai/local/execute'));
  const t = performance.now(); await start.click(); const nextRequest = await completed; const response = await nextRequest.response(); assert.equal(response.status(), 200);
  await page.getByRole('button', { name: 'Copiar respuesta', exact: true }).waitFor({ timeout: 240000 });
  // The app cancels its SSE reader after its revision-bound complete event;
  // Chromium may consequently discard Network.getResponseBody. Do not replay a request.
  const final = await page.locator('.local-response pre').innerText(); assert.ok(final.trim());
  await page.getByRole('button', { name: 'Copiar respuesta', exact: true }).click(); assert.equal((await page.evaluate(() => navigator.clipboard.readText())).replace(/\r\n/g, '\n'), final.replace(/\r\n/g, '\n'));
  assert.equal(await page.locator('.nova-logo[data-state="generating"]').count(), 0);
  await page.screenshot({ path: path.join(out, 'real-qwen-complete.png') });
  assert.deepEqual(errors, []); fs.writeFileSync(path.join(out, 'validation.json'), JSON.stringify({ status: 'PASS', headed: true, realOllama: true, syntheticDataOnly: true, checks: ['authentic-local-loading', 'cancel-clears-progress-and-partial-response', 'retry-stream-completes', 'complete-dom-clipboard-equal', 'stable-state-after-completion'], clipboardComparison: 'Windows CRLF normalized to LF only', elapsedMs: Math.round(performance.now() - t), browserErrors: errors }, null, 2)); console.log('PASS', out);
})().catch(async error => { console.error(error); fs.writeFileSync(path.join(out, 'failure.txt'), error.stack); await page?.screenshot({ path: path.join(out, 'failure.png') }).catch(() => {}); process.exitCode = 1; }).finally(async () => { await context?.close(); await browser?.close(); if (api?.exitCode === null) { const done = new Promise(resolve => api.once('exit', resolve)); api.kill(); await done; } fs.writeFileSync(path.join(out, 'api.log'), log); });
