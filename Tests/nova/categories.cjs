// PAR-01: real React/API/SQLite, with installed Edge. No model calls or user data.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { createHash } = require('node:crypto'), { spawn } = require('node:child_process');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
const temporary = path.join(root, '.playwright/categories-' + stamp);
const out = path.join(root, 'docs/evidence/categories-20261019', String(stamp));
const apiBuild = path.resolve(root, process.env.NOVA_TEST_BUILD || 'backend/QuickPrompt.Api/bin/Debug/net8.0/QuickPrompt.Api.dll');
const web = path.resolve(root, process.env.NOVA_TEST_WEBROOT || 'apps/web/dist');
const port = Number(process.env.NOVA_QA_PORT || 5240), origin = 'http://127.0.0.1:' + port;
fs.mkdirSync(temporary, { recursive:true }); fs.mkdirSync(out, { recursive:true });
const report = { status:'RUNNING', syntheticOnly:true, origin, apiBuild, web,
  hashes:Object.fromEntries(['QuickPrompt.Api.dll','QuickPrompt.Application.dll','QuickPrompt.Infrastructure.dll','QuickPrompt.Domain.dll'].map(name=>[name,createHash('sha256').update(fs.readFileSync(path.join(path.dirname(apiBuild),name))).digest('hex')])),
  webIndexSHA256:createHash('sha256').update(fs.readFileSync(path.join(web,'index.html'))).digest('hex'), checks:[], errors:[] };
let server, browser, page, log='';
const save = () => fs.writeFileSync(path.join(out,'results.json'),JSON.stringify(report,null,2));
const check = name => { report.checks.push({name,status:'PASS'}); save(); };
async function api(route, body, method=body ? 'POST' : 'GET') {
  const response = await fetch(origin+'/api/v1'+route,{method,headers:{'Content-Type':'application/json','X-QuickPrompt-Client':'nova-v1'},body:body?JSON.stringify(body):undefined});
  assert.ok(response.ok, await response.clone().text()); return response.status===204 ? null : response.json();
}
async function poll(read,timeout=20000) {
  const end=Date.now()+timeout; while(Date.now()<end) { const value=await read(); if(value)return value; await new Promise(resolve=>setTimeout(resolve,100)); } throw Error('QA_TIMEOUT');
}
async function editorCategory() {
  await page.getByTestId('prompt-editor').waitFor();
  const details=page.getByTestId('prompt-editor').locator('details').filter({has:page.locator('summary',{hasText:'Categoría y descripción opcionales'})});
  if(!await details.evaluate(element=>element.open)) await details.locator('summary').click();
  const category=page.getByRole('combobox',{name:'Categoría (opcional)',exact:true}); await category.waitFor();
  await poll(async()=>!await category.isDisabled()); return category;
}
(async()=>{
  server=spawn('dotnet',[apiBuild,'--QuickPrompt:Port='+port,'--QuickPrompt:DataPath='+path.join(temporary,'synthetic.sqlite'),'--QuickPrompt:WebRoot='+web,'--Logging:LogLevel:Microsoft.Hosting.Lifetime=Information'],{windowsHide:true,stdio:['ignore','pipe','pipe']});
  server.stdout.on('data',chunk=>{log+=chunk;});server.stderr.on('data',chunk=>{log+=chunk;});
  await poll(()=>{if(server.exitCode!==null)throw Error('API_EXITED');return log.includes('Now listening on: '+origin);});
  assert.equal((await api('/prompts')).total,0);
  const categories=await api('/categories'); report.categories=categories;
  assert.equal(categories.length,29); assert.equal(new Set(categories.map(category=>category.id)).size,29);
  const programming=categories.find(category=>category.name==='Programación'), education=categories.find(category=>category.name==='Educación');
  assert.ok(programming&&education);check('domain-catalogue-no-inference-or-writes');
  browser=await chromium.launch({channel:'msedge',headless:process.env.NOVA_QA_HEADLESS==='1'});
  const context=await browser.newContext({viewport:{width:1440,height:1000},acceptDownloads:true});page=await context.newPage();
  page.on('pageerror',error=>{report.errors.push(String(error));save();});
  await page.goto(origin);await page.bringToFront();
  await page.getByRole('button',{name:'Nuevo prompt',exact:true}).click();
  await page.getByLabel('Título',{exact:true}).fill('QA · Categorías persistidas');
  await page.getByLabel('Contenido',{exact:true}).fill('Explica <tema> sin inventar datos.');
  let category=await editorCategory();await category.selectOption(String(programming.id));
  await page.getByRole('button',{name:'Guardar prompt',exact:true}).click();await page.getByTestId('prompt-focus').waitFor();
  let document=(await api('/prompts')).items[0];report.promptId=document.id;
  assert.equal(document.categoryId,programming.id);assert.equal(document.templateText,'Explica <tema> sin inventar datos.');check('create-and-save-selected-category');
  await page.reload();await page.getByRole('button',{name:document.title,exact:true}).click();
  await page.getByRole('button',{name:'Editar',exact:true}).click();category=await editorCategory();
  assert.equal(await category.inputValue(),String(programming.id));check('reload-and-edit-retain-category');
  await category.selectOption(String(education.id));await page.getByRole('button',{name:'Guardar versión',exact:true}).click();await page.getByTestId('prompt-focus').waitFor();
  document=await api('/prompts/'+document.id);assert.equal(document.categoryId,education.id);assert.equal(document.revision,2);check('change-category-uses-current-revision');
  await page.getByRole('button',{name:'Editar',exact:true}).click();category=await editorCategory();
  await category.selectOption('0');await page.getByRole('button',{name:'Cancelar',exact:true}).click();
  await page.getByRole('button',{name:'Salir del editor',exact:true}).click();await page.getByTestId('prompt-focus').waitFor();
  assert.equal((await api('/prompts/'+document.id)).categoryId,education.id);assert.equal((await api('/prompts/'+document.id)).revision,2);check('cancel-does-not-write-category');
  await page.getByRole('button',{name:'Configuración',exact:true}).click();
  const [download]=await Promise.all([page.waitForEvent('download'),page.getByRole('button',{name:'Exportar respaldo JSON',exact:true}).click()]);
  const exportPath=path.join(out,'synthetic-export.json');await download.saveAs(exportPath);
  const exported=JSON.parse(fs.readFileSync(exportPath,'utf8'));assert.equal(exported.templates.find(template=>template.id===document.id).category,education.id);check('ui-export-retains-category');
  await page.getByRole('button',{name:'Cerrar diálogo',exact:true}).click();
  await page.reload();await page.getByRole('button',{name:document.title,exact:true}).click();await page.getByRole('button',{name:'Editar',exact:true}).click();
  category=await editorCategory();assert.equal(await category.inputValue(),String(education.id));
  await page.screenshot({path:path.join(out,'category-desktop.png'),fullPage:true});
  await page.setViewportSize({width:375,height:812});await category.scrollIntoViewIfNeeded();
  const box=await category.boundingBox();assert.ok(box&&box.x>=0&&box.x+box.width<=375);
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
  await page.screenshot({path:path.join(out,'category-mobile.png'),fullPage:true});check('mobile-visible-labeled-selector-without-overflow');
  const invalid=await fetch(origin+'/api/v1/prompts',{method:'POST',headers:{'Content-Type':'application/json','X-QuickPrompt-Client':'nova-v1'},body:JSON.stringify({title:'Invalid',templateText:'Nothing',categoryId:99999})});
  assert.equal(invalid.status,400);assert.equal((await api('/prompts')).total,1);check('unknown-category-rejected-without-write');
  assert.deepEqual(report.errors,[]);report.status='PASS';save();
})().catch(async error=>{report.status='FAIL';report.failure=String(error.stack||error);save();if(page)await page.screenshot({path:path.join(out,'failure.png'),fullPage:true}).catch(()=>{});process.exitCode=1;})
  .finally(async()=>{if(browser)await browser.close();if(server&&server.exitCode===null)server.kill();fs.writeFileSync(path.join(out,'server.log'),log);console.log('Evidence: '+out);});
