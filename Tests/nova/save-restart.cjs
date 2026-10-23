// Persistence follow-up for a completed, owned synthetic save/cancel UI run.
// No model calls, new downloads or access to the user's library.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process'), { createHash } = require('node:crypto');
const { authoredContentRequested } = require('./acceptance.cjs');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..');
const evidence = path.resolve(root, process.argv[2] || '');
assert.ok(evidence.toLowerCase().startsWith((path.join(root, 'docs/evidence/latency-')).toLowerCase()), 'An owned latency fixture is required');
const environment = JSON.parse(fs.readFileSync(path.join(evidence, 'environment.json'), 'utf8'));
assert.equal(environment.syntheticDataOnly, true);
const rows = JSON.parse(fs.readFileSync(path.join(evidence, 'measurements.json'), 'utf8'));
const selected = process.argv[3] ? rows.filter(row => row.scenario === process.argv[3]) : rows;
assert.equal(selected.length, 1, 'Choose one unique completed save scenario; its exact library snapshot must still match after restart');
const row = selected[0], fixture = path.resolve(root, environment.snapshot);
assert.ok(fixture.toLowerCase().startsWith((path.join(root, '.playwright') + path.sep).toLowerCase()));
// Authored-content campaigns deliberately retain null until independent review.
// Consume that hash-bound adjudication without allowing it to override failed UI.
if (row.correct !== true || authoredContentRequested(row.semanticSummary, ...(row.precedingRuns ?? []).map(run => run.semantic), row.unsavedCreation?.semantic)) {
  assert.notEqual(row.correct, false); assert.equal(row.controlCorrect, true);
  const adjudication = JSON.parse(fs.readFileSync(path.join(evidence, 'acceptance.json'), 'utf8'));
  assert.equal(adjudication.measurementsSha256, createHash('sha256').update(fs.readFileSync(path.join(evidence, 'measurements.json'))).digest('hex'));
  const reviewed = adjudication.rows.filter(item => item.scenario === row.scenario && item.index === row.index);
  assert.equal(reviewed.length, 1); assert.equal(reviewed[0].correct, true);
}
assert.ok(row.syntheticPersistence);
assert.ok(['saveVersion', 'saveCopy', 'unsavedSaveCopy', 'rejectSave'].includes(row.scenario));
assert.equal(!!process.argv[5], !!process.argv[6], 'Rollback requires both the frozen API and its prior evidence manifest');
const expected = row.syntheticPersistence, out = path.join(evidence, process.argv[5] ? 'rollback-' + row.scenario : process.argv[4] ? 'restart-' + row.scenario : 'restart'); fs.mkdirSync(out, { recursive: true });
const hash = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex');
let database = path.join(fixture, 'synthetic.sqlite'), sourceBackupHash = null;
if (process.argv[4]) {
  // A multi-case campaign continues writing after an earlier scenario. Reopen
  // its actual SQLite backup in a fresh owned location, never rewind the live
  // fixture or edit the expected library snapshot to match a later case.
  const backup = path.resolve(root, process.argv[4]);
  assert.ok(backup.toLowerCase().startsWith((path.join(fixture, 'backups') + path.sep).toLowerCase()), 'Use an actual backup of this exact owned fixture');
  assert.equal(path.extname(backup), '.sqlite'); sourceBackupHash = hash(backup);
  const restart = path.join(root, '.playwright/restart-' + Date.now()); fs.mkdirSync(restart);
  database = path.join(restart, 'synthetic.sqlite'); fs.copyFileSync(backup, database);
}
for (const [name, digest] of Object.entries(environment.hashes)) assert.equal(hash(path.join(fixture, 'api', name)), digest);
assert.equal(hash(path.join(fixture, 'web/index.html')), environment.webHashes['index.html']);
let apiBuild = path.join(fixture, 'api/QuickPrompt.Api.dll'), apiHashes = environment.hashes;
if (process.argv[5]) {
  assert.ok(process.argv[4], 'Rollback reopens a backup in an isolated copy');
  apiBuild = path.resolve(root, process.argv[5]); const manifest = path.resolve(root, process.argv[6]);
  assert.ok(apiBuild.toLowerCase().startsWith((path.join(root, '.playwright') + path.sep).toLowerCase()));
  assert.ok(manifest.toLowerCase().startsWith((path.join(root, 'docs/evidence') + path.sep).toLowerCase()));
  apiHashes = JSON.parse(fs.readFileSync(manifest, 'utf8')).apiHashes;
  assert.ok(apiHashes && Object.keys(environment.hashes).every(name => apiHashes[name]));
  for (const name of Object.keys(environment.hashes)) assert.equal(hash(path.join(path.dirname(apiBuild), name)), apiHashes[name]);
  assert.notEqual(apiHashes['QuickPrompt.Application.dll'], environment.hashes['QuickPrompt.Application.dll'], 'Rollback must actually select a different application build');
}
const origin = 'http://127.0.0.1:5240'; let server, browser, log = '';
const report = { status: 'RUNNING', syntheticOnly: true, scenario: row.scenario, priorRunId: row.runId, origin,
  inferenceMode: 'NO_INFERENCE_PERSISTENCE_AND_NATIVE_UI_ONLY', sourceBackupHash, rollback: !!process.argv[5], apiHashes, originalCandidateApiHashes: environment.hashes, webIndexHash: environment.webHashes['index.html'], preparationOptions: environment.extraOptions };
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2)); save();
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(check, timeout = 20000) { const deadline = Date.now() + timeout; while (Date.now() < deadline) { const result = await check(); if (result) return result; await wait(100); } throw Error('RESTART_CHECK_TIMEOUT'); }
async function api(route, body) {
  const response = await fetch(origin + '/api/v1' + route, { method: body ? 'POST' : 'GET', headers: { Origin: origin, 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' }, body: body ? JSON.stringify(body) : undefined, signal: AbortSignal.timeout(6000) });
  return { status: response.status, data: response.status === 204 ? null : await response.json() };
}
(async () => {
  server = spawn('dotnet', [apiBuild, '--QuickPrompt:Port=5240', '--QuickPrompt:DataPath=' + database, '--QuickPrompt:WebRoot=' + path.join(fixture, 'web'), ...(environment.extraOptions ?? [])], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', bytes => { log += bytes; }); server.stderr.on('data', bytes => { log += bytes; });
  await until(() => { if (server.exitCode !== null) throw Error('OWNED_API_EXITED'); return log.includes('Now listening on: ' + origin); });
  const library = await api('/prompts?size=100'); assert.equal(library.status, 200);
  const normalize = listing => ({ total: listing.total, items: [...listing.items].sort((a, b) => a.id.localeCompare(b.id)) });
  assert.deepEqual(normalize(library.data), normalize(expected.libraryAfter)); report.libraryMatchesAfterRestart = true;
  const target = library.data.items.find(prompt => prompt.id === (row.scenario === 'rejectSave' ? expected.sourceBefore.id : expected.targetId));
  assert.ok(target); report.target = target;
  if (row.scenario === 'rejectSave') assert.deepEqual(normalize(library.data), normalize(expected.libraryBefore));
  else {
    assert.ok(row.authorizedWrite); assert.equal(target.templateText, expected.approvedDraft.templateText); assert.equal(target.title, expected.approvedDraft.title);
    assert.equal(hash(path.join(fixture, 'web/index.html')), report.webIndexHash);
    assert.equal(createHash('sha256').update(target.templateText).digest('hex'), row.observedTextHash);
  }
  const replay = await api('/nova/live/' + row.runId + '/commit', expected.approvedDraft || { title: target.title, templateText: target.templateText, revision: target.revision });
  assert.ok([404, 409].includes(replay.status)); report.oldApprovalReplayStatus = replay.status;
  const unchanged = await api('/prompts?size=100'); assert.deepEqual(normalize(unchanged.data), normalize(library.data)); report.replayDidNotWrite = true;
  browser = await chromium.launch({ channel: 'msedge', headless: false });
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } }); const page = await context.newPage();
  await page.goto(origin); await page.bringToFront();
  await page.locator('[data-testid="library-prompt"][data-prompt-id="' + target.id + '"]').click();
  const focus = page.getByTestId('prompt-focus'); await focus.waitFor();
  assert.equal(await focus.getAttribute('data-prompt-id'), target.id); assert.equal(await focus.getAttribute('data-revision'), String(target.revision));
  assert.equal(await focus.getAttribute('data-template-text'), target.templateText);
  assert.equal((await page.getByTestId('focus-title').textContent()).trim(), target.title); assert.equal(context.pages().length, 1);
  report.nativeDetailMatchesPersistedRecord = true; await page.screenshot({ path: path.join(out, 'reopened.png'), fullPage: true });
  report.status = 'PASS'; save(); console.log(JSON.stringify({ status: report.status, scenario: row.scenario, evidence: out }));
})().catch(error => { report.status = 'FAIL'; report.error = String(error.stack || error); save(); console.error(error); process.exitCode = 1; }).finally(async () => {
  await browser?.close().catch(() => {});
  if (server && server.exitCode === null && server.signalCode === null) { const exited = new Promise(resolve => server.once('exit', resolve)); server.kill(); await exited; }
});
