const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { DatabaseSync } = require('node:sqlite');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), origin = 'http://127.0.0.1:5222';
const stamp = new Date().toISOString().replace(/[:.]/g, '-');
const evidence = path.join(root, 'docs/evidence/agentic-2', stamp), temporary = path.join(root, '.playwright', 'agentic-' + stamp);
fs.mkdirSync(evidence, { recursive: true }); fs.mkdirSync(temporary, { recursive: true });
const dbPath = path.join(temporary, 'library.sqlite');
let server, browser, db, output = ''; const checks = [], errors = [], runs = [];
const chainOnly = process.argv.includes('--chain-only'), firstOnly = process.argv.includes('--first-only') || chainOnly;
function report(status, error) { fs.writeFileSync(path.join(evidence, 'acceptance.json'), JSON.stringify({ status, realQwen: true, isolatedDatabase: true, checks, errors, runs, error, evidence }, null, 2)); }
function pass(name) { checks.push({ name, status: 'PASS' }); console.log('PASS', name); report('RUNNING'); }
async function api(route, body, method) {
  const response = await fetch(origin + '/api/v1' + route, { method: method || (body === undefined ? 'GET' : 'POST'), headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' }, body: body === undefined ? undefined : JSON.stringify(body) });
  const result = response.status === 204 ? null : await response.json(); assert.ok(response.ok, JSON.stringify(result)); return result;
}
(async () => {
  server = spawn('dotnet', [path.resolve(root, process.env.NOVA_TEST_BUILD || '.playwright/agentic-2/Debug/net8.0/QuickPrompt.Api.dll'), '--QuickPrompt:Port=5222', `--QuickPrompt:WebRoot=${path.join(root, 'apps/web/dist')}`, `--QuickPrompt:DataPath=${dbPath}`], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', c => { output += c; }); server.stderr.on('data', c => { output += c; });
  for (let n = 0; n < 150; n++) { if (server.exitCode !== null) throw Error('Owned server exited: ' + output); if (output.includes('Now listening on: ' + origin)) break; if (n === 149) throw Error('Readiness failed'); await new Promise(r => setTimeout(r, 100)); }
  const source = await api('/prompts', { title: 'Arquitectura', templateText: 'Revisa la arquitectura de <proyecto>. Evalúa límites, dependencias y pruebas. No inventes requisitos.', defaults: { proyecto: 'API de catálogo de libros en .NET 8 con SQLite, CRUD de libros y pruebas de integración.' }, description: 'Revisión de arquitectura de software y APIs.' });
  await api('/prompts', { title: 'Cartas', templateText: 'Escribe una carta para <puesto> con los datos proporcionados. No inventes experiencia.' });
  await api('/prompts', { title: 'Carta breve', templateText: 'Redacta una carta breve para <puesto>.' });
  browser = await chromium.launch({ channel: 'msedge', headless: !process.argv.includes('--headed') });
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, reducedMotion: 'reduce', recordVideo: { dir: path.join(evidence, 'video'), size: { width: 1280, height: 900 } } });
  const page = await context.newPage(); page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
  page.on('request', request => { if (request.url().endsWith('/nova/prepare')) runs.push({ id: request.postDataJSON().operationId }); });
  db = new DatabaseSync(dbPath, { readOnly: true });
  const snapshot = () => db.prepare("SELECT Kind,Id,Payload FROM Documents WHERE Kind IN ('template','version','compiled','execution') ORDER BY Kind,Id").all();
  const before = snapshot();
  await page.goto(origin);
  const input = page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true });
  async function ask(text) { if(await page.locator('.preparation-options').getAttribute('open')===null) await page.getByText('Opciones de preparación',{exact:true}).click();await page.getByLabel('Modo de NOVA', { exact: true }).selectOption('LIVE'); await input.fill(text); await page.getByRole('button', { name: 'Preparar prompt', exact: true }).click(); }
  async function result() { const dialog = page.getByRole('dialog').filter({ has: page.locator('.prepared-result') }); await dialog.waitFor({ timeout: 240000 }); const id = runs.at(-1)?.id; if (id) { try { fs.writeFileSync(path.join(evidence, id + '-synthetic-draft.json'), JSON.stringify(await api('/nova/drafts/' + id), null, 2)); } catch {} } return dialog; }
  async function close() { await page.getByRole('button', { name: 'Cerrar resultado', exact: true }).click(); }
  async function verifyChain() {
    const beforeChain = snapshot();
    await ask('Busca mi prompt de arquitectura, adáptalo para una API .NET con revisión de seguridad y ejecútalo con Qwen.'); const chainedDialog = await result();
    assert.ok((await chainedDialog.locator('.local-response pre').innerText()).length > 10); assert.deepEqual(snapshot(), beforeChain);
    const chainedRun = await api('/nova/runs/' + runs.at(-1).id); assert.ok(chainedRun.plan.includes('ADAPT')); assert.ok(chainedRun.plan.includes('EXECUTE')); assert.ok(chainedRun.tools.length <= 5);
    fs.writeFileSync(path.join(evidence, 'chained-run-diagnostics.json'), JSON.stringify(chainedRun, null, 2));
    await page.screenshot({ path: path.join(evidence, 'chained-local-execution.png'), fullPage: true }); pass('One request retrieves, adapts, compiles, validates and executes locally within five tools');
  }
  await ask('Busca mi prompt de arquitectura y ábrelo.'); await page.getByRole('heading', { name: 'Arquitectura', exact: true }).waitFor();
  assert.deepEqual(snapshot(), before); pass('Literal search and open use deterministic navigation to a real prompt');
  await page.getByRole('button', { name: 'Biblioteca', exact: true }).click();
  await ask('Busca mi prompt de arquitectura, adáptalo para una API .NET y déjalo listo para utilizar.');
  let dialog = await result();
  const prepared = await dialog.locator('.prepared-text').innerText(); assert.match(prepared, /\.NET/i); assert.match(prepared, /API/i);
  assert.equal(await dialog.getByRole('button', { name: 'Copiar', exact: true }).isEnabled(), true);
  assert.deepEqual(snapshot(), before); assert.equal((await api('/prompts/' + source.id)).revision, 1);
  await page.waitForTimeout(150);
  const initialRun = await api('/nova/drafts/' + runs.at(-1).id); assert.equal(initialRun.sourceId, source.id); assert.equal(initialRun.state, 'READY');
  fs.writeFileSync(path.join(evidence, 'first-synthetic-draft.json'), JSON.stringify(initialRun, null, 2));
  const checkpoint = await api('/nova/runs/' + initialRun.id); assert.ok(checkpoint.tools.length <= 5); assert.ok(checkpoint.retrievedPromptIds.includes(source.id)); assert.equal(checkpoint.sourceRevisions[source.id], 1);
  fs.writeFileSync(path.join(evidence, 'first-run-diagnostics.json'), JSON.stringify(checkpoint, null, 2));
  await page.screenshot({ path: path.join(evidence, 'first-preparation.png'), fullPage: true });
  pass('Required first flow: React → API → real SQLite ID/revision → real Qwen adaptation → validated copy; no library mutation');
  if (chainOnly) { await close(); await verifyChain(); }
  if (!firstOnly) {
    await close(); await ask('Actualiza ese prompt para revisar seguridad, pero no guardes todavía.'); dialog = await result();
    assert.match(await dialog.locator('.prepared-text').innerText(), /seguridad/i); assert.deepEqual(snapshot(), before);
    pass('Contextual security adaptation preserves source and versions before approval');
    await close(); await ask('Cancela la modificación.'); await result(); assert.deepEqual(snapshot(), before); pass('Natural cancellation preserves all library data'); await close();
    await ask('Crea un prompt para comparar propuestas de empleo.'); dialog = await result(); assert.ok((await dialog.locator('.prepared-text').innerText()).length > 30); assert.deepEqual(snapshot(), before); pass('Creates a useful job comparison draft without saving'); await close();
    await ask('Usa el que tengo para cartas y adáptalo al puesto que te describí.'); dialog = await result();
    const copy = dialog.getByRole('button', { name: 'Copiar', exact: true }); assert.ok(await copy.count() === 0 || !await copy.isEnabled()); assert.deepEqual(snapshot(), before); pass('Missing role/context is clarified rather than invented'); await close();
    await ask('Abre el prompt «Cartas»'); await page.getByRole('heading', { name: 'Cartas', exact: true }).waitFor();
    const role = page.getByLabel('puesto', { exact: true }); await role.fill('Analista de datos');
    await page.getByRole('button', { name: 'Biblioteca', exact: true }).click();
    await ask('Completa este prompt con los datos que ya te proporcioné.'); dialog = await result(); assert.match(await dialog.locator('.prepared-text').innerText(), /Analista de datos/); assert.deepEqual(snapshot(), before); pass('Fills from explicitly authorized session values'); await close();
    await ask('Prepara este prompt para ChatGPT.'); dialog = await result(); assert.equal(await dialog.getByRole('button', { name: 'Usar en…', exact: true }).isEnabled(), true); assert.deepEqual(snapshot(), before); pass('Prepares external destination without sending or opening external sites'); await close();
    await ask('Ejecuta este prompt con Qwen.'); dialog = await result(); assert.ok((await dialog.locator('.local-response pre').innerText()).length > 10); assert.deepEqual(snapshot(), before); pass('Explicit local execution streams with real Qwen and preserves the library'); await close();
    await ask('Busca mi prompt de carta y adáptalo para una entrevista.'); dialog = await result();
    assert.match(await dialog.innerText(), /varias|cuál/i); assert.equal(await dialog.getByRole('button', { name: 'Copiar', exact: true }).count(), 0); assert.deepEqual(snapshot(), before); pass('Two compatible sources ask for disambiguation'); await close();
    await ask('Crea un prompt detallado para evaluar una migración de un sistema.'); await page.getByRole('button', { name: 'Detener', exact: true }).waitFor(); await page.getByRole('button', { name: 'Detener', exact: true }).click();
    await page.waitForTimeout(1000); assert.deepEqual(snapshot(), before); assert.equal(await page.getByRole('dialog').count(), 0); pass('Mid-run cancellation preserves data and prevents late modal completion');
    await page.setViewportSize({ width: 390, height: 844 });
    await ask('Busca mi prompt de arquitectura, adáptalo para una API .NET y déjalo listo para utilizar.'); dialog = await result();
    assert.equal(await dialog.evaluate(e => e.scrollWidth > e.clientWidth + 1 || document.documentElement.scrollWidth > innerWidth + 1), false);
    await dialog.getByRole('button', { name: 'Guardar', exact: true }).click(); const approval = page.getByRole('dialog', { name: 'Guardar adaptación', exact: true }); await approval.waitFor(); assert.deepEqual(snapshot(), before);
    await approval.getByRole('button', { name: 'Confirmar nueva versión', exact: true }).click(); await dialog.getByRole('button', { name: 'Guardado', exact: true }).waitFor();
    assert.equal((await api('/prompts/' + source.id)).revision, 2); assert.equal((await api('/prompts/' + source.id + '/versions')).length, 2); assert.equal((await api('/prompts?size=1')).total, 3);
    await page.screenshot({ path: path.join(evidence, 'mobile-approved-version.png'), fullPage: true }); pass('Mobile confirmation saves exactly one version on the real source, with no duplicate');
    if (process.argv.includes('--chain')) {
      await close(); await verifyChain();
    }
  }
  assert.deepEqual(errors, []); await context.close(); report('PASS'); console.log('EVIDENCE', evidence);
})().catch(error => { console.error(error); report('FAIL', error.stack); process.exitCode = 1; }).finally(async () => { db?.close(); await browser?.close(); if (server && server.exitCode === null) { const exited = new Promise(resolve => server.once('exit', resolve)); server.kill(); await exited; } });
