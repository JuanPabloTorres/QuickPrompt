// EXE-07: real API/browser under absent or stalled local inference. No user service is stopped.
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),net=require('node:net');
const assert=require('node:assert/strict'),{spawn}=require('node:child_process');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'../..'),stamp=Date.now(),temp=path.join(root,'.playwright/service-faults-'+stamp),out=path.join(root,'docs/evidence/service-faults-'+stamp);
const build=path.resolve(root,process.env.NOVA_TEST_BUILD||'.playwright/closure-goal-20261020/wave1/feedback/api/QuickPrompt.Api.dll');
const web=path.resolve(root,process.env.NOVA_WEB_BUILD||'.playwright/release-validation-20261020-a1/web');
const origin='http://127.0.0.1:5263',headers={'Content-Type':'application/json','X-QuickPrompt-Client':'nova-v1'};
fs.mkdirSync(temp,{recursive:true});fs.mkdirSync(out,{recursive:true});
const report={syntheticOnly:true,status:'RUNNING',build,web,timeoutCaseSeconds:5,absentCaseUsesProductionStageBudget:true,modelRequestsAreControlledFailures:true,checks:[]};
let browser,server,stall,page,log='';const save=()=>fs.writeFileSync(path.join(out,'results.json'),JSON.stringify(report,null,2));
async function vacant(port){const probe=net.createServer();await new Promise((resolve,reject)=>{probe.once('error',reject);probe.listen(port,'127.0.0.1',resolve);});await new Promise(resolve=>probe.close(resolve));}
async function poll(read,ms=20000){const end=Date.now()+ms;while(Date.now()<end){const value=await read();if(value)return value;await new Promise(resolve=>setTimeout(resolve,100));}throw Error('QA_TIMEOUT');}
async function api(route,body){const response=await fetch(origin+'/api/v1'+route,{method:body?'POST':'GET',headers,body:body?JSON.stringify(body):undefined,signal:AbortSignal.timeout(3000)});assert.ok(response.ok,route+': '+response.status);return response.json();}
async function stop(){if(server&&server.exitCode===null&&server.signalCode===null){const ended=new Promise(resolve=>server.once('exit',resolve));server.kill();await ended;}server=null;}
async function check(mode){
 let chats=0;
 if(mode==='timeout'){
  stall=http.createServer((request,response)=>{request.resume();if(request.url==='/api/chat'){chats++;return;}response.writeHead(503);response.end('{}');});
  await new Promise(resolve=>stall.listen(5264,'127.0.0.1',resolve));
 }
 server=spawn('dotnet',[build,'--QuickPrompt:Port=5263','--QuickPrompt:Ollama=http://127.0.0.1:5264','--QuickPrompt:DataPath='+path.join(temp,mode+'.sqlite'),'--QuickPrompt:WebRoot='+web,...(mode==='timeout'?['--QuickPrompt:Preparation:StageTimeoutSeconds=5','--QuickPrompt:Preparation:TotalTimeoutSeconds=10']:[])],{cwd:root,windowsHide:true,stdio:['ignore','pipe','pipe']});
 server.stdout.on('data',bytes=>{log+=bytes;});server.stderr.on('data',bytes=>{log+=bytes;});
 await poll(async()=>{try{return(await fetch(origin+'/health',{signal:AbortSignal.timeout(500)})).ok;}catch{return false;}});
 const source=await api('/prompts',{title:'QA · Biblioteca sin modelo',templateText:'Resume <material> sin inventar datos.'});
 const before=await api('/prompts?size=100');
 const context=await browser.newContext({viewport:{width:1440,height:1000}});page=await context.newPage();const errors=[];page.on('pageerror',error=>errors.push(String(error)));
 await page.goto(origin);await page.getByTestId('library-ready').waitFor();
 const launcher=page.getByRole('button',{name:'Abrir asistente NOVA',exact:true});if(await launcher.count())await launcher.click();
 await page.getByRole('textbox',{name:'¿Qué quieres hacer hoy?',exact:true}).fill('Abre el prompt «'+source.title+'».');
 const pending=page.waitForResponse(response=>response.url().endsWith('/api/v1/nova/live')&&response.request().method()==='POST');
 await page.getByRole('button',{name:'Preparar prompt',exact:true}).click();const runId=(await(await pending).json()).runId;
 const view=await poll(async()=>{const v=await api('/nova/live/'+runId);return ['FAILED','BLOCKED','COMPLETED','CANCELLED'].includes(v.run.status)?v:null;});
 report.currentCase={mode,runId,run:view.run};save();
 assert.equal(view.run.status,'FAILED');assert.equal(view.run.errorCode,mode==='timeout'?'TIMEOUT':'MODEL_UNAVAILABLE');assert.equal(view.run.effectCommitted,false);
 const reopen=page.getByRole('button',{name:'Abrir asistente NOVA',exact:true});if(await reopen.isVisible())await reopen.click();
 await page.getByRole('button',{name:'Ver última actividad de NOVA',exact:true}).click();
 const message=mode==='timeout'?'El modelo tardó más de lo permitido. Puedes volver a intentarlo; la biblioteca y tu borrador se conservan.':'El modelo local no está disponible. Revisa que Ollama esté abierto y vuelve a intentarlo. Puedes seguir usando tu biblioteca.';
 await page.getByText(message,{exact:true}).waitFor();
 assert.deepEqual((await api('/prompts?size=100')).items,before.items);
 const performance=await api('/nova/live/'+runId+'/performance');assert.equal(performance.llmCalls,mode==='timeout'?1:2);
 if(mode==='timeout')assert.equal(chats,1);
 const close=page.getByRole('button',{name:'Cerrar actividad',exact:true});if(await close.isVisible())await close.click();
 await page.getByRole('button',{name:source.title,exact:true}).click();await page.getByTestId('prompt-focus').waitFor();
 await page.getByTestId('variable-field').fill('material sintético');await poll(async()=>await page.getByTestId('compiled-preview').textContent()==='Resume material sintético sin inventar datos.');
 assert.deepEqual((await api('/prompts?size=100')).items,before.items);assert.deepEqual(errors,[]);
 await page.screenshot({path:path.join(out,mode+'.png'),fullPage:true});
 report.checks.push({mode,status:'PASS',runId,run:view.run,publicMessage:message,libraryUnchanged:true,manualPreviewUsable:true,llmAttempts:performance.llmCalls,stalledRequests:chats});save();
 await context.close();await stop();if(stall){stall.closeAllConnections();await new Promise(resolve=>stall.close(resolve));stall=null;}
}
(async()=>{await vacant(5263);await vacant(5264);browser=await chromium.launch({channel:'msedge',headless:process.env.NOVA_QA_HEADLESS==='1'});await check('absent');await check('timeout');report.status='PASS';})()
.catch(async error=>{report.status='FAIL';report.failure=String(error.stack);process.exitCode=1;
 if(page){await page.screenshot({path:path.join(out,'failure.png'),fullPage:true}).catch(()=>{});report.failureScreenText=await page.locator('body').innerText().catch(()=>'unavailable');}
})
.finally(async()=>{await stop();if(stall){stall.closeAllConnections();stall.close();}await browser?.close();save();fs.writeFileSync(path.join(out,'server.log'),log);console.log(JSON.stringify({status:report.status,evidence:out}));});
