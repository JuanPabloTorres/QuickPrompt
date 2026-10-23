// Real UI -> domain compiler -> SQLite, with inference deliberately unavailable.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {spawn}=require('node:child_process'); const {createHash}=require('node:crypto');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'../..'),stamp=Date.now();
const temp=path.join(root,'.playwright','editor-defaults-'+stamp),out=path.join(root,'docs/evidence/skill-close-20261021','editor-defaults-'+stamp);
const build=path.resolve(root,process.env.NOVA_TEST_BUILD||'.playwright/skill-close-20261021/api/QuickPrompt.Api.dll');
const web=path.resolve(root,process.env.NOVA_WEB_BUILD||'.playwright/skill-close-20261021/web-v2');
const base='http://127.0.0.1:5273',headers={'Content-Type':'application/json','X-QuickPrompt-Client':'nova-v1'};
fs.mkdirSync(temp,{recursive:true});fs.mkdirSync(out,{recursive:true});
const checks=[],errors=[],external=[];let server,browser,page,log='';
const report={status:'RUNNING',syntheticOnly:true,inference:'UNAVAILABLE_BY_DESIGN',checks,errors,external,
  apiSha256:createHash('sha256').update(fs.readFileSync(build)).digest('hex'),webIndexSha256:createHash('sha256').update(fs.readFileSync(path.join(web,'index.html'))).digest('hex')};
const save=()=>fs.writeFileSync(path.join(out,'results.json'),JSON.stringify(report,null,2));
const until=async fn=>{const end=Date.now()+25000;while(Date.now()<end){if(await fn())return;await new Promise(r=>setTimeout(r,100));}throw Error('QA_TIMEOUT');};
async function api(route,body,method=body?'POST':'GET') {const res=await fetch(base+'/api/v1'+route,{method,headers,body:body?JSON.stringify(body):undefined});assert.ok(res.ok,route+':'+res.status);return res.json();}
async function start(){log='';server=spawn('dotnet',[build,'--QuickPrompt:Port=5273','--QuickPrompt:DataPath='+path.join(temp,'synthetic.sqlite'),'--QuickPrompt:WebRoot='+web,'--QuickPrompt:Ollama=http://127.0.0.1:1'],{cwd:root,windowsHide:true,stdio:['ignore','pipe','pipe']});server.stdout.on('data',b=>log+=b);server.stderr.on('data',b=>log+=b);await until(()=>{if(server.exitCode!==null)throw Error('API_EXIT');return log.includes('Now listening on: '+base);});}
async function stop(){if(server?.exitCode===null){const stopped=new Promise(r=>server.once('exit',r));server.kill();await stopped;}}
async function check(name,fn){await fn();checks.push({name,status:'PASS'});save();console.log('PASS',name);}
async function edit(){await page.locator('[data-nova-control="prompt.edit"]').click();await page.getByTestId('prompt-editor').waitFor();await page.getByText('Valores por defecto de las variables',{exact:true}).click();}
(async()=>{
 await start();
 const source=await api('/prompts',{title:'Defaults sintéticos',templateText:'Explica <tema> a <audiencia>.',defaults:{tema:'inicial',audiencia:'principiantes'}});
 const other=await api('/prompts',{title:'Registro independiente',templateText:'Sin cambios.'});
 browser=await chromium.launch({channel:'msedge',headless:false});page=await browser.newPage({viewport:{width:1366,height:950}});
 page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(!r.url().startsWith(base)&&!r.url().startsWith('data:'))external.push(r.url());});
 await page.goto(base);await page.getByRole('button',{name:source.title,exact:true}).click();await edit();
 const literal='C# & .NET 🚀\n<literal> Ω';
 await check('edit defaults in real form, undo/redo and persist exact Unicode',async()=>{
  const field=page.getByLabel('Valor por defecto de tema',{exact:true});await field.fill(literal);
  await page.getByRole('button',{name:'Deshacer',exact:true}).click();assert.equal(await field.inputValue(),'inicial');
  await page.getByRole('button',{name:'Rehacer',exact:true}).click();assert.equal(await field.inputValue(),literal);
  await page.getByRole('button',{name:'Guardar versión',exact:true}).click();await page.getByTestId('prompt-focus').waitFor();
  const actual=await api('/prompts/'+source.id);assert.equal(actual.revision,2);assert.deepEqual(actual.defaults,{tema:literal,audiencia:'principiantes'});assert.equal(actual.templateText,source.templateText);
  await page.getByTestId('compile-ready').waitFor();assert.equal(await page.getByTestId('compiled-preview').textContent(),'Explica '+literal+' a principiantes.');
  await page.screenshot({path:path.join(out,'defaults-saved.png'),fullPage:true});
 });
 await check('server restart and reopening preserve defaults and compilation',async()=>{
  await stop();await start();await page.reload();await page.getByRole('button',{name:source.title,exact:true}).click();
  await page.getByTestId('compile-ready').waitFor();assert.equal(await page.getByTestId('compiled-preview').textContent(),'Explica '+literal+' a principiantes.');
  assert.deepEqual(await api('/prompts/'+other.id),other);
 });
 await check('removing a default restores required input without changing template syntax',async()=>{
  await edit();await page.getByLabel('Valor por defecto de tema',{exact:true}).fill('');
  await page.getByRole('button',{name:'Guardar versión',exact:true}).click();await page.getByTestId('prompt-focus').waitFor();
  assert.deepEqual((await api('/prompts/'+source.id)).defaults,{audiencia:'principiantes'});
  assert.equal(await page.getByRole('button',{name:'Copiar prompt',exact:true}).isDisabled(),true);
 });
 await check('stale save rejects overwrite and retains the user draft',async()=>{
  await edit();await page.getByLabel('Valor por defecto de tema',{exact:true}).fill('Mi borrador');
  const current=await api('/prompts/'+source.id);const revised=await api('/prompts/'+source.id,{title:current.title,templateText:current.templateText,description:current.description,categoryId:current.categoryId,revision:current.revision,defaults:{tema:'Cambio concurrente',audiencia:'principiantes'}},'PUT');
  const response=page.waitForResponse(r=>r.url()===base+'/api/v1/prompts/'+source.id&&r.request().method()==='PUT');
  await page.getByRole('button',{name:'Guardar versión',exact:true}).click();assert.equal((await response).status(),409);
  assert.equal(await page.getByLabel('Valor por defecto de tema',{exact:true}).inputValue(),'Mi borrador');assert.deepEqual(await api('/prompts/'+source.id),revised);
 });
 await check('no page errors, external requests or inference dependency',async()=>{assert.deepEqual(errors,[]);assert.deepEqual(external,[]);});
 report.status='PASS';save();console.log('EVIDENCE',out);
})().catch(async error=>{report.status='FAIL';report.error=error.stack;save();console.error(error);await page?.screenshot({path:path.join(out,'failure.png'),fullPage:true}).catch(()=>{});process.exitCode=1;}).finally(async()=>{await browser?.close();await stop();});
