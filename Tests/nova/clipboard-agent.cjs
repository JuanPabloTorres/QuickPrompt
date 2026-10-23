// Synthetic browser/API/SQLite acceptance. Real Qwen semantics are opt-in and
// separate from explicit UI commands. No private clipboard is read before our write.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process'), { createHash } = require('node:crypto');
const { webSha256,coreArtifacts } = require('./artifact-hashes.cjs');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
const temp = path.join(root, '.playwright/clipboard-agent-' + stamp), out = path.join(root, process.env.NOVA_COPY_EVIDENCE_ROOT || 'docs/evidence/adaptive-apply-20261021', String(stamp));
const build = path.resolve(root, process.env.NOVA_TEST_BUILD || '.playwright/adaptive-apply-20261021/api/QuickPrompt.Api.dll');
const web = path.resolve(root, process.env.NOVA_TEST_WEBROOT || '.playwright/adaptive-apply-20261021/web');
const real = process.argv.includes('--real-qwen'), origin = 'http://127.0.0.1:5243';
if(process.argv.includes('--action-workbench')&&!process.env.NOVA_COPY_SCENARIOS)
  process.env.NOVA_COPY_SCENARIOS='workbench_search,productive_cycle';
const agentOptions=JSON.parse(process.env.NOVA_AGENT_OPTIONS||'[]');
const coreOption=name=>agentOptions.find(a=>a.startsWith('--QuickPrompt:AdaptiveCore:'+name+'='))?.split('=').slice(1).join('=');
const coreEnabled=agentOptions.includes('--QuickPrompt:Agent:Runtime=adaptive-core');
const adaptFixture = process.env.NOVA_ADAPT_COPY_FIXTURE ? JSON.parse(fs.readFileSync(process.env.NOVA_ADAPT_COPY_FIXTURE,'utf8').replace(/^\uFEFF/,'')) : null;
if(adaptFixture)assert.ok(adaptFixture.syntheticOnly===true&&adaptFixture.schema==='quickprompt-adapt-copy-fixture/1'
  &&typeof adaptFixture.request==='string'&&adaptFixture.request.length>0&&typeof adaptFixture.source?.title==='string'
  &&typeof adaptFixture.source?.templateText==='string'&&Array.isArray(adaptFixture.requiredText)&&Array.isArray(adaptFixture.preservedText));
const headers = { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' };
fs.mkdirSync(temp, { recursive: true }); fs.mkdirSync(out, { recursive: true });
let server, browser, page, runId, log = '', lease, scenarioUnderTest = null;
const report = { status: 'RUNNING', syntheticOnly: true, oracleVersion: 'clipboard-task-outcomes/2', harnessSha256: createHash('sha256').update(fs.readFileSync(__filename)).digest('hex'), realQwen: real, selectedSemanticScenarios: real ? (process.env.NOVA_COPY_SCENARIOS||'prepare,actual').split(',') : [], checks: [], runs: [], errors: [], hashes: Object.fromEntries(['QuickPrompt.Api.dll','QuickPrompt.Application.dll','QuickPrompt.Infrastructure.dll'].map(file => [file, createHash('sha256').update(fs.readFileSync(path.join(path.dirname(build),file))).digest('hex')])) };
const save = () => fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2));
report.webSha256 = webSha256(web);
if(adaptFixture)report.mission={goalId:adaptFixture.goalId,fixtureSha256:createHash('sha256').update(fs.readFileSync(process.env.NOVA_ADAPT_COPY_FIXTURE)).digest('hex')};
if(adaptFixture?.missionOnly){assert.equal(process.env.NOVA_COPY_SCENARIOS,'adapt_copy');assert.ok(real);report.mission.onlyAdaptCopy=true;}
const check = name => { report.checks.push({ name, status: 'PASS' }); save(); console.log('PASS', name); };
async function until(read, timeout = 25000) { const end = Date.now()+timeout; while (Date.now()<end) { const value = await read(); if(value) return value; await new Promise(resolve=>setTimeout(resolve,100)); } throw Error('QA_TIMEOUT'); }
async function api(route, body) { const r = await fetch(origin+'/api/v1'+route,{method:body?'POST':'GET',headers,body:body?JSON.stringify(body):undefined}); assert.ok(r.ok,route+':'+r.status); return r.status===204?null:r.json(); }
// Each no-copy scenario owns a known public clipboard sentinel. Comparisons stay
// in the browser so an unrelated user's clipboard text cannot enter QA output.
async function clipboardGuard() {
  const sentinel='NOVA QA · no copiar · '+crypto.randomUUID();
  await page.evaluate(text=>navigator.clipboard.writeText(text),sentinel);
  return async()=>assert.ok(await page.evaluate(async text=>(await navigator.clipboard.readText())===text,sentinel),'Browser clipboard changed during this no-copy scenario');
}
async function assistant() {
  // Close the previous activity with its ordinary control before opening the
  // next action. Never force a click through overlapping popover content.
  const activity=page.getByRole('button',{name:'Cerrar actividad',exact:true});
  if(await activity.isVisible())await activity.click();
  const b=page.getByRole('button',{name:'Abrir asistente NOVA',exact:true});if(await b.isVisible())await b.click();
  // The launcher intentionally restores an expanded activity. Close it after
  // reopening as well, once its visibility event has reached the live panel.
  if(await activity.isVisible())await activity.click();
}
async function completion(pending, expectedStatus='COMPLETED', taskEvidence=null) {
  runId=(await(await pending).json()).runId; assert.ok(runId);
  let lastRuntime;
  const result=await until(async()=> {
    const v=await api('/nova/live/'+runId);
    const runtime=v.runtime;
    const key=JSON.stringify([v.run.status,runtime?.modelState,runtime?.activeTool]);
    if(key!==lastRuntime){
      report.runtimeSamples??=[];
      if(report.runtimeSamples.length<300)report.runtimeSamples.push({runId,observedAt:new Date().toISOString(),status:v.run.status,modelState:runtime?.modelState,activeTool:runtime?.activeTool});
      lastRuntime=key;save();
    }
    if(!report.workingScreenshot&&runtime?.modelState==='interpreting'){
      await page.screenshot({path:path.join(out,'agent-working.png'),fullPage:true});
      report.workingScreenshot='agent-working.png';save();
    }
    return [expectedStatus,'COMPLETED','FAILED','BLOCKED','CANCELLED'].includes(v.run.status)?v:null;
  },180000);
  report.runs.push({runId,taskEvidence,status:result.run.status,errorCode:result.run.errorCode,question:result.question,semantic:result.semantic,observation:result.observation,decisions:result.decisions});save();
  assert.equal(result.run.status,expectedStatus,JSON.stringify({status:result.run.status,error:result.run.errorCode,question:result.question}));if(expectedStatus!=='AWAITING_APPROVAL')runId=null;return result;
}
async function chooseQuickAction(label) {
  await assistant();
  const actions=page.getByRole('region',{name:'Acciones rápidas de NOVA',exact:true});
  const button=actions.getByRole('button',{name:label,exact:true});
  if(!await button.isVisible()) {
    const more=actions.getByRole('button',{name:'Más acciones de NOVA',exact:true});
    try { await more.click({trial:true}); }
    catch(error) {
      const activity=page.getByRole('button',{name:'Cerrar actividad',exact:true});
      if(!await activity.isVisible())throw error;
      await activity.click();
    }
    await more.click();
  }
  await button.click();const dialog=page.getByRole('dialog',{name:label,exact:true});await dialog.waitFor();return dialog;
}
async function quick(label,submit,fill,expectedStatus='COMPLETED') {
  const dialog=await chooseQuickAction(label);await fill(dialog);
  const pending=page.waitForResponse(r=>r.url()===origin+'/api/v1/nova/live'&&r.request().method()==='POST');
  await dialog.getByRole('button',{name:submit,exact:true}).click();return completion(pending,expectedStatus,{kind:'explicit-ui-command',label});
}
async function free(message, expectedStatus='COMPLETED') {
  await assistant();await page.getByRole('textbox',{name:'¿Qué quieres hacer hoy?',exact:true}).fill(message);
  const pending=page.waitForResponse(r=>r.url()===origin+'/api/v1/nova/live'&&r.request().method()==='POST');
  await page.getByRole('button',{name:'Preparar prompt',exact:true}).click();
  const result=await completion(pending,expectedStatus,{kind:'natural-request',scenario:scenarioUnderTest,request:message});
  if ((process.env.NOVA_AGENT_OPTIONS||'').includes('Runtime=adaptive-core')) assert.ok(result.semantic.validationNotes.includes('ADAPTIVE_CORE'), 'Actual Core composition must interpret the free request');
  return result;
}
(async()=>{
  if(real||process.argv.includes('--adaptive-core')) lease=fs.openSync(path.join(require('node:os').tmpdir(),'QuickPrompt.Nova.SemanticModel.lease'),'a+');
  if(real){const tags=await(await fetch('http://127.0.0.1:11434/api/tags')).json();report.modelDigest=tags.models.find(m=>m.name==='qwen3:8b')?.digest;assert.match(report.modelDigest||'',/^[a-f0-9]{64}$/);save();}
  if(coreEnabled){assert.ok(coreOption('SdkRoot')&&coreOption('Worker'),'Explicit Core SDK and worker required for runtime identity');
    report.coreArtifactsBefore=coreArtifacts(coreOption('SdkRoot'),coreOption('Worker'));save();}
  server=spawn('dotnet',[build,'--QuickPrompt:Port=5243','--QuickPrompt:DataPath='+path.join(temp,'synthetic.sqlite'),'--QuickPrompt:WebRoot='+web,...agentOptions],{windowsHide:true,stdio:['ignore','pipe','pipe']});
  server.stdout.on('data',b=>{log+=b;});server.stderr.on('data',b=>{log+=b;});
  await until(()=>{if(server.exitCode!==null)throw Error('API_EXITED');return log.includes('Now listening on: '+origin);});
  const source=await api('/prompts',{title:'QA · Copia verificable',templateText:'Explica <tema> sin inventar datos.'});
  const other=await api('/prompts',{title:'QA · Registro independiente',templateText:'Texto que debe conservarse.'});
  browser=await chromium.launch({channel:'msedge',headless:false});
  const context=await browser.newContext({viewport:{width:1366,height:950},permissions:['clipboard-read','clipboard-write']});page=await context.newPage();
  await context.addInitScript(()=>localStorage.setItem('nova-interface-v1',JSON.stringify({view:'cards',executionMode:'SAME_TAB',rememberDraft:false,agentSpeed:'fast'})));
  page.on('pageerror',e=>{report.errors.push(String(e));save();});await page.goto(origin);await page.bringToFront();
  // Broader release runs retain the existing explicit-copy controls. An isolated
  // mission executes only its own natural request and preservation checks.
  if(!adaptFixture?.missionOnly){
  // Begin from the library: the agent must resolve and open the actual source.
  const typed=await quick('Copiar con NOVA','Copiar ahora',async dialog=>{
    await dialog.getByRole('group',{name:'Elegir prompt',exact:true}).getByRole('button').filter({hasText:source.title}).click();
    await dialog.getByRole('textbox',{name:'tema',exact:true}).fill('energía solar');
  });
  const expected='Explica energía solar sin inventar datos.';
  assert.equal(typed.observation.clipboard.text,expected);assert.equal(typed.observation.clipboard.promptId,source.id);assert.equal(typed.observation.clipboard.revision,1);
  assert.equal(await page.evaluate(()=>navigator.clipboard.readText()),expected);
  const copied=await api('/prompts/'+source.id);assert.equal(copied.uses,1);assert.equal(copied.templateText,source.templateText);assert.equal(copied.revision,1);
  const history=await api('/history');assert.equal(history.items.length,1);assert.equal(history.items[0].text,expected);
  check('explicit-ui-choice-navigates-fills-copies-browser-clipboard-and-records-one-use');
  await page.screenshot({path:path.join(out,'copy-verified.png'),fullPage:true});
  const old=typed.observation.clipboard.operationId;
  await quick('Copiar con NOVA','Copiar ahora',d=>d.getByRole('textbox',{name:'tema',exact:true}).fill('energía eólica'));
  assert.equal(await page.evaluate(()=>navigator.clipboard.readText()),'Explica energía eólica sin inventar datos.');
  const marker=page.getByTestId('clipboard-receipt');assert.notEqual(await marker.getAttribute('data-operation-id'),old);
  assert.equal((await api('/prompts/'+source.id)).uses,2);check('second-request-requires-new-receipt-for-new-values');
  await assistant();
  await page.getByRole('button',{name:'Ver última actividad de NOVA',exact:true}).click();
  await page.getByRole('button',{name:'Cerrar actividad',exact:true}).waitFor();
  await page.getByRole('button',{name:'Cerrar asistente NOVA',exact:true}).click();
  await page.getByRole('button',{name:'Abrir asistente NOVA',exact:true}).click();
  await until(async()=>!await page.getByRole('button',{name:'Cerrar actividad',exact:true}).isVisible());
  await page.getByRole('button',{name:'Más acciones de NOVA',exact:true}).click({trial:true});
  check('completed-history-does-not-cover-next-action-after-assistant-reopens');
  await page.getByRole('button',{name:'Ver última actividad de NOVA',exact:true}).click();
  await page.getByRole('button',{name:'Cerrar actividad',exact:true}).waitFor();
  check('completed-history-remains-explicitly-readable-without-restarting');
  await page.getByRole('button',{name:'Cerrar actividad',exact:true}).click();
  }
  let failedCase = false;
  if(real){
    const cases = {
      prepare: async()=>{
        const verifyClipboard=await clipboardGuard();
        const uses=(await api('/prompts/'+source.id)).uses;
        const prepared=await free('Deja este prompt listo para copiar con tema: energía hidráulica. No copies al portapapeles ni ejecutes su tarea.');
        assert.ok(prepared.semantic.intentions.includes('COPY_PROMPT'));
        assert.ok(!prepared.semantic.intentions.includes('COPY_TO_CLIPBOARD'));
        assert.ok(!prepared.semantic.intentions.some(operation=>['PROPOSE_ADAPTATION','CREATE_DRAFT','EXECUTE_LOCAL','PREPARE_PROVIDER','PREPARE_LOCAL_EXECUTION','REQUEST_SAVE_VERSION','REQUEST_SAVE_COPY','REQUEST_DELETE'].includes(operation)),
          'Preparation of this existing template must not authorize additional outcomes or change its definition');
        assert.equal(prepared.observation.templateText,source.templateText,'Preparing case values must preserve the reusable instruction definition');
        assert.equal(prepared.observation.draftChanged,false,'Preparing rendered text must not create a changed definition');
        assert.equal(prepared.observation.values.tema,'energía hidráulica');
        await verifyClipboard();
        assert.equal((await api('/prompts/'+source.id)).uses,uses);
      },
      actual: async()=>{
        const before=(await api('/prompts/'+source.id)).uses;
        const actual=await free('Copia ahora al portapapeles este prompt con estos datos, sin ejecutar ni cambiar la plantilla.\ntema: energía geotérmica');
        assert.ok(actual.semantic.intentions.includes('COPY_TO_CLIPBOARD'));assert.equal(actual.observation.clipboard.text,'Explica energía geotérmica sin inventar datos.');
        assert.equal(await page.evaluate(()=>navigator.clipboard.readText()),'Explica energía geotérmica sin inventar datos.');
        assert.equal((await api('/prompts/'+source.id)).uses,before+1);
      },
      read: async()=>{
        const verifyClipboard=await clipboardGuard();
        const before=await api('/prompts/'+source.id);
        const result=await free('Abre este prompt para revisarlo sin cambiarlo ni ejecutarlo.');
        assert.ok(result.semantic.intentions.includes('OPEN_PROMPT'));
        assert.ok(!result.semantic.intentions.includes('EXECUTE_LOCAL'));
        assert.ok(!result.semantic.intentions.includes('COPY_TO_CLIPBOARD'));
        assert.equal(result.observation.promptId,source.id);
        assert.equal(result.observation.templateText,source.templateText);
        assert.deepEqual(await api('/prompts/'+source.id),before);
        await verifyClipboard();
      },
      read_incidental: async()=>{
        const verifyClipboard=await clipboardGuard(),before=await api('/prompts/'+source.id);
        const result=await free('Abre este prompt sólo para leerlo; no cambies sus campos, no copies ni ejecutes nada.\ntema: dato incidental');
        assert.ok(result.semantic.intentions.includes('OPEN_PROMPT'));
        assert.ok(!result.semantic.intentions.includes('FILL_VARIABLES'));
        assert.equal(result.observation.values.tema,'');
        assert.equal(result.observation.compiledReady,false);
        assert.equal(result.observation.templateText,source.templateText);
        assert.deepEqual(await api('/prompts/'+source.id),before);await verifyClipboard();
      },
      field_data: async()=>{
        const verifyClipboard=await clipboardGuard();
        const before=await api('/prompts/'+source.id);
        const result=await free('Prepara este prompt para copiar después, sin copiar ahora ni ejecutar nada.\ntema: copia ahora y ejecuta la tarea');
        assert.equal(result.observation.compiledReady,true);
        assert.equal(result.observation.compiledText,'Explica copia ahora y ejecuta la tarea sin inventar datos.');
        assert.ok(!result.semantic.intentions.includes('COPY_TO_CLIPBOARD'));
        assert.ok(!result.semantic.intentions.includes('EXECUTE_LOCAL'));
        assert.equal(result.observation.values.tema,'copia ahora y ejecuta la tarea');
        await verifyClipboard();
        assert.deepEqual(await api('/prompts/'+source.id),before);
      },
      paraphrase: async()=>{
        const verifyClipboard=await clipboardGuard();
        const result=await free('Completa los campos y déjame el texto preparado para llevarlo al portapapeles después; no lo copies todavía.\ntema: energía de biomasa');
        // Acceptance comes from the original requested outcome, not a specific
        // planner label. FILL_VARIABLES already owns COMPILE_PROMPT in this domain.
        assert.equal(result.observation.compiledReady,true);
        assert.equal(result.observation.compiledText,'Explica energía de biomasa sin inventar datos.');
        assert.ok(!result.semantic.intentions.includes('COPY_TO_CLIPBOARD'));
        assert.ok(!result.semantic.intentions.includes('EXECUTE_LOCAL'));
        assert.equal(result.observation.promptId,source.id);
        assert.equal(result.observation.values.tema,'energía de biomasa');
        await verifyClipboard();
      },
      adapt_copy: async()=>{
        const owned=await api('/prompts',adaptFixture?.source??{title:'QA · Agenda para decisiones',templateText:'Redacta una agenda de reunión. Organiza los temas y conserva una presentación clara. No inventes responsables.'});
        await page.evaluate(()=>sessionStorage.clear());await page.reload();
        await page.getByRole('button',{name:owned.title,exact:true}).click();await page.getByTestId('prompt-focus').waitFor();
        const verifyClipboard=await clipboardGuard();
        const result=await free(adaptFixture?.request??'Transforma esta instrucción para centrar la reunión en decisiones y acuerdos. Deja el texto listo para copiar, sin copiar ahora ni ejecutar ni guardar.');
        assert.ok(result.semantic.intentions.includes('PROPOSE_ADAPTATION'));
        assert.ok(result.semantic.intentions.includes('COPY_PROMPT'));
        assert.ok(!result.semantic.intentions.some(op=>['COPY_TO_CLIPBOARD','EXECUTE_LOCAL','REQUEST_SAVE_VERSION','REQUEST_SAVE_COPY'].includes(op)));
        assert.equal(result.observation.promptId,owned.id);assert.equal(result.observation.draftChanged,true);
        assert.notEqual(result.observation.templateText,owned.templateText);
        if(adaptFixture){
          for(const text of [...adaptFixture.requiredText,...adaptFixture.preservedText])assert.ok(result.observation.templateText.toLowerCase().includes(text.toLowerCase()),'Adapted instruction must preserve requested content: '+text);
          for(const [name,value] of Object.entries(adaptFixture.values??{}))assert.equal(result.observation.values[name],value);
        }else{
          assert.match(result.observation.templateText,/decisi[oó]n|decisiones/i);assert.match(result.observation.templateText,/acuerd/i);
          assert.match(result.observation.templateText,/no inventes responsables/i);
        }
        assert.equal(result.observation.compiledReady,true);
        let expectedCompiled=result.observation.templateText;
        for(const [name,value] of Object.entries(adaptFixture?.values??{}))expectedCompiled=expectedCompiled.replaceAll('<'+name+'>',value);
        assert.equal(result.observation.compiledText,expectedCompiled);
        assert.equal(await page.getByTestId('compiled-preview').innerText(),result.observation.compiledText);
        assert.deepEqual(await api('/prompts/'+owned.id),owned);await verifyClipboard();
        await page.screenshot({path:path.join(out,'adapt-copy-verified.png'),fullPage:true});
      },
      provider_prepare: async()=>{
        const verifyClipboard=await clipboardGuard(),before=await api('/prompts/'+source.id);
        const result=await free('Prepara este prompt para Claude sin enviarlo ni cambiar su plantilla.\ntema: energía geotérmica');
        assert.ok(result.semantic.intentions.includes('PREPARE_PROVIDER'));assert.equal(result.semantic.providerId,'claude');
        assert.ok(!result.semantic.intentions.some(op=>['PROPOSE_ADAPTATION','CREATE_DRAFT','COPY_TO_CLIPBOARD','EXECUTE_LOCAL','REQUEST_SAVE_VERSION','REQUEST_SAVE_COPY'].includes(op)));
        assert.equal(result.observation.promptId,source.id);assert.equal(result.observation.providerOpen,true);
        assert.equal(result.observation.providerId,'claude');assert.equal(await page.locator('#prepared-provider').inputValue(),'claude');
        assert.equal(result.observation.compiledText,'Explica energía geotérmica sin inventar datos.');
        assert.equal(result.observation.templateText,source.templateText);assert.equal(result.observation.draftChanged,false);
        assert.deepEqual(await api('/prompts/'+source.id),before);await verifyClipboard();
        await page.screenshot({path:path.join(out,'claude-prepared-verified.png'),fullPage:true});
      },
      create_literal: async()=>{
        const verifyClipboard=await clipboardGuard();
        const before=await api('/prompts?size=100');
        const result=await free('Crea un prompt nuevo con el texto literal: «Responde únicamente LISTO». Déjalo como borrador sin guardarlo, copiarlo ni ejecutarlo.');
        assert.ok(result.semantic.intentions.includes('CREATE_DRAFT'));
        assert.equal(result.observation.mode,'create');
        assert.equal(result.observation.templateText,'Responde únicamente LISTO');
        assert.deepEqual(await api('/prompts?size=100'),before);
        await verifyClipboard();
      },
      literal_unicode: async()=>{
        const verifyClipboard=await clipboardGuard();
        const before=await api('/prompts?size=100');
        const literal='Primera línea.\nSegunda línea 🪴';
        const result=await free('Crea un prompt con este cuerpo completo literal: «'+literal+'». Déjalo como borrador; no lo guardes, copies ni ejecutes.');
        assert.ok(result.semantic.intentions.includes('CREATE_DRAFT'));
        assert.equal(result.observation.templateText,literal);
        assert.deepEqual(await api('/prompts?size=100'),before);
        await verifyClipboard();
      },
      named_source: async()=>{
        const named=await api('/prompts',{title:'QA · Otra fuente',templateText:'Hola <persona>.'});
        const result=await free('Prepara para copiar el prompt «QA · Otra fuente», sin copiar al portapapeles ni ejecutar.\npersona: Lucía');
        assert.equal(result.observation.compiledReady,true);
        assert.equal(result.observation.promptId,named.id);
        assert.equal(result.observation.values.persona,'Lucía');
        assert.equal(result.observation.compiledText,'Hola Lucía.');
        assert.equal((await api('/prompts/'+named.id)).uses,0);
      },
      save_literal: async()=>{
        const before=await api('/prompts?size=100');
        const verifyClipboard=await clipboardGuard();
        const review=await free('Crea un prompt nuevo con el cuerpo completo literal «Responde únicamente LISTO» y solicita mi aprobación para guardarlo como una copia nueva. No lo guardes antes de mi aprobación, ni lo copies ni lo ejecutes.','AWAITING_APPROVAL');
        assert.equal(review.proposal.promptId,null);
        assert.equal(review.observation.templateText,'Responde únicamente LISTO');
        assert.deepEqual(await api('/prompts?size=100'),before);
        await verifyClipboard();
        await page.getByRole('button',{name:'Revisar y guardar copia',exact:true}).click();
        const approval=page.getByRole('dialog',{name:'Guardar copia separada',exact:true});
        await approval.waitFor();
        assert.equal(await approval.locator('.prepared-text').innerText(),'Responde únicamente LISTO');
        await approval.screenshot({path:path.join(out,'save-approval.png')});
        assert.deepEqual(await api('/prompts?size=100'),before);
        await approval.getByRole('button',{name:'Confirmar guardado de esta copia',exact:true}).click();
        const saved=await until(async()=>{const view=await api('/nova/live/'+review.run.runId);return ['COMPLETED','FAILED','BLOCKED','CANCELLED'].includes(view.run.status)?view:null;},30000);
        assert.equal(saved.run.status,'COMPLETED');
        runId=null;
        const after=await api('/prompts?size=100');
        assert.equal(after.total,before.total+1);
        const added=after.items.filter(item=>!before.items.some(previous=>previous.id===item.id));
        assert.equal(added.length,1);
        const record=await api('/prompts/'+added[0].id);
        assert.equal(record.templateText,'Responde únicamente LISTO');
        assert.equal(record.revision,1);
        await verifyClipboard();
        const closed=server;closed.kill();await new Promise(resolve=>closed.once('exit',resolve));
        server=spawn('dotnet',[build,'--QuickPrompt:Port=5243','--QuickPrompt:DataPath='+path.join(temp,'synthetic.sqlite'),'--QuickPrompt:WebRoot='+web,...JSON.parse(process.env.NOVA_AGENT_OPTIONS||'[]')],{windowsHide:true,stdio:['ignore','pipe','pipe']});
        server.stdout.on('data',bytes=>{log+=bytes;});server.stderr.on('data',bytes=>{log+=bytes;});
        await until(async()=>{try{return(await fetch(origin+'/health')).ok;}catch{return false;}});
        assert.deepEqual(await api('/prompts/'+record.id),record);
        await page.evaluate(()=>sessionStorage.clear());await page.reload();await page.getByTestId('library-ready').waitFor();
        await page.getByRole('button',{name:record.title,exact:true}).click();
        await page.getByTestId('prompt-focus').waitFor();
        assert.equal(await page.getByTestId('compiled-preview').innerText(),'Responde únicamente LISTO');
        report.runs.push({runId:review.run.runId,status:saved.run.status,persistence:{id:record.id,revision:record.revision,templateText:record.templateText,reopenedAfterRestart:true}});save();
      },
      execute: async()=>{
        const executable=await api('/prompts',{title:'QA · Ejecución local verificable',templateText:'Responde exactamente con la palabra LISTO, sin signos, explicación ni formato.'});
        await page.evaluate(()=>sessionStorage.clear());await page.reload();
        await page.getByRole('button',{name:executable.title,exact:true}).click();await page.getByTestId('prompt-focus').waitFor();
        const result=await free('Ejecuta este prompt con Qwen local ahora, sin guardarlo ni copiarlo.');
        assert.ok(result.semantic.intentions.includes('EXECUTE_LOCAL'));
        assert.equal(result.observation.responseComplete,true);
        assert.equal(result.observation.response,'LISTO');
        assert.equal((await api('/prompts/'+executable.id)).revision,1);
      },
      execute_values: async()=>{
        const executable=await api('/prompts',{title:'QA · Ejecución con valores',templateText:'Responde exactamente con la palabra <respuesta>, sin signos, explicación ni formato.'});
        await page.evaluate(()=>sessionStorage.clear());await page.reload();
        await page.getByRole('button',{name:executable.title,exact:true}).click();await page.getByTestId('prompt-focus').waitFor();
        const result=await free('Completa los valores y ejecuta este prompt con Qwen local, sin guardar ni copiar.\nrespuesta: LISTO');
        assert.ok(result.semantic.intentions.includes('EXECUTE_LOCAL'));
        assert.equal(result.observation.values.respuesta,'LISTO');
        assert.equal(result.observation.compiledText,'Responde exactamente con la palabra LISTO, sin signos, explicación ni formato.');
        assert.equal(result.observation.responseComplete,true);assert.equal(result.observation.response,'LISTO');
        assert.equal((await api('/prompts/'+executable.id)).revision,1);
      },
      search_library: async()=>{
        await page.evaluate(()=>sessionStorage.clear());await page.reload();await page.getByTestId('library-ready').waitFor();
        const before=await api('/prompts?size=100');const verifyClipboard=await clipboardGuard();
        const result=await free('Busca y abre el prompt «QA · Registro independiente» para leerlo, sin modificarlo, copiarlo ni ejecutarlo.');
        assert.equal(result.observation.promptId,other.id);
        assert.equal(result.observation.templateText,other.templateText);
        assert.ok(result.semantic.intentions.includes('OPEN_PROMPT'));
        assert.deepEqual(await api('/prompts?size=100'),before);await verifyClipboard();
      },
      update_version: async()=>{
        const owned=await api('/prompts',{title:'QA · Actualización controlada',templateText:'Explica <tema> sin inventar datos.'});
        await page.evaluate(()=>sessionStorage.clear());await page.reload();
        await page.getByRole('button',{name:owned.title,exact:true}).click();await page.getByTestId('prompt-focus').waitFor();
        const verifyClipboard=await clipboardGuard();
        const review=await free('Reemplaza el cuerpo de este prompt por el texto literal completo «Explica <tema> en una frase sin inventar datos.» y solicita mi aprobación para guardar una nueva versión del mismo registro. No lo guardes antes de aprobar, ni lo copies ni lo ejecutes.','AWAITING_APPROVAL');
        assert.ok(review.semantic.intentions.includes('PROPOSE_ADAPTATION'));
        assert.ok(review.semantic.intentions.includes('REQUEST_SAVE_VERSION'));
        assert.equal(review.proposal.promptId,owned.id);assert.deepEqual(await api('/prompts/'+owned.id),owned);
        await page.getByRole('button',{name:'Revisar y guardar versión',exact:true}).click();
        const approval=page.getByRole('dialog',{name:'Confirmar nueva versión',exact:true});await approval.waitFor();
        assert.equal(await approval.locator('.prepared-text').innerText(),'Explica <tema> en una frase sin inventar datos.');
        await approval.getByRole('button',{name:'Confirmar guardado de esta versión',exact:true}).click();
        const saved=await until(async()=>{const view=await api('/nova/live/'+review.run.runId);return ['COMPLETED','FAILED','BLOCKED','CANCELLED'].includes(view.run.status)?view:null;},30000);
        assert.equal(saved.run.status,'COMPLETED');runId=null;
        const after=await api('/prompts/'+owned.id);assert.equal(after.id,owned.id);assert.equal(after.revision,2);
        assert.equal(after.templateText,'Explica <tema> en una frase sin inventar datos.');await verifyClipboard();
        report.runs.push({runId:review.run.runId,status:saved.run.status,persistence:{id:after.id,revision:after.revision,templateText:after.templateText}});save();
      },
      reject_update: async()=>{
        const owned=await api('/prompts',{title:'QA · Rechazo sin cambios',templateText:'Explica <tema> sin inventar datos.'});
        await page.evaluate(()=>sessionStorage.clear());await page.reload();
        await page.getByRole('button',{name:owned.title,exact:true}).click();await page.getByTestId('prompt-focus').waitFor();
        const review=await free('Propón una versión más breve de este prompt y pide mi aprobación para actualizar el mismo registro. No guardes nada antes de mi aprobación.','AWAITING_APPROVAL');
        assert.deepEqual(await api('/prompts/'+owned.id),owned);
        await page.getByRole('button',{name:'Revisar y guardar versión',exact:true}).click();
        const approval=page.getByRole('dialog',{name:'Confirmar nueva versión',exact:true});await approval.waitFor();
        await approval.getByRole('button',{name:'Rechazar propuesta',exact:true}).click();
        const rejected=await until(async()=>{const view=await api('/nova/live/'+review.run.runId);return ['COMPLETED','FAILED','BLOCKED','CANCELLED'].includes(view.run.status)?view:null;},30000);
        assert.equal(rejected.run.status,'CANCELLED');runId=null;assert.deepEqual(await api('/prompts/'+owned.id),owned);
      },
      delete_confirm: async()=>{
        const owned=await api('/prompts',{title:'QA · Borrado confirmado de fixture',templateText:'Contenido sintético desechable.'});
        await page.evaluate(()=>sessionStorage.clear());await page.reload();
        const review=await free('Elimina el prompt guardado «QA · Borrado confirmado de fixture» de mi biblioteca; muéstrame la confirmación antes de borrarlo.','AWAITING_APPROVAL');
        assert.ok(review.semantic.intentions.includes('REQUEST_DELETE'));assert.equal(review.deletion.promptId,owned.id);
        assert.deepEqual(await api('/prompts/'+owned.id),owned);
        const approval=page.getByRole('dialog',{name:'¿Eliminar este prompt?',exact:true});await approval.waitFor();
        await until(()=>approval.getByRole('button',{name:'Eliminar definitivamente',exact:true}).isEnabled());
        await approval.getByRole('button',{name:'Eliminar definitivamente',exact:true}).click();
        const deleted=await until(async()=>{const view=await api('/nova/live/'+review.run.runId);return ['COMPLETED','FAILED','BLOCKED','CANCELLED'].includes(view.run.status)?view:null;},30000);
        assert.equal(deleted.run.status,'COMPLETED');assert.equal(deleted.run.effectCommitted,true);runId=null;
        assert.equal((await fetch(origin+'/api/v1/prompts/'+owned.id)).status,404);assert.deepEqual(await api('/prompts/'+other.id),other);
        await page.evaluate(()=>sessionStorage.clear());await page.reload();await page.getByTestId('library-ready').waitFor();
        assert.equal(await page.getByRole('button',{name:owned.title,exact:true}).count(),0);
      },
      workbench_search: async()=>{
        const target=await api('/prompts',{title:'QA · Seguimiento de proyectos',templateText:'Resume el avance semanal de <equipo>. Enumera logros, riesgos y próximos pasos. No inventes cifras.'});
        const before=await api('/prompts?size=100'), verifyClipboard=await clipboardGuard();
        await page.reload();await page.getByTestId('library-ready').waitFor();
        assert.equal(await page.getByRole('textbox',{name:'¿Qué quieres hacer hoy?',exact:true}).count(),0);
        const description='Necesito la instrucción que resume el progreso semanal de un equipo y permite revisar los riesgos pendientes.';
        const started=page.waitForRequest(r=>r.url()===origin+'/api/v1/nova/live'&&r.method()==='POST');
        const found=await quick('Buscar por significado','Buscar con IA',dialog=>dialog.getByRole('textbox',{name:'¿Qué prompt necesitas?',exact:true}).fill(description));
        const request=(await started).postDataJSON();
        assert.deepEqual(request.command,{operation:'SEARCH_PROMPT',content:description});
        assert.equal(request.message,description);assert.equal(request.sourceId,undefined);
        assert.equal(found.semantic.query,description);
        assert.ok(found.semantic.validationNotes.includes('EXPLICIT_UI_COMMAND'));
        // The immutable domain contract is covered by ActionWorkbenchTests.
        // Public semantic DTOs expose operations, not internal contract objects.
        assert.deepEqual(new Set(found.semantic.intentions),new Set(['SEARCH_PROMPT','RESOLVE_PROMPT']));
        assert.equal(found.observation.promptId,target.id);
        assert.ok(!found.semantic.intentions.some(op=>['EXECUTE_LOCAL','COPY_TO_CLIPBOARD','CREATE_DRAFT','PROPOSE_ADAPTATION','REQUEST_DELETE','REQUEST_SAVE_COPY','REQUEST_SAVE_VERSION'].includes(op)));
        assert.deepEqual(await api('/prompts?size=100'),before);await verifyClipboard();
        report.workbenchSearch={description,requestCommand:request.command,sourceId:target.id,observedId:found.observation.promptId};save();
        await page.screenshot({path:path.join(out,'workbench-semantic-search.png'),fullPage:true});
        check('workbench-semantic-search-preserves-original-query-finds-meaning-and-acquires-no-write-effects');
      },
      productive_cycle: async()=>{
        // One additional user journey, not a replacement for the frozen release oracles.
        const before=await api('/prompts?size=100'), verifyClipboard=await clipboardGuard();
        const body='Extrae las tareas pendientes de <notas>. No inventes datos.';
        const created=await quick('Crear prompt','Crear con NOVA',dialog=>dialog.getByRole('textbox',{name:'¿Qué prompt quieres crear?',exact:true}).fill('Crea un prompt con este cuerpo completo literal: «'+body+'».'),'AWAITING_APPROVAL');
        assert.equal(created.observation.templateText,body);
        assert.deepEqual(await api('/prompts?size=100'),before);
        const editor=page.getByTestId('prompt-editor');await editor.waitFor();
        await page.getByRole('button',{name:'Revisar y guardar copia',exact:true}).click();
        const createApproval=page.getByRole('dialog',{name:'Guardar copia separada',exact:true});await createApproval.waitFor();
        assert.equal(await createApproval.locator('.prepared-text').innerText(),body);
        await createApproval.getByRole('button',{name:'Confirmar guardado de esta copia',exact:true}).click();
        await until(async()=>(await api('/nova/live/'+created.run.runId)).run.status==='COMPLETED',30000);runId=null;
        await page.getByTestId('prompt-focus').waitFor();
        const id=await page.getByTestId('prompt-focus').getAttribute('data-prompt-id');
        const saved=await api('/prompts/'+id);assert.equal(saved.templateText,body);assert.equal(saved.revision,1);
        await page.evaluate(()=>sessionStorage.clear());await page.reload();await page.getByTestId('library-ready').waitFor();
        const opened=await quick('Buscar y abrir','Abrir con NOVA',async dialog=>{
          await dialog.getByRole('textbox',{name:'¿Qué prompt?',exact:true}).fill(saved.title);
          await dialog.getByRole('group',{name:'Elegir prompt',exact:true}).getByRole('button').filter({hasText:saved.title}).click();
        });
        assert.equal(opened.observation.promptId,id);assert.deepEqual(await api('/prompts/'+id),saved);
        check('productive-save-reload-find-and-open-same-record');
        const improved=await quick('Actualizar prompt','Preparar cambios',dialog=>dialog.getByRole('textbox',{name:'¿Qué quieres cambiar?',exact:true}).fill('Organiza las tareas extraídas en una tabla con las columnas Tarea y Responsable. Usa solo los responsables mencionados en las notas; si falta uno, escribe No indicado. Conserva la prohibición de inventar datos y la entrada <notas>.'),'AWAITING_APPROVAL');
        assert.ok(improved.semantic.intentions.includes('PROPOSE_ADAPTATION'));
        assert.notEqual(improved.observation.templateText,body);assert.match(improved.observation.templateText,/Tarea/i);assert.match(improved.observation.templateText,/Responsable/i);
        assert.match(improved.observation.templateText,/<notas>/);assert.match(improved.observation.templateText,/No inventes datos\./);
        assert.deepEqual(await api('/prompts/'+id),saved);
        await page.getByRole('button',{name:'Revisar y guardar versión',exact:true}).click();
        const updateApproval=page.getByRole('dialog',{name:'Confirmar nueva versión',exact:true});await updateApproval.waitFor();
        assert.equal(await updateApproval.locator('.prepared-text').innerText(),improved.observation.templateText);
        await updateApproval.getByRole('button',{name:'Confirmar guardado de esta versión',exact:true}).click();
        await until(async()=>(await api('/nova/live/'+improved.run.runId)).run.status==='COMPLETED',30000);runId=null;
        await page.getByTestId('prompt-focus').waitFor();
        const updated=await api('/prompts/'+id);assert.equal(updated.revision,2);assert.equal(updated.templateText,improved.observation.templateText);
        check('productive-improvement-reviewed-and-saved-as-second-version');
        const notes='Lucía debe enviar el informe. Mateo debe revisar el presupuesto.';
        const applied=await quick('Ejecutar con IA local','Ejecutar ahora',dialog=>dialog.getByRole('textbox',{name:'notas',exact:true}).fill(notes));
        assert.ok(applied.semantic.intentions.includes('EXECUTE_LOCAL'));assert.equal(applied.observation.values.notas,notes);
        assert.equal(applied.observation.responseComplete,true);
        const answer=applied.observation.response;assert.equal(typeof answer,'string');
        assert.match(answer,/Tarea/i);assert.match(answer,/Responsable/i);assert.match(answer,/Lucía/);assert.match(answer,/informe/i);assert.match(answer,/Mateo/);assert.match(answer,/presupuesto/i);
        const afterUse=await api('/prompts/'+id);assert.equal(afterUse.revision,2);assert.equal(afterUse.templateText,updated.templateText);
        report.productiveOutcome={syntheticOnly:true,promptId:id,revision:2,notes,response:answer};save();
        await page.screenshot({path:path.join(out,'productive-response.png'),fullPage:true});
        check('productive-local-execution-returns-both-tasks-and-responsible-people');
        const resultDialog=page.getByRole('dialog',{name:'Usa tu prompt preparado',exact:true});
        await resultDialog.getByRole('button',{name:'Cerrar diálogo',exact:true}).click();
        await resultDialog.waitFor({state:'hidden'});
        const remove=await chooseQuickAction('Eliminar prompt');
        await remove.getByRole('button',{name:'Revisar eliminación',exact:true}).click();
        const approval=page.getByRole('dialog',{name:'¿Eliminar este prompt?',exact:true});await approval.waitFor();
        assert.equal((await api('/prompts/'+id)).id,id);
        await approval.getByRole('button',{name:'Eliminar definitivamente',exact:true}).click();
        await until(async()=>(await fetch(origin+'/api/v1/prompts/'+id)).status===404);
        await page.evaluate(()=>sessionStorage.clear());await page.reload();await page.getByTestId('library-ready').waitFor();
        assert.equal(await page.getByRole('button',{name:saved.title,exact:true}).count(),0);
        assert.deepEqual(await api('/prompts/'+other.id),other);await verifyClipboard();
        check('productive-confirmed-delete-preserves-independent-record-and-clipboard');
      },
      core_adaptation_chain: async()=>{
        const owned=await api('/prompts',{title:'QA · Flujo de revisión contextual',templateText:'Redacta un resumen del avance semanal de <equipo>. Describe los logros alcanzados, los riesgos observados y los próximos pasos. Conserva una presentación clara y ordenada para facilitar la revisión del trabajo. No inventes cifras.'});
        await page.evaluate(()=>sessionStorage.clear());await page.reload();
        await page.getByRole('button',{name:owned.title,exact:true}).click();await page.getByTestId('prompt-focus').waitFor();
        const verifyClipboard=await clipboardGuard();
        const concise=await free('Quiero una versión más concisa, sin perder la prohibición de inventar cifras.');
        assert.ok(concise.semantic.intentions.includes('PROPOSE_ADAPTATION'));
        assert.ok(!concise.semantic.intentions.includes('COPY_TO_CLIPBOARD'));
        assert.ok(!concise.semantic.intentions.includes('FILL_VARIABLES'));
        assert.ok(concise.observation.templateText.length<owned.templateText.length);
        assert.deepEqual(await api('/prompts/'+owned.id),owned);await verifyClipboard();
        await page.screenshot({path:path.join(out,'core-concise.png'),fullPage:true});
        const previous=await free('Usa la propuesta anterior y enfócala en obstáculos del equipo.');
        assert.ok(previous.semantic.intentions.includes('PROPOSE_ADAPTATION'));
        assert.ok(!previous.semantic.intentions.includes('FILL_VARIABLES'));
        assert.ok(!previous.semantic.intentions.includes('COPY_TO_CLIPBOARD'));
        assert.notEqual(previous.observation.templateText,concise.observation.templateText);
        assert.match(previous.observation.templateText,/obst[aá]cul|impediment|bloque/i);
        assert.equal(previous.observation.promptId,owned.id);
        assert.deepEqual(await api('/prompts/'+owned.id),owned);await verifyClipboard();
        await page.screenshot({path:path.join(out,'core-previous-draft.png'),fullPage:true});
      },
      core_create: async()=>{
        const before=await api('/prompts?size=100');const verifyClipboard=await clipboardGuard();
        const created=await free('Crea un prompt reutilizable para diseñar actividades de lectura infantil.');
        assert.ok(created.semantic.intentions.includes('CREATE_DRAFT'));
        assert.ok(!created.semantic.intentions.includes('COPY_PROMPT'));
        assert.ok(!created.semantic.intentions.includes('COPY_TO_CLIPBOARD'));
        assert.equal(created.observation.mode,'create');assert.equal(created.observation.promptId,null);
        assert.match(created.observation.templateText,/lectur/i);
        assert.deepEqual(await api('/prompts?size=100'),before);await verifyClipboard();
        await page.screenshot({path:path.join(out,'core-created-instruction.png'),fullPage:true});
      },
      core_missing: async()=>{
        await page.evaluate(()=>sessionStorage.clear());await page.reload();await page.getByTestId('library-ready').waitFor();
        const before=await api('/prompts?size=100');const verifyClipboard=await clipboardGuard();
        const missing=await free('Hazlo más claro.','BLOCKED');
        assert.ok(missing.semantic.intentions.includes('ASK_CLARIFICATION'));
        assert.ok(missing.question?.trim().length>0);
        assert.ok(!missing.semantic.intentions.includes('EXPLAIN'));
        assert.deepEqual(await api('/prompts?size=100'),before);await verifyClipboard();
        await page.screenshot({path:path.join(out,'core-essential-clarification.png'),fullPage:true});
      },
      core_english: async()=>{
        const before=await api('/prompts/'+source.id);const verifyClipboard=await clipboardGuard();
        const adapted=await free('Make this instruction suitable for a board briefing, but keep the saved original.');
        assert.ok(adapted.semantic.intentions.includes('PROPOSE_ADAPTATION'));
        assert.ok(!adapted.semantic.intentions.includes('COPY_PROMPT'));
        assert.ok(!adapted.semantic.intentions.includes('COPY_TO_CLIPBOARD'));
        assert.notEqual(adapted.observation.templateText,source.templateText);
        assert.match(adapted.observation.templateText,/board|junta|consejo|directiv/i);
        assert.deepEqual(await api('/prompts/'+source.id),before);await verifyClipboard();
        await page.screenshot({path:path.join(out,'core-board-instruction.png'),fullPage:true});
      }
    };
    for(const name of (process.env.NOVA_COPY_SCENARIOS||'prepare,actual').split(',')){
      scenarioUnderTest=name;
      // Each independent task starts with a fresh conversation and the same real source.
      await page.evaluate(()=>sessionStorage.clear());await page.reload();
      await page.getByRole('button',{name:source.title,exact:true}).click();await page.getByTestId('prompt-focus').waitFor();
      try {assert.ok(cases[name]);await cases[name]();check('real-qwen-'+name);}
      catch(error){failedCase=true;report.checks.push({name:'real-qwen-'+name,status:'FAIL',error:String(error)});save();console.error('FAIL real-qwen-'+name,String(error));
        await page.screenshot({path:path.join(out,name+'-failure.png'),fullPage:true});if(runId)await api('/nova/live/'+runId+'/cancel',{}).catch(()=>{});runId=null;
        if(process.env.NOVA_STOP_ON_FAILURE==='true')throw error;}
    }
  }
  if(process.argv.includes('--adaptive-core')) {
    const before=await api('/prompts?size=100');
    const request={version:'adaptive-inference/1',requestId:require('node:crypto').randomUUID(),model:'qwen3:8b',mode:'structured',
      messages:[{role:'system',content:'Prepare a customer follow-up note from the supplied tasks. No access to application data or effects. Return only the typed proposal.'},
        {role:'user',content:'Customer 7: call Carlos and review the appointment. Summarize both pending tasks in Spanish. Leave the note awaiting approval; do not save it.'}],
      outputSchema:{type:'object',properties:{customerId:{type:'integer',enum:[7]},summary:{type:'string'},requiresApproval:{type:'boolean'}},required:['customerId','summary','requiresApproval'],additionalProperties:false},
      limits:{requests:2,timeoutSeconds:90,contextTokens:12288,outputTokens:1000,temperature:0,thinking:false},
      ...(process.env.NOVA_CORE_DECODING?{decoding:process.env.NOVA_CORE_DECODING}:{})};
    const child=spawn(process.env.PYTHON||'python',[path.join(root,'agents/nova-core/nova_worker.py'),
      '--sdk-root',process.env.NOVA_CORE_SDK||path.resolve(root,'../Adaptive-AI-Core'),'--endpoint','http://127.0.0.1:11434'],
      {windowsHide:true,stdio:['pipe','pipe','pipe'],env:{...process.env,PYDANTIC_AI_NO_BANNER:'1'}});
    let result='',diagnostic='';child.stdout.on('data',b=>{result+=b;});child.stderr.on('data',b=>{diagnostic+=b;});
    child.stdin.end(JSON.stringify(request));
    const timer=setTimeout(()=>child.kill(),190000);
    const code=await new Promise((resolve,reject)=>{child.once('exit',resolve);child.once('error',reject);});clearTimeout(timer);
    fs.writeFileSync(path.join(out,'adaptive-core-run.log'),diagnostic);
    fs.writeFileSync(path.join(out,'adaptive-core-second-consumer.json'),result);
    assert.equal(code,0,'Generic Core integration failed');
    const sdk=JSON.parse(result);report.sdk={status:sdk.status,model:request.model,decoding:request.decoding||'native',usage:sdk.usage};
    assert.equal(sdk.requestId,request.requestId);assert.equal(sdk.status,'proposed');assert.equal(sdk.output.customerId,7);
    assert.equal(sdk.output.requiresApproval,true);assert.match(sdk.output.summary,/Carlos/i);assert.match(sdk.output.summary,/cita|appointment/i);
    assert.deepEqual(await api('/prompts?size=100'),before);
    check('generic-core-second-domain-schema-with-real-qwen-no-business-effects');
  }
  assert.deepEqual(await api('/prompts/'+other.id),other);assert.deepEqual(report.errors,[]);assert.equal(context.pages().length,1);
  if(real){const typedOnly=report.selectedSemanticScenarios.includes('productive_cycle')&&report.selectedSemanticScenarios.every(name=>['productive_cycle','workbench_search'].includes(name));
    assert.ok(report.runtimeSamples?.some(sample=>sample.modelState===(typedOnly?'generating':'interpreting')&&!['COMPLETED','FAILED','BLOCKED','CANCELLED'].includes(sample.status)));check('actual-runtime-reports-model-work-before-completion');}
  check('other-record-and-single-tab-preserved');
  if(real){const tags=await(await fetch('http://127.0.0.1:11434/api/tags')).json();report.modelDigestAfter=tags.models.find(m=>m.name==='qwen3:8b')?.digest;assert.equal(report.modelDigestAfter,report.modelDigest,'Model changed during browser acceptance');}
  if(coreEnabled){report.coreArtifactsAfter=coreArtifacts(coreOption('SdkRoot'),coreOption('Worker'));
    assert.deepEqual(report.coreArtifactsAfter,report.coreArtifactsBefore,'Core runtime changed during browser acceptance');}
  report.status=failedCase?'FAIL':'PASS';if(failedCase)process.exitCode=1;save();
})().catch(async e=>{report.status='FAIL';report.failure=String(e.stack||e);save();process.exitCode=1;console.error(report.failure);if(page)await page.screenshot({path:path.join(out,'failure.png'),fullPage:true}).catch(()=>{});})
.finally(async()=>{if(runId&&server?.exitCode===null)await api('/nova/live/'+runId+'/cancel',{}).catch(()=>{});await browser?.close();if(server?.exitCode===null){server.kill();await new Promise(resolve=>server.once('exit',resolve));}if(lease!==undefined)fs.closeSync(lease);fs.writeFileSync(path.join(out,'server.log'),log);console.log('EVIDENCE',out);});
