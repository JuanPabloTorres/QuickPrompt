// LIB-02: actual React/API/SQLite with an unavailable model endpoint.
// Synthetic owned data only; no calls to the user's Ollama or private library.
const fs = require('node:fs'), path = require('node:path'), http = require('node:http');
const assert = require('node:assert/strict'), { spawn } = require('node:child_process');
const { createHash } = require('node:crypto');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
const temporary = path.join(root, '.playwright/library-offline-' + stamp), out = path.join(root, 'docs/evidence/library-offline-' + stamp);
const apiBuild = path.resolve(root, process.env.NOVA_TEST_BUILD || 'backend/QuickPrompt.Api/bin/Debug/net8.0/QuickPrompt.Api.dll');
const web = path.resolve(root, process.env.NOVA_TEST_WEBROOT || 'apps/web/dist'), origin = 'http://127.0.0.1:5266';
fs.mkdirSync(temporary, { recursive: true }); fs.mkdirSync(out, { recursive: true });
const hash = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const report = { status: 'RUNNING', syntheticOnly: true, inferenceMode: 'UNAVAILABLE_503_ENDPOINT_NO_OLLAMA',
  apiHashes: Object.fromEntries(['QuickPrompt.Api.dll', 'QuickPrompt.Application.dll', 'QuickPrompt.Infrastructure.dll', 'QuickPrompt.Domain.dll'].map(name => [name, hash(path.join(path.dirname(apiBuild), name))])),
  webIndexHash: hash(path.join(web, 'index.html')), checks: [], errors: [], modelRequests: [] };
let server, browser, unavailable, page, log = '';
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2));
const check = (name, details) => { report.checks.push({ name, status: 'PASS', details }); save(); };
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(read, timeout = 15000) { const end = Date.now() + timeout; while (Date.now() < end) { const value = await read(); if (value) return value; await wait(100); } throw Error('LIBRARY_CHECK_TIMEOUT'); }
async function api(route, body) {
  const response = await fetch(origin + '/api/v1' + route, { method: body ? 'POST' : 'GET', headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' }, body: body ? JSON.stringify(body) : undefined, signal: AbortSignal.timeout(6000) });
  assert.ok(response.ok, await response.clone().text()); return response.status === 204 ? null : response.json();
}
const ids = () => page.getByTestId('library-prompt').evaluateAll(nodes => nodes.map(node => node.getAttribute('data-prompt-id')));
const sameSet = (a, b) => JSON.stringify([...a].sort()) === JSON.stringify([...b].sort());
async function expectItems(expected) { await until(async () => await page.getByTestId('library-ready').count() && sameSet(await ids(), expected)); }
async function search(text, expected) { await page.getByLabel('Buscar en la biblioteca', { exact: true }).fill(text); await expectItems(expected); }
async function filter(name, expected) { await page.getByRole('group', { name: 'Filtrar biblioteca', exact: true }).getByRole('button', { name, exact: true }).click(); await expectItems(expected); }
(async () => {
  unavailable = http.createServer((request, response) => { report.modelRequests.push({ method: request.method, path: request.url, status: 503 }); request.resume(); response.writeHead(503); response.end(); });
  await new Promise((resolve, reject) => { unavailable.once('error', reject); unavailable.listen(5267, '127.0.0.1', resolve); });
  assert.equal((await fetch('http://127.0.0.1:5267/api/tags')).status, 503);
  server = spawn('dotnet', [apiBuild, '--QuickPrompt:Port=5266', '--QuickPrompt:DataPath=' + path.join(temporary, 'synthetic.sqlite'), '--QuickPrompt:WebRoot=' + web, '--QuickPrompt:Ollama=http://127.0.0.1:5267'], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', bytes => { log += bytes; }); server.stderr.on('data', bytes => { log += bytes; });
  await until(() => { if (server.exitCode !== null) throw Error('OWNED_API_EXITED'); return log.includes('Now listening on: ' + origin); });
  const seeded = [];
  for (let i = 0; i < 26; i++) {
    let prompt = await api('/prompts', { title: `Catálogo ${String(i).padStart(2, '0')}`, description: i % 2 ? 'Operaciones' : 'Planificación', templateText: 'Revisa <material> sin inventar información.' });
    if (i % 7 === 0) prompt = await api('/prompts/' + prompt.id + '/favorite', { revision: prompt.revision });
    if (i === 25) prompt = await api('/prompts/' + prompt.id + '/archive', { revision: prompt.revision });
    seeded.push(prompt);
  }
  const group = await api('/groups', { name: 'Equipo QA' });
  for (const prompt of seeded.slice(0, 5)) await api('/prompts/' + prompt.id + '/groups', { groupId: group.id, revision: prompt.revision, member: true });
  const before = await api('/prompts?size=100');
  browser = await chromium.launch({ channel: 'msedge', headless: false });
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
  page = await context.newPage(); page.on('pageerror', error => report.errors.push(error.message));
  await page.goto(origin); await page.getByTestId('library-ready').waitFor();
  const pages = [];
  for (let index = 0; index < 3; index++) {
    await until(async () => await page.getByTestId('library-ready').count() && (await page.getByRole('navigation', { name: 'Páginas' }).textContent()).includes(`Página ${index + 1} de 3`));
    const current = await ids(); assert.equal(current.length, index < 2 ? 12 : 1); pages.push(current);
    if (index < 2) await page.getByRole('button', { name: 'Página siguiente', exact: true }).click();
  }
  assert.equal(new Set(pages.flat()).size, 25); assert.ok(sameSet(pages.flat(), seeded.filter(prompt => !prompt.isArchived).map(prompt => prompt.id)));
  assert.equal(await page.getByRole('button', { name: 'Página siguiente', exact: true }).isDisabled(), true);
  await page.getByRole('button', { name: 'Página anterior', exact: true }).click(); await expectItems(pages[1]);
  await page.getByTestId('library-prompt').first().click(); await page.getByTestId('prompt-focus').waitFor();
  await page.getByRole('button', { name: 'QuickPrompt NOVA, biblioteca', exact: true }).click(); await expectItems(pages[1]);
  check('pagination-exact-records-and-return-retains-page', { pageSizes: pages.map(items => items.length), uniqueIds: 25 });
  // The established library search is AND over substrings, not a title-prefix
  // match: the "0" term also matches 10 and 20. Keep that public behavior.
  const matches = seeded.filter(prompt => prompt.title.includes('0')).map(prompt => prompt.id);
  await search('Catálogo 0', matches);
  await page.getByTestId('library-prompt').first().click(); await page.getByTestId('prompt-focus').waitFor();
  await page.getByRole('button', { name: 'QuickPrompt NOVA, biblioteca', exact: true }).click(); await expectItems(matches);
  assert.equal(await page.getByLabel('Buscar en la biblioteca', { exact: true }).inputValue(), 'Catálogo 0');
  check('search-and-return-retain-exact-results', { matchingRecords: matches.length });
  await search('NO_EXISTE_QA', []); await page.getByText('No hay coincidencias en tu biblioteca. Prueba otras palabras o crea un prompt.', { exact: true }).waitFor();
  check('empty-search-is-visible-without-stale-results');
  await search('', pages[0]);
  await filter('Favoritos', seeded.filter(prompt => prompt.isFavorite && !prompt.isArchived).map(prompt => prompt.id));
  await search('Catálogo 0', seeded.filter(prompt => prompt.isFavorite && prompt.title.includes('Catálogo 0')).map(prompt => prompt.id));
  await search('', seeded.filter(prompt => prompt.isFavorite).map(prompt => prompt.id));
  await filter('Archivo', [seeded[25].id]); await filter('Recientes', []);
  await filter('Todos', pages[0]);
  await page.getByRole('combobox', { name: 'Colección', exact: true }).selectOption(group.id); await expectItems(seeded.slice(0, 5).map(prompt => prompt.id));
  await page.getByTestId('library-prompt').first().click(); await page.getByTestId('prompt-focus').waitFor();
  await page.getByRole('button', { name: 'QuickPrompt NOVA, biblioteca', exact: true }).click(); await expectItems(seeded.slice(0, 5).map(prompt => prompt.id));
  assert.equal(await page.getByRole('combobox', { name: 'Colección', exact: true }).inputValue(), group.id);
  check('favorites-archive-recent-collection-and-combined-search', { favorites: 4, archived: 1, recent: 0, collection: 5 });
  await page.getByRole('combobox', { name: 'Colección', exact: true }).selectOption(''); await expectItems(pages[0]);
  let injected = 0;
  const route = async route => { const query = new URL(route.request().url()).searchParams; if (query.get('search') === 'FALLO_SINTETICO') { injected++; await route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({ code: 'QA_UNAVAILABLE', message: 'Fallo sintético de la biblioteca.' }) }); } else await route.continue(); };
  await page.route('**/api/v1/prompts?*', route);
  await page.getByLabel('Buscar en la biblioteca', { exact: true }).fill('FALLO_SINTETICO');
  await page.getByRole('heading', { name: 'No pudimos abrir la biblioteca.', exact: true }).waitFor();
  assert.equal((await ids()).length, 0); assert.ok(injected > 0);
  await page.unroute('**/api/v1/prompts?*', route);
  await page.getByRole('button', { name: 'Reintentar', exact: true }).click(); await expectItems([]);
  await search('', pages[0]); check('list-error-clears-stale-results-and-retry-recovers', { injected });
  const after = await api('/prompts?size=100'); assert.deepEqual(after, before);
  assert.equal(report.modelRequests.some(request => request.path === '/api/chat'), false); assert.equal(report.errors.length, 0);
  check('reads-do-not-write-or-require-inference');
  await page.screenshot({ path: path.join(out, 'library-without-model.png'), fullPage: true });
  report.status = 'PASS'; save(); console.log(JSON.stringify({ status: report.status, evidence: path.relative(root, out), checks: report.checks.length }));
})().catch(async error => { report.status = 'FAIL'; report.error = String(error.stack || error); if (page) { report.visibleIds = await ids().catch(() => []); await page.screenshot({ path: path.join(out, 'failure.png'), fullPage: true }).catch(() => {}); } save(); console.error(error); process.exitCode = 1; }).finally(async () => {
  await browser?.close().catch(() => {});
  if (server && server.exitCode === null && server.signalCode === null) { const exited = new Promise(resolve => server.once('exit', resolve)); server.kill(); await exited; }
  if (unavailable?.listening) await new Promise(resolve => unavailable.close(resolve));
});
