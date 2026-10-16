const http=require('http'),fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {run}=require('../../automation/playwright/runner.cjs');
(async()=>{
  const out=process.env.QP_EVIDENCE_DIR||'docs/evidence/goal06/automation';fs.mkdirSync(out,{recursive:true});
  const server=http.createServer((req,res)=>{if(req.url==='/slow')return;res.setHeader('Content-Type','text/html');res.end('<!doctype html><html lang="es"><title>Fixture propia</title><h1>Control local</h1></html>');});
  await new Promise(r=>server.listen(0,'127.0.0.1',r));const origin='http://127.0.0.1:'+server.address().port,policy={origins:[origin],timeoutMs:10000};
  try{
    const failure=await run({mode:'qa',steps:[{action:'navigate',url:origin+'/'},{action:'expectText',role:'heading',name:'Control local',value:'No coincide'}]},policy,{out:path.join(out,'failure')});assert.equal(failure.state,'FAIL');
    const controller=new AbortController(),timer=setTimeout(()=>controller.abort(),1200);let cancelled;
    try{cancelled=await run({mode:'qa',steps:[{action:'navigate',url:origin+'/slow'}]},policy,{signal:controller.signal,out:path.join(out,'cancelled')});}finally{clearTimeout(timer);}assert.equal(cancelled.state,'CANCELLED');
    const recovered=await run({mode:'qa',steps:[{action:'navigate',url:origin+'/'},{action:'expectText',role:'heading',name:'Control local',value:'Control local'},{action:'screenshot'}]},policy,{out:path.join(out,'recovered')});assert.equal(recovered.state,'PASS');
    const timeout=await run({mode:'qa',steps:[{action:'navigate',url:origin+'/slow'}]},{...policy,timeoutMs:1000},{out:path.join(out,'timeout')});assert.equal(timeout.state,'TIMEOUT');
    fs.writeFileSync(path.join(out,'automation.json'),JSON.stringify({status:'PASS_LOCAL_FIXTURES',failure:failure.state,cancelled:cancelled.state,recovered:recovered.state,timeout:timeout.state,externalSupervised:'NOT_TESTED'},null,2));console.log('PASS_GOAL06_AUTOMATION');
  }finally{server.closeAllConnections();await new Promise(r=>server.close(r));}
})().catch(e=>{console.error(e);process.exitCode=1;});
