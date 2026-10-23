// Headed, synthetic React -> API -> SQLite validation. Optional real local-model case; never a personal library.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { createHash } = require('node:crypto');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
const temp = path.join(root, '.playwright/versions-' + stamp), out = path.join(root, 'docs/evidence/closure-20261020/versions-' + stamp);
const origin = 'http://127.0.0.1:5252', headers = { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' };
const build = path.resolve(root, process.env.NOVA_TEST_BUILD || '.playwright/closure-20261020/api-versions-v1/QuickPrompt.Api.dll');
const web = path.resolve(root, process.env.NOVA_WEB_BUILD || 'apps/web/dist');
const sharing = process.env.NOVA_TEST_SHARING === '1';
const operations = process.env.NOVA_TEST_OPERATIONS === '1';
const editorHistory = process.env.NOVA_TEST_EDITOR_HISTORY === '1';
const realExplanation = process.env.NOVA_TEST_EXPLANATION === '1';
const ollamaEndpoint = realExplanation ? 'http://127.0.0.1:11434' : 'http://127.0.0.1:1';
const modelLease = realExplanation ? fs.openSync(path.join(require('node:os').tmpdir(), 'QuickPrompt.Nova.SemanticModel.lease'), 'a+') : null;
assert.ok(fs.existsSync(build) && fs.existsSync(path.join(web, 'index.html')), 'Build API and web first; no downloads are permitted.');
fs.mkdirSync(temp, { recursive: true }); fs.mkdirSync(out, { recursive: true });
fs.cpSync(path.dirname(build), path.join(temp, 'api'), { recursive: true }); fs.cpSync(web, path.join(temp, 'web'), { recursive: true });
const rows = [], externalRequests = []; let server, browser, context, page, logs = '';
const report = { status: 'RUNNING', syntheticOnly: true, scope: 'VERSIONS_UI_API_SQLITE_NOT_AGENT_ACCEPTANCE',
  ollamaEndpoint, measuredAt: new Date().toISOString(), rows, externalRequests,
  hashes: Object.fromEntries(['QuickPrompt.Api.dll', 'QuickPrompt.Application.dll', 'QuickPrompt.Infrastructure.dll', 'QuickPrompt.Domain.dll']
    .map(file => [file, createHash('sha256').update(fs.readFileSync(path.join(temp, 'api', file))).digest('hex')])) };
if (sharing) report.scope = 'VERSIONS_AND_PORTABLE_SHARING_UI_API_SQLITE_NOT_AGENT_ACCEPTANCE';
if (operations) report.scope += '_WITH_DELAYED_OPERATION_CONTEXT';
if (editorHistory) report.scope += '_WITH_EDITOR_HISTORY';
if (realExplanation) report.scope += '_ONE_READ_EXPLAIN_AGENT_WITH_REAL_QWEN_NOT_GENERAL_ACCEPTANCE';
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2));
const pass = (name, evidence) => { rows.push({ name, status: 'PASS', evidence }); save(); console.log('PASS', name); };
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(read, timeout = 15000) { const end = Date.now() + timeout; while (Date.now() < end) { if (await read()) return; await wait(100); } throw Error('QA_TIMEOUT'); }
async function response(route, body, method = body ? 'POST' : 'GET') {
  return fetch(origin + '/api/v1' + route, { method, headers, body: body ? JSON.stringify(body) : undefined });
}
async function api(route, body, method) { const result = await response(route, body, method); assert.ok(result.ok, route + ': ' + result.status); return result.json(); }
async function start() {
  server = spawn('dotnet', [path.join(temp, 'api/QuickPrompt.Api.dll'), '--QuickPrompt:Port=5252', '--QuickPrompt:Ollama=' + ollamaEndpoint,
    '--QuickPrompt:DataPath=' + path.join(temp, 'synthetic.sqlite'), '--QuickPrompt:WebRoot=' + path.join(temp, 'web'), '--Logging:LogLevel:Default=Warning'],
  { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', bytes => { logs = (logs + bytes).slice(-6000); }); server.stderr.on('data', bytes => { logs = (logs + bytes).slice(-6000); });
  await until(async () => { try { return (await response('/prompts?size=1')).ok; } catch { return false; } }, 30000);
}
async function stop() { if (!server || server.exitCode !== null) return; const stopped = new Promise(resolve => server.once('exit', resolve)); server.kill(); await stopped; }

(async () => {
  save(); await start();
  const original = await api('/prompts', { title: 'Historial de órbita 🛰️', templateText: 'Explica <tema>.\nNo inventes datos.', description: 'Descripción original', defaults: { tema: 'ciencia α\nβ' }, categoryId: 3 });
  const edited = await api('/prompts/' + original.id, { title: 'Versión vigente', templateText: 'Revisa <material>.', description: 'Descripción actual', defaults: { material: 'código' }, revision: original.revision }, 'PUT');
  const current = await api('/prompts/' + original.id + '/favorite', { revision: edited.revision });
  const originalVersions = await api('/prompts/' + original.id + '/versions');
  browser = await chromium.launch({ channel: 'msedge', headless: false });
  context = await browser.newContext({ viewport: { width: 1280, height: 900 }, recordVideo: { dir: out, size: { width: 960, height: 720 } } });
  page = await context.newPage(); page.on('request', request => { if (/^https?:/.test(request.url()) && !request.url().startsWith(origin + '/')) externalRequests.push(request.url()); });
  page.on('response', async response => {
    if (response.url().endsWith('/templates/rename-variable')) {
      report.editorTrace ??= [];
      report.editorTrace.push({ status: response.status(), input: response.request().postDataJSON(), response: await response.json().catch(() => null) }); save();
    }
  });
  await page.goto(origin); await page.getByRole('button', { name: current.title, exact: true }).click();
  await page.getByTestId('prompt-focus').waitFor();
  await page.getByRole('button', { name: 'Versiones', exact: true }).click();
  const selector = page.getByRole('combobox', { name: 'Versión para comparar', exact: true });
  await selector.selectOption('1');
  assert.equal(await page.getByRole('region', { name: 'Versión seleccionada', exact: true }).locator('pre').first().textContent(), original.templateText);
  assert.equal(await page.getByRole('region', { name: 'Versión actual', exact: true }).locator('pre').first().textContent(), current.templateText);
  await page.screenshot({ path: path.join(out, 'comparison.png'), fullPage: true });
  await page.getByRole('button', { name: 'Cancelar', exact: true }).click();
  await until(async () => !await page.getByRole('dialog').count());
  assert.deepEqual(await api('/prompts/' + original.id), current);
  assert.deepEqual(await api('/prompts/' + original.id + '/versions'), originalVersions);
  assert.equal(await page.getByRole('button', { name: 'Versiones', exact: true }).evaluate(element => element === document.activeElement), true);
  pass('compare-cancel-and-focus-without-write', { promptId: original.id, revision: current.revision, historyCount: originalVersions.length });

  await page.getByRole('button', { name: 'Versiones', exact: true }).click(); await selector.selectOption('1');
  await page.getByRole('button', { name: 'Restaurar versión 1', exact: true }).click();
  await until(async () => !await page.getByRole('dialog').count());
  const restored = await api('/prompts/' + original.id), history = await api('/prompts/' + original.id + '/versions');
  for (const key of ['id', 'title', 'description', 'templateText', 'categoryId', 'defaults']) assert.deepEqual(restored[key], original[key], key);
  assert.equal(restored.revision, current.revision + 1); assert.equal(restored.isFavorite, true); assert.equal(restored.uses, current.uses);
  assert.equal(history.length, originalVersions.length + 1); assert.deepEqual(history.slice(1), originalVersions);
  assert.equal(await page.getByTestId('focus-title').textContent(), original.title);
  pass('confirmed-restore-preserves-exact-content-defaults-and-history', { restored, historyCount: history.length });

  assert.equal((await response(`/prompts/${original.id}/versions/1/restore`, { revision: current.revision, confirmed: true })).status, 409);
  assert.equal((await response(`/prompts/${original.id}/versions/1/restore`, { revision: restored.revision, confirmed: false })).status, 400);
  assert.deepEqual(await api('/prompts/' + original.id), restored);
  pass('replay-and-unconfirmed-restore-have-no-effects', { replayStatus: 409, unconfirmedStatus: 400 });

  await page.getByRole('button', { name: 'Versiones', exact: true }).click(); await selector.selectOption('1');
  const human = await api('/prompts/' + original.id, { title: 'Edición humana conservada', templateText: 'Cambio humano protegido.\nαβ', revision: restored.revision }, 'PUT');
  await page.getByRole('button', { name: 'Restaurar versión 1', exact: true }).click();
  await page.getByRole('alert').filter({ hasText: 'El prompt cambió' }).waitFor();
  assert.equal(await selector.inputValue(), '1'); assert.deepEqual(await api('/prompts/' + original.id), human);
  await page.screenshot({ path: path.join(out, 'conflict.png'), fullPage: true });
  await page.getByRole('button', { name: 'Actualizar comparación', exact: true }).click();
  await until(async () => await page.getByRole('region', { name: 'Versión actual', exact: true }).locator('pre').first().textContent() === human.templateText);
  assert.equal(await selector.inputValue(), String(human.revision));
  assert.equal(await page.getByRole('button', { name: 'Restaurar versión ' + human.revision, exact: true }).isDisabled(), true);
  assert.deepEqual(await api('/prompts/' + original.id), human);
  await page.getByRole('button', { name: 'Cancelar', exact: true }).click();
  pass('concurrent-edit-is-preserved-and-comparison-remains-available', { revision: human.revision, templateText: human.templateText });

  await stop(); await start(); assert.deepEqual(await api('/prompts/' + original.id), human);
  assert.equal((await api('/prompts/' + original.id + '/versions')).length, history.length + 1);
  await page.reload(); await page.getByRole('button', { name: human.title, exact: true }).click();
  assert.equal(await page.getByTestId('compiled-preview').textContent(), human.templateText);
  pass('restart-preserves-final-record-and-history', { revision: human.revision, historyCount: history.length + 1 });
  if (sharing) {
    const source = await api('/prompts', { title: 'Plantilla para compartir', templateText: 'Saluda a <persona>.\nNo inventes datos.',
      description: 'Plantilla pública reutilizable', categoryId: 3, defaults: { persona: 'PRIVATE_DEFAULT_UI' } });
    await page.reload(); await page.getByRole('button', { name: source.title, exact: true }).click();
    await page.getByRole('button', { name: 'Compartir plantilla', exact: true }).click();
    const portablePreview = page.getByRole('region', { name: 'Plantilla que se compartirá', exact: true });
    await portablePreview.waitFor(); assert.equal(await portablePreview.locator('pre').textContent(), source.templateText);
    assert.ok(!(await portablePreview.textContent()).includes('PRIVATE_DEFAULT_UI'));
    await page.getByRole('button', { name: 'Cancelar', exact: true }).click();
    assert.deepEqual(await api('/prompts/' + source.id), source);
    pass('share-preview-and-cancel-preserve-record-and-usage', { sourceId: source.id, uses: source.uses });

    await page.getByRole('button', { name: 'Compartir plantilla', exact: true }).click(); await portablePreview.waitFor();
    await page.screenshot({ path: path.join(out, 'share-preview.png'), fullPage: true });
    const downloaded = page.waitForEvent('download'); await page.getByRole('button', { name: 'Descargar plantilla (.json)', exact: true }).click();
    const download = await downloaded, file = path.join(out, 'portable-template.json'); await download.saveAs(file);
    const bytes = fs.readFileSync(file), packet = JSON.parse(bytes.toString('utf8'));
    assert.deepEqual(Object.keys(packet).sort(), ['format', 'formatVersion', 'prompt']);
    assert.deepEqual(Object.keys(packet.prompt).sort(), ['category', 'description', 'template', 'title']);
    assert.equal(packet.format, 'quickprompt-template'); assert.equal(packet.formatVersion, 1);
    assert.equal(packet.prompt.title, source.title); assert.equal(packet.prompt.template, source.templateText);
    assert.ok(!bytes.includes(Buffer.from('PRIVATE_DEFAULT_UI')) && !bytes.includes(Buffer.from(source.id)));
    assert.deepEqual(await api('/prompts/' + source.id), source);
    await page.getByRole('button', { name: 'Cancelar', exact: true }).click();
    const favored = await api('/prompts/' + source.id + '/favorite', { revision: source.revision });
    assert.equal((await response(`/prompts/${source.id}/share?revision=${source.revision}`)).status, 409);
    pass('portable-file-contains-only-allowlisted-metadata-and-stale-export-conflicts', { sha256: createHash('sha256').update(bytes).digest('hex'), staleStatus: 409 });

    await page.getByRole('button', { name: 'Configuración', exact: true }).click();
    const settings = page.getByRole('dialog', { name: 'Configuración', exact: true });
    await settings.getByText('Importar o restaurar un archivo', { exact: true }).click();
    const picker = settings.getByLabel('Archivo JSON o SQLite legado', { exact: true });
    const beforeImport = await api('/prompts?size=100'); await picker.setInputFiles(file);
    await settings.getByText('0 nuevos · 1 duplicados · 0 conflictos', { exact: true }).waitFor();
    await settings.getByRole('button', { name: 'Cancelar importación', exact: true }).click();
    assert.deepEqual(await api('/prompts?size=100'), beforeImport);
    pass('portable-import-previews-duplicates-and-cancels-without-write', { duplicateCount: 1 });

    assert.equal((await response('/prompts/' + source.id, { revision: favored.revision, confirmed: true }, 'DELETE')).status, 204);
    await picker.setInputFiles(file); await settings.getByText('1 nuevos · 0 duplicados · 0 conflictos', { exact: true }).waitFor();
    await settings.getByRole('button', { name: 'Confirmar importación', exact: true }).click();
    await settings.getByRole('status').filter({ hasText: '1 prompts procesados.' }).waitFor();
    const imported = (await api('/prompts?size=100')).items.find(item => item.title === source.title);
    assert.ok(imported && imported.id !== source.id); assert.equal(imported.templateText, source.templateText); assert.deepEqual(imported.defaults, {});
    assert.deepEqual(await api('/prompts/' + original.id), human);
    await picker.setInputFiles(file); await settings.getByText('0 nuevos · 1 duplicados · 0 conflictos', { exact: true }).waitFor();
    await settings.getByRole('button', { name: 'Confirmar importación', exact: true }).click();
    await settings.getByRole('status').filter({ hasText: '0 prompts procesados.' }).waitFor();
    assert.deepEqual(await api('/prompts/' + imported.id), imported);
    await settings.getByRole('button', { name: 'Cerrar', exact: true }).click();
    await page.getByRole('button', { name: imported.title, exact: true }).click();
    assert.equal(await page.getByTestId('variable-field').inputValue(), '');
    pass('portable-import-creates-a-clean-identity-and-repeat-import-is-idempotent', { importedId: imported.id, defaults: imported.defaults, originalPrivateRecordPreserved: true });
  }
  if (operations) {
    const source = await api('/prompts', { title: 'Operación ligada al formulario', templateText: 'Explica <tema>.', categoryId: 0 });
    await page.reload(); await page.getByRole('button', { name: source.title, exact: true }).click();
    await page.getByTestId('variable-field').fill('valor anterior'); await page.getByTestId('compile-ready').waitFor();
    await page.evaluate(() => { window.__qaClipboard = []; Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: async text => { window.__qaClipboard.push(text); } } }); });
    const pattern = `**/api/v1/prompts/${source.id}/compile`;
    let release, requested;
    const held = new Promise(resolve => { release = resolve; }), received = new Promise(resolve => { requested = resolve; });
    await page.route(pattern, async route => {
      const actual = await route.fetch(); requested(); await held;
      await route.fulfill({ response: actual }).catch(() => {}); // Aborted browser requests cannot be fulfilled.
    }, { times: 1 });
    await page.getByRole('button', { name: 'Copiar prompt', exact: true }).click(); await received;
    await page.getByTestId('variable-field').fill('valor vigente α'); release();
    await until(async () => await page.getByTestId('compiled-preview').textContent() === 'Explica valor vigente α.');
    assert.deepEqual(await page.evaluate(() => window.__qaClipboard), []);
    assert.equal((await api('/prompts/' + source.id)).uses, 0);
    pass('delayed-copy-does-not-write-after-values-change', { uses: 0, clipboard: 'instrumented-not-native-Windows' });
    await page.getByRole('button', { name: 'Copiar prompt', exact: true }).click();
    await until(async () => (await api('/prompts/' + source.id)).uses === 1);
    assert.deepEqual(await page.evaluate(() => window.__qaClipboard), ['Explica valor vigente α.']);
    assert.equal((await api('/prompts/' + source.id)).templateText, source.templateText);
    pass('fresh-copy-writes-current-values-and-registers-one-use', { uses: 1, templateUnchanged: true, clipboard: 'instrumented-not-native-Windows' });
    const downloadPromise = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Descargar texto (.txt)', exact: true }).click();
    const textDownload = await downloadPromise, textFile = path.join(out, 'compiled-current.txt');
    await textDownload.saveAs(textFile);
    assert.deepEqual(fs.readFileSync(textFile), Buffer.from('Explica valor vigente α.', 'utf8'));
    pass('downloaded-utf8-bytes-match-current-compilation', { sha256: createHash('sha256').update(fs.readFileSync(textFile)).digest('hex') });
    await page.evaluate(() => { navigator.clipboard.writeText = async () => { throw Error('Permiso de copia denegado por prueba'); }; });
    await page.getByRole('button', { name: 'Copiar prompt', exact: true }).click();
    await page.getByRole('alert').filter({ hasText: 'Permiso de copia denegado por prueba' }).waitFor();
    assert.equal((await api('/prompts/' + source.id)).uses, 1);
    await page.evaluate(() => { navigator.clipboard.writeText = async text => { window.__qaClipboard.push(text); }; });
    await page.getByRole('button', { name: 'Copiar prompt', exact: true }).click();
    await until(async () => (await api('/prompts/' + source.id)).uses === 2);
    pass('denied-clipboard-registers-no-use-and-explicit-retry-recovers', { usesBeforeDenied: 1, usesAfterDenied: 1, usesAfterRetry: 2, clipboard: 'instrumented-denial-not-native-Windows-permission' });
    await page.getByTestId('variable-field').fill('');
    assert.equal(await page.getByTestId('compiled-preview').isVisible(), true);
    assert.equal(await page.getByTestId('compile-ready').count(), 0);
    assert.equal(await page.getByRole('button', { name: 'Copiar prompt', exact: true }).isDisabled(), true);
    assert.equal((await api('/prompts/' + source.id)).uses, 2);
    pass('incomplete-values-keep-preview-visible-without-stale-readiness-or-use', { previewVisible: true, ready: false, uses: 2 });
    await page.screenshot({ path: path.join(out, 'current-operation.png'), fullPage: true });
    await page.getByRole('button', { name: 'Editar', exact: true }).click();
    const editor = page.getByLabel('Contenido', { exact: true });
    await editor.evaluate(field => { field.focus(); field.setSelectionRange(8, 14); field.dispatchEvent(new Event('select', { bubbles: true })); });
    await page.getByRole('button', { name: 'Crear / quitar variable', exact: true }).click();
    await until(async () => await editor.inputValue() === 'Explica tema.');
    await until(async () => await editor.evaluate(field => document.activeElement === field && field.selectionStart === 8 && field.selectionEnd === 12));
    await page.getByRole('button', { name: 'Crear / quitar variable', exact: true }).click();
    await until(async () => await editor.inputValue() === 'Explica <tema>.');
    await until(async () => await editor.evaluate(field => document.activeElement === field && field.selectionStart === 8 && field.selectionEnd === 14));
    pass('editor-toggle-roundtrip-preserves-literal-selection-and-focus', { templateUnchanged: true, selection: [8, 14] });
    let releaseToggle, toggleRequested;
    const toggleHeld = new Promise(resolve => { releaseToggle = resolve; }), toggleReceived = new Promise(resolve => { toggleRequested = resolve; });
    await page.route('**/api/v1/templates/toggle-variable', async route => {
      const actual = await route.fetch(); toggleRequested(); await toggleHeld;
      await route.fulfill({ response: actual }).catch(() => {});
    }, { times: 1 });
    await page.getByRole('button', { name: 'Crear / quitar variable', exact: true }).click(); await toggleReceived;
    await page.getByLabel('Título', { exact: true }).fill('Título humano vigente'); releaseToggle();
    await until(async () => !await editor.isDisabled());
    assert.equal(await page.getByLabel('Título', { exact: true }).inputValue(), 'Título humano vigente');
    assert.equal(await editor.inputValue(), 'Explica <tema>.');
    assert.equal((await api('/prompts/' + source.id)).title, source.title);
    pass('delayed-variable-transformation-preserves-newer-unsaved-editor', { persistedUnchanged: true, title: 'Título humano vigente', templateText: 'Explica <tema>.' });
  }
  if (editorHistory) {
    const sourceText = '<nombre> y <nombre> y \\<nombre>.\nExplica <otro>.';
    const source = await api('/prompts', { title: 'Editor Unicode α', templateText: sourceText, defaults: { nombre: 'Ana α\nβ', otro: 'C# & .NET' }, categoryId: 0 });
    await page.reload(); await page.getByRole('button', { name: source.title, exact: true }).click();
    await page.getByRole('button', { name: 'Editar', exact: true }).click();
    const content = page.getByLabel('Contenido', { exact: true });
    await page.getByText('Renombrar una variable', { exact: true }).click();
    async function selectEditor(start, end) {
      await content.evaluate((field, range) => { field.focus(); field.setSelectionRange(range.start, range.end); field.dispatchEvent(new Event('select', { bubbles: true })); }, { start, end });
    }
    await selectEditor(0, 8); await page.getByLabel('Nuevo nombre de variable').fill('otro');
    await page.getByRole('button', { name: 'Renombrar variable', exact: true }).click();
    await page.getByRole('status').filter({ hasText: 'Nombre ya utilizado' }).waitFor();
    assert.equal(await content.inputValue(), sourceText); assert.deepEqual(await api('/prompts/' + source.id), source);
    assert.equal(await page.getByRole('button', { name: 'Deshacer', exact: true }).isDisabled(), true);
    pass('rename-collision-preserves-editor-defaults-and-persisted-record', { revision: source.revision });
    await selectEditor(1, 1); await page.getByLabel('Nuevo nombre de variable').fill('persona');
    await page.getByRole('button', { name: 'Renombrar variable', exact: true }).click();
    const renamed = '<persona> y <persona> y \\<nombre>.\nExplica <otro>.';
    await until(async () => await content.inputValue() === renamed);
    await page.getByTestId('compile-ready').waitFor();
    assert.equal(await page.locator('[data-variable-name="persona"]').inputValue(), 'Ana α\nβ');
    assert.equal(await page.getByTestId('compiled-preview').textContent(), 'Ana α\nβ y Ana α\nβ y <nombre>.\nExplica C# & .NET.');
    await content.focus(); await content.press('Control+z'); assert.equal(await content.inputValue(), sourceText);
    await content.press('Control+Shift+z'); assert.equal(await content.inputValue(), renamed);
    await page.getByTestId('compile-ready').waitFor();
    await page.screenshot({ path: path.join(out, 'editor-renamed-defaults.png'), fullPage: true });
    pass('rename-caret-updates-repeated-tokens-defaults-preview-and-undo-redo', { escapedLiteralPreserved: true, defaults: { persona: 'Ana α\nβ', otro: 'C# & .NET' } });
    await page.getByRole('button', { name: 'Guardar versión', exact: true }).click();
    await page.getByTestId('prompt-focus').waitFor();
    const saved = await api('/prompts/' + source.id);
    assert.equal(saved.templateText, renamed); assert.deepEqual(saved.defaults, { persona: 'Ana α\nβ', otro: 'C# & .NET' }); assert.equal(saved.revision, 2);
    await stop(); await start(); await page.reload(); await page.getByRole('button', { name: source.title, exact: true }).click();
    assert.deepEqual(await api('/prompts/' + source.id), saved);
    pass('renamed-defaults-and-content-survive-explicit-save-and-restart', { revision: saved.revision, uses: saved.uses });

    const literal = 'Usa C# & .NET\npara 李. Literal \\<x\\>.';
    const second = await api('/prompts', { title: 'Literal multilinea 李', templateText: literal, categoryId: 0 });
    await page.reload(); await page.getByRole('button', { name: second.title, exact: true }).click(); await page.getByRole('button', { name: 'Editar', exact: true }).click();
    const end = literal.indexOf('. Literal'); await selectEditor(4, end);
    await page.getByRole('button', { name: 'Crear / quitar variable', exact: true }).click();
    await until(async () => await content.inputValue() !== literal);
    const createdText = await content.inputValue(), names = (await api('/templates/inspect', { templateText: createdText })).variableNames;
    assert.equal(names.length, 1);
    await page.getByText('Renombrar una variable', { exact: true }).click();
    await selectEditor(4, 6 + names[0].length); await page.getByLabel('Nuevo nombre de variable').fill('tema');
    await page.getByRole('button', { name: 'Renombrar variable', exact: true }).click();
    await until(async () => (await content.inputValue()).startsWith('Usa <tema>'));
    await page.getByRole('button', { name: 'Deshacer', exact: true }).click(); assert.equal(await content.inputValue(), createdText);
    await page.getByRole('button', { name: 'Rehacer', exact: true }).click();
    await selectEditor(4, 10); await page.getByRole('button', { name: 'Crear / quitar variable', exact: true }).click();
    await until(async () => await content.inputValue() === literal);
    assert.deepEqual(await api('/prompts/' + second.id), second);
    pass('multiline-unicode-original-survives-create-rename-history-and-remove', { originalExact: true, persistedUnchanged: true });
    await page.getByRole('button', { name: 'Deshacer', exact: true }).click();
    await content.fill('Rama nueva α\nβ');
    assert.equal(await page.getByRole('button', { name: 'Rehacer', exact: true }).isDisabled(), true);
    assert.deepEqual(await api('/prompts/' + second.id), second);
    pass('new-human-edit-discards-redo-without-implicit-save', { redo: false, persistedUnchanged: true });
  }
  if (realExplanation) {
    const source = await api('/prompts', { title: 'Guía de hallazgos del laboratorio', templateText: 'Resume <hallazgos> en dos viñetas. Conserva las cifras y no inventes información.', categoryId: 0 });
    await page.reload(); await page.bringToFront();
    await require('./ui-settings.cjs').openAssistant(page);
    const message = `Abre «${source.title}» y explícame cómo se usa; no lo modifiques ni lo ejecutes.`;
    await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).fill(message);
    const startTime = Date.now();
    const [started] = await Promise.all([page.waitForResponse(response => response.url() === origin + '/api/v1/nova/live' && response.request().method() === 'POST'), page.getByRole('button', { name: 'Preparar prompt', exact: true }).click()]);
    assert.equal(started.status(), 200); const run = await started.json();
    let view;
    await until(async () => {
      view = await api('/nova/live/' + run.runId);
      if (['FAILED', 'BLOCKED', 'CANCELLED'].includes(view.run.status)) throw Error('READ_EXPLAIN_NOT_COMPLETED: ' + JSON.stringify(view));
      return view.run.status === 'COMPLETED';
    }, 240000);
    const sourceAfter = await api('/prompts/' + source.id);
    report.readExplanation = { message, elapsedMs: Date.now() - startTime, view, sourceAfter }; save();
    assert.equal(view.observation.promptId, source.id); assert.equal(view.observation.revision, source.revision);
    assert.deepEqual(sourceAfter, source, 'Read/explain must preserve the actual saved record');
    assert.equal(view.observation.templateText, source.templateText, 'Read/explain must not rewrite the visible instruction');
    assert.equal(view.observation.draftChanged, false, 'Read/explain must not create an unrequested changed draft');
    assert.ok(!view.decisions.some(decision => ['ui_open_editor', 'ui_prepare_update', 'ui_open_create_prompt', 'ui_prepare_creation', 'ui_execute_local'].includes(decision.toolName)), 'No unrequested authoring or execution');
    assert.ok(typeof view.question === 'string' && view.question.trim().length > 0);
    assert.equal(await page.getByTestId('prompt-focus').getAttribute('data-prompt-id'), source.id);
    assert.equal(await page.getByTestId('focus-title').textContent(), source.title);
    assert.equal(await page.getByTestId('variable-field').inputValue(), '');
    assert.deepEqual(await api('/prompts/' + source.id), source);
    await require('./ui-settings.cjs').openAssistant(page);
    const activity = page.getByRole('button', { name: 'Última actividad de NOVA', exact: true });
    if (await activity.getAttribute('aria-expanded') !== 'true') await activity.click();
    await page.getByText(view.question, { exact: true }).first().waitFor();
    await page.screenshot({ path: path.join(out, 'read-explain-live.png'), fullPage: true });
    pass('real-agent-opens-the-requested-source-and-explains-without-filling-executing-or-saving', { runId: run.runId, sourceId: source.id, revision: source.revision, elapsedMs: Date.now() - startTime, explanation: view.question, sourceUnchanged: true });
  }
  assert.deepEqual(externalRequests, []); report.status = 'PASS'; save();
})().catch(async error => { report.status = 'FAIL'; report.error = String(error); report.errorStack = error.stack; report.serverLog = logs;
    if (page) report.editorSelection = await page.locator('#prompt-content').evaluateAll(fields => fields.map(field => ({
      text: field.value, start: field.selectionStart, end: field.selectionEnd, focused: document.activeElement === field, disabled: field.disabled
    }))).catch(() => []);
    if (page) { report.visibleMessages = await page.getByRole('status').allTextContents().catch(() => []); await page.screenshot({ path: path.join(out, 'failure.png'), fullPage: true }).catch(() => {}); }
    save(); console.error(String(error)); process.exitCode = 1; })
  .finally(async () => { if (context) await context.close(); if (browser) await browser.close(); await stop(); if (modelLease !== null) fs.closeSync(modelLease); save(); });
