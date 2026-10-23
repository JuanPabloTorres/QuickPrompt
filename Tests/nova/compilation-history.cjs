// Real UI/API/SQLite history workflow. Inference is unavailable; response is an explicit archive fixture.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {spawn}=require('node:child_process'),{randomUUID,createHash}=require('node:crypto');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'../..'),stamp=Date.now(),temp=path.join(root,'.playwright','compilation-history-'+stamp),out=path.join(root,'docs/evidence/skill-completion-20261021','history-'+stamp);
const build=path.resolve(root,process.env.NOVA_TEST_BUILD||'.playwright/skill-completion-20261021/api/QuickPrompt.Api.dll'),web=path.resolve(root,process.env.NOVA_WEB_BUILD||'.playwright/skill-completion-20261021/web');
const base='http://127.0.0.1:5274',headers={'Content-Type':'application/json','X-QuickPrompt-Client':'nova-v1'};
fs.mkdirSync(temp,{recursive:true});fs.mkdirSync(out,{recursive:true});
const checks=[],errors=[],external=[];let server,browser,page,log='';
const report={status:'RUNNING',syntheticOnly:true,inference:'UNAVAILABLE_BY_DESIGN',response:'EXPLICIT_ARCHIVE_FIXTURE_NOT_MODEL_OUTPUT',checks,errors,external,apiSha256:createHash('sha256').update(fs.readFileSync(build)).digest('hex'),webIndexSha256:createHash('sha256').update(fs.readFileSync(path.join(web,'index.html'))).digest('hex')};
const save=()=>fs.writeFileSync(path.join(out,'results.json'),JSON.stringify(report,null,2));
async function observeClipboard(expected) {
 const observed=await page.evaluate(()=>navigator.clipboard.readText());
 report.nativeClipboardEquality??=[];
 report.nativeClipboardEquality.push({status:observed===expected?'PASS':'FAIL',expected,observed,scope:'RAW_EQUALITY_NO_NORMALIZATION_VAR_04'});
 save(); return observed;
}
const until=async fn=>{const end=Date.now()+25000;while(Date.now()<end){if(await fn())return;await new Promise(r=>setTimeout(r,100));}throw Error('QA_TIMEOUT');};
async function api(route,body,method=body?'POST':'GET'){const res=await fetch(base+'/api/v1'+route,{method,headers,body:body?JSON.stringify(body):undefined});assert.ok(res.ok,route+':'+res.status+' '+(!res.ok?await res.text():''));return res.status===204?null:res.json();}
async function start(){log='';server=spawn('dotnet',[build,'--QuickPrompt:Port=5274','--QuickPrompt:DataPath='+path.join(temp,'synthetic.sqlite'),'--QuickPrompt:WebRoot='+web,'--QuickPrompt:Ollama=http://127.0.0.1:1'],{cwd:root,windowsHide:true,stdio:['ignore','pipe','pipe']});server.stdout.on('data',b=>log+=b);server.stderr.on('data',b=>log+=b);await until(()=>{if(server.exitCode!==null)throw Error('API_EXIT');return log.includes('Now listening on: '+base);});}
async function stop(){if(server?.exitCode===null){const stopped=new Promise(r=>server.once('exit',r));server.kill();await stopped;}}
async function check(name,fn){await fn();checks.push({name,status:'PASS'});save();console.log('PASS',name);}
(async()=>{
 await start();
 let source=await api('/prompts',{title:'Historial Unicode',templateText:'Explica <tema>.'});
 const other=await api('/prompts',{title:'Otro registro',templateText:'Texto independiente.'});
 browser=await chromium.launch({headless:false,channel:'msedge'});const context=await browser.newContext({viewport:{width:1280,height:900},permissions:['clipboard-read','clipboard-write']});page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(!r.url().startsWith(base)&&!r.url().startsWith('data:')&&!r.url().startsWith('blob:'))external.push(r.url());});
 const literal='C# & .NET 🚀\n<literal> Ω';let saved;
 await check('manual copy records the exact compiled snapshot through the current owner',async()=>{
  await page.goto(base);await page.getByRole('button',{name:source.title,exact:true}).click();await page.getByTestId('variable-field').fill(literal);await page.getByTestId('compile-ready').waitFor();await page.getByRole('button',{name:'Copiar prompt',exact:true}).click();
  await until(async()=>{const history=await api('/history');saved=history.items[0];return history.total===1;});assert.equal(saved.text,'Explica '+literal+'.');assert.equal((await api('/prompts/'+source.id)).uses,1);
 });
 await check('history preserves content after source changes and joins only its stored response',async()=>{
  source=await api('/prompts/'+source.id,{title:'Título posterior',templateText:'Contenido posterior.',description:'',categoryId:source.categoryId,revision:source.revision},'PUT');
  const archive=await api('/library/export');archive.executions.push({id:randomUUID(),compiledId:saved.id,model:'qwen3:8b',response:'Respuesta de fixture\nΩ <literal>',elapsedMs:120,createdAt:new Date().toISOString()});
  const preview=await api('/library/restore/preview',archive);await api('/library/restore',{data:archive,confirmed:true,expectedSnapshot:preview.expectedSnapshot});
  await page.getByRole('button',{name:'Historial de resultados',exact:true}).click();await page.getByText(/Historial Unicode · versión 1/).click();
  assert.equal(await page.getByRole('dialog').locator('pre').nth(0).textContent(),saved.text);assert.equal(await page.getByRole('dialog').locator('pre').nth(1).textContent(),'Respuesta de fixture\nΩ <literal>');
  await page.getByRole('button',{name:'Copiar respuesta guardada',exact:true}).click();await observeClipboard('Respuesta de fixture\nΩ <literal>');await page.getByText('Resultado del historial copiado.',{exact:true}).waitFor();
  await page.screenshot({path:path.join(out,'history-response.png'),fullPage:true});await page.getByRole('button',{name:'Cerrar historial',exact:true}).click();
 });
 await check('pagination and restart retain snapshots after the source is deleted',async()=>{
  await api('/prompts/'+source.id,{revision:source.revision,confirmed:true},'DELETE');
  for(let i=0;i<10;i++)await api('/prompts/'+other.id+'/usage',{revision:other.revision,values:{},operationId:randomUUID(),expectedText:other.templateText});
  const second=await api('/history?page=1');assert.equal(second.total,11);assert.equal(second.items.length,1);assert.equal(second.items[0].id,saved.id);
  await stop();await start();await page.goto(base);await page.getByRole('button',{name:'Historial de resultados',exact:true}).click();await page.getByRole('button',{name:'Siguiente',exact:true}).click();await page.getByText(/Historial Unicode · versión 1/).click();
  assert.equal(await page.getByRole('dialog').locator('pre').nth(0).textContent(),saved.text);assert.equal(await page.getByRole('button',{name:'Siguiente',exact:true}).isDisabled(),true);
  const before=await api('/library/export');await page.getByRole('button',{name:'Copiar instrucción guardada',exact:true}).click();await observeClipboard(saved.text);assert.deepEqual(await api('/library/export'),before);
  const downloaded=page.waitForEvent('download');await page.getByRole('button',{name:'Descargar instrucción',exact:true}).click();assert.equal(fs.readFileSync(await (await downloaded).path(),'utf8'),saved.text);
  for(const width of [320,390,768,1280]) {
   await page.setViewportSize({width,height:900});
   const layout=await page.getByRole('dialog').evaluate(element=>({width:element.clientWidth,scroll:element.scrollWidth,document:document.documentElement.scrollWidth,viewport:window.innerWidth}));
   assert.ok(layout.scroll<=layout.width+1,JSON.stringify(layout));assert.ok(layout.document<=layout.viewport+1,JSON.stringify(layout));
   report.viewports??=[];report.viewports.push({width,layout});
   if(width===320)await page.screenshot({path:path.join(out,'history-mobile.png'),fullPage:true});
  }
  await page.setViewportSize({width:1280,height:900});
  await page.keyboard.press('Escape');assert.equal(await page.getByRole('dialog').count(),0);assert.equal(await page.getByRole('button',{name:'Historial de resultados',exact:true}).evaluate(element=>element===document.activeElement),true);
  await page.getByRole('button',{name:'Activar tema claro',exact:true}).click();await page.getByRole('button',{name:'Historial de resultados',exact:true}).click();await page.getByRole('button',{name:'Siguiente',exact:true}).click();await page.getByText(/Historial Unicode · versión 1/).click();
  await page.screenshot({path:path.join(out,'history-light.png'),fullPage:true});
  await page.screenshot({path:path.join(out,'history-restart.png'),fullPage:true});
 });
 await check('invalid pages are rejected and no inference or external network is used',async()=>{
  const invalid=await fetch(base+'/api/v1/history?page=-1');assert.equal(invalid.status,400);const cross=await fetch(base+'/api/v1/history',{headers:{Origin:'https://untrusted.invalid'}});assert.equal(cross.status,403);assert.deepEqual(errors,[]);assert.deepEqual(external,[]);
 });
 await check('clearing requires fresh explicit consent, rejects concurrent changes and persists across restart',async()=>{
  await page.getByRole('button',{name:'Borrar historial…',exact:true}).click();assert.equal(await page.getByRole('button',{name:'Confirmar borrado del historial',exact:true}).isDisabled(),true);
  await api('/prompts/'+other.id+'/usage',{revision:other.revision,values:{},operationId:randomUUID(),expectedText:other.templateText});
  await page.getByLabel('Confirmo borrar este historial',{exact:true}).check();const rejected=page.waitForResponse(r=>r.url()===base+'/api/v1/history/clear');await page.getByRole('button',{name:'Confirmar borrado del historial',exact:true}).click();assert.equal((await rejected).status(),409);await page.getByRole('alert').waitFor();assert.equal((await api('/history')).total,12);
  await page.getByRole('button',{name:'Reintentar historial',exact:true}).click();await page.getByRole('button',{name:'Borrar historial…',exact:true}).click();await page.getByRole('button',{name:'Conservar historial',exact:true}).click();assert.equal((await api('/history')).total,12);
  const preserved=await api('/prompts/'+other.id);await page.getByRole('button',{name:'Borrar historial…',exact:true}).click();await page.getByLabel('Confirmo borrar este historial',{exact:true}).check();await page.getByRole('button',{name:'Confirmar borrado del historial',exact:true}).click();await page.getByText(/No hay resultados guardados/).waitFor();assert.deepEqual(await api('/prompts/'+other.id),preserved);
  await page.screenshot({path:path.join(out,'history-cleared.png'),fullPage:true});await stop();await start();const final=await api('/history');assert.equal(final.total,0);assert.equal(final.responseCount,0);assert.deepEqual(await api('/prompts/'+other.id),preserved);
 });
 report.historyStatus='PASS';report.status=report.nativeClipboardEquality?.some(check=>check.status==='FAIL')?'PARTIAL_NATIVE_CLIPBOARD_MISMATCH':'PASS';save();console.log('EVIDENCE',out);
})().catch(async error=>{report.status='FAIL';report.error=error.stack;save();console.error(error);await page?.screenshot({path:path.join(out,'failure.png'),fullPage:true}).catch(()=>{});process.exitCode=1;}).finally(async()=>{await browser?.close();await stop();});
