// DOM fixtures only. Network routes replace both provider sites with authored HTML.
// chrome.runtime below is a labelled test stub, NOT an extension or authenticated session.
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs=require('fs'),path=require('path'),assert=require('assert/strict'),{randomUUID}=require('crypto');
const out=process.env.QP_EVIDENCE_DIR||'docs/evidence/phase03',records=[];
const request=(action='PrepareOnly',text='ES / EN\n"quotes" <literal> 😀')=>({version:1,requestId:randomUUID(),provider:'ChatGptWeb',compiledPrompt:text,action,createdAtUtc:new Date().toISOString(),expiresAtUtc:new Date(Date.now()+60000).toISOString(),explicitUserAction:true});
(async()=>{fs.mkdirSync(out,{recursive:true});const browser=await chromium.launch({channel:'msedge',headless:true});try{
const c=await browser.newContext();let html;
await c.route('https://chatgpt.com/**',r=>r.fulfill({body:html,contentType:'text/html'}));await c.route('https://claude.ai/**',r=>r.fulfill({body:html,contentType:'text/html'}));
for(const [provider,host,adapter] of [['ChatGptWeb','chatgpt.com','chatgpt'],['ClaudeWeb','claude.ai','claude']]){
 const p=await c.newPage();
 const setup=async({empty=false,login=false,disabled=false,ambiguous=false,confirm=true}={})=>{
  html=`<!doctype html><h1>LOCAL DOM FIXTURE ONLY</h1><style>[contenteditable]{min-height:50px}</style>${login?'<a href="/auth/login">Login</a><a href="/login">Login</a>':''}${empty?'':'<div id="prompt-textarea" class="ProseMirror" contenteditable="true"></div>'}<button data-testid="send-button" aria-label="Send message" ${disabled?'disabled':''}>Send</button>${ambiguous?'<button data-testid="send-button" aria-label="Send message">Ambiguous</button>':''}<output id="count">0</output>`;
  await p.goto('https://'+host+(provider==='ClaudeWeb'?'/new':'/'));await p.bringToFront();
  await p.evaluate(confirm=>{window.qpListeners=[];window.chrome={runtime:{onMessage:{addListener:f=>qpListeners.push(f)}}};window.qpCancel=id=>qpListeners.forEach(f=>f({kind:'qp-cancel',requestId:id}));document.querySelector('button').onclick=()=>{document.querySelector('#count').textContent=String(+document.querySelector('#count').textContent+1);if(confirm)document.querySelector('[contenteditable]').textContent='';};},confirm);
  await p.addScriptTag({path:'extensions/quickprompt-browser/content/providers/editor.js'});await p.addScriptTag({path:`extensions/quickprompt-browser/content/providers/${adapter}.js`});
 };
 const prepare=r=>p.evaluate(r=>QuickPromptProvider.prepare(r),{...r,provider});
 const commit=r=>p.evaluate(id=>QuickPromptProvider.commit(id),r.requestId);
 const count=()=>p.locator('#count').innerText();
 const record=(name,status)=>records.push({provider,case:name,status,scope:'LOCAL_DOM_FIXTURE_ONLY'});
 await setup();let r=request(),result=await prepare(r);assert.equal(result.state,'Prepared');assert.equal(await p.locator('[contenteditable]').innerText(),r.compiledPrompt);assert.equal(await count(),'0');record('prepare exact Unicode/newlines, zero submissions','PASS');
 await setup();r=request('PrepareAndSend');assert.equal((await prepare(r)).state,'Prepared');assert.equal((await commit(r)).state,'SubmittedUI');assert.equal((await commit(r)).state,'Failed');assert.equal(await count(),'1');record('explicit send once; repeated commit refused','PASS');
 await setup();await p.locator('[contenteditable]').fill('Mi borrador');assert.equal((await prepare(request('PrepareAndSend'))).code,'editor-not-empty');assert.equal(await p.locator('[contenteditable]').innerText(),'Mi borrador');assert.equal(await count(),'0');record('nonempty editor preserved','PASS');
 await setup({login:true});assert.equal((await prepare(request())).state,'LoginRequired');record('unauthenticated login state','PASS');
 await setup({empty:true});await p.evaluate(()=>setTimeout(()=>{const e=document.createElement('div');e.id='prompt-textarea';e.className='ProseMirror';e.contentEditable='true';document.body.prepend(e);},150));assert.equal((await prepare(request())).state,'Prepared');record('slow editor discovered by MutationObserver','PASS');
 await setup();r=request('PrepareAndSend');await prepare(r);await p.evaluate(()=>history.pushState(null,'','/c/another-conversation'));assert.notEqual((await commit(r)).state,'SubmittedUI');assert.equal(await count(),'0');record('SPA navigation blocks commit','PASS');
 await setup();r=request('PrepareAndSend');await prepare(r);await p.evaluate(()=>Object.defineProperty(document,'hasFocus',{value:()=>false}));assert.notEqual((await commit(r)).state,'SubmittedUI');assert.equal(await count(),'0');record('simulated document focus loss blocks commit (headless focus is not a real-window proof)','PASS');
 for(const option of [{disabled:true},{ambiguous:true}]){await setup(option);r=request('PrepareAndSend');await prepare(r);assert.equal((await commit(r)).state,'NeedsManualPaste');assert.equal(await count(),'0');}record('disabled or ambiguous send is never clicked','PASS');
 await setup({empty:true});r=request();const waiting=prepare(r);await p.evaluate(id=>qpCancel(id),r.requestId);assert.equal((await waiting).state,'Cancelled');assert.equal(await count(),'0');record('cancellation during editor wait','PASS');
 await setup();r=request('PrepareAndSend','<img src=x onerror="window.bad=1">\n<script>bad()</script> 😀');assert.equal((await prepare(r)).state,'Prepared');assert.equal(await p.locator('[contenteditable] img,[contenteditable] script').count(),0);assert.equal(await p.evaluate(()=>window.bad),undefined);record('prompt treated as text, no HTML execution','PASS');
 await setup();r=request('PrepareOnly','x'.repeat(20000));assert.equal((await prepare(r)).state,'Prepared');assert.equal((await p.locator('[contenteditable]').innerText()).length,20000);record('exact 20000 character editor insertion','PASS');
 await setup();r=request();r.expiresAtUtc=new Date(0).toISOString();assert.equal((await prepare(r)).state,'Timeout');assert.equal(await count(),'0');record('expired adapter request blocked','PASS');
 await setup({confirm:false});r=request('PrepareAndSend');await prepare(r);assert.equal((await commit(r)).code,'submission-unconfirmed');assert.equal(await count(),'1');assert.equal((await commit(r)).state,'Failed');record('unconfirmed send one attempt, no false SubmittedUI','PASS');
 await setup({confirm:false});r=request('PrepareAndSend');await prepare(r);await p.evaluate(id=>document.querySelector('button').addEventListener('click',()=>qpCancel(id)),r.requestId);assert.equal((await commit(r)).code,'cancelled-after-ui-attempt');assert.equal(await count(),'1');record('cancellation after attempt does not claim undo','PASS');
 await setup({empty:true});r=request();r.expiresAtUtc=new Date(Date.now()+500).toISOString();const timeout=prepare(r);await p.evaluate(()=>setTimeout(()=>document.body.append(document.createElement('span')),600));assert.equal((await timeout).state,'Timeout');record('selector absence and expiration','PASS');
 await p.close();
}
fs.writeFileSync(path.join(out,'adapter-fixtures.json'),JSON.stringify({status:'PASS_LOCAL_DOM_FIXTURES_ONLY',browser:await browser.version(),records,realProviderSuccess:false},null,2));console.log('PASS_ADAPTER_DOM_FIXTURES',records.length);
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exit(1);});
