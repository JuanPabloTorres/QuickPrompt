const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { run } = require('../../automation/playwright/runner.cjs');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), origin = 'http://127.0.0.1:5216', evidence = path.join(root, 'docs/evidence/local-commands');
const temporary = path.join(root, '.playwright', 'commands-' + Date.now()), headed = process.argv.includes('--headed');
fs.mkdirSync(temporary, { recursive: true }); fs.mkdirSync(evidence, { recursive: true });
let server, browser, output = ''; const checks = [], errors = [];
function pass(name) { checks.push({ name, status: 'PASS' }); console.log('PASS', name); }
async function api(route, body) {
  const response = await fetch(origin + '/api/v1' + route, { method: body === undefined ? 'GET' : 'POST', headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' }, body: body === undefined ? undefined : JSON.stringify(body) });
  const result = await response.json(); assert.ok(response.ok, result.detail); return result;
}
(async () => {
  server = spawn('dotnet', [path.join(root, '.playwright/command-build/Debug/net8.0/QuickPrompt.Api.dll'), '--QuickPrompt:Port=5216', `--QuickPrompt:WebRoot=${path.join(root, 'apps/web/dist')}`, `--QuickPrompt:DataPath=${path.join(temporary, 'library.sqlite')}`], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', chunk => { output += chunk; }); server.stderr.on('data', chunk => { output += chunk; });
  for (let index = 0; index < 150; index++) { if (server.exitCode !== null) throw Error('Owned server exited.'); if (output.includes('Now listening on: ' + origin)) break; if (index === 149) throw Error('Owned server readiness failed.'); await new Promise(resolve => setTimeout(resolve, 100)); }
  const source = await api('/prompts', { title: 'Revisar arquitectura', templateText: 'Revisa <codigo> y enumera tres mejoras.', description: 'Revisión de código de ejemplo.' });
  await api('/prompts', { title: 'Correo para clientes', templateText: 'Escribe a <cliente>.' }); await api('/prompts', { title: 'Correo para equipo', templateText: 'Escribe a <equipo>.' });
  const worker = await run({ mode: 'qa', steps: [
    { action: 'navigate', url: origin + '/' },
    { action: 'fill', role: 'textbox', name: '¿Qué quieres hacer hoy?', value: 'Ve al prompt «Revisar arquitectura»' },
    { action: 'click', role: 'button', name: 'Preparar prompt' },
    { action: 'expectVisible', role: 'heading', name: source.title },
    { action: 'screenshot' },
  ] }, { origins: [origin], timeoutMs: 120000 }, { headed, record: process.argv.includes('--record'), stepDelayMs: headed ? 700 : 0, locatorTimeoutMs: 90000, out: path.join(evidence, 'worker') });
  assert.equal(worker.state, 'PASS'); pass('Closed Playwright worker drives a natural request to the actual prompt');
  browser = await chromium.launch({ channel: 'msedge', headless: !headed });
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, reducedMotion: 'reduce' }), page = await context.newPage();
  page.on('pageerror', error => errors.push(error.message)); page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
  await page.goto(origin); const input = page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true });
  async function ask(text) { await input.fill(text); await page.getByRole('button', { name: 'Preparar prompt', exact: true }).click(); }
  await ask('Ve al prompt correo'); const choices = page.getByRole('dialog', { name: 'Encontré varios prompts', exact: true }); await choices.waitFor();
  await choices.getByRole('button', { name: 'Correo para equipo', exact: true }).click(); await page.getByRole('heading', { name: 'Correo para equipo', exact: true }).waitFor();
  pass('Ambiguous titles ask and navigate only after selecting a real candidate');
  await page.getByRole('button', { name: 'Biblioteca', exact: true }).click(); await ask('Actualiza el prompt «Revisar arquitectura»');
  const change = page.getByRole('dialog', { name: '¿Qué quieres cambiar?', exact: true }); await change.waitFor();
  await change.getByLabel('Cambio que necesitas', { exact: true }).fill('Añade al final exactamente la frase CIERREVERIFICADO. Conserva el resto del contenido y la variable codigo.');
  await change.getByRole('button', { name: 'Preparar cambio', exact: true }).click();
  await page.getByRole('heading', { name: 'Afina tu prompt.', exact: true }).waitFor({ timeout: 180000 });
  const text = await page.getByLabel('Contenido', { exact: true }).inputValue(); assert.ok(text.includes('CIERREVERIFICADO')); assert.ok(text.includes('<codigo>'));
  assert.equal((await api('/prompts/' + source.id)).revision, 1); pass('Real local Qwen prepares the requested change on the selected source without saving');
  await page.screenshot({ path: path.join(evidence, 'review-before-save.png'), fullPage: true });
  await page.getByRole('button', { name: 'Guardar versión', exact: true }).click(); await page.getByRole('heading', { name: source.title, exact: true }).waitFor();
  const updated = await api('/prompts/' + source.id); assert.equal(updated.revision, 2); assert.ok(updated.templateText.includes('CIERREVERIFICADO'));
  assert.equal((await api('/prompts/' + source.id + '/versions')).length, 2); assert.equal((await api('/prompts?size=1')).total, 3);
  pass('Explicit save updates the same ID, retains both versions and creates no duplicate');
  await page.getByRole('button', { name: 'Biblioteca', exact: true }).click();
  await ask('Actualiza el prompt «Revisar arquitectura» para añadir al final exactamente SEGUNDOREVISION. Conserva el resto y la variable codigo.');
  await page.getByRole('heading', { name: 'Afina tu prompt.', exact: true }).waitFor({ timeout: 180000 });
  assert.ok((await page.getByLabel('Contenido', { exact: true }).inputValue()).includes('SEGUNDOREVISION'));
  assert.equal((await api('/prompts/' + source.id)).revision, 2);
  await page.getByRole('button', { name: 'Cancelar', exact: true }).click();
  await page.getByRole('dialog', { name: '¿Salir del editor?', exact: true }).getByRole('button', { name: 'Salir del editor', exact: true }).click();
  await page.getByRole('heading', { name: source.title, exact: true }).waitFor();
  assert.equal((await api('/prompts/' + source.id)).revision, 2);
  pass('An inline update instruction reaches the correct editor; cancelling leaves the stored version unchanged');
  await page.getByRole('button', { name: 'Biblioteca', exact: true }).click(); await ask('Ve al prompt QZNONE');
  const missing = page.getByRole('dialog', { name: '¿Qué prompt buscas?', exact: true }); await missing.waitFor(); await missing.getByLabel('Título del prompt', { exact: true }).fill(source.title);
  await missing.getByRole('button', { name: 'Buscar prompt', exact: true }).click(); await page.getByRole('heading', { name: source.title, exact: true }).waitFor();
  pass('Missing item asks for a title and resolves the clarification without inventing an ID');
  await page.getByRole('button', { name: 'Biblioteca', exact: true }).click(); await page.setViewportSize({ width: 390, height: 844 }); await ask('Ve al prompt correo'); await choices.waitFor();
  assert.equal(await choices.evaluate(element => element.scrollWidth > element.clientWidth + 1 || document.documentElement.scrollWidth > innerWidth + 1), false);
  await page.screenshot({ path: path.join(evidence, 'clarification-mobile.png'), fullPage: true }); await page.keyboard.press('Escape'); await choices.waitFor({ state: 'hidden' });
  await page.waitForFunction(() => document.activeElement?.id === 'search'); pass('Mobile clarification closes with Escape and restores focus');
  assert.deepEqual(errors, []);
  fs.writeFileSync(path.join(evidence, 'acceptance.json'), JSON.stringify({ status: 'PASS', checks, errors, realLocalQwen: true, isolatedDatabase: true, worker: worker.state, headed }, null, 2));
})().catch(error => { console.error(error); fs.writeFileSync(path.join(evidence, 'acceptance.json'), JSON.stringify({ status: 'FAIL', checks, errors, error: error.stack }, null, 2)); process.exitCode = 1; }).finally(async () => {
  await browser?.close(); if (server && server.exitCode === null) { const exited = new Promise(resolve => server.once('exit', resolve)); server.kill(); await exited; }
});
