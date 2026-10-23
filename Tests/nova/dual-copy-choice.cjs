// Independent browser acceptance of explicit UI choices. This does not measure
// free-form Qwen intent accuracy. Actual authoring uses the frozen Core/Qwen.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {spawn}=require('node:child_process'),{createHash}=require('node:crypto');
const {chromium}=require('C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const {webSha256,coreArtifacts}=require('./artifact-hashes.cjs');
const candidate=process.env.NOVA_COPY_CHOICE_CANDIDATE||'dual-copy-title-authority-safe';
assert.match(candidate,/^[a-z0-9-]+$/);
const root=path.resolve(__dirname,'../..'),runtime=path.join(root,'.playwright/closure-lab-20261021',candidate);
const stamp=Date.now(),out=path.join(root,'docs/evidence/closure-lab-20261021/dual-copy-choice-v13-browser-'+stamp);
const data=path.join(root,'.playwright/dual-copy-choice-'+stamp,'synthetic.sqlite');
const web=path.join(root,'.playwright/closure-lab-20261021/dual-copy-choice-final-web');
const feed=path.join(out,'lab-feed'),campaign='dual-copy-choice-'+stamp;
const origin='http://127.0.0.1:5243',headers={'Content-Type':'application/json','X-QuickPrompt-Client':'nova-v1'};
fs.mkdirSync(out,{recursive:true});
fs.copyFileSync(__filename,path.join(out,'harness-source.cjs'));
const allCases=['manual-template-and-rendered','adapt-prepare-template','adapt-prepare-rendered','rendered-missing-input','agent-explicit-template-copy'];
const selectedCases=process.env.NOVA_COPY_CHOICE_ONLY?process.env.NOVA_COPY_CHOICE_ONLY.split(','):allCases;
assert.ok(selectedCases.length>0&&selectedCases.every(id=>allCases.includes(id))&&new Set(selectedCases).size===selectedCases.length);
const sha=p=>createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const report={scope:'EXPLICIT_UI_CHOICES_WITH_REAL_CORE_QWEN_AUTHORING_NOT_FREE_INTENT_CERTIFICATION',status:'RUNNING',syntheticOnly:true,
 candidate,campaignId:campaign,requestedCases:selectedCases,fullSuite:selectedCases.length===allCases.length,harnessSha256:sha(__filename),rows:[],runs:[],errors:[],
 runtimeHashes:Object.fromEntries(['QuickPrompt.Api.dll','QuickPrompt.Application.dll','QuickPrompt.Infrastructure.dll','QuickPrompt.Domain.dll'].map(n=>[n,sha(path.join(runtime,'eval',n))])),
 coreBefore:coreArtifacts(path.join(runtime,'sdk'),path.join(runtime,'nova_worker.py')),webSha256:webSha256(web)};
let server,browser,page,source,runId,log='';
if(selectedCases.length===1&&selectedCases[0]==='manual-template-and-rendered')report.scope='MANUAL_COPY_CONTROL_NO_MODEL_INFERENCE';
const ownedRuns=new Set(),mirrored=new Set();
// Forward only actual events from this owned public-fixture feed into the
// existing Lab journal. Preserve times, source locations and event IDs; no
// synthetic execution events, private reasoning or commands are generated.
function pumpFeed(){
 const lines=[];
 for(const name of ['app.jsonl','core.jsonl']){
  const file=path.join(feed,name);if(!fs.existsSync(file))continue;
  for(const line of fs.readFileSync(file,'utf8').split('\n')){
   if(!line)continue;let e;try{e=JSON.parse(line);}catch{continue;}
   if(e.schema!=='ai-delivery-event/1'||!ownedRuns.has(e.runId||e.caseId)||!e.id||mirrored.has(e.id))continue;
   mirrored.add(e.id);lines.push(JSON.stringify({...e,originCampaignId:e.campaignId,originJournal:file,campaignId:campaign}));
  }
 }
 if(lines.length)fs.appendFileSync('C:/Projects/Repos/AI-Delivery-Lab/artifacts/visual/events.jsonl',lines.join('\n')+'\n','utf8');
}
const save=()=>fs.writeFileSync(path.join(out,'results.json'),JSON.stringify(report,null,2));
async function api(route,body){const r=await fetch(origin+'/api/v1'+route,{method:body?'POST':'GET',headers,body:body?JSON.stringify(body):undefined});assert.ok(r.ok,route+':'+r.status);return r.status===204?null:r.json();}
async function until(read,timeout=180000){const end=Date.now()+timeout;while(Date.now()<end){const x=await read();if(x)return x;await new Promise(r=>setTimeout(r,150));}throw Error('QA_TIMEOUT');}
async function fresh(){await page.evaluate(()=>sessionStorage.clear());await page.goto(origin);await page.getByRole('button',{name:source.title,exact:true}).click();await page.getByTestId('prompt-focus').waitFor();}
async function guard(){const sentinel='NOVA QA owned '+crypto.randomUUID();await page.evaluate(s=>navigator.clipboard.writeText(s),sentinel);return async()=>assert.ok(await page.evaluate(async s=>(await navigator.clipboard.readText())===s,sentinel),'Preparation wrote clipboard');}
async function copySettled(expected,kind,prior=null){await until(async()=>{
 const receipt=page.getByTestId('clipboard-receipt');if(await receipt.count()!==1)return false;
 return await receipt.getAttribute('data-operation-id')!==prior && await receipt.getAttribute('data-clipboard-text')===expected
  && await receipt.getAttribute('data-copy-content')===kind;
},25000);assert.equal(await page.evaluate(()=>navigator.clipboard.readText()),expected);}
async function action(label){const toggle=page.getByRole('button',{name:'Abrir asistente NOVA',exact:true});if(await toggle.isVisible())await toggle.click();const b=page.getByRole('button',{name:label,exact:true});if(!await b.isVisible())await page.getByRole('button',{name:'Más acciones de NOVA',exact:true}).click();await b.click();return page.getByRole('dialog',{name:label,exact:true});}
async function submit(dialog,label){const pending=page.waitForResponse(r=>r.url()===origin+'/api/v1/nova/live'&&r.request().method()==='POST');await dialog.getByRole('button',{name:label,exact:true}).click();const response=await pending;const submitted=response.request().postDataJSON();runId=(await response.json()).runId;ownedRuns.add(runId);
 const result=await until(async()=>{const v=await api('/nova/live/'+runId);pumpFeed();report.active={runId,status:v.run.status,modelState:v.runtime?.modelState,activeTool:v.runtime?.activeTool};save();return ['COMPLETED','BLOCKED','FAILED','CANCELLED'].includes(v.run.status)?v:null;});report.runs.push({submitted,result});save();return result;}
async function scenario(id,expected,body){if(!selectedCases.includes(id))return;try{await fresh();await body();report.rows.push({id:runId||id,title:id,status:'PASS',expected,observed:'assertions satisfied'});}catch(e){report.rows.push({id:runId||id,title:id,status:'FAIL',expected,observed:String(e.stack||e)});if(runId)await api('/nova/live/'+runId+'/cancel',{}).catch(()=>{});}finally{pumpFeed();await page.screenshot({path:path.join(out,id+'.png'),fullPage:true}).catch(()=>{});runId=null;save();}}
(async()=>{
 assert.equal(process.env.NOVA_SEMANTIC_CAPTURE_EXCLUSIVE_LEASE,'1','Caller must hold exclusive evaluation lease');
 const tags=await(await fetch('http://127.0.0.1:11434/api/tags')).json();report.modelDigest=tags.models.find(m=>m.name==='qwen3:8b')?.digest;assert.match(report.modelDigest||'',/^[a-f0-9]{64}$/);
 try{const r=await fetch(origin+'/api/v1/health',{signal:AbortSignal.timeout(1000)});if(r)throw Error('QA port already occupied');}catch(e){if(e.message==='QA port already occupied')throw e;}
 server=spawn('dotnet',[path.join(runtime,'eval/QuickPrompt.Api.dll'),'--QuickPrompt:Port=5243','--QuickPrompt:DataPath='+data,'--QuickPrompt:WebRoot='+web,
 '--QuickPrompt:Agent:Runtime=adaptive-core','--QuickPrompt:AdaptiveCore:Python=C:/Projects/Repos/Adaptive-AI-Core/.venv/Scripts/python.exe',
 '--QuickPrompt:AdaptiveCore:Worker='+path.join(runtime,'nova_worker.py'),'--QuickPrompt:AdaptiveCore:SdkRoot='+path.join(runtime,'sdk'),
 '--QuickPrompt:Diagnostics:Lab:Enabled=true','--QuickPrompt:Diagnostics:Lab:CaptureSyntheticContent=true','--QuickPrompt:Diagnostics:Lab:Directory='+feed],
 {cwd:root,windowsHide:true,stdio:['ignore','pipe','pipe'],env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development'}});
 server.stdout.on('data',b=>log+=b);server.stderr.on('data',b=>log+=b);await until(()=>{if(server.exitCode!==null)throw Error('API_EXITED');return log.includes('Now listening on: '+origin);},25000);
 source=await api('/prompts',{title:'QA · Elección de copia',templateText:'Organiza una agenda con <puntos>. Registra acuerdos sin inventar responsables.'});
 browser=await chromium.launch({channel:'msedge',headless:true});const context=await browser.newContext({permissions:['clipboard-read','clipboard-write'],viewport:{width:1366,height:950}});
 await context.addInitScript(()=>localStorage.setItem('nova-interface-v1',JSON.stringify({view:'cards',executionMode:'SAME_TAB',rememberDraft:false,agentSpeed:'fast'})));
 page=await context.newPage();page.on('pageerror',e=>report.errors.push(e.message));await page.goto(origin);
 await scenario('manual-template-and-rendered',{template:'preserve markers, no usage',rendered:'fill values, one use'},async()=>{
  await page.getByText('Plantilla con marcadores',{exact:true}).click();await page.getByRole('button',{name:'Copiar plantilla con marcadores',exact:true}).click();
  await copySettled(source.templateText,'TEMPLATE');assert.deepEqual(await api('/prompts/'+source.id),source);
  assert.equal(await page.getByRole('button',{name:'Copiar prompt',exact:true}).isEnabled(),false);
  const prior=await page.getByTestId('clipboard-receipt').getAttribute('data-operation-id');
  await page.getByLabel('puntos',{exact:true}).fill('Presupuesto y contratación');await page.getByTestId('compile-ready').waitFor();
  await page.getByRole('button',{name:'Copiar prompt',exact:true}).click();await copySettled(source.templateText.replace('<puntos>','Presupuesto y contratación'),'RENDERED',prior);
  await until(async()=>(await api('/prompts/'+source.id)).uses===1);source=await api('/prompts/'+source.id);
 });
 const instruction='Centra la agenda en decisiones. Conserva el marcador <puntos> y la prohibición de inventar responsables.';
 for(const kind of ['TEMPLATE','RENDERED'])await scenario('adapt-prepare-'+kind.toLowerCase(),{status:'COMPLETED',goals:['PROPOSE_ADAPTATION','COPY_PROMPT'],clipboard:'unchanged',source:'unchanged'},async()=>{
  const noWrite=await guard(),before=await api('/prompts/'+source.id);const dialog=await action('Adaptar y preparar copia');
  await dialog.getByLabel('¿Qué quieres adaptar?',{exact:true}).fill(instruction);
  await dialog.getByRole('radio',{name:kind==='TEMPLATE'?'Plantilla con marcadores':'Texto rellenado',exact:true}).check();
  if(kind==='RENDERED')await dialog.getByLabel('puntos',{exact:true}).fill('Presupuesto y contratación');
  const result=await submit(dialog,'Adaptar y preparar');assert.equal(result.run.status,'COMPLETED',JSON.stringify({error:result.run.errorCode,question:result.question}));
  assert.ok(result.semantic.intentions.includes('PROPOSE_ADAPTATION'));assert.ok(result.semantic.intentions.includes('COPY_PROMPT'));
  assert.ok(!result.semantic.intentions.some(x=>['COPY_TO_CLIPBOARD','EXECUTE_LOCAL','REQUEST_SAVE_VERSION','REQUEST_SAVE_COPY'].includes(x)));
  const state=result.observation;assert.equal(state.draftChanged,true);assert.notEqual(state.templateText,source.templateText);assert.match(state.templateText,/decisi[oó]n|decisiones/i);assert.ok(state.templateText.includes('<puntos>'));
  if(kind==='TEMPLATE'){assert.equal(state.compiledReady,false);assert.equal(state.templatePreviewText,state.templateText);}
  else {assert.equal(state.compiledReady,true);assert.equal(state.compiledText,state.templateText.replaceAll('<puntos>','Presupuesto y contratación'));}
  await noWrite();assert.deepEqual(await api('/prompts/'+source.id),before);
 });
 await scenario('rendered-missing-input',{status:'BLOCKED',question:'puntos',clipboard:'unchanged'},async()=>{
  const noWrite=await guard(),before=await api('/prompts/'+source.id),dialog=await action('Adaptar y preparar copia');
  await dialog.getByLabel('¿Qué quieres adaptar?',{exact:true}).fill(instruction);await dialog.getByRole('radio',{name:'Texto rellenado',exact:true}).check();
  const result=await submit(dialog,'Adaptar y preparar');assert.equal(result.run.status,'BLOCKED');assert.equal(result.run.errorCode,'NEEDS_CLARIFICATION');assert.match(result.question,/puntos/);await noWrite();assert.deepEqual(await api('/prompts/'+source.id),before);
 });
 await scenario('agent-explicit-template-copy',{status:'COMPLETED',clipboard:'exact template',source:'unchanged'},async()=>{
  const before=await api('/prompts/'+source.id),dialog=await action('Copiar con NOVA');await dialog.getByRole('radio',{name:'Plantilla con marcadores',exact:true}).check();
  const result=await submit(dialog,'Copiar ahora');assert.equal(result.run.status,'COMPLETED',JSON.stringify({error:result.run.errorCode,question:result.question}));
  assert.equal(result.observation.clipboard.copyContent,'TEMPLATE');assert.equal(result.observation.clipboard.text,source.templateText);
  assert.equal(await page.evaluate(()=>navigator.clipboard.readText()),source.templateText);assert.deepEqual(await api('/prompts/'+source.id),before);
 });
 const after=await(await fetch('http://127.0.0.1:11434/api/tags')).json();assert.equal(after.models.find(m=>m.name==='qwen3:8b')?.digest,report.modelDigest);
 assert.deepEqual(coreArtifacts(path.join(runtime,'sdk'),path.join(runtime,'nova_worker.py')),report.coreBefore);assert.deepEqual(report.errors,[]);
 report.status=report.rows.length===selectedCases.length&&report.rows.every(r=>r.status==='PASS')?'PASS':'FAIL';report.count=report.rows.length;report.active=null;pumpFeed();report.mirroredActualEvents=mirrored.size;save();if(report.status!=='PASS')process.exitCode=1;
})().catch(e=>{report.status='FAIL';report.failure=String(e.stack||e);save();process.exitCode=1;})
.finally(async()=>{if(runId&&server?.exitCode===null)await api('/nova/live/'+runId+'/cancel',{}).catch(()=>{});await browser?.close();if(server?.exitCode===null){server.kill();await new Promise(r=>server.once('exit',r));}fs.writeFileSync(path.join(out,'server.log'),log);console.log(JSON.stringify({status:report.status,count:report.rows.length,output:out}));});
