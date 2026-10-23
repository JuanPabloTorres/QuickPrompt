// Visual identity evidence. Own API, frozen assets and synthetic data only.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), phase = process.argv.includes('--before') ? 'before' : 'after';
const out = path.join(root, 'docs/evidence/identity-' + phase + '-' + Date.now()), temp = path.join(root, '.playwright', path.basename(out));
const origin = 'http://127.0.0.1:5195', headers = { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' };
const librarySize = process.argv.includes('--large-library') ? 1000 : 35;
fs.mkdirSync(out, { recursive: true }); fs.mkdirSync(temp, { recursive: true }); fs.cpSync(process.env.NOVA_VISUAL_WEBROOT || path.join(root, 'apps/web/dist'), path.join(temp, 'web'), { recursive: true });
const sleep = ms => new Promise(r => setTimeout(r, ms));
let server, browser, context, page, log = '', checks = [], measurements = [], errors = [];
async function api(route, body) { const r = await fetch(origin + '/api/v1' + route, { method: body ? 'POST' : 'GET', headers, body: body && JSON.stringify(body) }); assert.ok(r.ok, await r.clone().text()); return r.json(); }
async function shot(name) { await page.screenshot({ path: path.join(out, name + '.png') }); }
async function library() { await page.getByRole('button', { name: 'QuickPrompt NOVA, biblioteca', exact: true }).click(); await page.getByTestId('library-ready').waitFor(); }
async function openDock() { const b = page.getByRole('button', { name: 'Abrir asistente NOVA', exact: true }); if (await b.isVisible()) await b.click(); }
async function bounds() {
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'horizontal overflow');
  const box = await page.locator('.nova-dock-panel').boundingBox(), vp = page.viewportSize();
  assert.ok(box && box.x >= 0 && box.y >= 0 && box.x + box.width <= vp.width + 1 && box.y + box.height <= vp.height, JSON.stringify(box));
}
(async () => {
  server = spawn('dotnet', [path.join(root, 'backend/QuickPrompt.Api/bin/Debug/net8.0/QuickPrompt.Api.dll'), '--QuickPrompt:Port=5195', '--QuickPrompt:DataPath=' + path.join(temp, 'synthetic.sqlite'), '--QuickPrompt:WebRoot=' + path.join(temp, 'web')], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', c => log += c); server.stderr.on('data', c => log += c);
  for (let i = 0; i < 150 && !log.includes('Now listening on: ' + origin); i++) { if (server.exitCode !== null) throw Error(log); await sleep(200); }
  const prompt = await api('/prompts', { title: 'Una propuesta con contexto', description: 'Convierte una idea en un siguiente paso concreto.', templateText: 'Prepara una propuesta para <proyecto>. Resume el objetivo, tres pasos y un criterio de éxito. No inventes datos.' });
  const plain = await api('/prompts', { title: 'Revisar una decisión', description: 'Evalúa opciones y elige el siguiente paso.', templateText: 'Pídeme una decisión pendiente. Compara las opciones y sus riesgos antes de recomendar el siguiente paso.' });
  for (let i = 0; i < librarySize; i++) await api('/prompts', { title: `Idea ${i + 1}: organizar el trabajo`, description: 'Instrucciones prácticas, reutilizables y fáciles de adaptar.', templateText: 'Organiza <tarea> en tres pasos.' });
  browser = await chromium.launch({ channel: 'msedge', headless: false });
  context = await browser.newContext({ viewport: { width: 1440, height: 960 }, permissions: ['clipboard-read', 'clipboard-write'], recordVideo: { dir: path.join(out, 'video') } });
  await context.addInitScript(() => {
    window.__visual = { longTasks: [], cls: 0, frames: [] };
    new PerformanceObserver(list => list.getEntries().forEach(e => window.__visual.longTasks.push(Math.round(e.duration)))).observe({ type: 'longtask', buffered: true });
    new PerformanceObserver(list => list.getEntries().forEach(e => { if (!e.hadRecentInput) window.__visual.cls += e.value; })).observe({ type: 'layout-shift', buffered: true });
  });
  page = await context.newPage(); page.on('pageerror', e => errors.push(e.message)); page.setDefaultTimeout(15000);
  await page.goto(origin); await page.getByTestId('library-ready').waitFor();
  measurements.push({ libraryTotal: librarySize + 2, renderedPageItems: await page.getByTestId('library-prompt').count(), firstLibraryReadyMs: await page.evaluate(() => performance.now()) });
  await page.bringToFront();
  const frames = page.evaluate(() => new Promise(resolve => {
    const intervals = []; let previous, done = false;
    const finish = () => { if (!done) { done = true; resolve(intervals); } };
    const timer = setTimeout(finish, 2500);
    function frame(time) { if (done) return; if (previous) intervals.push(time - previous); previous = time; if (intervals.length >= 40) { clearTimeout(timer); finish(); } else requestAnimationFrame(frame); }
    requestAnimationFrame(frame);
  }));
  await openDock(); measurements.push({ panelFrameIntervals: await frames }); await page.getByRole('button', { name: 'Cerrar panel de NOVA' }).click();
  measurements.push(await page.evaluate(() => ({ checkpoint: 'initial-library-and-first-panel', navigationMs: performance.now(), ...window.__visual, fontRequests: performance.getEntriesByType('resource').filter(e => /woff|ttf|fonts\./.test(e.name)).length })));
  if (process.argv.includes('--performance-only')) {
    const queries = ['Una propuesta', 'Revisar una decisión', 'Idea 1000', 'Una propuesta', 'Revisar una decisión'];
    for (const query of queries) {
      const started = performance.now(), received = page.waitForResponse(response => response.url().startsWith(origin + '/api/v1/prompts?') && new URL(response.url()).searchParams.get('search') === query && response.status() === 200);
      await page.getByRole('textbox', { name: 'Buscar en la biblioteca', exact: true }).fill(query);
      const response = await received; await page.getByTestId('library-ready').waitFor();
      measurements.push({ query, searchMs: Math.round(performance.now() - started), requestTiming: response.request().timing() });
    }
    fs.writeFileSync(path.join(out, 'validation.json'), JSON.stringify({ status: 'PASS', phase, performanceOnly: true, syntheticDataOnly: true, measurements }, null, 2)); console.log('PASS', out); return;
  }
  for (const theme of ['dark', 'light']) {
    if (theme === 'light') await page.getByRole('button', { name: 'Activar tema claro' }).click();
    for (const width of [320, 390, 768, 1024, 1440]) {
      await page.setViewportSize({ width, height: 960 }); await page.evaluate(() => scrollTo(0, 0));
      await shot(`discover-${theme}-${width}`); await openDock(); await sleep(250); await bounds();
      await shot(`assistant-${theme}-${width}`); await page.getByRole('button', { name: 'Cerrar panel de NOVA' }).click();
    }
    checks.push(theme + '-320-390-768-1024-1440');
  }
  await page.setViewportSize({ width: 1440, height: 960 });
  const search = page.getByRole('textbox', { name: 'Buscar en la biblioteca', exact: true });
  if (phase === 'after') {
    await page.route('**/api/v1/prompts?*', async route => { await sleep(650); await route.continue(); });
    await search.fill('Una'); await page.locator('.prompt-skeleton').waitFor(); await shot('sustained-library-load');
    await page.getByTestId('library-ready').waitFor(); assert.equal(await page.locator('.prompt-skeleton').count(), 0);
    await page.unroute('**/api/v1/prompts?*'); checks.push('delayed-library-skeleton-and-stable-cleanup');
  }
  const t = performance.now(); await search.fill('Una propuesta'); await page.getByRole('button', { name: prompt.title, exact: true }).waitFor(); await page.getByTestId('library-ready').waitFor();
  measurements.push({ searchMs: Math.round(performance.now() - t), renderedItems: await page.getByTestId('library-prompt').count(), totalSyntheticItems: librarySize + 2 });
  await page.getByRole('button', { name: prompt.title, exact: true }).click(); await page.getByTestId('prompt-focus').waitFor(); await shot('focus-light');
  await page.getByTestId('variable-field').fill('una biblioteca de ideas'); await page.getByTestId('compile-ready').waitFor();
  const compiled = await page.getByTestId('compiled-preview').innerText(); assert.ok(compiled.includes('una biblioteca de ideas'));
  await page.getByRole('button', { name: 'Copiar prompt', exact: true }).click(); assert.equal(await page.evaluate(() => navigator.clipboard.readText()), compiled);
  await page.getByRole('button', { name: 'Editar', exact: true }).click(); await page.getByTestId('prompt-editor').waitFor(); await shot('edit-light');
  await page.getByRole('button', { name: 'Guardar versión', exact: true }).click(); await page.getByTestId('prompt-focus').waitFor(); assert.equal((await api('/prompts/' + prompt.id)).templateText, prompt.templateText);
  await library(); await search.fill('Revisar una decisión'); await page.getByRole('button', { name: plain.title, exact: true }).click(); await page.getByTestId('compile-ready').waitFor();
  await page.getByRole('button', { name: 'Usar en…', exact: true }).click(); await page.getByRole('dialog').waitFor(); await shot('providers-light');
  for (let index = 0; index < 16; index++) { await page.keyboard.press(index < 8 ? 'Tab' : 'Shift+Tab'); assert.ok(await page.evaluate(() => !!document.activeElement.closest('[role="dialog"]')), 'Dialog focus trap'); }
  const accessibility = await context.newCDPSession(page); const tree = await accessibility.send('Accessibility.getFullAXTree'); await accessibility.detach();
  fs.writeFileSync(path.join(out, 'dialog-accessibility-tree.json'), JSON.stringify(tree, null, 2));
  checks.push('dialog-tab-shift-tab-focus-containment-and-accessibility-tree');
  await page.keyboard.press('Escape');
  const restoredFocus = await page.getByRole('button', { name: 'Usar en…', exact: true }).evaluate(n => n === document.activeElement);
  measurements.push({ providerDialogRestoresFocus: restoredFocus }); if (phase === 'after') assert.equal(restoredFocus, true);
  if (phase === 'after') {
    // UI fault injection is limited to the owned browser; the real backend/model are unchanged.
    await page.route('**/api/v1/ai/local/execute', route => route.fulfill({ status: 503, contentType: 'application/json', body: '{}' }));
    await page.getByRole('button', { name: 'Usar en…', exact: true }).click();
    await page.getByRole('button', { name: 'Ejecutar en este equipo', exact: true }).click();
    await page.getByRole('alert').waitFor(); await shot('ollama-error');
    await page.unroute('**/api/v1/ai/local/execute');
    await context.route('https://chatgpt.com/**', route => route.fulfill({ contentType: 'text/html', body: '<title>Destino de prueba</title><p>Destino interceptado por QA, sin envío externo.</p>' }));
    await page.getByRole('button', { name: 'ChatGPT', exact: true }).click();
    const popup = page.waitForEvent('popup'); await page.getByRole('button', { name: 'Copiar y abrir', exact: true }).click();
    const target = await popup; await target.waitForURL('https://chatgpt.com/'); assert.equal(await page.evaluate(() => navigator.clipboard.readText()), plain.templateText); await target.close();
    await page.keyboard.press('Escape');
    await page.route('**/api/v1/providers', async route => { const response = await route.fetch(), providers = await response.json(); providers.find(p => p.providerId === 'claude').modesSupported = []; await route.fulfill({ response, json: providers }); });
    await page.route('**/api/v1/ai/local/models', route => route.fulfill({ contentType: 'application/json', body: '{"models":[]}' }));
    await page.getByRole('button', { name: 'Usar en…', exact: true }).click();
    assert.equal(await page.getByRole('button', { name: 'Claude · no disponible', exact: true }).isDisabled(), true);
    assert.equal(await page.getByRole('button', { name: 'Ejecutar en este equipo', exact: true }).isDisabled(), true);
    await shot('unavailable-provider-and-model'); await page.keyboard.press('Escape');
    await page.unroute('**/api/v1/providers'); await page.unroute('**/api/v1/ai/local/models');
    checks.push('injected-ollama-error-unsupported-provider-no-model-and-copy-open-intercepted-destination');
  }
  await library(); await search.fill(''); await page.getByTestId('library-ready').waitFor();
  await page.getByRole('button', { name: 'Nuevo prompt', exact: true }).click(); await page.getByTestId('prompt-editor').waitFor(); await shot('create-light');
  await page.getByLabel('Título', { exact: true }).fill('Mi nueva instrucción'); await page.getByLabel('Contenido', { exact: true }).fill('Resume el objetivo y propone tres pasos.');
  await page.getByRole('button', { name: 'Guardar prompt', exact: true }).click(); await page.getByTestId('prompt-focus').waitFor();
  assert.equal(await page.getByTestId('focus-title').innerText(), 'Mi nueva instrucción'); checks.push('search-variables-copy-edit-save-create-provider-dialog-keyboard');
  await page.getByRole('button', { name: 'Activar tema oscuro' }).click(); await shot('focus-dark'); await library();
  await page.getByRole('button', { name: 'Nuevo prompt', exact: true }).click(); await page.getByTestId('prompt-editor').waitFor(); await shot('create-dark');
  await page.getByRole('button', { name: 'Volver', exact: true }).click();
  const leave = page.getByRole('button', { name: 'Salir del editor', exact: true }); if (await leave.isVisible()) await leave.click();
  await library();
  await page.setViewportSize({ width: 640, height: 480 }); await openDock(); await sleep(250); await bounds(); await shot('short-height-and-200-percent-equivalent');
  await page.getByRole('button', { name: 'Cerrar panel de NOVA' }).click();
  await page.setViewportSize({ width: 390, height: 480 }); await page.emulateMedia({ reducedMotion: 'reduce' }); await openDock(); await sleep(50); await bounds(); await shot('reduced-motion');
  if (phase === 'after') {
    assert.equal(await page.locator('.nova-logo').first().evaluate(n => getComputedStyle(n).animationName), 'none');
    await page.getByRole('button', { name: 'Cerrar panel de NOVA' }).click(); await page.getByRole('button', { name: 'Configuración', exact: true }).click();
    await page.getByLabel('Animaciones decorativas', { exact: true }).uncheck(); await page.getByRole('button', { name: 'Cerrar diálogo' }).click();
    await page.reload(); await page.getByTestId('library-ready').waitFor(); assert.equal(await page.locator('html').getAttribute('data-motion'), 'reduced');
    checks.push('decorative-motion-setting-persists');
  }
  checks.push('short-height-reduced-motion-and-reflow');
  await page.emulateMedia({ reducedMotion: 'no-preference' }); await page.setViewportSize({ width: 1440, height: 960 });
  await page.bringToFront();
  const nativeBefore = await page.evaluate(() => ({ width: innerWidth, dpr: devicePixelRatio }));
  for (let index = 0; index < 5; index++) await page.keyboard.press('Control+Equal');
  const nativeAfter = await page.evaluate(() => ({ width: innerWidth, dpr: devicePixelRatio }));
  const nativeZoom200 = Math.abs(nativeAfter.dpr / nativeBefore.dpr - 2) < .05;
  measurements.push({ nativeZoom200, nativeBefore, nativeAfter });
  if (nativeZoom200) { assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)); await shot('native-zoom-200'); }
  await page.keyboard.press('Control+0');
  await page.evaluate(() => { document.documentElement.style.zoom = '2'; }); await openDock(); await sleep(250);
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)); await shot('content-zoom-200');
  measurements.push({ zoom200Mechanism: 'CSS zoom: 2 (content enlargement, not native browser chrome zoom)' });
  await page.evaluate(() => { document.documentElement.style.zoom = ''; });
  measurements.push(await page.evaluate(() => ({ ...window.__visual, fontRequests: performance.getEntriesByType('resource').filter(e => /woff|ttf|fonts\./.test(e.name)).length })));
  if (phase === 'after') { const preview = await context.newPage(); await preview.goto('file:///' + path.join(root, 'docs/nova/NOVA_ORBIT_PREVIEW.html').replaceAll('\\', '/')); await preview.screenshot({ path: path.join(out, 'orbit-sizes.png'), fullPage: true }); await preview.close(); }
  assert.deepEqual(errors, []);
  const assets = fs.readdirSync(path.join(temp, 'web/assets')).filter(f => /\.(js|css)$/.test(f)).map(f => ({ name: f, bytes: fs.statSync(path.join(temp, 'web/assets', f)).size }));
  fs.writeFileSync(path.join(out, 'validation.json'), JSON.stringify({ status: 'PASS', phase, headed: true, syntheticDataOnly: true, checks, measurements, assets, browserErrors: errors }, null, 2)); console.log('PASS', out);
})().catch(async e => { fs.writeFileSync(path.join(out, 'failure.txt'), e.stack); console.error(e); await page?.screenshot({ path: path.join(out, 'failure.png') }).catch(() => {}); process.exitCode = 1; }).finally(async () => { await context?.close(); await browser?.close(); if (server?.exitCode === null) { server.kill(); await new Promise(r => { server.once('exit', r); setTimeout(r, 5000).unref(); }); } fs.writeFileSync(path.join(out, 'api.log'), log); });
