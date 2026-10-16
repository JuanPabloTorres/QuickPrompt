// Actual app recordings in a separate database/browser. No personal tab or provider site is used.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const { install } = require('./Install-PracticalLibrary.cjs');
const { run } = require('../automation/playwright/runner.cjs');
const repo = path.resolve(__dirname, '..'), visible = process.argv.includes('--visible'), demoOnly = process.argv.includes('--demo-only'), workerOnly = process.argv.includes('--worker-only');
const temporary = path.join(repo, '.playwright', 'usage-videos-' + Date.now());
const media = path.join(repo, 'apps/web/public/tutoriales'), evidence = path.join(repo, 'docs/evidence/learning');
for (const dir of [temporary, media, evidence]) fs.mkdirSync(dir, { recursive: true });
const origin = 'http://127.0.0.1:5202'; let server, browser, serverOutput = ''; const reports = [];
if (workerOnly) {
  const priorFile = fs.existsSync(path.join(evidence, 'recordings.json')) ? 'recordings.json' : 'recording-failure.json';
  const prior = JSON.parse(fs.readFileSync(path.join(evidence, priorFile), 'utf8'));
  for (const item of prior.videos || prior.completed) if (item.id !== '04-playwright' && item.status === 'PASS' && fs.existsSync(path.join(media, item.id + '.webm'))) reports.push({ ...item, sourceRun: prior.temporary });
}
async function api(route, body) { const response = await fetch(origin + '/api/v1' + route, { method: body === undefined ? 'GET' : 'POST', headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' }, body: body === undefined ? undefined : JSON.stringify(body) }); const value = await response.json(); assert.ok(response.ok, value.detail); return value; }
async function start() {
  server = spawn('dotnet', [path.join(repo, '.playwright/learning-build/Debug/net8.0/QuickPrompt.Api.dll'), '--QuickPrompt:Port=5202', `--QuickPrompt:DataPath=${path.join(temporary, 'library.sqlite')}`, `--QuickPrompt:WebRoot=${path.join(repo, 'apps/web/dist')}`], { cwd: repo, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  const output = chunk => { serverOutput += chunk; fs.appendFileSync(path.join(temporary, 'server.log'), chunk); }; server.stdout.on('data', output); server.stderr.on('data', output);
  for (let attempt = 0; attempt < 150; attempt++) { if (server.exitCode !== null) throw Error('La instancia de grabación no pudo arrancar.'); if (serverOutput.includes('Now listening on: ' + origin) && (await fetch(origin + '/health')).ok) return; await new Promise(resolve => setTimeout(resolve, 100)); }
  throw Error('No se confirmó el arranque de la instancia propia.');
}
function timestamp(ms) { return new Date(Math.max(0, ms)).toISOString().slice(11, 23); }
function captions(file, cues, end) { fs.writeFileSync(path.join(media, file + '.vtt'), 'WEBVTT\n\n' + cues.map((cue, index) => `${index + 1}\n${timestamp(cue.ms)} --> ${timestamp(cues[index + 1]?.ms ?? end)}\n${cue.text}\n`).join('\n')); }
async function lesson(id, title, actions) {
  const context = await browser.newContext({ viewport: { width: 1280, height: 800 }, reducedMotion: 'reduce', permissions: ['clipboard-read', 'clipboard-write'], recordVideo: { dir: temporary, size: { width: 1280, height: 800 } } });
  const page = await context.newPage(); page.setDefaultTimeout(90000); const errors = []; page.on('pageerror', error => errors.push(error.message)); const cues = [], started = Date.now();
  await page.goto(origin); await page.getByRole('button', { name: 'Nuevo prompt', exact: true }).waitFor();
  await page.evaluate(() => { document.body.style.paddingTop = '90px'; const banner = document.createElement('div'); banner.id = 'usage-caption'; Object.assign(banner.style, { position: 'fixed', inset: '0 0 auto', zIndex: '2147483647', background: '#102b3b', color: '#fff', padding: '18px 36px', minHeight: '84px', font: '22px/1.4 Segoe UI, sans-serif', borderBottom: '3px solid #65e3d2' }); document.body.append(banner); });
  const pause = ms => page.waitForTimeout(ms);
  async function say(text, delay = 1500) { cues.push({ ms: Date.now() - started, text }); await page.locator('#usage-caption').evaluate((node, text) => node.textContent = text, text); await pause(delay); }
  async function highlight(locator) { await locator.scrollIntoViewIfNeeded(); const previous = await locator.evaluate(node => { const value = node.style.outline; node.style.outline = '3px solid #65e3d2'; node.style.outlineOffset = '4px'; return value; }); await pause(500); await locator.evaluate((node, value) => node.style.outline = value, previous); }
  await say(title + ' · Datos de ejemplo, biblioteca de prueba.');
  await page.screenshot({ path: path.join(media, id + '.png') });
  try { await actions({ page, say, pause, highlight }); assert.deepEqual(errors, []); await pause(1600); }
  finally { const end = Date.now() - started; await context.close(); await page.video().saveAs(path.join(media, id + '.webm')); captions(id, cues, end); reports.push({ id, title, durationMs: end, status: errors.length ? 'FAIL' : 'PASS', errors }); console.log('VIDEO', id, Math.round(end / 1000) + 's'); }
}
(async () => {
  await start(); const installed = await install(origin); assert.equal(installed.added, 60);
  if (!demoOnly && !workerOnly) {
    browser = await chromium.launch({ channel: 'msedge', headless: !visible, slowMo: visible ? 90 : 0 });
    await lesson('01-biblioteca', '1. De un prompt guardado a una respuesta de Qwen', async ({ page, say, pause, highlight }) => {
      await say('Elige una colección. Aquí usaremos Correos y comunicación.');
      const collection = page.getByLabel('Colección', { exact: true }); await highlight(collection); await collection.selectOption({ label: 'Correos y comunicación' });
      const prompt = page.getByRole('button', { name: 'Escribir un correo profesional', exact: true }); await highlight(prompt); await prompt.click();
      await say('Las variables son tus datos. Completa destinatario, objetivo y contexto.');
      await page.getByLabel('destinatario', { exact: true }).fill('equipo de soporte'); await pause(600);
      await page.getByLabel('objetivo', { exact: true }).fill('solicitar el estado de mi pedido'); await pause(600);
      await page.getByLabel('contexto', { exact: true }).fill('Pedido de ejemplo 123. Se esperaba ayer y no recibí novedades. Quiero conocer una fecha estimada.');
      await say('Copiar prepara la instrucción. Para obtener una respuesta, pulsa Usar en…');
      await highlight(page.getByRole('button', { name: 'Usar en…', exact: true })); await page.getByRole('button', { name: 'Usar en…', exact: true }).click();
      await say('Ejecutar en este equipo usa Qwen local. Este clic confirma la ejecución.');
      await page.getByRole('button', { name: 'Ejecutar en este equipo', exact: true }).click();
      await page.getByText('RESPUESTA COMPLETA · SIN GUARDAR', { exact: true }).waitFor({ timeout: 180000 });
      await page.locator('.local-response').scrollIntoViewIfNeeded(); assert.ok((await page.locator('.local-response pre').innerText()).length > 20);
      await say('Esta es la respuesta de la IA. Puedes copiarla; la plantilla queda lista para reutilizar.'); await pause(2200);
    });
    await lesson('02-nova', '2. Preparar una instrucción con NOVA', async ({ page, say, pause }) => {
      await say('Escribe qué instrucción necesitas. No hace falta redactar una plantilla.');
      await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?' }).fill('Crea una instrucción breve, sin variables, que pida responder con tres ideas para hacer reuniones de equipo más claras.');
      await say('Pulsa la flecha. NOVA busca primero en la biblioteca y después prepara el prompt.');
      await page.getByRole('button', { name: 'Preparar prompt', exact: true }).click(); await page.locator('.prepared-result').waitFor({ timeout: 180000 });
      await page.locator('.prepared-result').scrollIntoViewIfNeeded(); assert.ok(await page.locator('.preparation-dialog h2').innerText());
      assert.equal(await page.getByRole('button', { name: 'Copiar', exact: true }).isEnabled(), true);
      await say('Ya tienes un título y una instrucción. Guardar la conserva, sin abrir un formulario.');
      await page.getByRole('button', { name: 'Guardar', exact: true }).click(); await page.getByRole('button', { name: 'Guardado', exact: true }).waitFor();
      await say('Este es el prompt, no la respuesta. Pulsa Usar en… cuando quieras que Qwen lo ejecute.'); await pause(1000);
    });
    await lesson('03-variables', '3. Crear y quitar variables con el mismo botón', async ({ page, say, pause }) => {
      await page.getByRole('button', { name: 'Nuevo prompt', exact: true }).click();
      await page.getByLabel('Título', { exact: true }).fill('Explicar un tema con un ejemplo');
      const content = page.getByLabel('Contenido', { exact: true }), original = 'Explica inteligencia artificial con un ejemplo sencillo.';
      await content.fill(original); const start = original.indexOf('inteligencia'), end = start + 'inteligencia artificial'.length;
      await say('Selecciona la parte que cambiarás cada vez: inteligencia artificial.');
      await content.evaluate((node, range) => { node.focus(); node.setSelectionRange(range.start, range.end); node.dispatchEvent(new Event('select', { bubbles: true })); }, { start, end }); await pause(1000);
      await say('Pulsa Crear / quitar variable. El texto seleccionado queda entre signos de variable.');
      const toggle = page.getByRole('button', { name: 'Crear / quitar variable', exact: true }); await toggle.click(); await page.getByText(/Variable inteligencia artificial creada/).waitFor();
      assert.equal(await content.inputValue(), 'Explica <inteligencia artificial> con un ejemplo sencillo.');
      await say('Vuelve a pulsar el MISMO botón: recuperas el texto normal, sin perderlo.'); await toggle.click(); await page.getByText('Variable convertida en texto normal.', { exact: true }).waitFor(); assert.equal(await content.inputValue(), original);
      await say('Créala otra vez y guarda. Así tendrás un campo para usar otro tema.'); await toggle.click(); await page.getByText(/Variable inteligencia artificial creada/).waitFor();
      await page.getByRole('button', { name: 'Guardar prompt', exact: true }).click();
      await page.getByLabel('inteligencia artificial', { exact: true }).fill('cómo funciona una biblioteca de prompts');
      await say('Al usar el prompt, el campo recibe tus datos. La plantilla original se conserva.'); await pause(1000);
    });
    await browser.close(); browser = null;
  }
  const workflow = { mode: 'qa', steps: [
    { action: 'navigate', url: origin + '/' },
    { action: 'fill', role: 'textbox', name: '¿Qué quieres hacer hoy?', value: 'Entender un tema con ejemplos' },
    { action: 'click', role: 'button', name: 'Entender un tema con ejemplos' },
    { action: 'expectVisible', role: 'heading', name: 'Entender un tema con ejemplos' },
    { action: 'fill', role: 'textbox', name: 'tema', value: 'cómo funciona QuickPrompt' },
    { action: 'fill', role: 'textbox', name: 'nivel', value: 'principiante' },
    { action: 'click', role: 'button', name: 'Copiar prompt' },
    { action: 'expectVisible', role: 'button', name: 'Copiar prompt' }, { action: 'screenshot' },
  ] };
  const out = path.join(temporary, 'visible-worker');
  console.log('PLAYWRIGHT: abre su navegador, busca, completa y copia. No controla tu pestaña.');
  const result = await run(workflow, { origins: [origin], timeoutMs: 60000 }, { out, headed: visible, record: true, stepDelayMs: 1400 }); assert.equal(result.state, 'PASS');
  const learning = await api('/prompts?search=' + encodeURIComponent('Entender un tema con ejemplos'));
  assert.equal(learning.items[0].uses, 1); assert.equal(learning.items[0].templateText.includes('<tema>'), true);
  if (!demoOnly) {
    fs.copyFileSync(path.join(out, 'recorrido.webm'), path.join(media, '04-playwright.webm')); fs.copyFileSync(path.join(out, 'step-8.png'), path.join(media, '04-playwright.png'));
    const texts = ['Playwright abre una biblioteca de prueba, en un navegador separado.', 'Escribe la tarea que necesita encontrar.', 'Abre el prompt guardado.', 'Comprueba que llegó a la pantalla correcta.', 'Completa el tema de ejemplo.', 'Completa el nivel: principiante.', 'Pulsa Copiar prompt. No envía mensajes externos.', 'Comprueba la acción.', 'Guarda una captura y finaliza el recorrido.'];
    captions('04-playwright', texts.map((text, index) => ({ text, ms: index === 0 ? 0 : result.steps[index - 1].elapsedMs })), result.elapsedMs);
    reports.push({ id: '04-playwright', status: 'PASS', durationMs: result.elapsedMs, actualClosedWorker: true, usageAcknowledged: true });
  }
  fs.writeFileSync(path.join(evidence, demoOnly ? 'visible-demo.json' : 'recordings.json'), JSON.stringify({ status: 'PASS', visible, syntheticDataOnly: true, temporary, videos: reports, worker: result }, null, 2));
})().catch(error => { console.error(error); fs.writeFileSync(path.join(evidence, 'recording-failure.json'), JSON.stringify({ error: error.stack, temporary, completed: reports }, null, 2)); process.exitCode = 1; }).finally(async () => { await browser?.close(); if (server && server.exitCode === null) { const exited = new Promise(resolve => server.once('exit', resolve)); server.kill(); await exited; } });
