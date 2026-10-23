// Real engine failures in real Blazor forms, with owned SQLite and no model calls.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process'), { createHash } = require('node:crypto');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
assert.ok(process.env.NOVA_TEST_BUILD && process.env.LEGACY_TEST_BUILD && process.env.LEGACY_TEST_CONTENT, 'Provide owned builds and content.');
const apiBuild = path.resolve(root, process.env.NOVA_TEST_BUILD), webBuild = path.resolve(root, process.env.LEGACY_TEST_BUILD);
const temporary = path.join(root, '.playwright/storage-failure-ui-' + stamp), out = path.join(root, 'docs/evidence/storage-failure-ui-' + stamp);
fs.mkdirSync(temporary); fs.mkdirSync(out);
const database = path.join(temporary, 'synthetic.sqlite'), apiOrigin = 'http://127.0.0.1:5256', origin = 'http://127.0.0.1:5257';
const hash = p => createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const shared = ['QuickPrompt.Domain.dll', 'QuickPrompt.Application.dll', 'QuickPrompt.Infrastructure.dll'];
const report = { status: 'RUNNING', scope: 'STORAGE_FAILURE_PORT_REAL_SQLITE_UI_NOT_AGENT_SEMANTICS', syntheticOnly: true,
  assemblies: shared.map(name => ({ name, api: hash(path.join(path.dirname(apiBuild), name)), web: hash(path.join(path.dirname(webBuild), name)) })),
  checks: [], browserErrors: [], externalRequests: [] };
for (const item of report.assemblies) assert.equal(item.api, item.web);
const processes = [], logs = []; let browser, context, page;
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2));
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(read, timeout = 20000) { const end = Date.now() + timeout; while (Date.now() < end) { const value = await read(); if (value) return value; await sleep(100); } throw Error('STORAGE_FAILURE_QA_TIMEOUT'); }
async function start(build, port, name, extra) {
  const child = spawn('dotnet', [build, '--QuickPrompt:Port=' + port, '--QuickPrompt:DataPath=' + database, ...extra], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] }); processes.push(child);
  const capture = { name, text: '' }; logs.push(capture); child.stdout.on('data', data => { capture.text += data; }); child.stderr.on('data', data => { capture.text += data; });
  await until(() => { if (child.exitCode !== null) throw Error('OWNED_HOST_EXITED:' + name); return capture.text.includes('Now listening on: http://127.0.0.1:' + port); });
}
async function api(route) { const response = await fetch(apiOrigin + '/api/v1' + route); assert.ok(response.ok); return response.json(); }
const check = name => { report.checks.push({ name, status: 'PASS' }); save(); console.log('PASS', name); };
async function details(locator) { if (!await locator.evaluate(node => node.open)) await locator.locator(':scope > summary').click(); }
async function fixture(operation) {
  const code = `import pathlib,sqlite3,sys,json
p=pathlib.Path(sys.argv[1]).resolve(); owned=(pathlib.Path.cwd()/'.playwright').resolve()
assert p.name=='synthetic.sqlite' and p.parent.parent==owned and p.parent.name.startswith('storage-failure-ui-')
op=sys.argv[2]
with sqlite3.connect(p) as db:
 if op=='counts':
  result=dict(db.execute('SELECT Kind,COUNT(*) FROM Documents GROUP BY Kind'));result['groups']=db.execute('SELECT COUNT(*) FROM PromptGroups').fetchone()[0]
 else:
  db.execute('DROP TRIGGER IF EXISTS qa_storage_abort')
  if op!='release':
   table,condition={'template':('Documents',"NEW.Kind='version'"),'instruction':('Documents',"NEW.Kind='instruction'"),'group':('PromptGroups','1')}[op]
   db.execute("CREATE TRIGGER qa_storage_abort BEFORE INSERT ON "+table+" WHEN "+condition+" BEGIN SELECT RAISE(ABORT,'QA_PRIVATE_STORAGE_PATH'); END;")
  result={'operation':op,'syntheticOnly':True}
print(json.dumps(result))`;
  const child = spawn('uv', ['run', '--offline', '--no-project', '--python', '3.13', 'python', '-c', code, database, operation], { cwd: root, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  let output = '', error = ''; child.stdout.on('data', data => { output += data; }); child.stderr.on('data', data => { error += data; });
  await new Promise((resolve, reject) => { child.once('error', reject); child.once('exit', code => code === 0 ? resolve() : reject(Error('OWNED_SQL_FIXTURE:' + error))); });
  return JSON.parse(output);
}
async function safeMessage(message, screenshot) {
  await page.getByText(message, { exact: true }).waitFor();
  assert.ok(!(await page.locator('body').innerText()).includes('QA_PRIVATE_STORAGE_PATH'));
  await page.screenshot({ path: path.join(out, screenshot) });
}
(async () => {
  await start(apiBuild, 5256, 'api', []); await start(webBuild, 5257, 'web', ['--contentRoot=' + path.resolve(root, process.env.LEGACY_TEST_CONTENT)]);
  browser = await chromium.launch({ channel: 'msedge', headless: false }); context = await browser.newContext({ viewport: { width: 1440, height: 1050 } }); page = await context.newPage(); page.setDefaultTimeout(12000);
  page.on('pageerror', error => { report.browserErrors.push(String(error)); save(); });
  page.on('request', request => { if (!request.url().startsWith(origin) && !/^(about|data|blob):/.test(request.url())) report.externalRequests.push(request.url()); });
  await page.goto(origin); await page.getByRole('button', { name: 'Nueva plantilla', exact: true }).click();
  await page.locator('#title').fill('QA · Plantilla recuperada'); await page.locator('#template').fill('Explica <tema>.');
  await fixture('template'); await page.getByRole('button', { name: 'Guardar plantilla', exact: true }).click();
  await safeMessage('Error de almacenamiento. No se aplicó la operación.', 'template-storage-failure.png');
  assert.equal((await api('/prompts')).total, 0); const empty = await fixture('counts'); assert.equal(empty.version || 0, 0);
  check('real-engine-abort-in-home-displays-safe-message-and-rolls-back-template-and-version');
  await fixture('release'); await page.getByRole('button', { name: 'Guardar plantilla', exact: true }).click();
  await until(async () => (await api('/prompts')).total === 1); const source = (await api('/prompts')).items[0]; assert.equal(source.revision, 1); assert.equal((await api('/prompts/' + source.id + '/versions')).length, 1);
  check('template-recovers-through-same-form-with-one-committed-version');
  await details(page.locator('details.compact-filters')); await page.locator('details.compact-filters').getByRole('button', { name: 'Gestionar grupos', exact: true }).click();
  await page.locator('#group-name').fill('QA · Grupo recuperado'); await fixture('group'); await page.getByRole('button', { name: 'Guardar grupo', exact: true }).click();
  await safeMessage('No se pudo guardar el grupo.', 'group-storage-failure.png'); assert.equal((await api('/groups')).length, 0);
  check('real-engine-group-failure-is-handled-without-partial-creation-or-technical-detail');
  await fixture('release'); await page.getByRole('button', { name: 'Guardar grupo', exact: true }).click(); await until(async () => (await api('/groups')).length === 1);
  assert.equal((await api('/groups'))[0].group.revision, 1); check('group-recovers-through-same-form-without-duplicate-creation');
  await page.getByRole('button', { name: 'Cerrar organización', exact: true }).click(); await page.getByRole('button', { name: 'Configuración', exact: true }).click();
  await details(page.locator('details.settings-advanced')); await page.getByRole('button', { name: 'Instrucciones guardadas', exact: true }).click();
  await page.locator('#instruction-name').fill('QA · Perfil recuperado'); await page.locator('#instruction-text').fill('Responde con claridad.'); await fixture('instruction'); await page.getByRole('button', { name: 'Guardar instrucción', exact: true }).click();
  await safeMessage('No se pudo guardar la instrucción.', 'instruction-storage-failure.png'); let archive = await api('/library/export'); assert.equal(archive.instructions.length, 0); assert.equal(archive.instructionVersions.length, 0);
  check('real-engine-instruction-failure-preserves-absence-of-profile-and-versions');
  await fixture('release'); await page.getByRole('button', { name: 'Guardar instrucción', exact: true }).click(); await until(async () => (await api('/library/export')).instructions.length === 1);
  archive = await api('/library/export'); assert.equal(archive.instructions[0].revision, 1); assert.equal(archive.instructionVersions.length, 1); assert.equal(archive.history.length, 0);
  check('instruction-recovers-through-same-form-with-one-version-and-no-history-effects');
  assert.deepEqual(report.browserErrors, []); assert.deepEqual(report.externalRequests, []); await page.screenshot({ path: path.join(out, 'recovered-final.png') }); report.status = 'PASS'; save(); console.log('EVIDENCE', out);
})().catch(async error => { report.status = 'FAIL'; report.failure = String(error.stack); if (page && !page.isClosed()) { await page.screenshot({ path: path.join(out, 'failure.png') }).catch(() => {}); report.visibleText = await page.locator('body').innerText().catch(() => ''); } save(); console.error(error); process.exitCode = 1; }).finally(async () => {
  if (fs.existsSync(database)) await fixture('release').catch(() => {});
  await context?.close().catch(() => {}); await browser?.close().catch(() => {});
  for (const child of processes) if (child.exitCode === null) { child.kill(); await new Promise(resolve => { child.once('exit', resolve); setTimeout(resolve, 3000).unref(); }); }
  for (const log of logs) fs.writeFileSync(path.join(out, log.name + '.log'), log.text);
});
