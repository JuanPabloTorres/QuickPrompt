// Test-only exact proposal substitution. Frozen .NET and actual UI remain real;
// subsequent authoring uses the same local Qwen through the frozen Core worker.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {spawn}=require('node:child_process'),{createHash}=require('node:crypto');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const config=JSON.parse(fs.readFileSync(process.argv[2],'utf8').replace(/^\uFEFF/,''));
assert.equal(config.scope,'SYNTHETIC_COUNTERFACTUAL_NOT_RELEASE');
const out=path.resolve(config.output);fs.mkdirSync(out,{recursive:false});
const origin='http://127.0.0.1:5243',headers={'Content-Type':'application/json','X-QuickPrompt-Client':'nova-v1'};
let server,browser,log='',runId;
const report={scope:config.scope,status:'UNCONFIRMED',syntheticInterpretation:true,checks:[],observations:[],realProviderSemanticReliability:'NOT_TESTED',harnessSha256:createHash('sha256').update(fs.readFileSync(__filename)).digest('hex')};
const save=()=>fs.writeFileSync(path.join(out,'results.json'),JSON.stringify(report,null,2));
async function api(route,body){const r=await fetch(origin+'/api/v1'+route,{method:body?'POST':'GET',headers,body:body?JSON.stringify(body):undefined});assert.ok(r.ok,route+':'+r.status);return r.status===204?null:r.json();}
async function until(read,ms=180000){const end=Date.now()+ms;while(Date.now()<end){const x=await read();if(x)return x;await new Promise(r=>setTimeout(r,200));}throw Error('Timed out');}
(async()=>{
 assert.ok(path.resolve(config.database).startsWith(path.resolve(config.repo,'.playwright')+path.sep),'Owned QA database required');
 fs.mkdirSync(path.dirname(config.database),{recursive:true});
 server=spawn('dotnet',[config.api,'--QuickPrompt:Port=5243','--QuickPrompt:DataPath='+config.database,'--QuickPrompt:WebRoot='+config.web,
 '--QuickPrompt:Agent:Runtime=adaptive-core','--QuickPrompt:AdaptiveCore:Python='+config.python,
 '--QuickPrompt:AdaptiveCore:SdkRoot='+config.sdk,'--QuickPrompt:AdaptiveCore:Worker='+config.replayWorker,
 '--QuickPrompt:Diagnostics:Lab:Enabled=true','--QuickPrompt:Diagnostics:Lab:CaptureSyntheticContent=true',
 '--QuickPrompt:Diagnostics:Lab:Directory='+path.join(out,'lab-feed')],{cwd:config.repo,windowsHide:true,stdio:['ignore','pipe','pipe'],env:{...process.env,NOVA_COUNTERFACTUAL_CONFIG:config.workerConfig,ASPNETCORE_ENVIRONMENT:'Development'}});
 server.stdout.on('data',b=>log+=b);server.stderr.on('data',b=>log+=b);
 await until(()=>{if(server.exitCode!==null)throw Error('API exited');return log.includes('Now listening on: '+origin);},25000);
 const source=await api('/prompts',config.source);
 browser=await chromium.launch({channel:'msedge',headless:false});
 const context=await browser.newContext({permissions:['clipboard-read','clipboard-write'],viewport:{width:1366,height:950}});
 await context.addInitScript(()=>localStorage.setItem('nova-interface-v1',JSON.stringify({view:'cards',executionMode:'SAME_TAB',rememberDraft:false,agentSpeed:'fast'})));
 const page=await context.newPage();await page.goto(origin);
 await page.getByRole('button',{name:source.title,exact:true}).click();await page.getByTestId('prompt-focus').waitFor();
 const sentinel='NOVA synthetic counterfactual '+crypto.randomUUID();await page.evaluate(s=>navigator.clipboard.writeText(s),sentinel);
 const toggle=page.getByRole('button',{name:'Abrir asistente NOVA',exact:true});if(await toggle.isVisible())await toggle.click();
 await page.getByRole('textbox',{name:'¿Qué quieres hacer hoy?',exact:true}).fill(config.request);
 const pending=page.waitForResponse(r=>r.url()===origin+'/api/v1/nova/live'&&r.request().method()==='POST');
 await page.getByRole('button',{name:'Preparar prompt',exact:true}).click();runId=(await(await pending).json()).runId;
 const result=await until(async()=>{const v=await api('/nova/live/'+runId);report.observations.push({at:new Date().toISOString(),status:v.run.status,activeTool:v.runtime?.activeTool});return ['COMPLETED','FAILED','BLOCKED','CANCELLED'].includes(v.run.status)?v:null;});
 report.result=result;save();
 assert.ok(config.effects.every(op=>result.semantic?.intentions.includes(op)),'Injected goals must survive binding and compilation');
 report.checks.push({id:'compiled-goals',status:'PASS'});
 assert.deepEqual(await api('/prompts/'+source.id),source);assert.ok(await page.evaluate(async s=>(await navigator.clipboard.readText())===s,sentinel));
 report.checks.push({id:'library-and-clipboard-unchanged',status:'PASS'});
 // Preserve the real outcome, including suspension for missing input. Neither
 // a legal proposal nor a synthetic task is a semantic release certificate.
 report.status=result.run.status==='COMPLETED'?'PASS':'FAIL';
 if(report.status==='FAIL')process.exitCode=1;
 await page.screenshot({path:path.join(out,'result.png'),fullPage:true});save();
})().catch(e=>{report.status='FAIL';report.error=String(e.stack||e);save();process.exitCode=1;})
.finally(async()=>{if(runId&&server?.exitCode===null)await api('/nova/live/'+runId+'/cancel',{}).catch(()=>{});await browser?.close();if(server?.exitCode===null){server.kill();await new Promise(r=>server.once('exit',r));}fs.writeFileSync(path.join(out,'server.log'),log);console.log(JSON.stringify({status:report.status,output:out}));});
