// SAV-04 qualification: real Qwen3 8B + headed Edge + API + isolated synthetic SQLite.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {spawn,execFileSync}=require('node:child_process'),{createHash}=require('node:crypto');
const http=require('node:http');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'../..'),stamp=Date.now(),temporary=path.join(root,'.playwright/agent-deletion-'+stamp);
const out=path.join(root,'docs/evidence/agent-deletion-20261019',String(stamp));
const build=path.resolve(root,process.env.NOVA_TEST_BUILD||'.playwright/source-selection-20261019/api-v5/QuickPrompt.Api.dll');
const web=path.resolve(root,process.env.NOVA_TEST_WEBROOT||'.playwright/agent-deletion-20261019/web-v2');
const port=Number(process.env.NOVA_QA_PORT||5241),origin='http://127.0.0.1:'+port;
fs.mkdirSync(temporary,{recursive:true});fs.mkdirSync(out,{recursive:true});
const report={status:'RUNNING',syntheticOnly:true,model:'qwen3:8b',origin,build,web,checks:[],errors:[],
  hashes:Object.fromEntries(['QuickPrompt.Api.dll','QuickPrompt.Application.dll','QuickPrompt.Infrastructure.dll','QuickPrompt.Domain.dll'].map(name=>[name,createHash('sha256').update(fs.readFileSync(path.join(path.dirname(build),name))).digest('hex')])),
  webIndexSHA256:createHash('sha256').update(fs.readFileSync(path.join(web,'index.html'))).digest('hex')};
report.scope=process.env.NOVA_QA_LIFECYCLE==='1'?'lifecycle-from-selected-source':'frozen-six-semantic-cases';
const debugPoints=process.env.NOVA_QA_DEBUG_POINTS==='1';
if(debugPoints)report.debugPoints=[];
let server,browser,page,proxy,log='';const save=()=>fs.writeFileSync(path.join(out,'results.json'),JSON.stringify(report,null,2));save();
function observeServer(server) {
  let debugBuffer='';
  server.stdout.on('data',chunk=>{
    log+=chunk;
    if(debugPoints){debugBuffer+=String(chunk);let end;
      while((end=debugBuffer.indexOf('\n'))>=0){const line=debugBuffer.slice(0,end).trim();debugBuffer=debugBuffer.slice(end+1);
        if(line.startsWith('NOVA_DEBUG_POINT ')&&report.debugPoints.length<256)report.debugPoints.push(JSON.parse(line.slice(17)));
      }
    }
  });server.stderr.on('data',chunk=>{log+=chunk;});
}
async function api(route,body,method=body?'POST':'GET') {
  const response=await fetch(origin+'/api/v1'+route,{method,headers:{'Content-Type':'application/json','X-QuickPrompt-Client':'nova-v1'},body:body?JSON.stringify(body):undefined,signal:AbortSignal.timeout(15000)});
  assert.ok(response.ok,route+': '+response.status+' '+await response.clone().text());return response.status===204?null:response.json();
}
async function poll(read,timeout=20000) {const end=Date.now()+timeout;while(Date.now()<end){const value=await read();if(value)return value;await new Promise(resolve=>setTimeout(resolve,150));}throw Error('QA_TIMEOUT');}
const terminal=status=>['COMPLETED','CANCELLED','FAILED','BLOCKED'].includes(status);
async function begin(name,message) {
  const row={name,message,status:'RUNNING',startedAt:new Date().toISOString()};report.checks.push(row);save();
  const launcher=page.getByRole('button',{name:'Abrir asistente NOVA',exact:true});if(await launcher.count())await launcher.click();
  const composer=page.getByRole('textbox',{name:'¿Qué quieres hacer hoy?',exact:true});await composer.fill(message);
  const responsePromise=page.waitForResponse(response=>response.url().endsWith('/api/v1/nova/live')&&response.request().method()==='POST',{timeout:15000});
  await page.getByRole('button',{name:'Preparar prompt',exact:true}).click();const response=await responsePromise;assert.ok(response.ok(),await response.text());row.runId=(await response.json()).runId;save();return row;
}
async function settled(row,review=false) {
  const view=await poll(async()=>{const view=await api('/nova/live/'+row.runId);row.lastStatus=view.run.status;row.errorCode=view.run.errorCode;save();return terminal(view.run.status)||review&&view.deletion&&view.run.approval==='PENDING'?view:null;},240000);
  row.semantic=view.semantic;row.run=view.run;row.question=view.question;
  return view;
}
async function finish(row) {
  row.finalRun=(await api('/nova/live/'+row.runId)).run;
  const response=await fetch(origin+'/api/v1/nova/live/'+row.runId+'/events?after=0',{signal:AbortSignal.timeout(10000)});assert.ok(response.ok);
  fs.writeFileSync(path.join(out,row.name+'-events.sse'),await response.text());
  row.status='PASS';row.finishedAt=new Date().toISOString();await page.screenshot({path:path.join(out,row.name+'.png'),fullPage:true});save();
}
async function freshLibrary() {
  await page.evaluate(()=>sessionStorage.clear());await page.goto(origin);await page.getByTestId('library-ready').waitFor();
}
async function qualifyLifecycle(first,other,args) {
  const message='Elimina de mi biblioteca el prompt guardado «'+first.title+'».';
  const dialog=page.getByRole('dialog',{name:'¿Eliminar este prompt?',exact:true});
  const confirm=page.getByRole('button',{name:'Eliminar definitivamente',exact:true});
  async function review(name) {
    await freshLibrary();await page.getByRole('button',{name:first.title,exact:true}).click();await page.getByTestId('prompt-focus').waitFor();
    const row=await begin(name,message),view=await settled(row,true);
    assert.equal(view.run.approval,'PENDING');assert.equal(view.deletion.promptId,first.id);
    row.reviewedDeletion=view.deletion;save();
    await dialog.waitFor();await poll(async()=>await confirm.isEnabled());return {row,view};
  }
  async function cancel(row) {
    await page.getByRole('button',{name:'Cancelar',exact:true}).click();await poll(async()=>(await api('/nova/live/'+row.runId)).run.status==='CANCELLED');
    assert.equal(await page.getByTestId('prompt-focus').count(),1);await page.getByRole('heading',{name:first.title,exact:true}).waitFor();await finish(row);
  }
  async function rejectedConfirmation(row) {
    const response=page.waitForResponse(response=>response.url().endsWith('/nova/live/'+row.runId+'/deletion')&&response.request().method()==='POST');
    await confirm.click();assert.equal((await response).status(),409);await dialog.getByRole('alert').waitFor();
    assert.equal((await api('/nova/live/'+row.runId)).run.effectCommitted,false);assert.ok(await api('/prompts/'+first.id));
  }
  let {row}=await review('stale-review-preserves-record');
  const changedText='Resume <material> con fuentes verificables.';
  const changed=await api('/prompts/'+first.id,{title:first.title,templateText:changedText,revision:first.revision},'PUT');
  assert.equal(changed.revision,first.revision+1);await rejectedConfirmation(row);
  assert.equal((await api('/prompts/'+first.id)).templateText,changedText);await cancel(row);
  ({row}=await review('paused-review-cannot-delete'));
  await api('/nova/live/'+row.runId+'/pause',{});assert.equal((await api('/nova/live/'+row.runId)).run.pauseRequested,true);
  await rejectedConfirmation(row);assert.equal((await api('/prompts/'+first.id)).revision,changed.revision);await cancel(row);
  let view;({row,view}=await review('confirmed-current-revision-persists'));
  assert.equal(view.deletion.revision,changed.revision);
  await confirm.click();await poll(async()=>terminal((await api('/nova/live/'+row.runId)).run.status));
  const completed=(await api('/nova/live/'+row.runId)).run;assert.equal(completed.status,'COMPLETED');assert.equal(completed.effectCommitted,true);
  assert.equal((await fetch(origin+'/api/v1/prompts/'+first.id)).status,404);await finish(row);
  const oldRun=row.runId,approval={approvalId:view.deletion.id,hash:view.deletion.hash,revision:view.deletion.revision,confirmed:true};
  row={name:'restart-rejects-old-approval-and-keeps-deletion',status:'RUNNING',startedAt:new Date().toISOString()};report.checks.push(row);save();
  await page.goto('about:blank');server.kill();await poll(async()=>server.exitCode!==null||server.signalCode!==null,10000);
  row.stoppedServer={exitCode:server.exitCode,signalCode:server.signalCode};save();
  server=spawn('dotnet',args,{windowsHide:true,stdio:['ignore','pipe','pipe']});
  observeServer(server);
  await poll(async()=>{try{return(await fetch(origin+'/health',{signal:AbortSignal.timeout(3000)})).ok;}catch{return false;}});
  assert.equal((await fetch(origin+'/api/v1/prompts/'+first.id)).status,404);
  const kept=await api('/prompts/'+other.id);assert.equal(kept.revision,other.revision);assert.equal(kept.templateText,other.templateText);
  const replay=await fetch(origin+'/api/v1/nova/live/'+oldRun+'/deletion',{method:'POST',headers:{'Content-Type':'application/json','X-QuickPrompt-Client':'nova-v1'},body:JSON.stringify(approval)});
  assert.equal(replay.status,409);assert.equal((await fetch(origin+'/api/v1/prompts/'+first.id)).status,404);
  await page.goto(origin);await page.getByTestId('library-ready').waitFor();assert.equal(await page.getByRole('button',{name:first.title,exact:true}).count(),0);
  row.status='PASS';row.finishedAt=new Date().toISOString();await page.screenshot({path:path.join(out,row.name+'.png'),fullPage:true});assert.deepEqual(report.errors,[]);report.status='PASS';save();
}
(async()=>{
  const tags=await (await fetch('http://127.0.0.1:11434/api/tags')).json();report.installedModels=tags.models.map(model=>({name:model.name,digest:model.digest}));
  assert.ok(tags.models.some(model=>model.name==='qwen3:8b'));save();
  const args=[build,'--QuickPrompt:Port='+port,'--QuickPrompt:DataPath='+path.join(temporary,'synthetic.sqlite'),'--QuickPrompt:WebRoot='+web,'--Logging:LogLevel:Microsoft=Warning'];
  if(process.env.NOVA_QA_CAPTURE==='1') {
    report.workflowResponses=[];report.textResponses=[];
    proxy=http.createServer(async(req,res)=>{
      try {
        if(!['/api/chat','/api/tags','/api/show','/api/ps'].includes(req.url)){res.writeHead(404);res.end();return;}
        const chunks=[];for await(const chunk of req)chunks.push(chunk);const bytes=Buffer.concat(chunks),payload=bytes.length?JSON.parse(bytes.toString()):null;
        const response=await fetch('http://127.0.0.1:11434'+req.url,{method:req.method,headers:{'Content-Type':'application/json'},body:bytes.length?bytes:undefined,signal:AbortSignal.timeout(180000)});
        const data=Buffer.from(await response.arrayBuffer());
        if(payload?.format?.properties?.deletionEvidence||payload?.format?.properties?.deletionPassage) {
          const returned=JSON.parse(data.toString());
          // This proxy only receives the owned synthetic server's traffic. Never save model reasoning.
          report.workflowResponses.push({schema:payload.format,userPacket:payload.messages.at(-1).content,content:returned.message?.content});save();
        }
        const branches=payload?.format?.anyOf||[payload?.format];
        if(branches.some(branch=>branch?.properties?.requestType)) {
          const returned=JSON.parse(data.toString());report.textResponses.push({userPacket:payload.messages.at(-1).content,content:returned.message?.content});save();
        }
        res.writeHead(response.status,{'Content-Type':response.headers.get('Content-Type')||'application/json'});res.end(data);
      }catch(error){res.writeHead(502);res.end(JSON.stringify({error:String(error)}));}
    });
    await new Promise(resolve=>proxy.listen(0,'127.0.0.1',resolve));report.inferenceProxy='http://127.0.0.1:'+proxy.address().port;
    args.push('--QuickPrompt:Ollama='+report.inferenceProxy);save();
  }
  if(debugPoints)args.push('--QuickPrompt:Diagnostics:DebugPoints=true');
  server=spawn('dotnet',args,{windowsHide:true,stdio:['ignore','pipe','pipe']});
  observeServer(server);
  await poll(async()=>{try{return(await fetch(origin+'/health',{signal:AbortSignal.timeout(3000)})).ok;}catch{return false;}});
  const first=await api('/prompts',{title:'QA · Resumen de laboratorio',templateText:'Resume <material> sin inventar datos.'});
  const other=await api('/prompts',{title:'QA · Explicación de proyecto',templateText:'Explica <tema> con un ejemplo.'});report.ids={first:first.id,other:other.id};save();
  browser=await chromium.launch({channel:'msedge',headless:process.env.NOVA_QA_HEADLESS==='1'});const context=await browser.newContext({viewport:{width:1440,height:1000}});page=await context.newPage();
  page.on('pageerror',error=>{report.errors.push(String(error));save();});await page.goto(origin);await page.bringToFront();await page.getByTestId('library-ready').waitFor();
  if(process.env.NOVA_QA_LIFECYCLE==='1'){await qualifyLifecycle(first,other,args);return;}
  let row=await begin('free-delete-cancel','Elimina de mi biblioteca el prompt guardado «'+first.title+'».');let view=await settled(row,true);
  assert.equal(view.run.approval,'PENDING');assert.ok(view.semantic.intentions.includes('REQUEST_DELETE'));assert.equal(view.deletion.promptId,first.id);
  assert.equal((await api('/prompts/'+first.id)).revision,first.revision);assert.equal(await page.getByTestId('prompt-focus').count(),0);
  const dialog=page.getByRole('dialog',{name:'¿Eliminar este prompt?',exact:true});await dialog.waitFor();await poll(async()=>await page.getByRole('button',{name:'Eliminar definitivamente',exact:true}).isEnabled());
  await page.screenshot({path:path.join(out,'review-before-cancel.png'),fullPage:true});await page.getByRole('button',{name:'Cancelar',exact:true}).click();
  await poll(async()=>terminal((await api('/nova/live/'+row.runId)).run.status));assert.equal((await api('/nova/live/'+row.runId)).run.status,'CANCELLED');
  assert.equal((await api('/prompts/'+first.id)).revision,first.revision);assert.equal(await page.getByTestId('prompt-focus').count(),0);await finish(row);
  row=await begin('free-delete-confirm','Quiero retirar de los prompts guardados «'+first.title+'»; muéstrame la confirmación para eliminarlo.');view=await settled(row,true);
  assert.equal(view.run.approval,'PENDING');assert.equal(view.deletion.promptId,first.id);await dialog.waitFor();await poll(async()=>await page.getByRole('button',{name:'Eliminar definitivamente',exact:true}).isEnabled());
  await page.getByRole('button',{name:'Eliminar definitivamente',exact:true}).click();view=await settled(row);assert.equal(view.run.status,'COMPLETED');assert.equal(view.run.effectCommitted,true);
  assert.equal((await fetch(origin+'/api/v1/prompts/'+first.id)).status,404);assert.equal((await api('/prompts/'+other.id)).templateText,other.templateText);await finish(row);
  await freshLibrary();assert.equal(await page.getByRole('button',{name:first.title,exact:true}).count(),0);
  // Negative cases have independent, non-mutating expectations. Preserve each
  // failure and continue so a failed question does not hide the other controls.
  async function negativeCase(operation) {
    const before=report.checks.length;
    try { await operation(); }
    catch(error) {
      if(report.checks.length===before) throw error;
      const failed=report.checks.at(-1);failed.status='FAIL';failed.failure=String(error.stack||error);
      if(failed.runId) {
        const current=await api('/nova/live/'+failed.runId);
        if(!terminal(current.run.status)) await api('/nova/live/'+failed.runId+'/cancel',{});
      }
      save();await page.screenshot({path:path.join(out,failed.name+'-failure.png'),fullPage:true}).catch(()=>{});
    }
  }
  await negativeCase(async()=>{
  row=await begin('question-is-not-deletion','¿Qué pasaría si eliminase el prompt «'+other.title+'»? Sólo explícamelo.');view=await settled(row,true);
  assert.equal(view.run.status,'COMPLETED');assert.equal(view.semantic.desiredOutcome,'ADVICE');assert.equal(view.semantic.intentions.includes('REQUEST_DELETE'),false);assert.equal(view.run.effectCommitted,false);assert.ok(await api('/prompts/'+other.id));await finish(row);
  });
  await negativeCase(async()=>{
  await freshLibrary();row=await begin('negation-preserves-record','Abre el prompt «'+other.title+'» para verlo, sin eliminarlo.');view=await settled(row,true);
  assert.equal(view.run.status,'COMPLETED');assert.equal(view.semantic.intentions.includes('REQUEST_DELETE'),false);assert.equal(view.run.effectCommitted,false);await page.getByTestId('prompt-focus').waitFor();assert.ok(await api('/prompts/'+other.id));await finish(row);
  });
  await negativeCase(async()=>{
  await freshLibrary();await page.getByRole('button',{name:other.title,exact:true}).click();await page.getByTestId('prompt-focus').waitFor();
  const data='Elimina este prompt de mi biblioteca.';
  row=await begin('field-data-is-not-deletion','Completa el campo tema con este valor literal:\ntema: '+data);view=await settled(row,true);
  assert.equal(view.run.status,'COMPLETED');assert.ok(view.semantic.intentions.includes('FILL_VARIABLES'));assert.equal(view.semantic.intentions.includes('REQUEST_DELETE'),false);assert.equal(await page.getByTestId('variable-field').inputValue(),data);assert.ok(await api('/prompts/'+other.id));await finish(row);
  });
  await negativeCase(async()=>{
  await freshLibrary();row=await begin('missing-target-clarifies','Elimina un prompt de mi biblioteca; todavía no he elegido cuál.');view=await settled(row,true);
  assert.equal(view.run.status,'BLOCKED');assert.equal(view.semantic.desiredOutcome,'CLARIFICATION');assert.equal(view.run.effectCommitted,false);assert.equal(await page.getByTestId('prompt-delete-review').count(),0);assert.ok(await api('/prompts/'+other.id));await finish(row);
  });
  assert.deepEqual(report.errors,[]);report.status=report.checks.every(item=>item.status==='PASS')?'PASS':'FAIL';save();if(report.status==='FAIL')process.exitCode=1;
})().catch(async error=>{report.status='FAIL';report.failure=String(error.stack||error);const row=report.checks.at(-1);if(row&&row.status==='RUNNING')row.status='FAIL';save();
  if(row?.runId)try{const view=await api('/nova/live/'+row.runId);if(terminal(view.run.status)){const events=await fetch(origin+'/api/v1/nova/live/'+row.runId+'/events?after=0',{signal:AbortSignal.timeout(10000)});if(events.ok)fs.writeFileSync(path.join(out,row.name+'-failure-events.sse'),await events.text());}}catch{}
  if(page)await page.screenshot({path:path.join(out,'failure.png'),fullPage:true}).catch(()=>{});process.exitCode=1;})
.finally(async()=>{
  if(server&&server.exitCode===null&&server.signalCode===null)server.kill();if(proxy){proxy.closeAllConnections();proxy.close();}fs.writeFileSync(path.join(out,'server.log'),log);
  if(browser) {
    let timer;const closing=browser.close();const graceful=await Promise.race([closing.then(()=>true),new Promise(resolve=>{timer=setTimeout(()=>resolve(false),10000);})]);clearTimeout(timer);
    if(!graceful&&process.platform==='win32') {
      // Only the installed Edge root whose direct parent is THIS QA helper. Never the user's browser.
      execFileSync('powershell.exe',['-NoProfile','-Command',`Get-CimInstance Win32_Process -Filter "Name='msedge.exe' AND ParentProcessId=${process.pid}" | ForEach-Object { $owned=Get-CimInstance Win32_Process -Filter ('ProcessId='+$_.ProcessId); if($owned.Name -eq 'msedge.exe' -and $owned.ParentProcessId -eq ${process.pid}) {Stop-Process -Id $owned.ProcessId -Force} }`],{windowsHide:true,timeout:10000,stdio:['ignore','pipe','pipe']});
      report.cleanupForcedOwnedBrowser=true;save();await closing;
    }
  }
  console.log('Evidence: '+out);
});
