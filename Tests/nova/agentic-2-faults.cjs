// Real Edge + API + SQLite. Ollama is deliberately replaced ONLY in this fault suite.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict'), http = require('node:http');
const { spawn } = require('node:child_process'), { randomUUID } = require('node:crypto'), { DatabaseSync } = require('node:sqlite');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), origin = 'http://127.0.0.1:5223', stamp = Date.now();
const evidence = path.join(root, 'docs/evidence/agentic-2', 'faults-' + stamp), temporary = path.join(root, '.playwright', 'agentic-faults-' + stamp);
fs.mkdirSync(evidence, { recursive: true }); fs.mkdirSync(temporary, { recursive: true });
let apiProcess, browser, db, output = '', mode = 'healthy', calls = 0, executions = 0, active = 0, page, boundSourceId;
const checks = [], errors = [], externalRequests = [];
let inconsistentFeedbackObserved = false, boundClauseBody;
function pass(name) { checks.push({ name, status: 'PASS' }); console.log('PASS', name); }
const fake = http.createServer(async (req, res) => {
  let raw = ''; for await (const chunk of req) raw += chunk; const body = JSON.parse(raw || '{}'); calls++;
  if (mode === 'offline') { res.writeHead(503); res.end('{}'); return; }
  if (body.stream === true) { executions++; res.writeHead(200, { 'Content-Type': 'application/x-ndjson' }); res.end(JSON.stringify({ message: { content: 'RESPUESTA SINTÉTICA' }, done: true, done_reason: 'stop' }) + '\n'); return; }
  const properties = body.format?.properties;
  function structured(value) { res.writeHead(200, { 'Content-Type': 'application/json' }); res.end(JSON.stringify({ done: true, message: { content: JSON.stringify(value) } })); }
  // Controlled stage fixtures prove validation/effects, never Qwen interpretation quality.
  function packet(){const p=JSON.parse(body.messages.at(-1).content);return p.context??p;}
  if(properties?.sourceRole){const d=packet(),fill=d.request.startsWith('Completa');const v={sourceRole:mode==='source-workflow-conflict'?(d.sourceConsistencyReview?'NAMED_LIBRARY_SOURCE':'NO_SOURCE_REQUIRED'):['bound-ui-original','invalid-source-role'].includes(mode)?(mode==='invalid-source-role'?'OUT_OF_CATALOG_SOURCE':d.roles?.SAVED_TASK_ORIGINAL?'SAVED_TASK_ORIGINAL':'OTHER_UI_SELECTION'):mode==='current-task-copy'?'CURRENT_CONVERSATION_ARTIFACT':mode==='unresolved-search'?'UNRESOLVED_REFERENCE':['bound-search','corrected-content-evidence'].includes(mode)?'CURRENT_UI_INSTRUCTION':['semantic-id','unbound-query'].includes(mode)?'NAMED_LIBRARY_SOURCE':fill?(d.roles?.CURRENT_CONVERSATION_ARTIFACT?'CURRENT_CONVERSATION_ARTIFACT':d.roles?.CURRENT_UI_INSTRUCTION?'CURRENT_UI_INSTRUCTION':d.roles?.SAVED_TASK_ORIGINAL?'SAVED_TASK_ORIGINAL':d.roles?.OTHER_UI_SELECTION?'OTHER_UI_SELECTION':'UNRESOLVED_REFERENCE'):'NO_SOURCE_REQUIRED'};if(mode==='unknown-tool')v.toolName='shell';structured(v);return;}
  if(properties?.userPassageIndex){if(mode==='source-workflow-conflict'){structured({searchText:'Código de prueba',userPassageIndex:0});return;}if(mode==='unbound-query'){structured({searchText:'',userPassageIndex:0});return;}structured(mode==='unresolved-search'?{searchText:'Código de prueba',userPassageIndex:0}:{searchText:'acoplamiento',userPassageIndex:0});return;}
  if(properties?.additionalArtifactRequested){structured({additionalArtifactRequested:['current-task-copy','context-inherited-copy'].includes(mode)});return;}
  if(properties?.requestType){if(mode==='source-workflow-conflict')assert.ok(!body.messages[0].content.includes('SOURCE_WORKFLOW_CONFLICT'),'Source correction must not contaminate content classification');const d=packet(),fill=d.request.startsWith('Completa'),readonly=fill||['unbound-query','semantic-id','bound-search','unresolved-search','bound-ui-original','invalid-source-role','inconsistent-content-evidence','corrected-content-evidence','source-workflow-conflict'].includes(mode);if(mode==='inconsistent-content-evidence'&&d.contractCorrection?.previousResponse)inconsistentFeedbackObserved=true;
    structured({authoringRequestEvidence:mode==='corrected-authoring-evidence'?(d.contractCorrection?d.request:''):mode==='ungrounded-content-evidence'?'Texto que no pertenece a la petición':(mode==='inconsistent-content-evidence'||mode==='corrected-content-evidence'&&!d.contractCorrection)?d.request:readonly?'':d.request,requestType:readonly?'NO_CONTENT_REQUEST':['bound-semantic-clause','unsupported-content-clause','source-target-scopes'].includes(mode)?'REVISION_REQUEST':'NEW_INSTRUCTION_REQUEST',instructionTask:mode==='missing-content-task'||readonly?'':'Explica una API.',question:'',explanation:'Resultado sintético.',continuesPendingTask:false});return;}
  if(properties?.access){const d=packet(),fill=d.request.startsWith('Completa');structured({access:['bound-ui-original','invalid-source-role','corrected-content-evidence'].includes(mode)?'VIEW_UNCHANGED':mode==='source-workflow-conflict'||mode==='unbound-query'||mode==='semantic-id'||mode==='bound-search'||mode==='unresolved-search'||['bound-ui-original','invalid-source-role'].includes(mode)?'FIND_SAVED':'NONE',values:fill?'FILL_TEMPLATE_INPUTS':'NONE',execution:['missing-content-task','ungrounded-content-evidence','inconsistent-content-evidence'].includes(mode)?'RUN_LOCAL_QWEN_NOW':'NONE',control:'NONE',save:'NONE',missingInformation:[],executionEvidence:['missing-content-task','ungrounded-content-evidence'].includes(mode)?d.request:'',providerId:null,explanation:'Workflow sintético.'});return;}
  if(properties?.userProhibitions){structured({userProhibitions:[],preserveOriginal:true,language:null,format:null,literalClauses:['bound-semantic-clause','unsupported-content-clause'].includes(mode)?['enfocarse en auditar un módulo']:[],targetTerms:mode==='source-target-scopes'?['Código de prueba','Rust']:[],shortening:false,languageEvidence:'',formatEvidence:''});return;}
  if(properties?.terms){const d=packet();assert.equal(d.sourceInstruction,boundClauseBody);structured({terms:d.candidateTerms.map(c=>({index:c.index,scope:c.text===d.sourceTitle?'SOURCE_IDENTIFIER':'CONTENT_TECHNICAL_TARGET'}))});return;}
  if(properties?.clauses){const d=packet();assert.equal(d.templateBeforeEdit,boundClauseBody);structured({clauses:d.candidateClauses.map(c=>({index:c.index,scope:mode==='unsupported-content-clause'?'FUTURE_TASK_INVARIANT':'SEMANTIC_REQUIREMENT'}))});return;}
  if(properties?.values&&properties?.question&&!properties?.decision){structured({values:[],question:''});return;}
  const generation = !!body.format?.properties?.decision;
  if (generation && ['timeout', 'cancel'].includes(mode)) { active++; res.on('close', () => { active--; }); return; }
  res.writeHead(200, { 'Content-Type': 'application/json' });
  if (body.format?.properties?.promptIds) { res.end(JSON.stringify({ done: true, message: { content: JSON.stringify({ promptIds: [['bound-search','unresolved-search','source-workflow-conflict'].includes(mode) ? boundSourceId : randomUUID()], reason: mode === 'bound-search' ? 'Origen real ya seleccionado.' : 'Intento de ID inventado.' }) } })); return; }
  if (!generation) res.end(JSON.stringify({ done: true, message: { content: '', tool_calls: [{ function: { name: mode === 'unknown-tool' ? 'shell' : 'search_prompts', arguments: { intent: mode === 'semantic-id' ? 'FIND_PROMPT' : 'CREATE_PROMPT', query: mode === 'semantic-id' ? 'acoplamiento' : '', clarification: '', operations: [mode === 'semantic-id' ? 'FIND_PROMPT' : 'CREATE_PROMPT'] } } }] } }));
  else res.end(JSON.stringify({ done: true, message: { content: JSON.stringify({ decision: mode === 'invalid-id' ? 'ADAPT_EXISTING' : 'CREATE_NEW', sourceId: mode === 'invalid-id' ? randomUUID() : null, title: 'Instrucción sintética', description: '', templateText: 'Explica una API de forma breve.', values: [], requiredInputs: [], clarification: '', reason: 'Fixture controlada para errores y recuperación.' }) } }));
});
async function api(route, body, method) { const response = await fetch(origin + '/api/v1' + route, { method: method || (body === undefined ? 'GET' : 'POST'), headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' }, body: body === undefined ? undefined : JSON.stringify(body) }); return { response, data: response.status === 204 ? null : response.headers.get('content-type')?.includes('event-stream') ? await response.text() : await response.json() }; }
(async () => {
  await new Promise(resolve => fake.listen(11439, '127.0.0.1', resolve));
  apiProcess = spawn('dotnet', [path.resolve(root, process.env.NOVA_TEST_BUILD || '.playwright/agentic-2/Debug/net8.0/QuickPrompt.Api.dll'), '--QuickPrompt:Port=5223', '--QuickPrompt:Ollama=http://127.0.0.1:11439', '--QuickPrompt:Preparation:StageTimeoutSeconds=5', '--QuickPrompt:Preparation:TotalTimeoutSeconds=15', `--QuickPrompt:DataPath=${path.join(temporary, 'library.sqlite')}`, `--QuickPrompt:WebRoot=${path.join(root, 'apps/web/dist')}`], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  apiProcess.stdout.on('data', c => { output += c; }); apiProcess.stderr.on('data', c => { output += c; });
  for (let n = 0; n < 150; n++) { if (apiProcess.exitCode !== null) throw Error('Owned server exited'); if (output.includes('Now listening on: ' + origin)) break; if (n === 149) throw Error('Readiness failed'); await new Promise(r => setTimeout(r, 100)); }
  const source = (await api('/prompts', { title: 'Fuente de prueba', templateText: 'Original intacto.' })).data;
  const variableSource = (await api('/prompts', { title: 'Código de prueba', templateText: 'Analiza <codigo>.' })).data;
  db = new DatabaseSync(path.join(temporary, 'library.sqlite'), { readOnly: true });
  const snapshot = () => db.prepare("SELECT Kind,Id,Payload FROM Documents WHERE Kind IN ('template','version','compiled','execution') ORDER BY Kind,Id").all(); const before = snapshot();
  browser = await chromium.launch({ channel: 'msedge', headless: true }); const context = await browser.newContext({ viewport: { width: 390, height: 844 }, reducedMotion: 'reduce' }); page = await context.newPage();
  page.on('pageerror', e => errors.push(e.message)); page.on('request', r => { if (!r.url().startsWith(origin) && !r.url().startsWith('data:')) externalRequests.push(r.url()); });
  await page.goto(origin); const input = page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }); let lastRun;
  page.on('request', r => { if (r.url().endsWith('/nova/prepare')) lastRun = r.postDataJSON().operationId; });
  async function ask(text) { if(await page.locator('.preparation-options').getAttribute('open')===null) await page.getByText('Opciones de preparación',{exact:true}).click();await page.getByLabel('Modo de NOVA', { exact: true }).selectOption('LIVE'); await input.fill(text); const [sent]=await Promise.all([page.waitForRequest(r=>r.url()===origin+'/api/v1/nova/prepare'),page.getByRole('button', { name: 'Preparar prompt', exact: true }).click()]);lastRun=sent.postDataJSON().operationId;return sent; }
  await ask('Crea una instrucción sintética sencilla.'); let dialog = page.getByRole('dialog').filter({ has: page.locator('.prepared-result') }); await dialog.waitFor(); const preparedId = lastRun;
  await dialog.getByRole('button', { name: 'Cerrar resultado', exact: true }).click();
  for (const fault of ['offline', 'unknown-tool', 'invalid-id', 'timeout', 'semantic-id']) {
    mode = fault; const countBefore = calls; const sent = await ask(fault === 'semantic-id' ? 'Busca mi prompt para revisar acoplamiento entre módulos.' : 'Crea otra instrucción sintética: ' + fault);
    const completed = await sent.response(); assert.equal(completed.status(), 200);
    for (let n=0;n<200 && (await api('/nova/runs/'+lastRun)).data.state!=='FAILED';n++) await page.waitForTimeout(100);
    await page.getByRole('alert').waitFor({ timeout: 15000 }); assert.deepEqual(snapshot(), before); assert.equal(await page.getByRole('dialog').count(), 0);
    const run = (await api('/nova/runs/' + lastRun)).data; assert.equal(run.state, 'FAILED'); if (fault === 'unknown-tool') assert.equal(calls - countBefore, 2);
    if (fault === 'timeout') assert.equal(run.errorCode, 'TIMEOUT');
    await page.getByRole('button', { name: 'Ver resultado de NOVA', exact: true }).click(); await dialog.waitFor(); assert.match(await dialog.locator('.prepared-text').innerText(), /Explica una API/); await dialog.getByRole('button', { name: 'Cerrar resultado', exact: true }).click();
    pass(fault + ': preserves previous result and every source/version; no partial result or unauthorized tool');
  }
  mode = 'cancel'; await ask('Crea un prompt que detendré mientras genera.'); await page.waitForFunction(() => document.querySelector('[role="status"]')?.textContent?.includes('Preparando') || !!document.querySelector('.preparation-progress'));
  for (let n = 0; n < 50 && active === 0; n++) await page.waitForTimeout(50); assert.equal(active, 1);
  await page.getByRole('button', { name: 'Detener', exact: true }).click();
  for (let n = 0; n < 100 && active; n++) await page.waitForTimeout(50); assert.equal(active, 0);
  for (let n = 0; n < 100 && (await api('/nova/runs/' + lastRun)).data.state !== 'CANCELLED'; n++) await page.waitForTimeout(50);
  assert.deepEqual(snapshot(), before); assert.equal((await api('/nova/runs/' + lastRun)).data.state, 'CANCELLED'); pass('Mid-generation cancel disconnects the local model request and preserves the database');
  mode = 'healthy';
  const draft = (await api('/nova/drafts/' + preparedId)).data; const operationId = randomUUID();
  const execution = await api('/nova/drafts/' + preparedId + '/execute', { operationId, confirmed: true, expectedText: draft.compiledText }); assert.match(execution.data, /event: complete/);
  assert.equal((await api('/nova/drafts/' + preparedId + '/execute', { operationId, confirmed: true, expectedText: draft.compiledText })).response.status, 409);
  await api('/nova/runs/' + operationId, undefined, 'DELETE'); assert.equal((await api('/nova/drafts/' + preparedId + '/execute', { operationId, confirmed: true, expectedText: draft.compiledText })).response.status, 409); assert.equal(executions, 1); assert.deepEqual(snapshot(), before); pass('Execution retries and deleting diagnostics cannot replay an effect');
  assert.equal((await api('/nova/drafts/' + preparedId + '/save', { confirmed: false })).response.status, 400);
  assert.equal((await api('/nova/preferences', { preferences: { language: 'en', format: 'JSON', providerId: 'chatgpt' }, confirmed: false }, 'PUT')).response.status, 400);
  assert.equal((await api('/nova/preferences')).data.language, null); pass('Unconfirmed save and preference changes are rejected');
  const sessionId = randomUUID(); await api('/nova/sessions/' + sessionId + '/context', { promptId: variableSource.id, revision: 1 });
  const missingResponse = await api('/nova/prepare', { operationId: randomUUID(), sessionId, message: 'Completa este prompt.' });
  const missing = JSON.parse(missingResponse.data.split('event: complete\ndata: ')[1].split('\n\n')[0]); assert.equal(missing.state, 'NEEDS_INFORMATION');
  const filled = await api('/nova/drafts/' + missing.id + '/complete', { operationId: randomUUID(), values: { codigo: '<div>{{value}}</div>' } });
  assert.equal(filled.data.compiledText, 'Analiza <div>{{value}}</div>.');
  const reused = await api('/nova/prepare', { operationId: randomUUID(), sessionId, message: 'Completa este prompt con los datos que ya te proporcioné.' });
  const compiledAgain = JSON.parse(reused.data.split('event: complete\ndata: ')[1].split('\n\n')[0]); assert.equal(compiledAgain.compiledText, filled.data.compiledText); assert.deepEqual(snapshot(), before);
  pass('Completing a draft updates session values and preserves literal HTML/code without touching the source');
  mode = 'bound-search'; boundSourceId = variableSource.id; const boundSession = randomUUID();
  await api('/nova/sessions/' + boundSession + '/context', { promptId: variableSource.id, revision: 1 });
  const boundResponse = await api('/nova/prepare', { operationId: randomUUID(), sessionId: boundSession, message: 'Localiza la fuente seleccionada sin cambiarla.' });
  assert.match(boundResponse.data, /event: complete/);
  const boundDraft = JSON.parse(boundResponse.data.split('event: complete\ndata: ')[1].split('\n\n')[0]);
  assert.equal(boundDraft.sourceId, variableSource.id); assert.equal(boundDraft.templateText, variableSource.templateText);
  assert.ok(boundDraft.semantic.intentions.includes('SEARCH_PROMPT')); assert.ok(!boundDraft.semantic.intentions.includes('PROPOSE_ADAPTATION')); assert.deepEqual(snapshot(), before);
  pass('Searching an already bound real entity uses its actual title without inventing a query or modifying it');
  mode = 'source-workflow-conflict'; const conflictResponse = await api('/nova/prepare', { operationId: randomUUID(), sessionId: randomUUID(), message: 'Localiza Código de prueba sin cambiarlo.' });
  assert.match(conflictResponse.data, /event: complete/); const conflictDraft = JSON.parse(conflictResponse.data.split('event: complete\ndata: ')[1].split('\n\n')[0]);
  assert.equal(conflictDraft.sourceId, variableSource.id); assert.equal(conflictDraft.semantic.desiredOutcome, 'READ'); assert.equal(conflictDraft.templateText, variableSource.templateText); assert.deepEqual(snapshot(), before);
  pass('A source-free/find-saved contradiction gets one model reference correction before resolving a real repository ID');
  mode = 'unresolved-search'; const lookupResponse = await api('/nova/prepare', { operationId: randomUUID(), sessionId: randomUUID(), message: 'Localiza Código de prueba sin cambiarlo.' });
  assert.match(lookupResponse.data, /event: complete/); const lookupDraft = JSON.parse(lookupResponse.data.split('event: complete\ndata: ')[1].split('\n\n')[0]);
  assert.equal(lookupDraft.sourceId, null); assert.equal(lookupDraft.semantic.desiredOutcome, 'CLARIFICATION'); assert.equal((await api('/nova/runs/' + lookupDraft.id)).data.tools.length, 0); assert.deepEqual(snapshot(), before);
  pass('An unresolved model source reference asks before lookup instead of choosing an arbitrary record');
  mode = 'unbound-query'; const unboundResponse = await api('/nova/prepare', { operationId: randomUUID(), sessionId: randomUUID(), message: 'Convierte la instrucción anterior para un nuevo público.' });
  assert.match(unboundResponse.data, /event: complete/); const unboundDraft = JSON.parse(unboundResponse.data.split('event: complete\ndata: ')[1].split('\n\n')[0]);
  assert.equal(unboundDraft.sourceId, null); assert.equal(unboundDraft.semantic.desiredOutcome, 'CLARIFICATION'); assert.equal((await api('/nova/runs/' + unboundDraft.id)).data.tools.length, 0); assert.deepEqual(snapshot(), before);
  pass('An empty source descriptor asks before lookup even when the earlier model proposed a named-source role');
  for (const missingMode of ['missing-content-task','ungrounded-content-evidence','inconsistent-content-evidence']) {
  mode = missingMode; const missingTaskOperation = randomUUID(), effectsBeforeMissingTask = executions;
  const missingTaskResponse = await api('/nova/prepare', { operationId: missingTaskOperation, sessionId: randomUUID(), message: 'Prepara una instrucción; falta describir su tarea.' });
  assert.match(missingTaskResponse.data, /event: complete/); const missingTaskDraft = JSON.parse(missingTaskResponse.data.split('event: complete\ndata: ')[1].split('\n\n')[0]);
  assert.equal(missingTaskDraft.semantic.desiredOutcome, 'CLARIFICATION'); assert.ok(missingTaskDraft.semantic.validationNotes.includes(missingMode === 'missing-content-task' ? 'MISSING_TEXT_TASK' : 'UNGROUNDED_CONTENT_REQUEST'));
  assert.ok((await api('/nova/runs/' + missingTaskOperation)).data.tools.every(tool => tool.tool === 'ask_clarification')); assert.equal(executions, effectsBeforeMissingTask); assert.deepEqual(snapshot(), before);
  if (missingMode === 'inconsistent-content-evidence') assert.ok(inconsistentFeedbackObserved, 'One real schema correction must precede clarification');
  pass(missingMode + ': asks before generation and withholds the model-proposed executor');
  }
  mode = 'corrected-content-evidence'; const correctedCallsBefore = calls, correctedOperation = randomUUID();
  const correctedResponse = await api('/nova/prepare', { operationId: correctedOperation, sessionId: randomUUID(), sourcePromptId: source.id, sourceRevision: 1, message: 'Muestra la instrucción guardada sin cambiarla.' });
  assert.match(correctedResponse.data, /event: complete/); const correctedDraft = JSON.parse(correctedResponse.data.split('event: complete\ndata: ')[1].split('\n\n')[0]);
  assert.equal(correctedDraft.sourceId, source.id); assert.equal(correctedDraft.semantic.desiredOutcome, 'READ'); assert.equal(correctedDraft.templateText, source.templateText);
  assert.equal(calls - correctedCallsBefore, 3, 'Only the inconsistent content contract is repaired; workflow and generation are not replayed'); assert.deepEqual(snapshot(), before);
  pass('A concrete rejected-content response can be corrected once into a read-only plan without generation or source mutation');
  mode = 'corrected-authoring-evidence'; const authoringCallsBefore = calls;
  const authoringResponse = await api('/nova/prepare', { operationId: randomUUID(), sessionId: randomUUID(), message: 'Crea una instrucción independiente para explicar una API.' });
  assert.match(authoringResponse.data, /event: complete/); const authoringDraft = JSON.parse(authoringResponse.data.split('event: complete\ndata: ')[1].split('\n\n')[0]);
  assert.equal(authoringDraft.semantic.desiredOutcome, 'NEW_DRAFT'); assert.equal(authoringDraft.sourceId, null); assert.match(authoringDraft.templateText, /API/); assert.equal(calls - authoringCallsBefore, 6); assert.deepEqual(snapshot(), before);
  pass('Missing authoring evidence is repaired once against the literal request before any actual proposal generation');
  boundClauseBody = variableSource.templateText;
  for (const clauseMode of ['bound-semantic-clause','unsupported-content-clause']) {
    mode = clauseMode; const clauseResult = await api('/nova/interpret', { message: 'Que esta ficha se enfoque en auditar un módulo.', promptId: variableSource.id, revision: 1 });
    if (clauseMode === 'unsupported-content-clause') assert.equal(clauseResult.response.status, 400, 'A fabricated retained invariant still fails literal grounding');
    else { assert.equal(clauseResult.response.status, 200); assert.equal(clauseResult.data.semantic.resolvedTargetId, variableSource.id); assert.ok(clauseResult.data.semantic.intentions.includes('PROPOSE_ADAPTATION')); assert.deepEqual(clauseResult.data.semantic.constraints.literalClauses, []); assert.ok(clauseResult.data.semantic.validationNotes.includes('CONTENT_CLAUSES_REVIEWED_WITH_SOURCE')); }
    assert.deepEqual(snapshot(), before);
    pass(clauseMode + ': actual source determines clause scope before grounding; no invented invariant or source mutation');
  }
  mode = 'source-target-scopes'; const targetReview = await api('/nova/interpret', { message: 'Orienta Código de prueba a Rust.', promptId: variableSource.id, revision: 1 });
  assert.equal(targetReview.response.status, 200); assert.deepEqual(targetReview.data.semantic.constraints.targetTerms, ['Rust']); assert.deepEqual(snapshot(), before);
  pass('A source title is removed from content targets only after real source resolution, while the requested technical identifier survives');
  for (const copyMode of ['current-task-copy','context-inherited-copy','independent-task']) {
  const copySession = randomUUID(); await api('/nova/editor-drafts', { promptId: variableSource.id, revision: 1, title: variableSource.title, templateText: variableSource.templateText, values: {}, sessionId: copySession });
  await api('/nova/sessions/' + copySession + '/context', { promptId: source.id, revision: 1 }); mode = copyMode;
  const contextCopyResponse = await api('/nova/prepare', { operationId: randomUUID(), sessionId: copySession, message: copyMode === 'independent-task' ? 'Crea una instrucción independiente para describir un jardín.' : 'Prepara una nueva copia de la tarea actual sin reemplazarla.' });
  assert.match(contextCopyResponse.data, /event: complete/); const contextCopy = JSON.parse(contextCopyResponse.data.split('event: complete\ndata: ')[1].split('\n\n')[0]);
  assert.equal(contextCopy.semantic.resolvedTargetId, copyMode === 'independent-task' ? null : variableSource.id); assert.equal(contextCopy.semantic.targetReference, copyMode === 'independent-task' ? 'NONE' : 'PREVIOUS_RESULT'); assert.equal(contextCopy.sourceId, null); assert.equal(contextCopy.decision, 'CREATE_NEW'); assert.deepEqual(snapshot(), before);
  pass(copyMode + ': model-declared artifact basis binds only the real current task; independent creation excludes incidental selection');
  }
  for (const indexedMode of ['bound-ui-original','invalid-source-role']) {
    const indexSession = randomUUID(); await api('/nova/editor-drafts', { promptId: variableSource.id, revision: 1, title: variableSource.title, templateText: variableSource.templateText, values: {}, sessionId: indexSession });
    await api('/nova/sessions/' + indexSession + '/context', { promptId: source.id, revision: 1 }); mode = indexedMode; const indexOperation = randomUUID();
    const indexedResponse = await api('/nova/prepare', { operationId: indexOperation, sessionId: indexSession, message: 'Muestra el otro registro seleccionado sin cambiar su instrucción.' });
    if (indexedMode === 'invalid-source-role') { assert.match(indexedResponse.data, /event: error/); assert.equal((await api('/nova/runs/' + indexOperation)).data.tools.length, 0); }
    else { assert.match(indexedResponse.data, /event: complete/); const indexedDraft = JSON.parse(indexedResponse.data.split('event: complete\ndata: ')[1].split('\n\n')[0]); assert.equal(indexedDraft.sourceId, source.id); assert.equal(indexedDraft.templateText, source.templateText); }
    assert.deepEqual(snapshot(), before); pass(indexedMode === 'invalid-source-role' ? 'An out-of-catalog source role is rejected before any domain tool or mutation' : 'A typed model source role opens the actual separate UI selection without rewriting either source');
  } mode = 'healthy';
  await page.getByRole('button', { name: 'Datos y respaldos', exact: true }).click(); const settings = page.getByRole('dialog', { name: 'Tu biblioteca permanece contigo', exact: true });
  await settings.getByText('Preferencias de NOVA', { exact: true }).click(); await settings.getByLabel('Idioma de respuesta', { exact: true }).selectOption('en'); await settings.getByLabel('Formato preferido', { exact: true }).selectOption('JSON');
  assert.equal((await api('/nova/preferences')).data.language, null); await settings.getByRole('button', { name: 'Confirmar preferencias', exact: true }).click(); await settings.getByText('Preferencias confirmadas en este equipo.', { exact: true }).waitFor();
  assert.equal((await api('/nova/preferences')).data.language, 'en'); await settings.getByRole('button', { name: 'Eliminar preferencias', exact: true }).click(); await settings.getByText('Preferencias eliminadas.', { exact: true }).waitFor(); assert.equal((await api('/nova/preferences')).data.language, null);
  assert.equal(await settings.evaluate(e => e.scrollWidth > e.clientWidth + 1 || document.documentElement.scrollWidth > innerWidth + 1), false); await page.screenshot({ path: path.join(evidence, 'mobile-preferences.png'), fullPage: true }); pass('Mobile preferences are explicit, reviewable, deletable and preserve the library');
  assert.equal((await api('/prompts/' + source.id)).data.revision, 1); assert.deepEqual(externalRequests, []); assert.deepEqual(errors, []);
  fs.writeFileSync(path.join(evidence, 'acceptance.json'), JSON.stringify({ status: 'PASS', realQwen: false, mockedOllama: true, checks, errors, externalRequests, executions }, null, 2)); console.log('EVIDENCE', evidence);
})().catch(async error => { console.error(error); try { await page?.screenshot({ path: path.join(evidence, 'failure.png'), fullPage: true }); } catch {} fs.writeFileSync(path.join(evidence, 'acceptance.json'), JSON.stringify({ status: 'FAIL', realQwen: false, mockedOllama: true, checks, errors, error: error.stack }, null, 2)); process.exitCode = 1; }).finally(async () => { db?.close(); await browser?.close(); fake.closeAllConnections(); await new Promise(r => fake.close(r)); if (apiProcess && apiProcess.exitCode === null) { const exited = new Promise(r => apiProcess.once('exit', r)); apiProcess.kill(); await exited; } });
