// Real Edge -> React -> HTTP API -> isolated SQLite. Never opens the user's library.
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const repo = path.resolve(__dirname, '../..');
const evidence = path.join(repo, 'docs/evidence/nova');
const temporary = path.join(repo, '.playwright', 'nova-' + Date.now());
fs.mkdirSync(evidence, { recursive: true }); fs.mkdirSync(temporary, { recursive: true });
const port = Number(process.env.NOVA_TEST_PORT || 5197), origin = `http://127.0.0.1:${port}`;
const realAi = process.env.NOVA_AI === '1';
let server, browser, promptId;
const checks = [], errors = [], failedResponses = [];
const record = (name, detail) => { checks.push({ name, status: 'EXECUTED_PASS', detail }); console.log('EXECUTED_PASS', name); };
async function start() {
  let output = '';
  server = spawn('dotnet', [path.join(repo, 'backend/QuickPrompt.Api/bin/Debug/net8.0/QuickPrompt.Api.dll'),
    `--QuickPrompt:Port=${port}`, `--QuickPrompt:DataPath=${path.join(temporary, 'library.sqlite')}`,
    `--QuickPrompt:WebRoot=${path.join(repo, 'apps/web/dist')}`, ...(!realAi ? ['--QuickPrompt:Ollama=http://127.0.0.1:11435'] : [])], { cwd: repo, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', chunk => { output += chunk; fs.appendFileSync(path.join(temporary, 'server.log'), chunk); });
  server.stderr.on('data', chunk => fs.appendFileSync(path.join(temporary, 'server.log'), chunk));
  for (let i = 0; i < 150; i++) {
    if (server.exitCode !== null) throw new Error('Owned API exited. Inspect ' + path.join(temporary, 'server.log'));
    if (output.includes(`Now listening on: ${origin}`)) {
      const response = await fetch(origin + '/health'); if (response.ok) return;
    }
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  throw new Error('Owned API did not start');
}
async function stop() {
  if (server && server.exitCode === null) { const exited = new Promise(resolve => server.once('exit', resolve)); server.kill(); await exited; }
  server = null;
}
async function api(route, method = 'GET', body, extra = {}) {
  const response = await fetch(origin + '/api/v1' + route, { method, headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1', ...extra }, body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await response.text();
  const value = text ? JSON.parse(text) : undefined;
  return { status: response.status, value, response };
}
async function clipboard(page, expected) {
  // Capture the actual call without replacing the real OS clipboard write.
  await page.waitForFunction(expected => window.__novaCopiedText === expected, expected, { timeout: 15000 });
  assert.equal(await page.evaluate(() => window.__novaCopiedText), expected, 'Exact text passed to real Clipboard API');
  const actual = await page.evaluate(() => navigator.clipboard.readText());
  if (actual !== expected) {
    assert.equal(actual.replace(/\r\n/g, '\n'), expected.replace(/\r\n/g, '\n'), 'Only native Windows line-ending conversion is tolerated here');
    checks.push({ name: 'Native clipboard byte equality (Windows LF -> CRLF)', status: 'EXECUTED_FAIL', detail: 'Exact API argument verified; installed Edge converts line endings in text/plain. Raw comparison failed; not reported as exact clipboard PASS.' });
  }
}
(async () => {
  await start();
  assert.equal((await api('/prompts')).value.total, 0);
  const created = await api('/prompts', 'POST', { title: 'Arquitectura reusable', templateText: 'Revisa <proyecto>.\nIdioma: <idioma>. Repite: <proyecto>. Literal: \\<ejemplo\\>', description: 'Analiza límites y dependencias.' });
  assert.equal(created.status, 201); promptId = created.value.id;
  const plain = (await api('/prompts', 'POST', { title: 'Instrucción directa', templateText: 'Resume el texto con claridad.\nConserva los matices.' })).value;
  const badCompile = await api(`/prompts/${promptId}/compile`, 'POST', { revision: 1, values: {} }); assert.equal(badCompile.status, 400);
  const exact = 'Revisa α\nβ <proyecto>.\nIdioma: español. Repite: α\nβ <proyecto>. Literal: <ejemplo>';
  const values = { proyecto: 'α\nβ <proyecto>', idioma: 'español' };
  assert.equal((await api(`/prompts/${promptId}/compile`, 'POST', { revision: 1, values })).value.text, exact);
  assert.equal((await api(`/prompts/${promptId}`)).value.uses, 0);
  const favorite = (await api(`/prompts/${promptId}/favorite`, 'POST', { revision: 1 })).value;
  assert.equal((await api(`/prompts/${promptId}`, 'PUT', { title: 'Stale', templateText: 'Overwrite', revision: 1 })).status, 409);
  assert.equal((await api(`/prompts/${promptId}`, 'DELETE', { revision: favorite.revision, confirmed: false })).status, 400);
  record('API contracts, exact compiler, read-only preview and revision conflicts');
  assert.equal((await api('/prompts', 'POST', { title: 'Attack', templateText: 'Unsafe' }, { Origin: 'https://evil.invalid' })).status, 403);
  const noHeader = await fetch(origin + '/api/v1/prompts', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' }); assert.equal(noHeader.status, 403);
  const forgedHost = await new Promise((resolve, reject) => require('node:http').get(origin + '/api/v1/prompts', { headers: { Host: `evil.invalid:${port}` } }, response => { response.resume(); resolve(response.statusCode); }).on('error', reject));
  assert.equal(forgedHost, 403);
  assert.equal((await fetch(origin + '/api/v1/prompts', { headers: { 'Sec-Fetch-Site': 'cross-site' } })).status, 403);
  const html = await fetch(origin); assert.equal(html.status, 200); assert.match(html.headers.get('content-security-policy'), /script-src 'self'/);
  assert.equal((await api('/prompts?size=101')).status, 400);
  record('Loopback host, origin, CSRF header, CSP and input limits');
  browser = await chromium.launch({ channel: 'msedge', headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, permissions: ['clipboard-read', 'clipboard-write'], reducedMotion: 'reduce' });
  const page = await context.newPage();
  await page.addInitScript(() => {
    const write = navigator.clipboard.writeText.bind(navigator.clipboard);
    navigator.clipboard.writeText = text => { window.__novaCopiedText = text; return write(text); };
  });
  page.on('pageerror', error => errors.push(error.message));
  page.on('response', response => { if (response.status() >= 400) failedResponses.push({ url: response.url(), status: response.status() }); });
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
  await page.goto(origin);
  await page.getByRole('button', { name: /Instrucción directa/ }).click();
  await page.getByRole('button', { name: 'Copiar prompt', exact: true }).click();
  await page.locator('.action-dock').getByText('Copiado. Listo para tu siguiente idea.', { exact: true }).waitFor();
  await clipboard(page, plain.templateText);
  record('Select and copy plain prompt in two actions', { actions: 2 });
  await page.keyboard.press('Control+k');
  await page.waitForFunction(() => document.activeElement?.id === 'search');
  await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?' }).fill('dependencias');
  await page.getByRole('button', { name: /Arquitectura reusable/ }).click();
  await page.getByLabel('proyecto', { exact: true }).fill(values.proyecto);
  await page.getByLabel('idioma', { exact: true }).fill(values.idioma);
  await page.getByRole('button', { name: 'Copiar prompt', exact: true }).click();
  await page.locator('.action-dock').getByText('Copiado. Listo para tu siguiente idea.', { exact: true }).waitFor();
  await clipboard(page, exact);
  await page.getByRole('region', { name: 'Vista previa del resultado', exact: true }).locator('pre').waitFor({ state: 'visible' });
  const downloading = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Descargar texto (.txt)' }).click();
  const downloaded = await downloading;
  assert.equal(fs.readFileSync(await downloaded.path(), 'utf8'), exact);
  record('UTF-8 download preserves LF and all compiled bytes exactly');
  assert.equal((await api(`/prompts/${promptId}`)).value.templateText, created.value.templateText);
  await page.screenshot({ path: path.join(evidence, 'focus-desktop.png'), fullPage: true });
  record('Search content, fill repeated Unicode/multiline variables; exact API argument (native EOL caveat recorded separately)');
  await page.getByRole('button', { name: 'Editar', exact: true }).click();
  await page.getByLabel('Título', { exact: true }).fill('Arquitectura revisada');
  await page.getByRole('button', { name: 'Guardar versión' }).click();
  await page.getByRole('heading', { name: 'Arquitectura revisada', exact: true }).waitFor();
  await page.getByRole('button', { name: 'Archivar', exact: true }).click();
  await page.getByText('Recupera este prompt para volver a utilizarlo.').waitFor();
  await page.getByRole('button', { name: 'Recuperar', exact: true }).click();
  await page.getByRole('button', { name: 'Copiar prompt', exact: true }).waitFor();
  await page.getByRole('button', { name: 'Eliminar prompt', exact: true }).click();
  await page.getByRole('dialog').waitFor();
  await page.keyboard.press('Escape');
  await page.getByRole('dialog').waitFor({ state: 'hidden' });
  assert.equal((await api(`/prompts/${promptId}`)).status, 200);
  record('Edit/version, archive/restore and cancel deletion with Escape');
  await page.getByRole('button', { name: 'Biblioteca', exact: true }).click();
  await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?' }).fill('');
  await page.getByRole('button', { name: 'Nuevo prompt', exact: true }).click();
  await page.getByLabel('Título', { exact: true }).fill('Creado en React');
  await page.getByLabel('Contenido', { exact: true }).fill('Explica <tema> con un ejemplo.');
  await page.getByText('Variables detectadas: tema', { exact: true }).waitFor();
  await page.getByRole('button', { name: 'Guardar prompt', exact: true }).click();
  await page.getByLabel('tema', { exact: true }).waitFor();
  await page.getByLabel('tema', { exact: true }).fill('composición');
  await page.getByRole('button', { name: 'Copiar prompt', exact: true }).click();
  await page.locator('.action-dock').getByText('Copiado. Listo para tu siguiente idea.', { exact: true }).waitFor();
  assert.equal(await page.evaluate(() => navigator.clipboard.readText()), 'Explica composición con un ejemplo.');
  record('Create in React, detect variables on server, persist and copy');
  await page.getByRole('button', { name: 'Colecciones', exact: true }).click();
  await page.getByLabel('Nueva colección', { exact: true }).fill('Trabajo NOVA');
  await page.getByRole('button', { name: 'Crear colección', exact: true }).click();
  await page.getByRole('checkbox', { name: 'Asignar Trabajo NOVA' }).check();
  await page.getByRole('button', { name: 'Listo', exact: true }).click();
  await page.getByRole('button', { name: 'Biblioteca', exact: true }).click();
  const allGroups = (await api('/groups')).value;
  await page.getByLabel('Colección', { exact: true }).selectOption(allGroups[0].group.id);
  await page.getByRole('button', { name: /Creado en React/ }).waitFor();
  assert.equal(await page.locator('.prompt-item').count(), 1);
  await page.getByLabel('Colección', { exact: true }).selectOption('');
  await page.getByRole('button', { name: /Creado en React/ }).click();
  await page.getByLabel('tema', { exact: true }).fill('composición');
  record('Create collection, many-to-many assignment and filter through real UI/SQLite');
  const layout = [];
  for (const theme of ['dark', 'light']) {
    await page.evaluate(theme => { document.documentElement.dataset.theme = theme; }, theme);
    for (const width of [320, 390, 768, 1024, 1440]) {
      await page.setViewportSize({ width, height: 800 });
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, `Overflow ${theme} ${width}`);
      const copy = page.getByRole('button', { name: 'Copiar prompt', exact: true });
      await copy.scrollIntoViewIfNeeded();
      assert.equal(await copy.evaluate(button => { const r = button.getBoundingClientRect(); return document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2) === button || button.contains(document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2)); }), true);
      layout.push({ theme, width, status: 'EXECUTED_PASS' });
    }
  }
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: path.join(evidence, 'focus-mobile-light.png'), fullPage: true });
  await page.getByRole('button', { name: 'Biblioteca', exact: true }).click();
  await page.getByRole('button', { name: /Creado en React/ }).waitFor();
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.evaluate(() => { document.documentElement.dataset.theme = 'dark'; });
  await page.waitForFunction(() => getComputedStyle(document.querySelector('main > div')).opacity === '1');
  await page.screenshot({ path: path.join(evidence, 'discover-desktop.png'), fullPage: true });
  await page.keyboard.press('Control+k');
  assert.equal(await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?' }).evaluate(element => element === document.activeElement), true);
  await page.setViewportSize({ width: 720, height: 450 });
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
  record('Responsive widths, both themes, action hit-testing, reduced motion and keyboard', layout);
  // Verify copy/open against intercepted fixture pages. This is deliberately NOT a live provider integration claim.
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?' }).fill('Instrucción directa');
  await page.getByRole('button', { name: /Instrucción directa/ }).click();
  await page.getByRole('button', { name: 'Usar en…', exact: true }).click();
  for (const [name, url] of [['ChatGPT', 'https://chatgpt.com/'], ['Claude', 'https://claude.ai/']]) {
    await context.route(url + '**', route => route.fulfill({ contentType: 'text/html', body: '<title>Owned provider fallback fixture</title><p>Manual paste destination fixture</p>' }));
    await page.getByRole('button', { name, exact: true }).click();
    const popupPromise = context.waitForEvent('page');
    await page.getByRole('button', { name: 'Copiar y abrir', exact: true }).click();
    const popup = await popupPromise; await popup.waitForURL(url + '**');
    await clipboard(page, plain.templateText); await popup.close();
  }
  await page.getByRole('button', { name: 'Cerrar diálogo', exact: true }).click();
  record('ChatGPT/Claude copy-and-open fallback: local intercepted destinations only; no auto-submit');
  // Backup/restore data is synthetic and reviewed through the same public API used by the UI.
  const originalBackup = (await api('/library/export')).value;
  const restoreReview = (await api('/library/restore/preview', 'POST', originalBackup)).value;
  assert.equal((await api('/library/restore', 'POST', { data: originalBackup, confirmed: false, expectedSnapshot: restoreReview.expectedSnapshot })).status, 400);
  assert.equal((await api('/library/restore', 'POST', { data: originalBackup, confirmed: true, expectedSnapshot: 'stale' })).status, 409);
  assert.equal((await api('/library/restore', 'POST', { data: originalBackup, confirmed: true, expectedSnapshot: restoreReview.expectedSnapshot })).status, 200);
  assert.deepEqual((await api('/library/export')).value, originalBackup);
  assert.ok(fs.readdirSync(path.join(temporary, 'backups')).some(name => name.endsWith('.sqlite')));
  record('Reviewed restore requires confirmation and current snapshot; verified SQLite backup created first');
  if (realAi) {
    const dataset = JSON.parse(fs.readFileSync(path.join(repo, 'Tests/evaluation/nova-intents-v1.json'), 'utf8'));
    const selected = (await api(`/prompts/${promptId}`)).value;
    const beforeAi = (await api('/library/export')).value;
    const evaluation = [];
    for (const item of dataset.cases) {
      const start = performance.now();
      const result = await api('/nova/interpret', 'POST', { message: item.message, model: 'qwen3:8b', promptId, revision: selected.revision });
      const valid = result.status === 200 && result.value.contractVersion === 1;
      if (valid) for (const id of result.value.sourcePromptIds) assert.equal((await api(`/prompts/${id}`)).status, 200, 'Never invent source IDs');
      evaluation.push({ ...item, actual: result.value?.intent, schemaValid: valid, match: valid && item.intent === result.value.intent, elapsedMs: performance.now() - start, returnedIds: result.value?.sourcePromptIds ?? [] });
    }
    assert.deepEqual((await api('/library/export')).value, beforeAi, 'Interpretation has no write authority');
    const accuracy = evaluation.filter(item => item.match).length / evaluation.length;
    const schemaValidity = evaluation.filter(item => item.schemaValid).length / evaluation.length;
    const times = evaluation.map(item => item.elapsedMs).sort((a, b) => a - b);
    fs.writeFileSync(path.join(evidence, 'ai-evaluation.json'), JSON.stringify({ model: 'qwen3:8b', actualLocalInference: true, thresholds: dataset.thresholds, accuracy, schemaValidity, p50Ms: times[Math.floor(times.length / 2)], p95Ms: times[Math.min(times.length - 1, Math.ceil(times.length * .95) - 1)], status: accuracy >= dataset.thresholds.intentAccuracy && schemaValidity === 1 ? 'EXECUTED_PASS' : 'EXECUTED_FAIL', evaluation, notMeasured: ['Tokens/s', 'RAM/VRAM', 'Human proposal quality', 'Synonym retrieval recall benchmark', 'Multi-turn quality'] }, null, 2));
    await page.getByRole('button', { name: 'Biblioteca', exact: true }).click();
    await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?' }).fill('Explicar conceptos con ejemplos para principiantes');
    const preparedResponse = page.waitForResponse(response => response.url().endsWith('/nova/prepare'));
    await page.getByRole('button', { name: 'Preparar prompt', exact: true }).click();
    await preparedResponse;
    await page.locator('.prepared-result').waitFor({ timeout: 180000 });
    const prepared = { title: await page.locator('#prepared-title').textContent() };
    await page.getByRole('heading', { name: prepared.title, exact: true }).waitFor({ timeout: 180000 });
    assert.deepEqual((await api('/library/export')).value, beforeAi, 'Proposal does not silently save');
    await page.getByRole('button', { name: 'Guardar', exact: true }).click();
    await page.getByRole('button', { name: 'Guardado', exact: true }).waitFor();
    await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?' }).fill('');
    record('Real Qwen3 -> prepared UI -> explicit save without editor -> SQLite; legacy intent evaluation recorded separately', { accuracy, schemaValidity });
    const executionPrompt = (await api('/prompts', 'POST', { title: 'Ejecución local verificable', templateText: 'Explica en una frase breve por qué conviene reutilizar instrucciones.' })).value;
    await page.getByRole('button', { name: 'Biblioteca', exact: true }).click();
    await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?' }).fill(executionPrompt.title);
    await page.getByRole('button', { name: /Ejecución local verificable/ }).click();
    await page.getByRole('button', { name: 'Usar en…', exact: true }).click();
    await page.getByRole('button', { name: 'Ejecutar en este equipo', exact: true }).click();
    await page.getByText('RESPUESTA COMPLETA · SIN GUARDAR', { exact: true }).waitFor({ timeout: 180000 });
    assert.ok((await page.locator('.local-response pre').textContent()).trim().length > 0);
    await page.screenshot({ path: path.join(evidence, 'real-qwen-stream.png'), fullPage: true });
    await page.getByRole('button', { name: 'Cerrar diálogo', exact: true }).click();
    record('Real Edge -> API SSE -> installed Qwen3 -> complete visible response');
    const cancelPrompt = (await api('/prompts', 'POST', { title: 'Cancelación local verificable', templateText: 'Escribe 80 párrafos explicando técnicas de escritura. Numera cada párrafo y añade un ejemplo detallado a cada uno.' })).value;
    await page.getByRole('button', { name: 'Biblioteca', exact: true }).click();
    await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?' }).fill(cancelPrompt.title);
    await page.getByRole('button', { name: /Cancelación local verificable/ }).click();
    await page.getByRole('button', { name: 'Usar en…', exact: true }).click();
    await page.getByRole('button', { name: 'Ejecutar en este equipo', exact: true }).click();
    await page.locator('.local-response pre').waitFor({ timeout: 180000 });
    await page.getByRole('button', { name: 'Detener', exact: true }).click();
    await page.getByText('Cancelado. No se guardó ningún resultado parcial.', { exact: true }).waitFor();
    assert.equal(await page.getByRole('button', { name: 'Copiar respuesta', exact: true }).count(), 0);
    assert.equal((await api(`/prompts/${cancelPrompt.id}`)).value.uses, 0);
    for (let attempt = 0; attempt < 30; attempt++) {
      const recovered = await api('/nova/interpret', 'POST', { message: 'ayuda' });
      if (recovered.status === 200) break;
      assert.equal(recovered.status, 429); await new Promise(resolve => setTimeout(resolve, 100));
      if (attempt === 29) throw new Error('AI concurrency permit was not released after cancellation');
    }
    await page.getByRole('button', { name: 'Cerrar diálogo', exact: true }).click();
    record('Cancel real local streaming after visible tokens; no partial copy/usage and concurrency recovers');
  } else {
    assert.deepEqual((await api('/ai/local/models')).value.models, []);
    record('Core library is usable with a deliberately unavailable Ollama endpoint');
  }
  await browser.close(); browser = null;
  const beforeRestart = await api(`/prompts/${promptId}`);
  await stop(); await start();
  const afterRestart = await api(`/prompts/${promptId}`);
  assert.deepEqual(afterRestart.value, beforeRestart.value);
  const archive = await api('/library/export'); assert.equal(archive.value.schemaVersion, 2);
  assert.equal((await fetch(origin + '/health').then(r => r.json())).status, 'ok');
  assert.deepEqual(errors, []);
  record('Real process restart, SQLite persistence, backup v2 and no page errors');
  const report = { date: new Date().toISOString(), mode: realAi ? 'real-local-ai' : 'ollama-unavailable', status: checks.some(c => c.status === 'EXECUTED_FAIL') ? 'EXECUTED_FAIL' : 'EXECUTED_PASS', database: 'isolated synthetic SQLite', browser: 'real installed Edge, headless', checks, limitations: ['Native Windows clipboard byte equality EXECUTED_FAIL for LF; both writeText and ClipboardItem tested', 'Private original data NOT_TESTED', 'Human usability and screen reader NOT_TESTED', 'Ollama not required by the core library'] };
  const serialized = JSON.stringify(report, null, 2);
  fs.writeFileSync(path.join(evidence, realAi ? 'acceptance-ai.json' : 'acceptance-offline.json'), serialized);
  if (realAi || !fs.existsSync(path.join(evidence, 'acceptance.json'))) fs.writeFileSync(path.join(evidence, 'acceptance.json'), serialized);
  console.log('OVERALL', report.status, '(native Windows LF/CRLF equality reported separately from functional checks)');
  if (report.status === 'EXECUTED_FAIL') process.exitCode = 2;
  await stop();
})().catch(async error => {
  console.error(error);
  fs.writeFileSync(path.join(evidence, 'acceptance-failure.json'), JSON.stringify({ status: 'EXECUTED_FAIL', checks, error: String(error), errors, failedResponses }, null, 2));
  if (browser) await browser.close(); await stop(); process.exitCode = 1;
});
