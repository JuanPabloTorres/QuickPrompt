// Real Qwen + real React + isolated synthetic library. No model/transport mocks.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const { openAssistant } = require('./ui-settings.cjs');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
const out = path.join(root, 'docs/evidence/runtime-view', String(stamp));
const data = path.join(root, '.playwright', 'runtime-view-' + stamp);
fs.mkdirSync(out, { recursive: true }); fs.mkdirSync(data, { recursive: true });
const dll = path.resolve(root, process.env.NOVA_RUNTIME_BUILD || '.playwright/runtime-build/Debug/net8.0/QuickPrompt.Api.dll');
const artifacts = [dll, ...['QuickPrompt.Application.dll', 'QuickPrompt.Infrastructure.dll', 'QuickPrompt.Domain.dll'].map(name => path.join(path.dirname(dll), name)), ...fs.readdirSync(path.join(root, 'apps/web/dist/assets')).filter(name => /^index-.*\.(js|css)$/.test(name)).map(name => path.join(root, 'apps/web/dist/assets', name))];
const artifactHashes = () => Object.fromEntries(artifacts.map(file => [path.relative(root, file), require('node:crypto').createHash('sha256').update(fs.readFileSync(file)).digest('hex')]));
const hashes = artifactHashes();
fs.writeFileSync(path.join(out, 'environment.json'), JSON.stringify({ hashes, provider: 'ollama', endpoint: 'http://127.0.0.1:11434', diagnosticsProxy: process.env.NOVA_RUNTIME_DIAGNOSTICS === '1' ? 'http://127.0.0.1:5268' : null, model: 'qwen3:8b', stageTimeoutSeconds: 90, runTimeoutMinutes: 8, contextTokens: 12288, syntheticDataOnly: true }, null, 2));
const origin = 'http://127.0.0.1:5266', headers = { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' };
const checks = [], consoleErrors = [];
const quickActions = process.env.NOVA_RUNTIME_QUICK_ACTIONS === '1';
const diagnostics = process.env.NOVA_RUNTIME_DIAGNOSTICS === '1';
let server, browser, page, runId, proxy, log = '';
const exchanges = [];
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(check, ms = 360000) { const end = Date.now() + ms; while (Date.now() < end) { const value = await check(); if (value) return value; await delay(200); } throw Error('QA_TIMEOUT'); }
async function api(route, body) { const r = await fetch(origin + '/api/v1' + route, body === undefined ? {} : { method: 'POST', headers, body: JSON.stringify(body) }); assert.ok(r.ok, await r.clone().text()); return r.status === 204 ? null : r.json(); }
async function start(message, command) {
  await openAssistant(page);
  if (command) {
    await page.getByRole('button', { name: command === 'open' ? 'Buscar y abrir' : 'Crear prompt', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: command === 'open' ? 'Buscar y abrir' : 'Crear prompt', exact: true });
    if (command === 'open') {
      const choice = dialog.getByRole('button').filter({ hasText: 'Correo de prueba runtime' });
      await choice.first().click();
    } else await dialog.getByLabel('¿Qué prompt quieres crear?', { exact: true }).fill(message);
    const [response] = await Promise.all([page.waitForResponse(r => r.url() === origin + '/api/v1/nova/live' && r.request().method() === 'POST'), dialog.getByRole('button', { name: command === 'open' ? 'Abrir con NOVA' : 'Crear con NOVA', exact: true }).click()]);
    assert.equal(response.status(), 200, await response.text()); runId = (await response.json()).runId; return;
  }
  const home = page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }), inHome = await home.count() > 0;
  await (inHome ? home : page.getByLabel('Instrucción para NOVA', { exact: true })).fill(message);
  const [response] = await Promise.all([page.waitForResponse(r => r.url() === origin + '/api/v1/nova/live' && r.request().method() === 'POST'), page.getByRole('button', { name: inHome ? 'Preparar prompt' : 'Ejecutar en esta pestaña', exact: true }).click()]);
  assert.equal(response.status(), 200, await response.text()); runId = (await response.json()).runId;
}
async function show() { await openAssistant(page); const trigger = page.getByRole('button', { name: 'Actividad de NOVA', exact: true }); await trigger.waitFor(); if (await trigger.getAttribute('aria-expanded') !== 'true') await trigger.click(); await page.getByTestId('agent-runtime').waitFor(); }
async function bounds(name) {
  const panel = page.locator('.nova-live-popover'); const box = await panel.boundingBox(), viewport = page.viewportSize();
  assert.ok(box && box.x >= 0 && box.y >= 0 && box.x + box.width <= viewport.width + 1 && box.y + box.height <= viewport.height + 1, JSON.stringify({ name, box, viewport }));
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, 'no horizontal page overflow');
  assert.equal(await page.getByTestId('agent-runtime').evaluate(node => node.scrollWidth > node.clientWidth), false, 'no horizontal runtime overflow');
  assert.equal(await page.locator('.runtime-core .nova-logo').evaluate(node => getComputedStyle(node, '::after').animationName), 'none', 'reduced motion disables activity decoration');
  assert.ok(box.x >= 15, 'safe horizontal gutter');
  await page.screenshot({ path: path.join(out, name + '.png') }); checks.push(name);
}
(async () => {
  try {
    const tags = await (await fetch('http://127.0.0.1:11434/api/tags')).json(); assert.ok(tags.models.some(m => m.name === 'qwen3:8b'));
    if (diagnostics) {
      proxy = require('node:http').createServer(async (req, res) => {
        const abort = new AbortController(); res.on('close', () => { if (!res.writableEnded) abort.abort(); });
        let entry;
        try {
          let raw = ''; for await (const chunk of req) raw += chunk;
          const packet = raw ? JSON.parse(raw) : null;
          if (req.url === '/api/chat') {
            entry = { runId: req.headers['x-nova-run-id'], started: Date.now(), format: packet.format, users: packet.messages?.filter(message => message.role === 'user').map(message => message.content), options: packet.options, thinking: packet.think };
            exchanges.push(entry);
          }
          const response = await fetch('http://127.0.0.1:11434' + req.url, { method: req.method, headers: { 'Content-Type': 'application/json' }, body: raw || undefined, signal: abort.signal });
          const body = await response.text();
          if (entry) {
            const result = packet.stream ? body.trim().split(/\r?\n/).map(line => JSON.parse(line)) : [JSON.parse(body)];
            // Synthetic QA only. Never retain private model thinking or system messages.
            entry.responses = result.map(item => ({ content: item.message?.content, toolCalls: item.message?.tool_calls, done: item.done, reason: item.done_reason, promptTokens: item.prompt_eval_count, outputTokens: item.eval_count, loadNs: item.load_duration }));
            entry.elapsedMs = Date.now() - entry.started;
            fs.writeFileSync(path.join(out, 'inference.json'), JSON.stringify(exchanges, null, 2));
          }
          res.writeHead(response.status, { 'Content-Type': response.headers.get('content-type') || 'application/json' }); res.end(body);
        } catch (error) {
          if (entry) { entry.error = error.name; entry.elapsedMs = Date.now() - entry.started; fs.writeFileSync(path.join(out, 'inference.json'), JSON.stringify(exchanges, null, 2)); }
          if (!res.headersSent) res.writeHead(502); if (!res.destroyed) res.end('{}');
        }
      });
      await new Promise(resolve => proxy.listen(5268, '127.0.0.1', resolve));
    }
    server = spawn('dotnet', [dll, '--QuickPrompt:Port=5266', '--QuickPrompt:DataPath=' + path.join(data, 'synthetic.sqlite'), '--QuickPrompt:WebRoot=' + path.join(root, 'apps/web/dist'), ...(diagnostics ? ['--QuickPrompt:Diagnostics:DebugPoints=true', '--QuickPrompt:Ollama=http://127.0.0.1:5268'] : [])], { cwd: root, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
    server.stdout.on('data', chunk => log += chunk); server.stderr.on('data', chunk => log += chunk);
    await until(() => { if (server.exitCode !== null) throw Error('QA API exited'); return log.includes('Now listening on: ' + origin); }, 30000);
    const source = await api('/prompts', { title: 'Correo de prueba runtime', templateText: 'Redacta un correo profesional a <destinatario> sobre <tema>. No inventes datos.' });
    browser = await chromium.launch({ channel: 'msedge', headless: true });
    const context = await browser.newContext({ viewport: { width: 1366, height: 1000 }, reducedMotion: 'reduce' }); page = await context.newPage();
    page.on('pageerror', error => consoleErrors.push(error.message));
    await page.goto(origin); await page.getByTestId('library-ready').waitFor(); assert.equal(await page.getByTestId('agent-runtime').count(), 0);
    await start('Busca el prompt «Correo de prueba runtime», ábrelo y adáptalo para solicitar una entrevista de trabajo. No guardes cambios.', quickActions ? 'open' : undefined);
    await show(); await bounds('desktop-running-dark');
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    const runtimePanel = page.getByTestId('agent-runtime');
    await until(async () => await runtimePanel.getAttribute('data-activity') === 'working');
    assert.equal(await page.locator('.runtime-core').evaluate(node => getComputedStyle(node, '::before').animationName), 'runtime-orbit');
    assert.equal(await page.locator('.runtime-connections path').evaluate(node => getComputedStyle(node).animationName), 'runtime-flow');
    await page.screenshot({ path: path.join(out, 'live-animation.png') }); checks.push('confirmed-live-animation');
    await page.emulateMedia({ reducedMotion: 'reduce' });
    assert.equal(await page.locator('.runtime-core').evaluate(node => getComputedStyle(node, '::before').animationName), 'none');
    checks.push('runtime-reduced-motion');
    await page.getByRole('button', { name: 'Pausar', exact: true }).click();
    await until(async () => (await api('/nova/live/' + runId)).run.pauseRequested);
    await page.getByRole('button', { name: 'Continuar', exact: true }).waitFor();
    assert.equal(await runtimePanel.getAttribute('data-activity'), 'paused');
    checks.push('real-pause');
    const runtimeText = await page.getByTestId('agent-runtime').innerText();
    assert.ok(runtimeText.includes('qwen3:8b')); assert.ok(!runtimeText.includes('X-Nova-Surface')); assert.ok(!runtimeText.includes(source.templateText));
    await page.evaluate(() => document.documentElement.dataset.theme = 'light'); await bounds('desktop-paused-light');
    await page.setViewportSize({ width: 375, height: 812 }); await show(); await bounds('mobile-paused-light');
    await page.setViewportSize({ width: 812, height: 375 }); await show(); await bounds('landscape-paused-light');
    await page.setViewportSize({ width: 1366, height: 1000 }); await show();
    await page.getByRole('button', { name: 'Continuar', exact: true }).click();
    const completed = await until(async () => { const v = await api('/nova/live/' + runId); if (['FAILED','BLOCKED','CANCELLED'].includes(v.run.status)) throw Error('Run stopped: ' + v.run.status + '/' + v.run.errorCode); return v.run.status === 'COMPLETED' ? v : null; });
    assert.equal(completed.runtime.runId, runId); assert.ok(completed.runtime.tools.some(tool => tool.state === 'completed'));
    if (!quickActions) {
      assert.equal(completed.observation.draftChanged, true);
      assert.notEqual(completed.observation.templateText, source.templateText);
      assert.match(completed.observation.templateText, /entrevista/i, 'requested interview purpose appears in the actual draft');
      assert.equal(await page.getByLabel('Contenido', { exact: true }).inputValue(), completed.observation.templateText);
      assert.ok(!completed.semantic.intentions.includes('FILL_VARIABLES'), 'editing purpose does not request assigning case values');
      assert.ok(Object.values(completed.observation.values).every(value => !value.trim()), 'no unrequested field values');
      assert.equal((await api('/prompts')).total, 1, 'no save or extra record during adaptation');
    }
    assert.equal((await api('/prompts/' + source.id)).templateText, source.templateText); checks.push(quickActions ? 'explicit-navigation-original-preserved' : 'real-qwen-completion-original-preserved');
    fs.writeFileSync(path.join(out, 'completed-runtime.json'), JSON.stringify(completed.runtime, null, 2));
    await openAssistant(page);
    if (!await page.getByRole('button', { name: 'Cerrar actividad', exact: true }).isVisible()) await page.getByRole('button', { name: 'Ver última actividad de NOVA', exact: true }).click();
    await until(async () => await runtimePanel.getAttribute('data-activity') === 'idle');
    assert.equal(await runtimePanel.getAttribute('data-connected'), 'true', 'normal terminal SSE closure is not a lost connection');
    checks.push('terminal-activity-stopped-channel-confirmed');
    await page.getByRole('button', { name: 'Cerrar actividad', exact: true }).click(); await until(async () => await page.getByTestId('nova-live').count() === 0);
    checks.push('terminal-collapse');
    if (process.env.NOVA_RUNTIME_SCENARIO === 'adapt') {
      assert.equal(context.pages().length, 1); assert.deepEqual(consoleErrors, []); assert.deepEqual(artifactHashes(), hashes);
      checks.push('one-tab-no-console-errors', 'unchanged-build-artifacts');
      const result = { status: 'PASS', scope: 'original-adaptation-case', model: 'qwen3:8b', realModel: true, inputMode: quickActions ? 'explicit-quick-actions' : 'free-text', headlessQa: true, syntheticLibrary: true, checks };
      fs.writeFileSync(path.join(out, 'acceptance.json'), JSON.stringify(result, null, 2));
      console.log(JSON.stringify({ ...result, out })); return;
    }
    await start(quickActions ? 'Crea un prompt para revisar la claridad de un correo profesional.' : 'Crea un prompt para revisar la claridad de un correo profesional y prepara su guardado como copia para que yo lo revise y apruebe.', quickActions ? 'create' : undefined); await show();
    const proposal = await until(async () => { const v = await api('/nova/live/' + runId); if (['FAILED','BLOCKED','CANCELLED'].includes(v.run.status)) throw Error('Run stopped: ' + v.run.status + '/' + v.run.errorCode); return v.proposal ? v : null; });
    await show(); await bounds('human-approval');
    await page.getByRole('button', { name: 'Revisar y guardar copia', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: 'Guardar copia separada', exact: true }); await dialog.waitFor();
    await dialog.getByRole('button', { name: 'Confirmar guardado de esta copia', exact: true }).click();
    const saved = await until(async () => { const v = await api('/nova/live/' + runId); if (['FAILED','BLOCKED'].includes(v.run.status)) throw Error('Approval failed: ' + v.run.errorCode); return v.run.status === 'COMPLETED' ? v : null; });
    assert.equal(saved.run.effectCommitted, true); assert.equal((await api('/prompts/' + saved.run.targetId)).templateText, proposal.proposal.draft.templateText); checks.push('real-bound-approval-save');
    await page.goto(origin); await page.getByTestId('library-ready').waitFor();
    await start('Crea un prompt para preparar una reunión de equipo.', quickActions ? 'create' : undefined); await show();
    await page.getByRole('button', { name: 'Detener NOVA', exact: true }).click();
    await until(async () => (await api('/nova/live/' + runId)).run.status === 'CANCELLED'); checks.push('real-cancel');
    assert.equal(context.pages().length, 1); assert.deepEqual(consoleErrors, []); checks.push('one-tab-no-console-errors');
    assert.deepEqual(artifactHashes(), hashes); checks.push('unchanged-build-artifacts');
    fs.writeFileSync(path.join(out, 'acceptance.json'), JSON.stringify({ status: 'PASS', model: 'qwen3:8b', realModel: true, inputMode: quickActions ? 'explicit-quick-actions' : 'free-text', headlessQa: true, syntheticLibrary: true, checks }, null, 2));
    console.log(JSON.stringify({ status: 'PASS', out, checks }));
  } catch (error) {
    if (runId) {
      const performance = await api('/nova/live/' + runId + '/performance').catch(() => null);
      if (performance) fs.writeFileSync(path.join(out, 'performance.json'), JSON.stringify(performance, null, 2));
    }
    await page?.screenshot({ path: path.join(out, 'failure.png') }).catch(() => {});
    fs.writeFileSync(path.join(out, 'acceptance.json'), JSON.stringify({ status: 'FAIL', checks, error: error.stack, consoleErrors }, null, 2));
    console.error(error.stack); process.exitCode = 1;
  } finally {
    if (runId && server?.exitCode === null) await api('/nova/live/' + runId + '/cancel', {}).catch(() => {});
    await browser?.close(); if (server?.exitCode === null) server.kill(); fs.writeFileSync(path.join(out, 'server.log'), log);
    if (diagnostics) fs.writeFileSync(path.join(out, 'debug-points.json'), JSON.stringify(log.split(/\r?\n/).filter(line => line.startsWith('NOVA_DEBUG_POINT ')).map(line => JSON.parse(line.slice(17))), null, 2));
    proxy?.close(); proxy?.closeAllConnections();
  }
})();
