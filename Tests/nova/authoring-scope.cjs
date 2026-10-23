// Synthetic headed QA: real React, same-tab agent, real local Qwen and native approval.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const http = require('node:http'), { spawn } = require('node:child_process');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const { openAssistant } = require('./ui-settings.cjs');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
const temp = path.join(root, '.playwright', 'authoring-scope-ui-' + stamp);
const out = path.join(root, 'docs/evidence/authoring-scope-20261018', 'ui-' + stamp);
const build = path.resolve(root, process.env.NOVA_TEST_BUILD || 'backend/QuickPrompt.Api/bin/Debug/net8.0/QuickPrompt.Api.dll');
assert.ok(fs.existsSync(build), 'Build the API first; this test never installs a model or browser.');
fs.mkdirSync(temp, { recursive: true }); fs.mkdirSync(out, { recursive: true });
fs.cpSync(path.dirname(build), path.join(temp, 'api'), { recursive: true });
const origin = 'http://127.0.0.1:5238', proxyOrigin = 'http://127.0.0.1:5239';
const cases = [
  { id: 'meeting', approve: true, title: 'AUDITORÍA · Resumen de reuniones', variables: ['notas'],
    message: 'Crea un prompt reutilizable titulado AUDITORÍA · Resumen de reuniones para resumir <notas> en español en un máximo de cinco viñetas con decisiones y tareas. Cuando no consten responsables o fechas indica «no especificado». No inventes datos. No resumas una reunión ahora y no incluyas en el cuerpo instrucciones para crear, guardar o aprobar este prompt en QuickPrompt.' },
  { id: 'english-future-approval', approve: false, title: 'Facturas sintéticas', variables: ['factura', 'hoja', 'cliente'],
    message: "Titula el prompt 'Facturas sintéticas'. Redáctalo en inglés para un asistente futuro que lea <factura>, añada sus datos a <hoja> y pida aprobación antes de enviar el mensaje a <cliente>. Si faltan datos, debe preguntarlos sin inventar. Solo crea la instrucción; no realices esas operaciones ahora." },
  { id: 'mixed-clause', approve: false, variables: ['codigo'],
    message: 'Crea un prompt para revisar <codigo> y señalar tres problemas verificables sin revisarlo ahora ni guardar todavía' }
];
const report = { status: 'RUNNING', syntheticOnly: true, realModel: 'qwen3:8b', sameTab: true, build, temp, results: [], errors: [] };
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2));
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
let server, proxy, browser, context, page, activeId, lease, sequence = 0, serverLog = '';
async function api(route) {
  const response = await fetch(origin + '/api/v1' + route, { signal: AbortSignal.timeout(8000) });
  if (!response.ok) throw Error(await response.text());
  return response.json();
}
async function poll(check, timeout = 240000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) { const value = await check(); if (value) return value; await wait(500); }
  throw Error('QA_TIMEOUT');
}
async function runView() { return api('/nova/live/' + activeId); }
function publicView(view) { return { run: view.run, question: view.question, observation: view.observation,
  proposal: view.proposal && { draft: view.proposal.draft, promptId: view.proposal.promptId, revision: view.proposal.revision } }; }
async function reviewPanel() {
  await openAssistant(page);
  if (!await page.getByRole('button', { name: 'Revisar y guardar versión', exact: true }).count()) {
    const activity = page.getByRole('button', { name: 'Actividad de NOVA', exact: true });
    if (await activity.getAttribute('aria-expanded') !== 'true') await activity.click();
  }
  await page.getByRole('button', { name: 'Revisar y guardar versión', exact: true }).click();
}
function checkBody(item, body) {
  for (const name of item.variables) assert.ok(body.includes('<' + name + '>'), 'Missing variable ' + name);
  // Acceptance oracles for these fixed synthetic cases, never production intent routing.
  if (item.id === 'meeting') {
    assert.match(body, /cinco|5/iu); assert.match(body, /decisiones/iu); assert.match(body, /tareas/iu);
    assert.match(body, /no especificado/iu); assert.match(body, /no invent/iu);
    assert.doesNotMatch(body, /ahora|QuickPrompt|no resum|guard|aprobar/iu);
  } else if (item.id === 'english-future-approval') {
    assert.match(body, /read/iu); assert.match(body, /approval|permission|authorization/iu);
    assert.match(body, /send/iu); assert.match(body, /missing/iu);
    assert.doesNotMatch(body, /no realices|solo crea|do not.*now|write.*instruction/iu);
  } else {
    assert.match(body, /tres|three|3/iu); assert.match(body, /problemas|issues/iu);
    assert.doesNotMatch(body, /ahora|todavía|guard|no.*revis/iu);
  }
}
(async () => {
  lease = fs.openSync(path.join(require('node:os').tmpdir(), 'QuickPrompt.Nova.SemanticModel.lease'), 'a');
  proxy = http.createServer(async (req, res) => {
    const number = ++sequence, abort = new AbortController();
    res.on('close', () => { if (!res.writableEnded) abort.abort(); });
    let packet;
    try {
      let raw = ''; for await (const chunk of req) raw += chunk;
      packet = raw ? JSON.parse(raw) : null;
      const response = await fetch('http://127.0.0.1:11434' + req.url, { method: req.method,
        headers: { 'Content-Type': 'application/json' }, body: raw || undefined, signal: abort.signal });
      const text = await response.text(), parsed = JSON.parse(text);
      if (parsed.message) delete parsed.message.thinking; // Never archive private reasoning.
      fs.writeFileSync(path.join(out, 'model-' + number + '.json'), JSON.stringify({ runId: req.headers['x-nova-run-id'], request: packet, response: parsed }, null, 2));
      res.writeHead(response.status, { 'Content-Type': 'application/json' }); res.end(text);
    } catch (error) {
      fs.writeFileSync(path.join(out, 'model-' + number + '.json'), JSON.stringify({ request: packet, error: error.name }));
      if (!res.headersSent) res.writeHead(502); if (!res.destroyed) res.end('{}');
    }
  });
  await new Promise((resolve, reject) => { proxy.once('error', reject); proxy.listen(5239, '127.0.0.1', resolve); });
  server = spawn('dotnet', [path.join(temp, 'api/QuickPrompt.Api.dll'), '--QuickPrompt:Port=5238',
    '--QuickPrompt:Ollama=' + proxyOrigin, '--QuickPrompt:DataPath=' + path.join(temp, 'synthetic.sqlite'),
    '--QuickPrompt:WebRoot=' + path.join(root, 'apps/web/dist'), '--Logging:LogLevel:Microsoft=Warning', '--Logging:LogLevel:Microsoft.Hosting.Lifetime=Information'],
    { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', data => { serverLog += data; }); server.stderr.on('data', data => { serverLog += data; });
  await poll(async () => { if (server.exitCode !== null) throw Error('QA_API_EXITED'); return serverLog.includes('Now listening on: ' + origin); }, 20000);
  browser = await chromium.launch({ channel: 'msedge', headless: process.env.NOVA_QA_HEADLESS === '1' });
  context = await browser.newContext({ viewport: { width: 1440, height: 1000 } }); page = await context.newPage();
  page.on('pageerror', error => { report.errors.push(String(error)); save(); });
  report.initialTotal = (await api('/prompts')).total; assert.equal(report.initialTotal, 0);
  for (const item of cases) {
    const row = { id: item.id, message: item.message, status: 'RUNNING' }; report.results.push(row); save();
    await page.goto(origin); await page.bringToFront(); await openAssistant(page);
    await page.getByRole('button', { name: 'Crear prompt', exact: true }).first().click();
    await page.getByRole('textbox', { name: '¿Qué prompt quieres crear?', exact: true }).fill(item.message);
    const [response] = await Promise.all([page.waitForResponse(r => r.url() === origin + '/api/v1/nova/live' && r.request().method() === 'POST'),
      page.getByRole('button', { name: 'Crear con NOVA', exact: true }).click()]);
    assert.equal(response.status(), 200); activeId = (await response.json()).runId; row.runId = activeId; save();
    const close = page.getByRole('button', { name: 'Cerrar asistente NOVA', exact: true }); if (await close.count()) await close.click();
    let lastLog = 0;
    const view = await poll(async () => {
      const v = await runView(); row.latest = publicView(v);
      if (Date.now() - lastLog > 10000) { lastLog = Date.now(); console.log(item.id, v.run.status); save(); }
      assert.equal(v.run.pauseRequested, false);
      if (['FAILED', 'BLOCKED', 'CANCELLED'].includes(v.run.status)) throw Error(JSON.stringify(publicView(v)));
      return v.run.status === 'AWAITING_APPROVAL' ? v : null;
    });
    row.beforeApproval = publicView(view); row.readyPerformance = await api('/nova/live/' + activeId + '/performance');
    row.form = { title: await page.getByRole('textbox', { name: 'Título', exact: true }).inputValue(),
      body: await page.getByRole('textbox', { name: 'Contenido', exact: true }).inputValue() };
    assert.equal(row.form.title, view.proposal.draft.title); assert.equal(row.form.body, view.proposal.draft.templateText);
    if (item.title) assert.equal(row.form.title, item.title);
    checkBody(item, row.form.body);
    row.variableFields = await page.locator('[data-variable-name]').evaluateAll(nodes => nodes.map(node => ({ name: node.getAttribute('data-variable-name'), value: node.value })));
    assert.deepEqual(row.variableFields.map(field => field.name).sort(), [...item.variables].sort());
    assert.ok(row.variableFields.every(field => field.value === ''), 'Declarations must not become case values.');
    assert.equal(context.pages().length, 1, 'The agent must use the original page.');
    assert.equal(view.run.effectCommitted, false); row.totalBefore = (await api('/prompts')).total;
    await page.screenshot({ path: path.join(out, item.id + '-form.png'), fullPage: true }); save();
    await reviewPanel();
    await page.getByRole('button', { name: item.approve ? 'Confirmar guardado de esta versión' : 'Rechazar propuesta', exact: true }).click();
    const final = await poll(async () => { const v = await runView(); return ['COMPLETED', 'CANCELLED', 'FAILED', 'BLOCKED'].includes(v.run.status) ? v : null; });
    row.final = publicView(final); const listing = await api('/prompts'); row.totalAfter = listing.total;
    if (item.approve) {
      assert.equal(final.run.status, 'COMPLETED'); assert.equal(final.run.approval, 'CONSUMED'); assert.equal(final.run.effectCommitted, true);
      assert.equal(row.totalAfter, row.totalBefore + 1);
      row.saved = await api('/prompts/' + final.run.targetId); assert.equal(row.saved.templateText, row.form.body); assert.equal(row.saved.title, row.form.title);
      assert.deepEqual(row.saved.defaults, {});
    } else {
      assert.equal(final.run.status, 'CANCELLED'); assert.equal(final.run.effectCommitted, false); assert.equal(row.totalAfter, row.totalBefore);
    }
    row.status = 'PASS'; save(); console.log(item.id, 'PASS'); activeId = null;
  }
  assert.deepEqual(report.errors, []); report.status = 'PASS'; save(); console.log('Evidence:', out);
})().catch(async error => {
  report.status = 'FAIL'; report.failure = String(error); if (page) {
    report.lastPage = { url: page.url(), body: await page.locator('body').innerText().catch(() => '') };
    await page.screenshot({ path: path.join(out, 'failure.png'), fullPage: true }).catch(() => {});
  }
  save(); console.error(error); process.exitCode = 1;
}).finally(async () => {
  if (context) await context.close(); if (browser) await browser.close(); if (server) server.kill();
  if (proxy) proxy.close(); if (lease !== undefined) fs.closeSync(lease);
  fs.writeFileSync(path.join(out, 'server.log'), serverLog); save();
});
