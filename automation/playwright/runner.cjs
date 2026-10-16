// Fixed action interpreter. No eval, page scripts, auth state or model-provided code.
const fs=require('fs'),path=require('path'),readline=require('readline/promises');
const {validate,sensitive}=require('./policy.cjs');
async function run(workflow,policy,{signal,out,approve,headed=false,record=false,stepDelayMs=0,locatorTimeoutMs=10000}={}){
  policy=validate(workflow,policy);out=path.resolve(out||'.playwright/supervised-'+Date.now());fs.mkdirSync(out,{recursive:true});
  if(record&&workflow.mode!=='qa')throw Error('Recording is restricted to local QA');
  if(!Number.isInteger(stepDelayMs)||stepDelayMs<0||stepDelayMs>3000)throw Error('Invalid demonstration delay');
  if(!Number.isInteger(locatorTimeoutMs)||locatorTimeoutMs<1000||locatorTimeoutMs>90000)throw Error('Invalid locator timeout');
  const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
  const browser=await chromium.launch({channel:'msedge',headless:workflow.mode==='qa'&&!headed,slowMo:headed?350:0}),context=await browser.newContext({acceptDownloads:false,serviceWorkers:'block',...(record?{permissions:['clipboard-read','clipboard-write'],viewport:{width:1280,height:800},recordVideo:{dir:path.join(out,'video'),size:{width:1280,height:800}}}:{})}),page=await context.newPage();
  const started=Date.now(),results=[],deadline=new AbortController();let state='PASS',approvalWindow=0;
  const abort=()=>{deadline.abort();browser.close().catch(()=>{});},timer=setTimeout(abort,policy.timeoutMs);signal?.addEventListener('abort',abort,{once:true});page.setDefaultTimeout(Math.min(locatorTimeoutMs,policy.timeoutMs));
  try{
    await context.route('**/*',async route=>{
      const request=route.request(),u=new URL(request.url());
      const allowed=policy.origins.includes(u.origin);
      const local=['127.0.0.1','localhost','[::1]'].includes(u.hostname);
      if(!allowed||!local&&!['GET','HEAD'].includes(request.method())&&Date.now()>approvalWindow)return route.abort('blockedbyclient');
      return route.continue();
    });
    page.on('popup',popup=>popup.close().catch(()=>{}));
    for(const [index,step] of workflow.steps.entries()){
      if(signal?.aborted)throw Error('CANCELLED');
      if(Date.now()-started>=policy.timeoutMs)throw Error('TIMEOUT');
      if(step.action!=='navigate'&&!policy.origins.includes(new URL(page.url()).origin))throw Error('BLOCKED');
      if(sensitive(step,page.url())){
        if(/log\s?in|sign\s?in|password|contrase[nñ]a|iniciar sesi[oó]n|captcha/i.test(step.name))throw Error('BLOCKED');
        const decision=approve?.({index,action:step.action,role:step.role,name:step.name,value:step.value,origin:new URL(page.url()).origin,signal:deadline.signal});
        let approvalTimer;const timeout=new Promise(resolve=>{approvalTimer=setTimeout(()=>resolve(false),Math.max(1,policy.timeoutMs-(Date.now()-started)));});
        let approved;try{approved=await Promise.race([decision,timeout]);}finally{clearTimeout(approvalTimer);}
        if(workflow.mode!=='supervised'||!approved)throw Error('BLOCKED');
        approvalWindow=Date.now()+10000;
      }
      const locator=step.role?page.getByRole(step.role,{name:step.name,exact:true}):null;
      if(step.action==='navigate')await page.goto(step.url,{waitUntil:'domcontentloaded'});
      if(step.action==='click')await locator.click();
      if(step.action==='fill')await locator.fill(step.value);
      if(step.action==='press')await locator.press(step.key);
      if(step.action==='expectVisible')await locator.waitFor({state:'visible'});
      if(step.action==='expectText'){await locator.waitFor({state:'visible'});if((await locator.innerText())!==step.value)throw Error('STATE_MISMATCH');}
      if(step.action==='screenshot')await page.screenshot({path:path.join(out,'step-'+index+'.png'),fullPage:true});
      if(!policy.origins.includes(new URL(page.url()).origin))throw Error('BLOCKED');
      results.push({index,action:step.action,status:'PASS',elapsedMs:Date.now()-started});
      if(stepDelayMs)await new Promise(resolve=>setTimeout(resolve,stepDelayMs));
    }
  }catch(e){state=signal?.aborted?'CANCELLED':e.message==='BLOCKED'?'BLOCKED':Date.now()-started>=policy.timeoutMs?'TIMEOUT':'FAIL';results.push({index:results.length,status:state});}
  finally{
    clearTimeout(timer);deadline.abort();signal?.removeEventListener('abort',abort);
    try{if(record&&browser.isConnected()){await context.close();await page.video().saveAs(path.join(out,'recorrido.webm'));}}
    catch{if(state==='PASS')state='FAIL';results.push({index:results.length,status:'RECORDING_FAILED'});}
    finally{await browser.close();}
  }
  const result={state,mode:workflow.mode,elapsedMs:Date.now()-started,steps:results};fs.writeFileSync(path.join(out,'result.json'),JSON.stringify(result,null,2));return result;
}
if(require.main===module){
  const controller=new AbortController();process.on('SIGINT',()=>controller.abort());
  const approve=async step=>{if(!process.stdin.isTTY)return false;const ui=readline.createInterface({input:process.stdin,output:process.stdout});try{return(await ui.question(`Approve external ${step.action} on ${step.origin}, ${step.role} "${step.name}"${step.value===undefined?'':', value '+JSON.stringify(step.value)}? Type APPROVE: `,{signal:step.signal}))==='APPROVE';}finally{ui.close();}};
  const locatorOption=process.argv.find(value=>value.startsWith('--locator-timeout='));
  run(JSON.parse(fs.readFileSync(process.argv[2])),JSON.parse(fs.readFileSync(process.argv[3])),{signal:controller.signal,approve,out:process.argv[4],headed:process.argv.includes('--headed'),record:process.argv.includes('--record'),stepDelayMs:process.argv.includes('--headed')?1000:0,locatorTimeoutMs:locatorOption?Number(locatorOption.split('=')[1]):10000}).then(r=>{console.log(r.state);if(r.state!=='PASS')process.exitCode=1;}).catch(e=>{console.error(e.message);process.exitCode=1;});
}
module.exports={run};
