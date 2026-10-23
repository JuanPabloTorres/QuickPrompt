// Headed end-to-end comparison. Uses synthetic SQLite and a read-only Ollama
// proxy that records counts/timings, never model prompts or response content.
const fs = require('node:fs'), path = require('node:path'), http = require('node:http'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { createHash } = require('node:crypto');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), stamp = Date.now(), temporary = path.join(root, '.playwright/performance-' + stamp), out = path.join(root, 'docs/evidence/performance-' + stamp);
const reuseBaseline = process.argv.find(value => value.startsWith('--baseline='))?.slice('--baseline='.length);
fs.mkdirSync(temporary, { recursive: true }); fs.mkdirSync(out, { recursive: true });
const origin = 'http://127.0.0.1:5194', headers = { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' };
let browser, context, page, server, proxy, runId, log = ''; const chats = [], results = [], errors = [];
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(read, timeout = 420000) { const end = Date.now() + timeout; while (Date.now() < end) { const result = await read(); if (result) return result; await wait(100); } throw Error('QA_TIMEOUT'); }
async function api(route, body) { const response = await fetch(origin + '/api/v1' + route, { method: body ? 'POST' : 'GET', headers, body: body ? JSON.stringify(body) : undefined }); assert.ok(response.ok, route + ': ' + response.status); return response.status === 204 ? null : response.json(); }
async function stopApi() { if (server && server.exitCode === null) { server.kill(); await new Promise(resolve => { server.once('exit', resolve); setTimeout(resolve, 3000).unref(); }); } }
async function startApi(variant, dataPath) {
  const apiSource = variant === 'baseline' ? path.join(root, '.playwright/performance-baseline/api') : path.join(root, '.nova-build/performance/api');
  const webSource = variant === 'baseline' ? path.join(root, '.playwright/performance-baseline/web') : path.join(root, 'apps/web/dist');
  const folder = path.join(temporary, variant); fs.cpSync(apiSource, path.join(folder, 'api'), { recursive: true }); fs.cpSync(webSource, path.join(folder, 'web'), { recursive: true });
  let ready = false;
  server = spawn('dotnet', [path.join(folder, 'api/QuickPrompt.Api.dll'), '--QuickPrompt:Port=5194', '--QuickPrompt:Ollama=http://127.0.0.1:5195', '--QuickPrompt:DataPath=' + dataPath, '--QuickPrompt:WebRoot=' + path.join(folder, 'web'), '--QuickPrompt:Preparation:ReuseInference=' + (variant !== 'baseline')], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', chunk => { log += chunk; if (String(chunk).includes('Now listening on: ' + origin)) ready = true; }); server.stderr.on('data', chunk => { log += chunk; });
  await until(() => { if (server.exitCode !== null) throw Error('Owned API exited'); return ready; }, 20000);
}
async function measure(variant, repetition, source, value, showActivity) {
  context = await browser.newContext({ viewport: { width: 1280, height: 900 }, recordVideo: { dir: path.join(out, 'video') } });
  await context.addInitScript(() => localStorage.setItem('nova-interface-v1', JSON.stringify({ view: 'cards', executionMode: 'SAME_TAB', rememberDraft: false, agentSpeed: 'fast' })));
  page = await context.newPage(); page.setDefaultTimeout(15000); page.on('pageerror', error => errors.push(error.message));
  let snapshots = 0; page.on('request', request => { if (/\/nova\/live\/[0-9a-f-]+$/.test(request.url()) && request.method() === 'GET') snapshots++; });
  await page.goto(origin); await page.getByRole('button', { name: source.title, exact: true }).click(); await page.getByTestId('prompt-focus').waitFor();
  const launcher = page.getByRole('button', { name: 'Abrir asistente NOVA', exact: true }); if (await launcher.count()) await launcher.click();
  await page.getByRole('button', { name: 'Más acciones de NOVA', exact: true }).click(); await page.getByRole('button', { name: 'Completar variables', exact: true }).click();
  await page.getByRole('dialog').getByLabel('proyecto', { exact: true }).fill(value);
  const firstChat = chats.length, start = performance.now();
  const response = page.waitForResponse(response => response.url() === origin + '/api/v1/nova/live' && response.request().method() === 'POST');
  await page.getByRole('button', { name: 'Completar con NOVA', exact: true }).click(); const started = await response; assert.equal(started.status(), 200); runId = (await started.json()).runId;
  console.log('RUN', variant, repetition, runId);
  if (showActivity) await page.getByRole('button', { name: 'Actividad de NOVA', exact: true }).click();
  const result = await until(async () => { const view = await api('/nova/live/' + runId); if (['FAILED', 'BLOCKED', 'CANCELLED'].includes(view.run.status)) throw Error(variant + ': ' + view.run.status + '/' + view.run.errorCode); return view.run.status === 'COMPLETED' ? view : null; });
  const elapsedMs = Math.round(performance.now() - start), inference = chats.slice(firstChat);
  assert.equal(result.run.pauseRequested, false); assert.equal(result.observation.values.proyecto, value);
  assert.equal(await page.getByTestId('variable-field').inputValue(), value); assert.deepEqual(await api('/prompts/' + source.id), source);
  // Both variants must keep the task controllable and reflect the real value.
  if (variant === 'candidate') {
    if (!showActivity) await page.getByRole('button', { name: 'Ver última actividad de NOVA', exact: true }).click();
    await page.locator('.activity-fields pre').filter({ hasText: value }).first().waitFor();
  }
  await page.screenshot({ path: path.join(out, variant + '-' + repetition + '.png') });
  const events = await (await fetch(origin + '/api/v1/nova/live/' + runId + '/events?after=0')).text();
  const eventCount = events.split('\ndata: ').length - 1;
  results.push({ variant, repetition, activityOpenDuringExecution: showActivity, elapsedMs, modelCalls: inference.length, snapshotGets: snapshots, eventCount, inference });
  fs.writeFileSync(path.join(out, 'measurements.json'), JSON.stringify(results, null, 2)); console.log(JSON.stringify(results.at(-1)));
  runId = null; await context.close(); context = null;
}
(async () => {
  proxy = http.createServer((request, response) => {
    const start = performance.now(), requestChunks = [];
    request.on('data', chunk => { if (request.url === '/api/chat') requestChunks.push(chunk); });
    const upstream = http.request({ hostname: '127.0.0.1', port: 11434, path: request.url, method: request.method, headers: { ...request.headers, host: '127.0.0.1:11434' } }, remote => {
      response.writeHead(remote.statusCode || 502, remote.headers); const chunks = [];
      remote.on('data', chunk => { if (request.url === '/api/chat') chunks.push(chunk); }); remote.pipe(response);
      remote.on('end', () => { if (request.url === '/api/chat') { let usage = {}; try { const body = JSON.parse(Buffer.concat(chunks)), packet = JSON.parse(Buffer.concat(requestChunks)); usage = { model: packet.model, contextTokens: packet.options?.num_ctx, thinking: packet.think, requestHash: createHash('sha256').update(Buffer.concat(requestChunks)).digest('hex'), promptTokens: body.prompt_eval_count, generatedTokens: body.eval_count, loadMs: Math.round((body.load_duration || 0) / 1e6) }; } catch {} chats.push({ durationMs: Math.round(performance.now() - start), ...usage }); } });
    }); upstream.on('error', () => { if (!response.headersSent) response.writeHead(502); response.end(); }); response.on('close', () => upstream.destroy()); request.pipe(upstream);
  });
  await new Promise((resolve, reject) => { proxy.once('error', reject); proxy.listen(5195, '127.0.0.1', resolve); });
  browser = await chromium.launch({ channel: 'msedge', headless: false });
  let source; const baselineData = path.join(temporary, 'baseline.sqlite');
  const value = 'una aplicación React para organizar proyectos';
  if (reuseBaseline) {
    const previous = path.resolve(reuseBaseline);
    results.push(...JSON.parse(fs.readFileSync(path.join(previous, 'measurements.json'), 'utf8')).filter(item => item.variant === 'baseline'));
    assert.equal(results.length, 2); fs.copyFileSync(path.join(root, '.playwright', path.basename(previous), 'baseline.sqlite'), baselineData);
  } else {
    await startApi('baseline', baselineData);
    source = await api('/prompts', { title: 'Proyecto de rendimiento', description: 'Biblioteca sintética de QA.', templateText: 'Revisa el proyecto <proyecto> y enumera tres mejoras. No inventes datos.' });
    await measure('baseline', 1, source, value, false); await measure('baseline', 2, source, value, true); await stopApi();
  }
  const candidateData = path.join(temporary, 'candidate.sqlite'); fs.copyFileSync(baselineData, candidateData); await startApi('candidate', candidateData);
  if (!source) source = (await api('/prompts?size=12')).items.find(item => item.title === 'Proyecto de rendimiento');
  await measure('candidate', 1, source, value, true); await measure('candidate', 2, source, value, true);
  await measure('candidate', 3, source, 'un catálogo de productos con búsqueda y favoritos', true);
  await measure('candidate', 4, source, 'un catálogo de productos con búsqueda y favoritos', false);
  assert.deepEqual(errors, []);
  const baseline = results.find(item => item.variant === 'baseline' && item.repetition === 2), reused = results.find(item => item.variant === 'candidate' && item.repetition === 2);
  assert.ok(reused.modelCalls < baseline.modelCalls, 'Exact reuse must reduce model calls');
  fs.writeFileSync(path.join(out, 'validation.json'), JSON.stringify({ status: 'PASS', realQwen: true, headed: true, syntheticDataOnly: true, baselineEvidence: reuseBaseline || out, results, browserErrors: errors, repeatedTaskSpeedup: baseline.elapsedMs / reused.elapsedMs }, null, 2)); console.log('PASS', out);
})().catch(async error => { fs.writeFileSync(path.join(out, 'failure.txt'), String(error.stack)); if (page && !page.isClosed()) await page.screenshot({ path: path.join(out, 'failure.png') }).catch(() => {}); console.error(error); process.exitCode = 1; }).finally(async () => {
  if (runId && server?.exitCode === null) await api('/nova/live/' + runId + '/cancel', {}).catch(() => {});
  await context?.close().catch(() => {}); await browser?.close().catch(() => {}); await stopApi(); if (proxy) await new Promise(resolve => proxy.close(resolve)); fs.writeFileSync(path.join(out, 'api.log'), log);
});
