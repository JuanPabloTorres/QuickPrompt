// Real React/API/Qwen regression. Uses only synthetic prompts and an owned QA browser/server.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), stamp = Date.now(), origin = 'http://127.0.0.1:5240';
const out = path.join(root, 'docs/evidence/quick-actions', String(stamp)), tmp = path.join(root, '.playwright/quick-actions-' + stamp);
fs.mkdirSync(out, { recursive: true }); fs.mkdirSync(tmp, { recursive: true });
fs.cpSync(path.join(root, 'apps/web/dist'), path.join(tmp, 'web'), { recursive: true });
const dll = path.resolve(root, process.env.NOVA_TEST_BUILD || '.playwright/same-tab/Debug/net8.0/QuickPrompt.Api.dll');
const headers = { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' };
const results = []; let server, browser, context, page, proxy, log = '', currentRun;
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(check, timeout = 450000) { const end = Date.now() + timeout; while (Date.now() < end) { const value = await check(); if (value) return value; await sleep(300); } throw Error('QA_TIMEOUT'); }
async function api(route, body, method = body ? 'POST' : 'GET') { const response = await fetch(origin + '/api/v1' + route, { method, headers, body: body ? JSON.stringify(body) : undefined }); assert.ok(response.ok, await response.clone().text()); return response.status === 204 ? null : response.json(); }
async function quick(label) { const region = page.getByRole('region', { name: 'Acciones rápidas de NOVA' }); await region.getByRole('button', { name: label, exact: true }).click(); return page.getByRole('dialog', { name: label, exact: true }); }
async function launch(dialog, submit) {
  const [response] = await Promise.all([page.waitForResponse(r => r.url() === origin + '/api/v1/nova/live' && r.request().method() === 'POST'), dialog.getByRole('button', { name: submit, exact: true }).click()]);
  assert.equal(response.status(), 200, await response.text()); currentRun = (await response.json()).runId;
  await dialog.waitFor({ state: 'hidden' }); return currentRun;
}
async function ready() { return until(async () => { const view = await api('/nova/live/' + currentRun); if (['FAILED', 'BLOCKED', 'CANCELLED'].includes(view.run.status)) throw Error(JSON.stringify({ status: view.run.status, error: view.run.errorCode, question: view.question })); return ['COMPLETED', 'AWAITING_APPROVAL'].includes(view.run.status) ? view : null; }); }
async function cancel() { const button = page.getByRole('button', { name: 'Actividad de NOVA', exact: true }); if (await button.getAttribute('aria-expanded') !== 'true') await button.click(); await page.getByRole('button', { name: 'Detener NOVA', exact: true }).click(); await until(async () => (await api('/nova/live/' + currentRun)).run.status === 'CANCELLED', 10000); }
async function home() { await page.goto(origin); await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).waitFor(); }
async function record(name, view) { await page.screenshot({ path: path.join(out, name + '.png'), fullPage: true }); results.push({ name, status: 'PASS', run: view?.run, request: view?.request, observed: view?.observation }); fs.writeFileSync(path.join(out, 'progress.json'), JSON.stringify(results, null, 2)); console.log('PASS', name); }
(async () => {
  try {
    let modelSequence = 0;
    proxy = require('node:http').createServer(async (req, res) => {
      const sequence = ++modelSequence, abort = new AbortController(); let request;
      res.on('close', () => { if (!res.writableEnded) abort.abort(); });
      try {
        let raw = ''; for await (const chunk of req) raw += chunk;
        request = raw ? JSON.parse(raw) : null;
        const response = await fetch('http://127.0.0.1:11434' + req.url, { method: req.method, headers: { 'Content-Type': 'application/json' }, body: raw || undefined, signal: abort.signal });
        res.writeHead(response.status, { 'Content-Type': response.headers.get('content-type') || 'application/json' });
        let body = ''; const decoder = new TextDecoder();
        for await (const chunk of response.body) { body += decoder.decode(chunk, { stream: true }); res.write(Buffer.from(chunk)); } body += decoder.decode(); res.end();
        const sanitize = value => { if (value.message?.thinking) { delete value.message.thinking; value.thinkingOmitted = true; } return value; };
        const captured = request?.stream ? body.trim().split(/\r?\n/).filter(Boolean).map(line => sanitize(JSON.parse(line))) : sanitize(JSON.parse(body));
        fs.writeFileSync(path.join(out, 'model-' + sequence + '.json'), JSON.stringify({ request, response: captured }, null, 2));
      } catch (error) { fs.writeFileSync(path.join(out, 'model-' + sequence + '.json'), JSON.stringify({ request, error: String(error) }, null, 2)); if (!res.headersSent) res.writeHead(502); if (!res.destroyed) res.end('{}'); }
    });
    await new Promise(resolve => proxy.listen(5241, '127.0.0.1', resolve));
    server = spawn('dotnet', [dll, '--QuickPrompt:Port=5240', '--QuickPrompt:Ollama=http://127.0.0.1:5241', '--QuickPrompt:DataPath=' + path.join(tmp, 'synthetic.sqlite'), '--QuickPrompt:WebRoot=' + path.join(tmp, 'web')], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
    server.stdout.on('data', data => log += data); server.stderr.on('data', data => log += data);
    await until(() => { if (server.exitCode !== null) throw Error('Owned API failed: ' + log); return log.includes('Now listening on: ' + origin); }, 20000);
    const project = await api('/prompts', { title: 'Evaluar proyectos', description: 'Revisión de un proyecto.', templateText: 'Evalúa el proyecto <proyecto>. No inventes datos.', categoryId: 0 });
    const disposable = await api('/prompts', { title: 'Eliminar ejemplo QA', description: 'Solo datos sintéticos.', templateText: 'Ejemplo para eliminar.', categoryId: 0 });
    browser = await chromium.launch({ channel: 'msedge', headless: true });
    context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce', recordVideo: { dir: path.join(out, 'video') } }); page = await context.newPage();
    let extraPages = 0; context.on('page', () => extraPages++);
    await home();
    // Free text and search remain independent; selecting/cancelling a quick action launches nothing.
    await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).fill('Texto libre conservado');
    await page.getByRole('textbox', { name: 'Buscar en la biblioteca', exact: true }).fill('proyectos');
    let dialog = await quick('Crear prompt'); await dialog.getByLabel('¿Qué prompt quieres crear?').fill('Sin enviar');
    await page.keyboard.press('Escape'); await dialog.waitFor({ state: 'hidden' });
    assert.equal(await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).inputValue(), 'Texto libre conservado');
    assert.equal(await page.getByRole('textbox', { name: 'Buscar en la biblioteca', exact: true }).inputValue(), 'proyectos');
    assert.equal((await api('/prompts?size=12')).total, 2);
    await record('cancel-preserves-independent-inputs');
    for (const theme of ['dark', 'light']) {
      if (theme === 'light') await page.getByRole('button', { name: 'Activar tema claro' }).click();
      await page.setViewportSize({ width: 390, height: 844 }); dialog = await quick('Actualizar prompt');
      const bounds = await dialog.boundingBox(); assert.ok(bounds.x >= 0 && bounds.x + bounds.width <= 391);
      assert.ok(await dialog.getByLabel('¿Qué prompt?').isVisible());
      await record('mobile-' + theme); await page.keyboard.press('Escape');
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    }
    await page.setViewportSize({ width: 1440, height: 1000 }); await home();
    // Explicit selection and the existing final confirmation guard actual deletion.
    dialog = await quick('Eliminar prompt'); await dialog.getByLabel('¿Qué prompt?').fill('Eliminar ejemplo QA');
    await dialog.getByRole('button', { name: /Eliminar ejemplo QA Solo datos/ }).click();
    await dialog.getByRole('button', { name: 'Revisar eliminación' }).click();
    let confirmation = page.getByRole('dialog', { name: '¿Eliminar este prompt?', exact: true }); await confirmation.waitFor();
    assert.ok((await confirmation.textContent()).includes(disposable.title));
    assert.equal((await api('/prompts/' + disposable.id)).id, disposable.id);
    await confirmation.getByRole('button', { name: 'Cancelar', exact: true }).click();
    assert.equal((await api('/prompts/' + disposable.id)).id, disposable.id);
    await page.getByTestId('prompt-focus').waitFor();
    dialog = await quick('Eliminar prompt'); await dialog.getByRole('button', { name: 'Revisar eliminación' }).click();
    confirmation = page.getByRole('dialog', { name: '¿Eliminar este prompt?', exact: true });
    await confirmation.getByRole('button', { name: 'Eliminar definitivamente' }).click();
    await confirmation.waitFor({ state: 'hidden' });
    assert.equal((await api('/prompts?size=12')).total, 1); await record('delete-selection-review-confirm');
    // The following cases use the real installed Qwen through the shared semantic engine.
    await home(); dialog = await quick('Actualizar prompt');
    await dialog.getByLabel('¿Qué prompt?').fill(project.title); await dialog.getByRole('button', { name: /Evaluar proyectos Revisión/ }).click(); await dialog.getByLabel('¿Qué quieres cambiar?').fill('Añade una sección de riesgos y recomendaciones.');
    await launch(dialog, 'Preparar cambios'); let view = await ready(); assert.equal(view.run.status, 'AWAITING_APPROVAL'); assert.equal(view.proposal.promptId, project.id); assert.notEqual(view.proposal.draft.templateText, project.templateText);
    assert.equal((await api('/prompts/' + project.id)).revision, project.revision); await record('real-update-review-no-save', view); await cancel();
    await home(); dialog = await quick('Crear prompt'); await dialog.getByLabel('¿Qué prompt quieres crear?').fill('Analizar una aplicación React y proponer mejoras de arquitectura.');
    await launch(dialog, 'Crear con NOVA'); view = await ready(); assert.equal(view.run.status, 'AWAITING_APPROVAL');
    assert.ok(view.proposal.draft.title && view.proposal.draft.templateText); assert.equal((await api('/prompts?size=12')).total, 1);
    await record('real-create-draft-no-save', view);
    // A trusted editor save click must request review, not trigger human-takeover pause.
    await page.getByRole('button', { name: 'Guardar prompt', exact: true }).click();
    const approval = page.getByRole('dialog', { name: 'Guardar nuevo prompt', exact: true }); await approval.waitFor();
    assert.equal((await api('/nova/live/' + currentRun)).run.status, 'AWAITING_APPROVAL');
    assert.equal((await api('/prompts?size=12')).total, 1);
    await approval.getByRole('button', { name: 'Seguir revisando', exact: true }).click();
    await page.getByRole('button', { name: 'Guardar prompt', exact: true }).focus(); await page.keyboard.press('Enter');
    await approval.waitFor(); await approval.getByRole('button', { name: 'Confirmar guardado de esta versión', exact: true }).click();
    view = await ready(); assert.equal(view.run.status, 'COMPLETED'); assert.equal(view.run.effectCommitted, true);
    assert.equal((await api('/prompts?size=12')).total, 2);
    await record('real-create-editor-save-review-confirm', view);
    await home(); dialog = await quick('Buscar y abrir'); await dialog.getByLabel('¿Qué prompt?').fill(project.title);
    await dialog.getByRole('button', { name: /Evaluar proyectos Revisión/ }).click();
    await launch(dialog, 'Abrir con NOVA'); view = await ready(); assert.equal(view.run.status, 'COMPLETED'); assert.equal(view.observation.promptId, project.id); assert.equal(view.observation.templateText, project.templateText);
    await record('real-open-exact-selection', view);
    await page.getByText('Más acciones', { exact: true }).click(); dialog = await quick('Completar variables');
    await dialog.getByLabel('proyecto', { exact: true }).fill('una aplicación React');
    await launch(dialog, 'Completar con NOVA'); view = await ready(); assert.equal(view.run.status, 'COMPLETED'); assert.equal(view.observation.values.proyecto, 'una aplicación React'); assert.equal(view.observation.templateText, project.templateText);
    await record('real-fill-template-fields', view);

    assert.equal(extraPages, 0); assert.equal(context.pages().length, 1);
    const report = { status: 'PASS', qa: { headless: true, syntheticDataOnly: true, realQwen: true, onePage: true }, additionalPages: extraPages, results };
    fs.writeFileSync(path.join(out, 'report.json'), JSON.stringify(report, null, 2)); console.log('PASS', out);
  } catch (error) {
    fs.writeFileSync(path.join(out, 'failure.json'), JSON.stringify({ message: String(error), stack: error.stack, results }, null, 2));
    if (page) await page.screenshot({ path: path.join(out, 'failure.png'), fullPage: true }).catch(() => {});
    console.error(String(error)); process.exitCode = 1;
  } finally {
    if (currentRun && server?.exitCode === null) await api('/nova/live/' + currentRun + '/cancel', {}).catch(() => {});
    await context?.close(); await browser?.close(); server?.kill(); if (proxy) await new Promise(resolve => proxy.close(resolve));
  }
})();
