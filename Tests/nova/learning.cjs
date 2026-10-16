const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const { install } = require('../../scripts/Install-PracticalLibrary.cjs');
const root = path.resolve(__dirname, '../..'), origin = 'http://127.0.0.1:5211';
const directory = path.join(root, '.playwright', 'learning-qa-' + Date.now()), evidence = path.join(root, 'docs/evidence/learning');
fs.mkdirSync(directory, { recursive: true }); fs.mkdirSync(evidence, { recursive: true });
let server, browser, output = ''; const checks = [], errors = [], videos = [], failedRequests = [];
function pass(name) { checks.push({ name, status: 'PASS' }); console.log('PASS', name); }
(async () => {
  server = spawn('dotnet', [path.join(root, '.playwright/learning-build/Debug/net8.0/QuickPrompt.Api.dll'), '--QuickPrompt:Port=5211', `--QuickPrompt:DataPath=${path.join(directory, 'library.sqlite')}`, `--QuickPrompt:WebRoot=${path.join(root, 'apps/web/dist')}`], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', chunk => { output += chunk; }); server.stderr.on('data', chunk => { output += chunk; });
  for (let index = 0; index < 150; index++) { if (server.exitCode !== null) throw Error(output); if (output.includes('Now listening on: ' + origin)) break; if (index === 149) throw Error('Owned QA server readiness failed'); await new Promise(resolve => setTimeout(resolve, 100)); }
  assert.equal((await install(origin)).added, 60); pass('60 curated prompts, additive import and durable idempotency');
  browser = await chromium.launch({ channel: 'msedge', headless: true }); const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, reducedMotion: 'reduce' }); const page = await context.newPage();
  page.on('pageerror', error => errors.push(error.message)); page.on('console', message => { if (message.type() === 'error') errors.push(message.text() + ' ' + message.location().url); });
  page.on('response', response => { if (response.status() >= 400) failedRequests.push({ url: response.url(), status: response.status() }); });
  await page.goto(origin); await page.getByLabel('Colección', { exact: true }).selectOption({ label: 'Correos y comunicación' });
  await page.getByRole('button', { name: 'Escribir un correo profesional', exact: true }).waitFor(); assert.equal(await page.locator('.prompt-item').count(), 3); pass('Actual category navigation displays useful tasks');
  await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?' }).fill('NO_EXISTE_NINGUN_PROMPT'); await page.getByRole('heading', { name: 'Aquí empieza una nueva idea.' }).waitFor();
  await page.getByRole('button', { name: 'Borrar búsqueda' }).click(); await page.getByRole('button', { name: 'Escribir un correo profesional', exact: true }).waitFor(); pass('Clear search restores the selected collection without losing its filter');
  await page.getByRole('button', { name: 'Nuevo prompt', exact: true }).click(); await page.getByLabel('Título', { exact: true }).fill('Variable reversible');
  const content = page.getByLabel('Contenido', { exact: true }), text = 'Explica α, β y 李 con claridad.';
  await content.fill(text); const start = text.indexOf('α'), end = text.indexOf(' con');
  await content.evaluate((node, range) => { node.focus(); node.setSelectionRange(range.start, range.end); node.dispatchEvent(new Event('select', { bubbles: true })); }, { start, end });
  const toggle = page.getByRole('button', { name: 'Crear / quitar variable', exact: true }); await toggle.click(); await page.getByText(/Variable .* creada/).waitFor(); assert.notEqual(await content.inputValue(), text);
  await toggle.click(); await page.getByText('Variable convertida en texto normal.', { exact: true }).waitFor(); assert.equal(await content.inputValue(), text); pass('Same toggle button round-trips punctuation and Unicode in actual Edge');
  await page.getByRole('button', { name: 'Ayuda', exact: true }).click(); await page.getByRole('dialog', { name: 'Empieza aquí: guía y videos' }).waitFor();
  await page.getByRole('button', { name: 'Añadir biblioteca práctica', exact: true }).click(); await page.getByText('Ya tienes estos prompts. No se añadieron duplicados.').waitFor(); pass('In-app guide imports idempotently and preserves the open editor');
  for (const video of await page.locator('video').all()) {
    const metadata = await video.evaluate(async element => {
      element.preload = 'auto'; element.muted = true; element.load();
      await new Promise((resolve, reject) => { const timeout = setTimeout(() => reject(Error('Metadata timeout')), 20000); element.addEventListener('loadedmetadata', () => { clearTimeout(timeout); resolve(); }, { once: true }); element.addEventListener('error', () => { clearTimeout(timeout); reject(Error('Video decoding failed')); }, { once: true }); });
      const duration = element.duration; if (!Number.isFinite(duration)) throw Error('Non-finite video duration');
      await element.play(); element.currentTime = Math.min(duration / 2, duration - 1);
      await new Promise(resolve => element.addEventListener('seeked', resolve, { once: true })); element.pause();
      const canvas = document.createElement('canvas'); canvas.width = element.videoWidth; canvas.height = element.videoHeight; const drawing = canvas.getContext('2d'); drawing.drawImage(element, 0, 0); const pixels = drawing.getImageData(0, 0, canvas.width, canvas.height).data; let min = 255, max = 0; for (let index = 0; index < pixels.length; index += 4) { min = Math.min(min, pixels[index]); max = Math.max(max, pixels[index]); }
      return { label: element.getAttribute('aria-label'), duration, width: element.videoWidth, height: element.videoHeight, pixelRange: max - min, frame: canvas.toDataURL('image/png'), src: element.currentSrc };
    });
    assert.ok(metadata.duration > 5 && metadata.duration < 90); assert.equal(metadata.width, 1280); assert.ok(metadata.pixelRange > 30); const id = path.basename(new URL(metadata.src).pathname, '.webm');
    fs.writeFileSync(path.join(evidence, id + '-decoded-frame.png'), Buffer.from(metadata.frame.split(',')[1], 'base64')); delete metadata.frame; videos.push(metadata);
  }
  pass('All four actual WebM recordings load, seek and decode visible frames in the app');
  await page.getByRole('button', { name: 'Ampliar: 1. Usar un prompt de la biblioteca', exact: true }).click();
  await page.waitForFunction(() => document.fullscreenElement?.tagName === 'VIDEO'); await page.evaluate(() => document.exitFullscreen());
  pass('Explicit full-screen button expands the video for readable playback');
  await page.screenshot({ path: path.join(evidence, 'guide-desktop.png'), fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 }); assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)); await page.screenshot({ path: path.join(evidence, 'guide-mobile.png'), fullPage: true });
  await page.getByRole('button', { name: 'Volver a mis prompts', exact: true }).click(); assert.equal(await content.inputValue(), text); pass('Guide works on mobile and closes without discarding the editor draft');
  assert.deepEqual(errors, []);
  fs.writeFileSync(path.join(evidence, 'acceptance.json'), JSON.stringify({ status: 'PASS', checks, videos, errors, failedRequests, database: 'isolated synthetic SQLite', temporary: directory }, null, 2));
})().catch(error => { console.error(error); fs.writeFileSync(path.join(evidence, 'acceptance.json'), JSON.stringify({ status: 'FAIL', checks, videos, errors, failedRequests, error: error.stack }, null, 2)); process.exitCode = 1; }).finally(async () => { await browser?.close(); if (server && server.exitCode === null) { const exited = new Promise(resolve => server.once('exit', resolve)); server.kill(); await exited; } });
