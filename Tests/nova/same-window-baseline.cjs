// QA reproduction of the old parallel-browser surface. Synthetic data only.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {spawn,execFileSync}=require('node:child_process');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'../..'),origin='http://127.0.0.1:5235';
const out=path.join(root,'docs/evidence/same-window/baseline-'+Date.now()),temporary=path.join(root,'.playwright/same-window-baseline-'+Date.now());
fs.mkdirSync(out,{recursive:true});fs.mkdirSync(temporary,{recursive:true});
const lease=fs.openSync(path.join(require('node:os').tmpdir(),'QuickPrompt.Nova.SemanticModel.lease'),'a');
let server,browser,log='',runId;const headers={'Content-Type':'application/json','X-QuickPrompt-Client':'nova-v1'};
async function api(route,body){const r=await fetch(origin+'/api/v1'+route,{method:body?'POST':'GET',headers,body:body?JSON.stringify(body):undefined});assert.ok(r.ok,await r.clone().text());return r.status===204?null:r.json();}
function windows(){return Number(execFileSync('C:/Windows/System32/WindowsPowerShell/v1.0/powershell.exe',['-NoProfile','-Command',"@(Get-Process msedge -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }).Count"],{encoding:'utf8',windowsHide:true}).trim());}
(async()=>{
 const tags=await(await fetch('http://127.0.0.1:11434/api/tags')).json();assert.ok(tags.models.some(m=>m.name==='qwen3:8b'));
 server=spawn('dotnet',[path.join(root,'.playwright/semantic-compact-contract/Debug/net8.0/QuickPrompt.Api.dll'),'--QuickPrompt:Port=5235','--QuickPrompt:DataPath='+path.join(temporary,'synthetic.sqlite'),'--QuickPrompt:WebRoot='+path.join(root,'apps/web/dist')],{windowsHide:true,stdio:['ignore','pipe','pipe'],env:{...process.env,NOVA_UI_QA_OUT:path.join(out,'worker'),NOVA_UI_QA_APPROVED:'synthetic-only'}});
 server.stdout.on('data',x=>log+=x);server.stderr.on('data',x=>log+=x);
 for(let i=0;i<150;i++){if(server.exitCode!==null)throw Error('Owned QA API exited');if(log.includes('Now listening on: '+origin))break;await new Promise(r=>setTimeout(r,100));}
 const source=await api('/prompts',{title:'Revisión de evidencias sintéticas',templateText:'Evalúa <documento>. No inventes datos.'});
 browser=await chromium.launch({channel:'msedge',headless:false});const context=await browser.newContext({viewport:{width:1280,height:900},recordVideo:{dir:path.join(out,'original-tab-video')}});const page=await context.newPage();await page.goto(origin);
 const before=windows();await page.getByLabel('Modo de NOVA',{exact:true}).selectOption('BROWSER');const input='Abre el prompt Revisión de evidencias sintéticas, sin modificarlo.';
 await page.getByRole('textbox',{name:'¿Qué quieres hacer hoy?',exact:true}).fill(input);
 const [response]=await Promise.all([page.waitForResponse(r=>r.url()===origin+'/api/v1/nova/live'&&r.request().method()==='POST'),page.getByRole('button',{name:'Preparar prompt',exact:true}).click()]);runId=(await response.json()).runId;
 let view,maxWindows=before;for(let i=0;i<480;i++){view=await api('/nova/live/'+runId);if(view.run.browserOpen){maxWindows=Math.max(maxWindows,windows());if(view.observation?.promptId===source.id)break;}if(['FAILED','BLOCKED','CANCELLED'].includes(view.run.status))throw Error(JSON.stringify(view));await new Promise(r=>setTimeout(r,1000));}
 const originalFocused=await page.getByTestId('prompt-focus').count()>0;assert.equal(view.observation?.promptId,source.id);assert.equal(originalFocused,false);assert.ok(maxWindows>before);
 await page.screenshot({path:path.join(out,'original-tab-unchanged.png'),fullPage:true});assert.deepEqual(await api('/prompts/'+source.id),source);
 fs.writeFileSync(path.join(out,'baseline.json'),JSON.stringify({status:'REPRODUCED',qaSession:true,input,model:tags.models.find(m=>m.name==='qwen3:8b'),run:view,edgeWindowsBefore:before,edgeWindowsAfter:maxWindows,additionalWindows:maxWindows-before,originalTabFocused:originalFocused,sourceUnchanged:true,rootCause:'VisibleBrowserWorker.Start launches Node; worker start launches Edge, newContext, newPage and goto in a separate surface.'},null,2));
 console.log('REPRODUCED',out);await context.close();
})().catch(e=>{console.error(e);fs.writeFileSync(path.join(out,'failure.json'),JSON.stringify({error:e.stack}));process.exitCode=1;}).finally(async()=>{if(runId)await api('/nova/live/'+runId+'/cancel',{}).catch(()=>{});await browser?.close();if(server?.exitCode===null){const done=new Promise(r=>server.once('exit',r));server.kill();await done;}fs.writeFileSync(path.join(out,'server.log'),log);fs.closeSync(lease);});
