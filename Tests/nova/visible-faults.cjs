// Deliberate model/transport fault injection. These cases never count as evidence of Qwen quality.
const fs = require('node:fs'), path = require('node:path'), http = require('node:http'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const root = path.resolve(__dirname, '../..'), origin = 'http://127.0.0.1:5227';
const out = path.join(root, 'docs/evidence/goal08/faults-' + Date.now()), temporary = path.join(root, '.playwright/goal08-faults-' + Date.now()); fs.mkdirSync(out, { recursive: true }); fs.mkdirSync(temporary, { recursive: true });
fs.cpSync(path.join(root, 'apps/web/dist'), path.join(temporary, 'web'), { recursive: true }); fs.copyFileSync(path.join(root, '.playwright/browser-worker-build/worker.cjs'), path.join(temporary, 'worker.cjs'));
let server, mock, source, log = '', mode = 'down', calls = 0, aborted = 0, invalidArgumentsSent = false; const checks = [], ids = [];
function pass(name) { checks.push({ name, status: 'PASS' }); console.log('PASS', name); }
async function request(route, body, method = body === undefined ? 'GET' : 'POST') { return fetch(origin + '/api/v1' + route, { method, headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' }, body: body === undefined ? undefined : JSON.stringify(body) }); }
async function api(route, body) { const r = await request(route, body), json = await r.json(); assert.ok(r.ok, JSON.stringify(json)); return json; }
async function until(work, test, max = 80000) { const start = Date.now(); while (Date.now() - start < max) { const v = await work(); if(test(v))return v; await new Promise(r => setTimeout(r, 100)); } throw Error('Fault acceptance deadline exceeded'); }
async function start() {
  log = ''; server = spawn('dotnet', [path.resolve(root, process.env.NOVA_TEST_BUILD || '.playwright/goal08-build/Debug/net8.0/QuickPrompt.Api.dll'), '--QuickPrompt:Port=5227', '--QuickPrompt:Live:AllowQaBrowser=true', '--QuickPrompt:DataPath=' + path.join(temporary, 'library.sqlite'), '--QuickPrompt:WebRoot=' + path.join(temporary, 'web'), '--QuickPrompt:Live:Worker=' + path.join(temporary, 'worker.cjs'), '--QuickPrompt:Ollama=http://127.0.0.1:11440', '--QuickPrompt:Preparation:StageTimeoutSeconds=5'], { cwd: root, windowsHide: true, stdio: ['ignore','pipe','pipe'], env: { ...process.env, NOVA_UI_QA_OUT: path.join(out, 'worker'), NOVA_UI_QA_APPROVED: 'synthetic-only' } });
  server.stdout.on('data', x => log += x); server.stderr.on('data', x => log += x);
  await until(async () => { if(server.exitCode !== null)throw Error('Owned API exited'); return log; }, v => v.includes('Now listening on: ' + origin));
}
async function stop() { if (server?.exitCode === null) { const done = new Promise(resolve => server.once('exit', resolve)); server.kill(); await done; } }
async function begin(message = 'Abre el prompt de ejemplo.') { const run = await api('/nova/live', { runId: crypto.randomUUID(), sessionId: crypto.randomUUID(), message, mode: 'BROWSER' }); ids.push(run.runId); return run.runId; }
async function terminal(id) { return until(() => api('/nova/live/' + id), v => ['FAILED','BLOCKED','CANCELLED','COMPLETED'].includes(v.run.status) && !v.run.browserOpen); }
async function evidence(id, name) {
  const view = await api('/nova/live/' + id); const raw = await (await request('/nova/live/' + id + '/events')).text(); const events = [...raw.matchAll(/^data: (.*)$/gm)].map(m => JSON.parse(m[1]));
  events.forEach((e,i) => assert.equal(e.sequence, i + 1)); fs.writeFileSync(path.join(out, name + '.json'), JSON.stringify({ model: 'DELIBERATELY_SIMULATED_OLLAMA', browser: 'msedge', headed: true, view, events }, null, 2)); return events;
}
(async () => {
  mock = http.createServer(async (req, res) => {
    let raw = ''; for await (const chunk of req) raw += chunk; calls++;
    if (mode === 'down') { res.writeHead(503); res.end('{}'); return; }
    if (mode === 'hold') { res.on('close', () => { if (!res.writableEnded) aborted++; }); return; }
    const body = JSON.parse(raw);
    // Controlled stage fixtures reach the UI fault boundary; they never measure Qwen quality.
    function structured(value) { res.setHeader('Content-Type','application/json');res.end(JSON.stringify({done:true,message:{content:JSON.stringify(value)}})); }
    if(body.format?.properties?.sourceRole){structured({sourceRole:'NAMED_LIBRARY_SOURCE'});return;}
    if(body.format?.properties?.userPassageIndex){structured({searchText:source.title,userPassageIndex:0});return;}
    if(body.format?.properties?.requestType){structured({authoringRequestEvidence:'',requestType:'NO_CONTENT_REQUEST',instructionTask:'',question:'',explanation:'Lectura sintética.',continuesPendingTask:false});return;}
    if(body.format?.properties?.access){structured({access:'FIND_SAVED',values:'NONE',execution:'NONE',control:'NONE',save:'NONE',missingInformation:[],executionEvidence:'',providerId:null,explanation:'Fixture de lectura para fallos de UI.'});return;}
    if(body.format?.properties?.additionalArtifactRequested){structured({additionalArtifactRequested:false});return;}
    if(body.format?.properties?.promptIds){structured({promptIds:[source.id],reason:'Único origen sintético.'});return;}
    const data = JSON.parse(body.messages.at(-1).content), state = data.observation;
    let toolName, args = {};
    if ((mode === 'retry-good' || mode === 'retry-bad') && (!invalidArgumentsSent || mode === 'retry-bad' && body.format)) { invalidArgumentsSent = true; toolName = 'ui_read_visible_state'; args = { query: source.title }; }
    else if (mode === 'bad-tool') { toolName = 'page.evaluate'; args = { script: 'PRIVATE_FAULT_SENTINEL' }; }
    else if (state.mode === 'discover' && !data.executedTools.includes('ui_search_prompt')) { toolName = 'ui_search_prompt'; args = { query: source.title }; }
    else if (state.mode === 'discover') {
      toolName = 'ui_open_prompt'; args = { promptId: mode === 'invented-id' ? crypto.randomUUID() : source.id };
      if (mode === 'missing-ui-target') {
        const r = await request('/prompts/' + source.id, { revision: source.revision, confirmed: true }, 'DELETE'); assert.ok(r.ok);
      }
    } else { toolName = 'finish'; }
    res.setHeader('Content-Type', 'application/json'); res.end(JSON.stringify({ model: 'qwen3:8b', done: true, message: { role: 'assistant', content: body.format ? JSON.stringify({ toolName, arguments: args }) : '', tool_calls: body.format ? undefined : [{ function: { name: toolName, arguments: args } }] } }));
  }); await new Promise(resolve => mock.listen(11440, '127.0.0.1', resolve)); await start();
  source = await api('/prompts', { title: 'Ejemplo público', templateText: 'Instrucción de prueba, sin datos privados.' });
  mode = 'down'; const down = await begin(); assert.equal((await terminal(down)).run.errorCode, 'MODEL_UNAVAILABLE'); await evidence(down, 'ollama-unavailable'); assert.deepEqual(await api('/prompts/' + source.id), source); pass('Unavailable Ollama fails before UI tools and preserves the source');
  mode = 'bad-tool'; const invalid = await begin(); assert.equal((await terminal(invalid)).run.errorCode, 'UI_TOOL_NOT_ALLOWED'); const rejectedEvents = await evidence(invalid, 'unknown-tool'); assert.equal(rejectedEvents.some(e => e.toolName === 'page.evaluate'), false); assert.equal(JSON.stringify(rejectedEvents).includes('PRIVATE_FAULT_SENTINEL'), false); pass('Unknown model scripts/selectors are rejected and excluded from persistent event payloads');
  mode = 'retry-good'; invalidArgumentsSent = false; const goodCalls = calls; const repaired = await begin(); const repairedView = await terminal(repaired); assert.equal(repairedView.run.status, 'COMPLETED'); assert.equal(repairedView.run.errorCode, null); assert.equal(repairedView.observation.promptId, source.id); const repairEvents = await evidence(repaired, 'known-argument-reselection'); assert.equal(calls - goodCalls, 9); assert.equal(repairEvents.filter(e => e.stage === 'DecisionRejected').length, 1); assert.equal(repairEvents.filter(e => e.stage === 'ModelRetry').length, 1); assert.equal(repairEvents.some(e => e.toolName === 'ui_read_visible_state' && e.status === 'TOOL_RUNNING'), false); assert.deepEqual(await api('/prompts/' + source.id), source); pass('Known invalid arguments are visibly rejected; one fresh schema decision opens the actual prompt without executing invalid arguments');
  mode = 'retry-bad'; invalidArgumentsSent = false; const badCalls = calls; const stillInvalid = await begin(); assert.equal((await terminal(stillInvalid)).run.status, 'BLOCKED'); assert.equal(calls - badCalls, 7); const badEvents = await evidence(stillInvalid, 'argument-retry-exhausted'); assert.equal(badEvents.filter(e => e.stage === 'ModelRetry').length, 1); assert.equal(badEvents.some(e => e.status === 'TOOL_RUNNING' && e.toolName), false); assert.deepEqual(await api('/prompts/' + source.id), source); pass('A second invalid argument selection stops after exactly two model calls and performs no UI tool action');
  mode = 'invented-id'; const invented = await begin(); assert.equal((await terminal(invented)).run.status, 'BLOCKED'); const rejected = await evidence(invented, 'invented-id'); assert.equal(rejected.filter(e => e.toolName === 'ui_open_prompt' && e.status === 'TOOL_RUNNING').length, 0); assert.deepEqual(await api('/prompts/' + source.id), source); pass('An invented ID is rejected before any browser open action');
  mode = 'hold'; let before = calls; const timeout = await begin(); await until(async () => calls, n => n > before); assert.equal((await terminal(timeout)).run.errorCode, 'TIMEOUT'); await evidence(timeout, 'model-timeout'); assert.deepEqual(await api('/prompts/' + source.id), source); pass('A stage timeout cancels the pending model call and frees the real browser');
  before = calls; const cancelled = await begin(); await until(async () => calls, n => n > before); await api('/nova/live/' + cancelled + '/cancel', {}); assert.equal((await terminal(cancelled)).run.status, 'CANCELLED'); const cancelledEvents = await evidence(cancelled, 'cancel-model-call'); const cancelledAt = cancelledEvents.findIndex(e => e.status === 'CANCELLED'); assert.equal(cancelledEvents.slice(cancelledAt + 1).some(e => e.status === 'TOOL_RUNNING'), false); assert.ok(aborted >= 2); pass('Stop cancels actual upstream HTTP waits and prevents subsequent UI actions');
  mode = 'hold'; const interrupted = await begin(); await api('/nova/live/' + interrupted + '/pause', {}); await until(() => api('/nova/live/' + interrupted), v => v.run.status === 'PAUSED');
  const callsBeforeRestart = calls; await stop(); await start(); const recovered = await api('/nova/live/' + interrupted); assert.equal(recovered.run.status, 'BLOCKED'); assert.equal(recovered.run.errorCode, 'INTERRUPTED_NO_REPLAY'); assert.equal(calls, callsBeforeRestart); await evidence(interrupted, 'restart-recovery'); assert.deepEqual(await api('/prompts/' + source.id), source); pass('Restart recovers ordered events and blocks interrupted work without replaying tools');
  mode = 'missing-ui-target'; const missing = await begin(); const failedClick = await terminal(missing); assert.equal(failedClick.run.status, 'FAILED'); assert.equal(failedClick.run.errorCode, 'BROWSER_ACTION_FAILED'); await evidence(missing, 'actual-ui-not-confirmed');
  assert.equal((await api('/prompts?size=1')).total, 0); pass('Deliberate fixture deletion after search makes the real UI fail to open; no false success or agent write is reported');
  fs.writeFileSync(path.join(out, 'acceptance.json'), JSON.stringify({ status: 'PASS', checks, ids, realQwen: false, deliberatelyInjectedModelFaults: true, actualHeadedBrowser: true, upstreamConnectionsCancelled: aborted, isolatedDatabase: true, missingTargetWasDeletedOnlyByTestHarness: true }, null, 2));
})().catch(async error => { console.error(error); fs.writeFileSync(path.join(out, 'acceptance.json'), JSON.stringify({ status: 'FAIL', checks, ids, error: error.stack, log }, null, 2)); for(const id of ids)try { await api('/nova/live/' + id + '/cancel', {}); }catch{} process.exitCode = 1; }).finally(async () => { await stop(); if(mock) { mock.closeAllConnections(); await new Promise(resolve => mock.close(resolve)); } });
