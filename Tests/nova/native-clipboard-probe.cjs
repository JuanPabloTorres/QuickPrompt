// Actual manual React copy followed by raw Windows CF_UNICODETEXT inspection.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {spawn,spawnSync}=require('node:child_process'),{randomUUID,createHash}=require('node:crypto');
const {webSha256}=require('./artifact-hashes.cjs');
const {chromium}=require('C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'../..'),stamp=Date.now(),temp=path.join(root,'.playwright','native-clipboard-'+stamp),out=path.join(root,process.env.NOVA_NATIVE_EVIDENCE_ROOT||'docs/evidence/closure-lab-20261021','native-clipboard-'+stamp);
const build=path.resolve(root,process.env.NOVA_TEST_BUILD),web=path.resolve(root,process.env.NOVA_TEST_WEBROOT),port=Number(process.env.NOVA_NATIVE_PORT||5283),base='http://127.0.0.1:'+port;
fs.mkdirSync(temp,{recursive:true});fs.mkdirSync(out,{recursive:true});
const prefix='QP_NATIVE_'+randomUUID(),expected=prefix+'\nLínea dos · Ω · 🚀\n\nFin.\n\n';
const expectedFile=path.join(temp,'expected.json'),nativeFile=path.join(temp,'native.json');fs.writeFileSync(expectedFile,JSON.stringify({syntheticOnly:true,prefix,expected}));
const sourceSnapshot=path.join(out,'harness-source.cjs');fs.copyFileSync(__filename,sourceSnapshot);
const report={schema:'ai-delivery-tests/1',campaignId:'native-clipboard-'+stamp,syntheticOnly:true,status:'UNCONFIRMED',scope:'RAW_WINDOWS_CLIPBOARD_NO_NORMALIZATION',checks:[],rows:[],count:0,skipped:0,
 harnessSha256:createHash('sha256').update(fs.readFileSync(sourceSnapshot)).digest('hex'),
 probeSha256:createHash('sha256').update(fs.readFileSync(path.join(__dirname,'read-owned-native-clipboard.ps1'))).digest('hex'),
 apiSha256:createHash('sha256').update(fs.readFileSync(build)).digest('hex').toUpperCase(),webSha256:webSha256(web)};
function record(name,status,expected,observed){
 const frame=new Error().stack.split('\n')[2].match(/:(\d+):\d+\)?$/);
 report.checks.push({name,status});report.rows.push({id:name,title:name,status,observedAt:new Date().toISOString(),expected,observed,
  location:{file:sourceSnapshot,line:frame?Number(frame[1]):null,symbol:'native clipboard probe',kind:'assertion_observed',sourceSha256:report.harnessSha256},
  comparisons:[{field:'raw_equality',expected,observed,status}]});report.count=report.rows.length;
}
let server,browser,log='';
async function until(fn){const end=Date.now()+25000;while(Date.now()<end){if(await fn())return;await new Promise(r=>setTimeout(r,100));}throw Error('OWNED_QA_TIMEOUT');}
async function api(route,body){const r=await fetch(base+'/api/v1'+route,{method:body?'POST':'GET',headers:{'Content-Type':'application/json','X-QuickPrompt-Client':'nova-v1'},body:body?JSON.stringify(body):undefined});assert(r.ok);return r.json();}
(async()=>{
 server=spawn('dotnet',[build,'--QuickPrompt:Port='+port,'--QuickPrompt:DataPath='+path.join(temp,'synthetic.sqlite'),'--QuickPrompt:WebRoot='+web,'--QuickPrompt:Ollama=http://127.0.0.1:1',...JSON.parse(process.env.NOVA_AGENT_OPTIONS||'[]')],{windowsHide:true,stdio:['ignore','pipe','pipe']});
 server.stdout.on('data',b=>log+=b);server.stderr.on('data',b=>log+=b);
 await until(()=>{if(server.exitCode!==null)throw Error('OWNED_API_EXITED');return log.includes('Now listening on: '+base);});
 const source=await api('/prompts',{title:'QA · Portapapeles Windows exacto',templateText:expected});
 browser=await chromium.launch({channel:'msedge',headless:false});const context=await browser.newContext({permissions:['clipboard-read','clipboard-write']}),page=await context.newPage();
 await page.goto(base);await page.getByRole('button',{name:source.title,exact:true}).click();await page.getByTestId('compile-ready').waitFor();
 assert.equal(await page.getByTestId('compiled-preview').textContent(),expected);
 // Inspect real browser download bytes. The owned sentinel is the only clipboard
 // value read; unrelated clipboard content never enters this report.
 const sentinel='QP_DOWNLOAD_'+randomUUID();
 await page.evaluate(text=>navigator.clipboard.writeText(text),sentinel);
 const downloadEvent=page.waitForEvent('download');
 await page.getByRole('button',{name:'Descargar texto (.txt)',exact:true}).click();
 const download=await downloadEvent,downloadFile=await download.path();
 const bytes=fs.readFileSync(downloadFile),expectedBytes=Buffer.from(expected,'utf8');
 const afterDownload=await api('/prompts/'+source.id);
 const unchangedClipboard=await page.evaluate(async text=>(await navigator.clipboard.readText())===text,sentinel);
 const exactDownload=bytes.equals(expectedBytes)&&afterDownload.uses===0&&afterDownload.templateText===expected&&unchangedClipboard;
 record('downloaded-utf8-lf-exact-no-side-effects',exactDownload?'PASS':'FAIL',
  {utf8Sha256:createHash('sha256').update(expectedBytes).digest('hex'),uses:0,sourceUnchanged:true,clipboardUnchanged:true},
  {utf8Sha256:createHash('sha256').update(bytes).digest('hex'),uses:afterDownload.uses,sourceUnchanged:afterDownload.templateText===expected,clipboardUnchanged:unchangedClipboard});
 assert.ok(exactDownload,'Downloaded bytes and authority must match the local file contract');
 fs.writeFileSync(path.join(out,'downloaded-text.txt'),bytes);
 await page.getByRole('button',{name:'Copiar prompt',exact:true}).click();await until(async()=>(await api('/prompts/'+source.id)).uses===1);
 record('real-manual-copy-compiled-source-and-one-use','PASS',{compiledText:expected,uses:1},{compiledText:expected,uses:1});
 const probe=spawnSync(process.env.NOVA_PWSH||'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe',
  ['-NoProfile','-STA','-File',path.join(__dirname,'read-owned-native-clipboard.ps1'),'-ExpectedFile',expectedFile,'-ResultFile',nativeFile],{windowsHide:true,encoding:'utf8',timeout:10000});
 if(!fs.existsSync(nativeFile))throw Error('NATIVE_PROBE_UNAVAILABLE');report.native=JSON.parse(fs.readFileSync(nativeFile,'utf8'));
 record('raw-Win32-CF_UNICODETEXT-equals-original-compiled-text',report.native.status,report.native.expected,report.native.observed);report.status=report.native.status;
 // Independent platform control, not a replacement for the original LF-exact
 // requirement: CF_UNICODETEXT specifies CRLF (Microsoft Standard Clipboard Formats).
 const windowsText=report.native.expected.replace(/\n/g,'\r\n');
 record('windows-standard-unicode-text-and-lines',report.native.observed===null?'UNCONFIRMED':report.native.observed===windowsText?'PASS':'FAIL',windowsText,report.native.observed);
 report.platformContract='https://learn.microsoft.com/en-us/windows/win32/dataxchg/standard-clipboard-formats';
 const savedBefore=await api('/prompts/'+source.id),draftText='Borrador sin guardar · Ω · 🚀\n\nFinal.\n\n';
 await page.evaluate(text=>navigator.clipboard.writeText(text),sentinel);
 await page.getByRole('button',{name:'Editar',exact:true}).click();
 await page.getByLabel('Contenido',{exact:true}).fill(draftText);
 await until(async()=>await page.getByTestId('compile-ready').isVisible()&&await page.getByTestId('compiled-preview').textContent()===draftText);
 const draftDownloadEvent=page.waitForEvent('download');
 await page.getByRole('button',{name:'Descargar texto (.txt)',exact:true}).click();
 const draftDownload=await draftDownloadEvent,draftBytes=fs.readFileSync(await draftDownload.path());
 const draftSourceUnchanged=JSON.stringify(await api('/prompts/'+source.id))===JSON.stringify(savedBefore);
 const draftClipboardUnchanged=await page.evaluate(async text=>(await navigator.clipboard.readText())===text,sentinel);
 const draftPass=draftBytes.equals(Buffer.from(draftText,'utf8'))&&draftSourceUnchanged&&draftClipboardUnchanged;
 record('downloaded-unsaved-draft-utf8-lf-no-side-effects',draftPass?'PASS':'FAIL',
  {text:draftText,sourceUnchanged:true,clipboardUnchanged:true},
  {text:draftBytes.toString('utf8'),sourceUnchanged:draftSourceUnchanged,clipboardUnchanged:draftClipboardUnchanged});
 assert.ok(draftPass,'Unsaved draft export must not save, copy or increment usage');
 await page.screenshot({path:path.join(out,'native-copy.png')});if(probe.status!==0)process.exitCode=probe.status||1;
})().catch(e=>{report.status='FAIL';report.error=e.message;process.exitCode=1;})
.finally(async()=>{await browser?.close();if(server?.exitCode===null){server.kill();await new Promise(r=>server.once('exit',r));}
 fs.writeFileSync(path.join(out,'results.json'),JSON.stringify(report,null,2));fs.writeFileSync(path.join(out,'server.log'),log);
 console.log(JSON.stringify({status:report.status,code:report.native?.code,evidence:out}));});
