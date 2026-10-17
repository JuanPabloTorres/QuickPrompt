const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { createHash } = require('node:crypto');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), origin = 'http://127.0.0.1:5226';
const out = path.join(root, process.env.NOVA_EVIDENCE || 'docs/evidence/goal08/real-' + Date.now()); fs.mkdirSync(out, { recursive: true });
const data = path.join(root, '.playwright/goal08-real-' + Date.now() + '.sqlite');
const frozen = path.join(root, '.playwright/goal08-artifacts-' + Date.now()); fs.mkdirSync(frozen, { recursive: true });
fs.cpSync(path.join(root, 'apps/web/dist'), path.join(frozen, 'web'), { recursive: true });
fs.copyFileSync(path.join(root, '.playwright/browser-worker-build/worker.cjs'), path.join(frozen, 'worker.cjs'));
let server, browser, page, log = ''; const checks = [], runIds = [], errors = [];
function pass(name) { checks.push({ name, status: 'PASS' }); console.log('PASS', name); }
async function api(route, body) { const r = await fetch(origin + '/api/v1' + route, { method: body ? 'POST' : 'GET', headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' }, body: body ? JSON.stringify(body) : undefined }); const result = await r.json(); assert.ok(r.ok, JSON.stringify(result)); return result; }
async function until(work, predicate, max = 180000) { const start = Date.now(); while (Date.now() - start < max) { const result = await work(); if (predicate(result)) return result; await new Promise(r => setTimeout(r, 150)); } throw Error('Acceptance wait exceeded'); }
async function waitRun(id, states) { const result = await until(() => api('/nova/live/' + id), view => states.includes(view.run.status) || ['FAILED','BLOCKED','CANCELLED'].includes(view.run.status)); if (!states.includes(result.run.status)) throw Error('Unexpected run: ' + JSON.stringify(result)); return result; }
async function ask(text) {
  await page.getByLabel('Modo de NOVA', { exact: true }).selectOption('BROWSER');
  await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).fill(text);
  const [r] = await Promise.all([page.waitForResponse(r => r.url() === origin + '/api/v1/nova/live' && r.request().method() === 'POST'), page.getByRole('button', { name: 'Preparar prompt', exact: true }).click()]);
  const run = await r.json(); assert.ok(r.ok(), JSON.stringify(run)); runIds.push(run.runId); return run.runId;
}
async function followup(text) {
  await page.getByLabel('Instrucción para NOVA', { exact: true }).fill(text);
  const [r] = await Promise.all([page.waitForResponse(r => r.url() === origin + '/api/v1/nova/live' && r.request().method() === 'POST'), page.getByRole('button', { name: 'Ejecutar en navegador visible', exact: true }).click()]);
  const run = await r.json(); assert.ok(r.ok(), JSON.stringify(run)); runIds.push(run.runId); return run.runId;
}
async function capture(id, name) {
  const view = await api('/nova/live/' + id); const response = await fetch(origin + '/api/v1/nova/live/' + id + '/events');
  // For a review checkpoint the stream is intentionally still open; capture a snapshot then cancel only the observer.
  const reader = response.body.getReader(), decoder = new TextDecoder(); let raw = ''; while (true) { const {done,value} = await reader.read(); if(done)break; raw += decoder.decode(value, { stream: true }); const frames = [...raw.matchAll(/^data: (.*)$/gm)].map(m => JSON.parse(m[1])); if (frames.at(-1)?.sequence >= view.run.sequence) { await reader.cancel(); break; } }
  const events = [...raw.matchAll(/^data: (.*)$/gm)].map(m => JSON.parse(m[1]));
  events.forEach((event, index) => { assert.equal(event.runId, id); assert.equal(event.sequence, index + 1); assert.equal(typeof event.safeDisplayMessage, 'string'); });
  fs.writeFileSync(path.join(out, name + '.json'), JSON.stringify({ view, events, model: 'qwen3:8b', headed: true, browser: 'msedge', data }, null, 2));
  await until(async () => page.getByTestId('nova-live').getAttribute('data-status'), status => status === view.run.status);
  await page.screenshot({ path: path.join(out, name + '.png'), fullPage: true }); return {view,events};
}
(async () => {
  server = spawn('dotnet', [path.resolve(root, process.env.NOVA_TEST_BUILD || '.playwright/goal08-build/Debug/net8.0/QuickPrompt.Api.dll'), '--QuickPrompt:Port=5226', '--QuickPrompt:DataPath=' + data, '--QuickPrompt:WebRoot=' + path.join(frozen, 'web'), '--QuickPrompt:Live:Worker=' + path.join(frozen, 'worker.cjs')], { cwd: root, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'], env: { ...process.env, NOVA_UI_QA_OUT: path.join(out, 'worker'), NOVA_UI_QA_APPROVED: 'synthetic-only' } });
  server.stdout.on('data', x => log += x); server.stderr.on('data', x => log += x);
  for (let i = 0; i < 100; i++) { if (server.exitCode !== null) throw Error('Server exited'); if (log.includes('Now listening on: ' + origin)) break; await new Promise(r => setTimeout(r, 100)); }
  const dll = path.resolve(root, process.env.NOVA_TEST_BUILD || '.playwright/goal08-build/Debug/net8.0/QuickPrompt.Api.dll');
  const modelInfo = await (await fetch('http://127.0.0.1:11434/api/show', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ model: 'qwen3:8b' }) })).json();
  fs.writeFileSync(path.join(out, 'environment.json'), JSON.stringify({ model: 'qwen3:8b', details: modelInfo.details, browser: 'msedge', headed: true, frozenReactAndWorker: true, hashes: Object.fromEntries([dll, path.join(path.dirname(dll), 'QuickPrompt.Application.dll'), path.join(path.dirname(dll), 'QuickPrompt.Infrastructure.dll'), path.join(frozen, 'worker.cjs'), path.join(frozen, 'web/index.html')].map(file => [path.basename(file), createHash('sha256').update(fs.readFileSync(file)).digest('hex')])), parameters: { decision: { temperature: 0, num_ctx: 12288, num_predict: 1000 }, sourceEditor: { temperature: 0.2, num_ctx: 8192, num_predict: 1800 }, maxDecisions: 12, totalTimeoutSeconds: 480 } }, null, 2));
  const source = await api('/prompts', { title: 'Escribir un correo profesional', templateText: 'Redacta un correo dirigido a <destinatario> para <objetivo>. Usa estos datos: <contexto>. Incluye asunto, saludo, mensaje y un cierre con el siguiente paso. Sé claro, cordial y breve. Conserva nombres, fechas y compromisos tal como se proporcionan; no inventes información.' });
  const architecture = await api('/prompts', { title: 'Revisar arquitectura', templateText: 'Revisa cuidadosamente la arquitectura de <proyecto>. Considera sus componentes y los límites entre las distintas responsabilidades. Cuando hayas terminado esta revisión, responde únicamente con la palabra LISTO, sin añadir explicaciones ni comentarios adicionales.', defaults: { proyecto: 'una API .NET pública de ejemplo' } });
  browser = await chromium.launch({ channel: 'msedge', headless: false }); const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, reducedMotion: 'reduce' }); page = await context.newPage();
  page.on('pageerror', error => errors.push(error.message)); await page.goto(origin);
  if (process.argv.includes('--manual-only')) {
    await page.getByRole('button', { name: architecture.title, exact: true }).click(); await page.getByTestId('prompt-focus').waitFor();
    await page.getByRole('button', { name: 'Sugerir valores', exact: true }).click();
    await until(async () => page.getByTestId('nova-live').getAttribute('data-run-id'), value => !!value);
    const suggestion = await page.getByTestId('nova-live').getAttribute('data-run-id'); runIds.push(suggestion); await waitRun(suggestion, ['COMPLETED']);
    const suggested = await capture(suggestion, 'manual-variable-suggestions'); assert.ok(suggested.events.some(e => e.stage === 'Variables' && e.status === 'COMPLETED')); assert.deepEqual(await api('/prompts/' + architecture.id), architecture);
    pass('Manual variable suggestions consult actual Qwen through the observed run and preserve the source');
    await page.getByRole('button', { name: 'Editar', exact: true }).click(); await page.getByLabel('Cambio que necesitas', { exact: true }).fill('Actualiza este prompt para que sea más corto.');
    await page.getByRole('button', { name: 'Preparar cambio en el editor', exact: true }).click();
    await until(async () => page.getByTestId('nova-live').getAttribute('data-run-id'), value => !!value && value !== suggestion);
    const editor = await page.getByTestId('nova-live').getAttribute('data-run-id'); runIds.push(editor); await waitRun(editor, ['COMPLETED']); await page.getByTestId('draft-prepared').waitFor();
    const edited = await capture(editor, 'manual-editor-proposal'); assert.ok(edited.events.some(e => e.stage === 'Generation' && e.status === 'COMPLETED')); assert.ok((await page.getByLabel('Contenido', { exact: true }).inputValue()).length < architecture.templateText.length); assert.deepEqual(await api('/prompts/' + architecture.id), architecture); assert.deepEqual(errors, []);
    pass('Manual editor change uses real Qwen, shows its live run and changes only the visible unsaved draft'); return;
  }
  if (process.argv.includes('--local-only')) {
    await page.getByRole('button', { name: architecture.title, exact: true }).click(); await page.getByTestId('prompt-focus').waitFor(); await page.getByLabel('proyecto', { exact: true }).fill('API .NET para una biblioteca de barrio');
    const local = await followup('Utilízalo con Qwen local.'); await waitRun(local, ['COMPLETED']); const observed = await capture(local, 'local-execution');
    assert.equal(observed.view.observation.values.proyecto, 'API .NET para una biblioteca de barrio'); assert.ok(observed.view.observation.response.trim().includes('LISTO')); assert.ok(observed.view.decisions.some(d => d.toolName === 'ui_fill_variable')); assert.ok(observed.view.decisions.some(d => d.toolName === 'ui_execute_local')); assert.deepEqual(await api('/prompts/' + architecture.id), architecture); assert.deepEqual(errors, []);
    pass('Local-only regression: authorized current values -> real headed UI fill -> compiled preview -> actual local Qwen response -> unchanged source'); return;
  }
  if (process.argv.includes('--fill-only')) {
    await page.getByRole('button', { name: architecture.title, exact: true }).click(); await page.getByTestId('prompt-focus').waitFor();
    const fill = await followup('Completa sus variables con la información de mi petición.\nproyecto: API .NET para una biblioteca de barrio'); await waitRun(fill, ['COMPLETED']); const result = await capture(fill, 'selected-source-fill');
    assert.equal(result.view.observation.promptId, architecture.id); assert.equal(result.view.observation.values.proyecto, 'API .NET para una biblioteca de barrio'); assert.ok(result.view.decisions.some(d => d.toolName === 'ui_fill_variable')); assert.deepEqual(await api('/prompts/' + architecture.id), architecture); assert.deepEqual(errors, []);
    pass('Selected-source regression: model opens the authoritative selected source and applies the labeled project through actual React fields'); return;
  }
  const first = await ask('NOVA, busca el prompt Escribir un correo profesional, ábrelo y prepara una versión más breve.');
  const prepared = await waitRun(first, ['AWAITING_APPROVAL']);
  assert.equal(prepared.observation.promptId, source.id); assert.equal(prepared.observation.draftChanged, true); assert.ok(prepared.proposal.draft.templateText.length < source.templateText.length);
  assert.match(prepared.proposal.draft.templateText, /no inventes información/iu, 'A shorter draft must preserve the original prohibition in its actual body');
  assert.match(prepared.proposal.draft.templateText, /conserva nombres, fechas y compromisos tal como se proporcionan/iu, 'A shorter draft must also preserve the explicit instruction about names, dates and commitments');
  for (const tool of ['ui_search_prompt','ui_open_prompt','ui_open_editor','ui_prepare_update']) assert.ok(prepared.decisions.some(d => d.toolName === tool));
  assert.deepEqual(await api('/prompts/' + source.id), source);
  await capture(first, 'first-demo-before-cancel'); pass('Mandatory first demonstration: real Qwen decisions -> headed React search/open/edit -> changed shorter draft; original unchanged');
  await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).fill('Cancela la modificación.'); await page.getByRole('button', { name: 'Preparar prompt', exact: true }).click(); await waitRun(first, ['CANCELLED']); await until(() => api('/nova/live/' + first), v => !v.run.browserOpen);
  assert.deepEqual(await api('/prompts/' + source.id), source); assert.equal((await api('/prompts/' + source.id + '/versions')).length, 1); await capture(first, 'first-demo-cancelled'); pass('Cancel stops the worker and preserves the original and version history');
  const second = await ask('NOVA, busca el prompt Escribir un correo profesional, ábrelo y prepara una versión más breve.'); await waitRun(second, ['AWAITING_APPROVAL']);
  await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).fill('Guarda versión.'); await page.getByRole('button', { name: 'Preparar prompt', exact: true }).click(); const dialog = page.getByRole('dialog', { name: 'Confirmar nueva versión', exact: true }); await dialog.waitFor();
  await dialog.getByRole('button', { name: 'Confirmar guardado de esta versión', exact: true }).click(); await waitRun(second, ['COMPLETED']); await until(() => api('/nova/live/' + second), v => !v.run.browserOpen);
  const updated = await api('/prompts/' + source.id); assert.equal(updated.revision, 2); assert.notEqual(updated.templateText, source.templateText); assert.equal((await api('/prompts/' + source.id + '/versions')).length, 2);
  await capture(second, 'first-demo-approved'); pass('Human approval binds the proposal; a real React save control persists exactly one new source version');
  if (process.argv.includes('--first-only')) { assert.deepEqual(errors, []); return; }
  const open = await ask('Abre mi prompt de arquitectura.'); await waitRun(open, ['COMPLETED']); const opened = await capture(open, 'case-A-open');
  assert.equal(opened.view.observation.promptId, architecture.id); assert.ok(opened.view.decisions.some(d => d.toolName === 'ui_open_prompt')); pass('A: model-selected opening verifies the real Focus View identity');
  await page.getByRole('button', { name: 'Revisar en este lienzo', exact: true }).click(); await page.getByTestId('prompt-focus').waitFor();
  const shorter = await followup('Actualiza este prompt para que sea más corto.'); await waitRun(shorter, ['AWAITING_APPROVAL']);
  const shortened = await capture(shorter, 'case-B-context-update'); assert.equal(shortened.view.observation.promptId, architecture.id); assert.ok(shortened.view.observation.templateText.length < architecture.templateText.length);
  assert.deepEqual(await api('/prompts/' + architecture.id), architecture); await page.getByRole('button', { name: 'Cancelar modificación', exact: true }).click(); await until(() => api('/nova/live/' + shorter), v => !v.run.browserOpen); pass('B: contextual update uses the selected source and leaves its persisted revision unchanged');
  const fill = await followup('Completa sus variables con la información de mi petición.\nproyecto: API .NET para una biblioteca de barrio');
  await waitRun(fill, ['COMPLETED']); const filled = await capture(fill, 'case-C-fields'); assert.equal(filled.view.observation.values.proyecto, 'API .NET para una biblioteca de barrio'); assert.ok(filled.view.decisions.some(d => d.toolName === 'ui_fill_variable')); assert.deepEqual(await api('/prompts/' + architecture.id), architecture); pass('C: model-selected fill changes the actual React field using only labeled authorized material');
  await page.getByRole('button', { name: 'Revisar en este lienzo', exact: true }).click();
  await until(async () => page.getByLabel('proyecto', { exact: true }).inputValue(), value => value === 'API .NET para una biblioteca de barrio');
  const execute = await followup('Utilízalo con Qwen local.'); await waitRun(execute, ['COMPLETED']); const executed = await capture(execute, 'case-D-local-qwen');
  assert.equal(executed.view.observation.responseComplete, true); assert.ok(executed.view.observation.response.trim().includes('LISTO')); assert.equal(executed.view.observation.values.proyecto, 'API .NET para una biblioteca de barrio'); assert.ok(executed.view.decisions.some(d => d.toolName === 'ui_execute_local')); assert.deepEqual(await api('/prompts/' + architecture.id), architecture); pass('D: explicit local execution compiles authorized values, streams actual Qwen output in React and verifies completion');
  await page.getByRole('button', { name: 'Biblioteca', exact: true }).click();
  await api('/prompts', { title: 'Correo para clientes', templateText: 'Escribe a un cliente.' }); await api('/prompts', { title: 'Correo para equipo', templateText: 'Escribe al equipo.' });
  const ambiguous = await ask('Abre mi prompt de correo.'); await waitRun(ambiguous, ['BLOCKED']); const ambiguity = await capture(ambiguous, 'case-G-ambiguity'); assert.equal(ambiguity.view.run.errorCode, 'NEEDS_CLARIFICATION'); assert.ok(ambiguity.view.question); assert.equal(ambiguity.view.observation.promptId, null); assert.equal(ambiguity.view.decisions.some(d => d.toolName === 'ui_open_prompt'), false); pass('G: ambiguous real candidates produce a clarification without arbitrary selection');
  const paused = await ask('Abre mi prompt de arquitectura.'); await page.getByRole('button', { name: 'Pausar', exact: true }).click(); await waitRun(paused, ['PAUSED']);
  await context.setOffline(true); const checkpoint = await until(() => api('/nova/live/' + paused), v => v.run.browserOpen && v.run.status === 'PAUSED');
  const pausedEvents = await (await fetch(origin + '/api/v1/nova/live/' + paused + '/events?after=' + checkpoint.run.sequence)).body.getReader(); await pausedEvents.cancel();
  await context.setOffline(false); await page.reload(); await page.getByTestId('nova-live').waitFor(); assert.equal(await page.getByTestId('nova-live').getAttribute('data-run-id'), paused); await page.getByRole('button', { name: 'Continuar', exact: true }).click(); await waitRun(paused, ['COMPLETED']);
  const resumed = await capture(paused, 'case-I-pause-reconnect'); const pauseIndex = resumed.events.findIndex(e => e.status === 'PAUSED'); const resumeIndex = resumed.events.findIndex((e,i) => i > pauseIndex && e.actor === 'USER_SELECTED' && e.status === 'INTERPRETING');
  assert.ok(pauseIndex >= 0 && resumeIndex > pauseIndex); assert.equal(resumed.events.slice(pauseIndex + 1, resumeIndex).filter(e => e.status === 'TOOL_RUNNING').length, 0);
  assert.equal(resumed.view.decisions.filter(d => d.toolName === 'ui_open_prompt').length, 1); pass('H/I: pause prevents new operations; a real observer disconnect/reconnect resumes ordered events without replaying the click');
  const stopped = await ask('NOVA, busca el prompt Escribir un correo profesional, ábrelo y prepara una versión más breve.');
  await until(() => api('/nova/live/' + stopped), v => v.run.status === 'INTERPRETING'); await page.getByRole('button', { name: 'Detener NOVA', exact: true }).click(); await waitRun(stopped, ['CANCELLED']); await until(() => api('/nova/live/' + stopped), v => !v.run.browserOpen);
  const stoppedTrace = await capture(stopped, 'case-E-cancel-inference'); const cancelIndex = stoppedTrace.events.findIndex(e => e.status === 'CANCELLED'); assert.equal(stoppedTrace.events.slice(cancelIndex + 1).filter(e => e.status === 'TOOL_RUNNING').length, 0); assert.deepEqual(await api('/prompts/' + source.id), updated); pass('E: cancelling a pending real model decision stops subsequent browser actions and preserves all stored revisions');
  assert.equal(opened.view.decisions.some(d => d.toolName === 'ui_prepare_update'), false); assert.ok(shortened.view.decisions.some(d => d.toolName === 'ui_prepare_update')); assert.equal(filled.view.decisions.some(d => d.toolName === 'ui_execute_local'), false); pass('J: different intents produce different actual Qwen-selected tools and corresponding verified UI effects');
  await page.setViewportSize({ width: 390, height: 844 }); assert.equal(await page.locator('body').evaluate(element => element.scrollWidth > innerWidth + 1), false); await page.screenshot({ path: path.join(out, 'live-mobile.png'), fullPage: true }); pass('Live panel remains readable and controllable at 390px without horizontal overflow');
  assert.deepEqual(errors, []);
})().catch(async error => { console.error(error); fs.writeFileSync(path.join(out, 'failure.json'), JSON.stringify({ error: error.stack, log, runIds, checks, errors }, null, 2)); for (const id of runIds) { try { await api('/nova/live/' + id + '/cancel', {}); } catch {} } process.exitCode = 1; }).finally(async () => {
  fs.writeFileSync(path.join(out, 'acceptance.json'), JSON.stringify({ status: process.exitCode ? 'FAIL' : 'PASS', checks, runIds, errors, headed: true, realQwen: true, out }, null, 2));
  await browser?.close(); if (server?.exitCode === null) server.kill();
});
