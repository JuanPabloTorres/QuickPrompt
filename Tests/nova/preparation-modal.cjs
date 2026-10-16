const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const { install } = require('../../scripts/Install-PracticalLibrary.cjs');
const root = path.resolve(__dirname, '../..'), origin = 'http://127.0.0.1:5214';
const temporary = path.join(root, '.playwright', 'modal-qa-' + Date.now()), evidence = path.join(root, 'docs/evidence/preparation-modal');
fs.mkdirSync(temporary, { recursive: true }); fs.mkdirSync(evidence, { recursive: true });
let server, browser, output = '';
const checks = [], errors = [];
async function focused(page, selector) { await page.waitForFunction(value => document.activeElement?.matches(value), selector, { timeout: 5000 }); }
(async () => {
  server = spawn('dotnet', [path.join(root, 'backend/QuickPrompt.Api/bin/Debug/net8.0/QuickPrompt.Api.dll'), '--QuickPrompt:Port=5214', `--QuickPrompt:DataPath=${path.join(temporary, 'library.sqlite')}`, `--QuickPrompt:WebRoot=${path.join(root, 'apps/web/dist')}`], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', chunk => { output += chunk; }); server.stderr.on('data', chunk => { output += chunk; });
  for (let index = 0; index < 150; index++) {
    if (server.exitCode !== null) throw Error('Owned QA server exited before readiness.');
    if (output.includes('Now listening on: ' + origin)) break;
    if (index === 149) throw Error('Owned QA server was not ready.');
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  assert.equal((await install(origin)).after, 60);
  browser = await chromium.launch({ channel: 'msedge', headless: !process.argv.includes('--headed') });
  for (const width of [1280, 390]) {
    const context = await browser.newContext({ viewport: { width, height: 900 }, reducedMotion: 'reduce' });
    const page = await context.newPage(); page.setDefaultTimeout(10000);
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
    let preparations = 0;
    page.on('request', request => { if (request.url() === origin + '/api/v1/nova/prepare') preparations++; });
    await page.goto(origin);
    const home = page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true });
    await home.fill('Encuentra el prompt Responder comentarios de clientes');
    await page.getByRole('button', { name: 'Preparar prompt', exact: true }).click();
    const result = page.getByRole('dialog', { name: 'Responder comentarios de clientes', exact: true });
    await result.waitFor({ timeout: 180000 }); await result.getByLabel('comentarios', { exact: true }).waitFor();
    console.log('OPEN modal', width);
    assert.equal(await page.locator('main .prepared-result').count(), 0);
    assert.equal(await page.evaluate(() => document.body.style.overflow), 'hidden');
    const layout = await result.evaluate(element => ({ pageOverflow: document.documentElement.scrollWidth > innerWidth + 1, modalOverflow: element.scrollWidth > element.clientWidth + 1, width: element.getBoundingClientRect().width }));
    assert.equal(layout.pageOverflow, false); assert.equal(layout.modalOverflow, false);
    await result.getByLabel('comentarios', { exact: true }).fill('¿Cuándo llega mi pedido?');
    await result.getByLabel('contexto', { exact: true }).fill('Podemos revisar el estado del pedido.');
    for (let index = 0; index < 12; index++) {
      await page.keyboard.press('Tab');
      assert.equal(await page.evaluate(() => !!document.activeElement?.closest('.preparation-dialog')), true);
    }
    await result.getByRole('button', { name: 'Cerrar diálogo', exact: true }).click();
    await result.waitFor({ state: 'hidden' }); await focused(page, '#search');
    const reopen = page.getByRole('button', { name: 'Ver resultado de NOVA', exact: true });
    await reopen.click(); await result.waitFor();
    assert.equal(await result.getByLabel('comentarios', { exact: true }).inputValue(), '¿Cuándo llega mi pedido?');
    assert.equal(await result.getByLabel('contexto', { exact: true }).inputValue(), 'Podemos revisar el estado del pedido.');
    console.log('PASS retained fields', width);
    await page.keyboard.press('Escape'); await result.waitFor({ state: 'hidden' }); await focused(page, '.prepared-launcher button');
    await reopen.click(); await result.waitFor();
    await result.getByRole('button', { name: 'Ajustar', exact: true }).click();
    await result.getByLabel('Ajuste que necesitas').fill('Utiliza un tono cordial.');
    await result.getByRole('button', { name: 'Cerrar resultado', exact: true }).click(); await result.waitFor({ state: 'hidden' });
    await reopen.click(); await result.waitFor();
    assert.equal(await result.getByLabel('Ajuste que necesitas').inputValue(), 'Utiliza un tono cordial.');
    await result.getByRole('button', { name: 'Ajustar', exact: true }).click();
    await result.screenshot({ path: path.join(evidence, `modal-${width}.png`) });
    await result.getByRole('button', { name: 'Completar prompt', exact: true }).click();
    await page.waitForFunction(() => !document.querySelector('.prepared-missing'));
    assert.equal(await result.getByRole('button', { name: 'Copiar', exact: true }).isEnabled(), true);
    assert.ok((await result.locator('.prepared-text').textContent()).includes('¿Cuándo llega mi pedido?'));
    await result.getByRole('button', { name: 'Usar en…', exact: true }).click();
    const use = page.getByRole('dialog', { name: 'Usa tu prompt preparado', exact: true });
    await use.waitFor(); await page.keyboard.press('Escape'); await use.waitFor({ state: 'hidden' }); await result.waitFor();
    await focused(page, '.prepared-actions button:nth-child(3)');
    console.log('PASS nested use dialog', width);
    assert.equal(await page.evaluate(() => document.body.style.overflow), 'hidden');
    await result.getByRole('button', { name: 'Cerrar resultado', exact: true }).click(); await result.waitFor({ state: 'hidden' });
    await page.waitForFunction(() => document.body.style.overflow === '');
    await reopen.click(); await result.waitFor();
    await page.locator('.dialog-overlay').last().click({ position: { x: 3, y: 3 }, timeout: 5000 });
    await result.waitFor({ state: 'hidden' });
    await page.waitForFunction(() => document.body.style.overflow === '');
    assert.equal(await page.locator('main .prepared-result').count(), 0);
    assert.equal(preparations, 1, 'Reopening must not repeat the model request.');
    const library = await (await page.request.get(origin + '/api/v1/prompts?size=1')).json(); assert.equal(library.total, 60, 'Closing must not save a prompt.');
    checks.push({ width, status: 'PASS', layout, preservedFields: true, preservedAdjustment: true, closeXEscapeButtonBackdrop: true, focusTrapAndReturn: true, nestedUseDialog: true, preparations });
    console.log('PASS modal', width); await context.close();
  }
  assert.deepEqual(errors, []);
  fs.writeFileSync(path.join(evidence, 'acceptance.json'), JSON.stringify({ status: 'PASS', checks, errors, data: 'isolated synthetic SQLite', temporary }, null, 2));
})().catch(error => { console.error(error); fs.writeFileSync(path.join(evidence, 'acceptance.json'), JSON.stringify({ status: 'FAIL', checks, errors, error: error.stack }, null, 2)); process.exitCode = 1; }).finally(async () => {
  await browser?.close();
  if (server && server.exitCode === null) { const exited = new Promise(resolve => server.once('exit', resolve)); server.kill(); await exited; }
});
