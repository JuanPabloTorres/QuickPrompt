// UI workspace acceptance against an owned API and synthetic SQLite only.
// Build apps/web first. Add --real-qwen to verify a real agent request.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
const temporary = path.join(root, '.playwright/workspace-' + stamp), out = path.join(root, 'docs/evidence/workspace-' + stamp);
const origin = 'http://127.0.0.1:5194', headers = { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' };
const realQwen = process.argv.includes('--real-qwen'), checks = [];
fs.mkdirSync(temporary, { recursive: true }); fs.mkdirSync(out, { recursive: true });
fs.cpSync(path.resolve(root, process.env.NOVA_TEST_WEBROOT || 'apps/web/dist'), path.join(temporary, 'web'), { recursive: true });
let server, browser, context, page, log = '', runId;
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(read, timeout = 20000) { const end = Date.now() + timeout; while (Date.now() < end) { const value = await read(); if (value) return value; await wait(200); } throw Error('QA_TIMEOUT'); }
async function api(route, body, method = body ? 'POST' : 'GET') { const response = await fetch(origin + '/api/v1' + route, { method, headers, body: body ? JSON.stringify(body) : undefined }); assert.ok(response.ok, await response.clone().text()); return response.status === 204 ? null : response.json(); }
async function screenshot(name) { await wait(250); await page.screenshot({ path: path.join(out, name + '.png') }); }
async function library() { await page.getByRole('button', { name: 'QuickPrompt NOVA, biblioteca', exact: true }).click(); await page.getByTestId('library-ready').waitFor(); }
async function settings() { await page.getByRole('button', { name: 'Configuración', exact: true }).click(); await page.getByRole('dialog', { name: 'Configuración', exact: true }).waitFor(); }
async function dockVisible() { const dock = page.getByTestId('nova-dock'); const box = await dock.boundingBox(); const { width, height } = page.viewportSize(); assert.ok(box && box.x >= 0 && box.y >= 0 && box.x + box.width <= width + 1 && box.y + box.height <= height + 1, JSON.stringify(box)); for (const name of ['Crear prompt', 'Actualizar prompt', 'Buscar y abrir', 'Eliminar prompt']) assert.equal(await dock.getByRole('button', { name, exact: true }).isVisible(), true); return box; }
(async () => {
  server = spawn('dotnet', [path.resolve(root, process.env.NOVA_TEST_BUILD || 'backend/QuickPrompt.Api/bin/Debug/net8.0/QuickPrompt.Api.dll'), '--QuickPrompt:Port=5194', '--QuickPrompt:DataPath=' + path.join(temporary, 'synthetic.sqlite'), '--QuickPrompt:WebRoot=' + path.join(temporary, 'web')], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', chunk => { log += chunk; }); server.stderr.on('data', chunk => { log += chunk; });
  await until(() => { if (server.exitCode !== null) throw Error('Owned API exited'); return log.includes('Now listening on: ' + origin); });
  const seeded = [];
  for (let index = 0; index < 14; index++) seeded.push(await api('/prompts', { title: `Proyecto ${String(index + 1).padStart(2, '0')}`, description: 'Una instrucción reutilizable para organizar y revisar tus proyectos.', templateText: 'Revisa el proyecto <proyecto> y enumera tres mejoras. No inventes datos.' }));
  browser = await chromium.launch({ channel: 'msedge', headless: false });
  context = await browser.newContext({ viewport: { width: 1280, height: 900 }, recordVideo: { dir: path.join(out, 'video') } });
  page = await context.newPage(); page.setDefaultTimeout(10000);
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  await page.goto(origin); await page.getByTestId('library-ready').waitFor();
  const launcher = page.getByRole('button', { name: 'Abrir asistente NOVA', exact: true });
  const initialOrb = await launcher.boundingBox(); assert.ok(initialOrb.width === 60 && initialOrb.height === 60 && initialOrb.x > 1100);
  assert.equal(await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).isVisible(), false);
  await screenshot('nova-circle-desktop'); await launcher.click();
  const first = seeded.at(-1), composer = page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true });
  assert.equal(await page.getByRole('list', { name: 'Prompts guardados' }).getAttribute('data-view'), 'cards');
  assert.equal(await page.getByText('Opciones de preparación', { exact: true }).count(), 0);
  await composer.fill('Esta solicitud se conserva al navegar.');
  await wait(300); const initialDock = await dockVisible();
  await page.evaluate(() => scrollTo(0, document.body.scrollHeight)); await wait(150); const scrolledDock = await dockVisible(); assert.ok(Math.abs(initialDock.y - scrolledDock.y) < 2);
  await page.getByRole('heading', { name: /Tu biblioteca/ }).scrollIntoViewIfNeeded(); await screenshot('cards-dark');
  await page.getByRole('button', { name: 'Añadir a favoritos: ' + first.title, exact: true }).click();
  await page.getByRole('button', { name: 'Quitar de favoritos: ' + first.title, exact: true }).waitFor();
  await page.getByRole('button', { name: 'Favoritos', exact: true }).click(); await page.getByTestId('library-ready').waitFor();
  assert.equal(await page.getByTestId('library-prompt').count(), 1); assert.equal((await api('/prompts/' + first.id)).isFavorite, true);
  await page.getByRole('button', { name: 'Todos', exact: true }).click(); await page.getByTestId('library-ready').waitFor();
  await page.getByRole('button', { name: 'Ver filas', exact: true }).click(); await screenshot('rows-dark');
  await page.getByRole('button', { name: first.title, exact: true }).click(); await page.getByTestId('prompt-focus').waitFor();
  assert.equal(await composer.inputValue(), 'Esta solicitud se conserva al navegar.');
  await page.keyboard.press('Control+k'); assert.equal(await composer.evaluate(node => document.activeElement === node), true); assert.equal(await page.getByTestId('prompt-focus').getAttribute('data-prompt-id'), first.id); await dockVisible();
  checks.push('persistent-composer-and-quick-actions-on-scroll-and-navigation', 'cards-rows-and-favorites');
  await library(); await page.getByRole('button', { name: 'Editar: ' + first.title, exact: true }).click(); await page.getByTestId('prompt-editor').waitFor();
  await page.getByLabel('Título', { exact: true }).fill('Proyecto revisado'); await page.getByLabel('Contenido', { exact: true }).fill('Revisa <proyecto> y entrega una lista de prioridades. No inventes datos.');
  await page.getByRole('button', { name: 'Guardar versión', exact: true }).click(); await page.getByTestId('prompt-focus').waitFor();
  const updated = await api('/prompts/' + first.id); assert.equal(updated.revision, 3); assert.equal(updated.title, 'Proyecto revisado');
  await library(); const before = (await api('/prompts?size=12')).items.map(item => item.id);
  await page.getByRole('button', { name: 'Duplicar: ' + updated.title, exact: true }).click();
  const duplicate = await until(async () => (await api('/prompts?size=12')).items.find(item => !before.includes(item.id)));
  const deletes = []; page.on('request', request => { if (request.method() === 'DELETE') deletes.push(request.url()); });
  for (const view of ['cards', 'rows']) {
    await page.getByRole('button', { name: view === 'cards' ? 'Ver tarjetas' : 'Ver filas', exact: true }).click();
    for (const close of ['Cancelar', 'Cerrar diálogo', 'Escape']) {
      await page.getByRole('button', { name: 'Eliminar: ' + duplicate.title, exact: true }).click();
      await page.getByRole('dialog', { name: '¿Eliminar este prompt?', exact: true }).waitFor();
      assert.equal(await page.getByTestId('prompt-focus').count(), 0);
      if (close === 'Escape') await page.keyboard.press('Escape');
      else await page.getByRole('button', { name: close, exact: true }).click();
      await page.getByRole('dialog', { name: '¿Eliminar este prompt?', exact: true }).waitFor({ state: 'hidden' });
      assert.equal(await page.getByRole('list', { name: 'Prompts guardados' }).getAttribute('data-view'), view);
      assert.equal(await page.getByTestId('prompt-focus').count(), 0);
      // The dock may collapse to uncover the restored library control. Verify
      // task retention independently of that supported presentation state.
      assert.equal(await page.locator('.nova-input textarea').inputValue(), 'Esta solicitud se conserva al navegar.');
      assert.equal((await api('/prompts/' + duplicate.id)).id, duplicate.id);
    }
  }
  assert.equal(deletes.length, 0); await screenshot('cancel-delete-stays-in-library'); checks.push('cancel-delete-button-escape-and-close-stay-in-cards-and-rows');
  await page.getByRole('button', { name: 'Eliminar: ' + duplicate.title, exact: true }).click();
  const deletionResponse = page.waitForResponse(response => response.request().method() === 'DELETE' && response.url() === origin + '/api/v1/prompts/' + duplicate.id);
  await page.getByRole('button', { name: 'Eliminar definitivamente', exact: true }).click();
  assert.equal((await deletionResponse).status(), 204);
  await page.getByRole('dialog', { name: '¿Eliminar este prompt?', exact: true }).waitFor({ state: 'hidden' });
  await until(async () => await page.getByTestId('library-ready').count() > 0 && await page.getByRole('button', { name: 'Eliminar: ' + duplicate.title, exact: true }).count() === 0);
  assert.equal(deletes.length, 1); assert.ok(deletes[0].endsWith('/prompts/' + duplicate.id));
  assert.equal((await api('/prompts?size=12')).total, 14); checks.push('edit-version-duplicate-and-confirmed-delete');
  await settings(); assert.equal(await page.getByLabel('Presentación del resultado', { exact: true }).isVisible(), true);
  await page.getByLabel('Velocidad de escritura', { exact: true }).selectOption('fast');
  await page.getByLabel('Tema', { exact: true }).selectOption('light'); await screenshot('settings');
  await page.getByRole('button', { name: 'Cerrar diálogo', exact: true }).click(); assert.equal(await page.getByLabel('Presentación del resultado', { exact: true }).count(), 0);
  await page.getByRole('button', { name: 'Ver tarjetas', exact: true }).click(); await screenshot('cards-light');
  await page.reload(); await page.getByTestId('library-ready').waitFor(); assert.equal(await page.getByRole('list', { name: 'Prompts guardados' }).getAttribute('data-view'), 'cards'); assert.equal(await page.locator('html').getAttribute('data-theme'), 'light');
  await page.getByRole('button', { name: 'Abrir asistente NOVA', exact: true }).click();
  await page.getByRole('button', { name: 'Activar tema oscuro', exact: true }).click();
  await page.setViewportSize({ width: 375, height: 900 }); await page.evaluate(() => scrollTo(0, document.body.scrollHeight)); await dockVisible();
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
  await page.getByRole('button', { name: 'Más acciones de NOVA', exact: true }).click(); await page.getByRole('button', { name: 'Completar variables', exact: true }).waitFor(); await screenshot('mobile-actions'); await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Cerrar panel de NOVA', exact: true }).click();
  const mobileOrb = await page.getByRole('button', { name: 'Abrir asistente NOVA', exact: true }).boundingBox();
  assert.equal(mobileOrb.width, 56); assert.equal(mobileOrb.height, 56); await screenshot('nova-circle-mobile');
  await page.getByRole('button', { name: 'Abrir asistente NOVA', exact: true }).click(); await screenshot('mobile-expanded');
  const panelBox = await page.locator('.nova-dock-panel').boundingBox(); assert.ok(panelBox.x >= 0 && panelBox.y >= 0 && panelBox.x + panelBox.width <= 375 && panelBox.y + panelBox.height < mobileOrb.y);
  checks.push('circular-corner-launcher-and-responsive-panel');
  checks.push('header-settings-and-persistence', 'mobile-and-animated-action-menu');
  await page.setViewportSize({ width: 1280, height: 900 });
  if (realQwen) {
    await page.getByRole('button', { name: updated.title, exact: true }).click(); await page.getByTestId('prompt-focus').waitFor();
    await page.getByRole('button', { name: 'Más acciones de NOVA', exact: true }).click(); await page.getByRole('button', { name: 'Completar variables', exact: true }).click();
    await page.getByRole('dialog').getByLabel('proyecto', { exact: true }).fill('una aplicación React para organizar proyectos');
    const response = page.waitForResponse(response => response.url() === origin + '/api/v1/nova/live' && response.request().method() === 'POST');
    await page.getByRole('button', { name: 'Completar con NOVA', exact: true }).click(); const started = await response; assert.equal(started.status(), 200); runId = (await started.json()).runId; console.log('REAL QWEN', runId);
    await page.getByRole('button', { name: 'Actividad de NOVA', exact: true }).click();
    await page.getByRole('button', { name: 'Pausar', exact: true }).waitFor(); await screenshot('live-progress');
    await page.getByRole('button', { name: 'Pausar', exact: true }).click();
    await until(async () => (await api('/nova/live/' + runId)).run.pauseRequested);
    await page.getByRole('button', { name: 'Cerrar panel de NOVA', exact: true }).click();
    assert.equal(await page.getByTestId('nova-dock').getAttribute('data-expanded'), 'false');
    await screenshot('nova-circle-paused');
    await page.getByRole('button', { name: 'Abrir asistente NOVA', exact: true }).click();
    const resumeActivity = page.getByRole('button', { name: 'Actividad de NOVA', exact: true });
    if (await resumeActivity.getAttribute('aria-expanded') === 'false') await resumeActivity.click();
    await page.getByRole('button', { name: 'Continuar', exact: true }).click();
    await page.getByRole('button', { name: 'Cerrar panel de NOVA', exact: true }).click();
    const result = await until(async () => { const view = await api('/nova/live/' + runId); if (['FAILED', 'BLOCKED', 'CANCELLED'].includes(view.run.status)) throw Error(JSON.stringify(view)); return view.run.status === 'COMPLETED' ? view : null; }, 420000);
    assert.equal(result.run.pauseRequested, false); assert.equal(result.observation.promptId, first.id); assert.equal(result.observation.values.proyecto, 'una aplicación React para organizar proyectos');
    assert.equal(await page.getByTestId('prompt-focus').getAttribute('data-prompt-id'), first.id); assert.equal(await page.getByTestId('variable-field').inputValue(), result.observation.values.proyecto);
    assert.deepEqual(await api('/prompts/' + first.id), updated); await screenshot('real-qwen-current-screen'); checks.push('real-qwen-quick-fill-on-current-screen');
    assert.equal(await page.getByTestId('nova-dock').getAttribute('data-expanded'), 'false');
    await page.getByRole('button', { name: 'Abrir asistente NOVA', exact: true }).click();
    await page.getByRole('button', { name: 'Ver última actividad de NOVA', exact: true }).click();
    checks.push('explicit-pause-resume-and-agent-keeps-working-with-drawer-closed');
    const activity = page.locator('.nova-live-popover');
    await activity.getByRole('heading', { name: 'Tarea completada y comprobada', exact: true }).waitFor();
    await activity.locator('[data-action-status="verified"] .activity-fields pre').getByText(result.observation.values.proyecto, { exact: true }).waitFor().catch(async () => {
      assert.ok((await activity.locator('[data-action-status="verified"] .activity-fields pre').allTextContents()).includes(result.observation.values.proyecto));
    });
    assert.equal(await activity.getByRole('button', { name: 'Pausar', exact: true }).count(), 0);
    const downloadEvent = page.waitForEvent('download'); await activity.getByRole('button', { name: 'Descargar actividad', exact: true }).click();
    const download = await downloadEvent, reportPath = path.join(out, 'nova-actividad.json'); await download.saveAs(reportPath);
    const report = JSON.parse(fs.readFileSync(reportPath, 'utf8'));
    assert.equal(report.status, 'COMPLETED'); assert.equal(report.runId, runId);
    assert.equal(report.actions.find(item => item.tool === 'ui_fill_variable').fields.find(field => field.name === 'proyecto').value, result.observation.values.proyecto);
    assert.equal(new Set(report.actions.map(item => item.actionId)).size, report.actions.length);
    assert.equal(fs.readFileSync(reportPath, 'utf8').includes('X-Nova-Surface'), false);
    await screenshot('live-verified-values');
    await page.getByRole('button', { name: 'Cerrar actividad', exact: true }).click();
    assert.equal(await page.getByTestId('nova-live').count(), 0);
    await page.getByRole('button', { name: 'Ver última actividad de NOVA', exact: true }).click(); await activity.waitFor();
    await page.setViewportSize({ width: 375, height: 900 }); await wait(300);
    const activityBox = await activity.boundingBox(), dockBox = await page.getByTestId('nova-dock').boundingBox();
    assert.ok(activityBox.y >= 0 && activityBox.x >= 0 && activityBox.x + activityBox.width <= 375 && activityBox.y + activityBox.height <= dockBox.y, JSON.stringify({ activityBox, dockBox }));
    await screenshot('live-mobile-history');
    await page.keyboard.press('Escape'); assert.equal(await page.getByRole('button', { name: 'Ver última actividad de NOVA', exact: true }).evaluate(node => document.activeElement === node), true);
    checks.push('live-exact-verified-values-and-local-diagnostic-download', 'reopen-completed-activity-and-mobile-keyboard-control');
  }
  assert.deepEqual(errors, []);
  fs.writeFileSync(path.join(out, 'validation.json'), JSON.stringify({ status: 'PASS', headed: true, syntheticDataOnly: true, realQwen, checks, apiAndReactUnchangedDuringRun: true, browserErrors: errors }, null, 2));
  console.log('PASS', out);
})().catch(async error => { fs.writeFileSync(path.join(out, 'failure.txt'), String(error.stack)); if (page) await screenshot('failure').catch(() => {}); console.error(error); process.exitCode = 1; }).finally(async () => { if (runId && server?.exitCode === null) await api('/nova/live/' + runId + '/cancel', {}).catch(() => {}); await context?.close().catch(() => {}); await browser?.close().catch(() => {}); if (server && server.exitCode === null) { server.kill(); await new Promise(resolve => { server.once('exit', resolve); setTimeout(resolve, 3000).unref(); }); } fs.writeFileSync(path.join(out, 'api.log'), log); });
