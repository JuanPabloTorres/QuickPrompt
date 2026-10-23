// Verifies diagnostic outcome classification with controlled HTTP replies only.
// This is not a real-model, authoring, UI, or semantic-accuracy evaluation.
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),assert=require('node:assert/strict');
const {spawn}=require('node:child_process');
const root=path.resolve(__dirname,'../..'),stamp=Date.now();
const out=path.join(root,'docs/evidence/review-diagnostic-20261019',String(stamp));fs.mkdirSync(out,{recursive:true});
const executable=path.resolve(root,process.env.NOVA_DIAGNOSTIC_BUILD||'backend/QuickPrompt.Evaluation/bin/Debug/net8.0/QuickPrompt.Evaluation.dll');
let server,profile,chats=0;
const summary={scope:'DIAGNOSTIC_CONTRACT_ONLY_CONTROLLED_HTTP',status:'RUNNING',rows:[]};
(async()=>{
  server=http.createServer(async(req,res)=>{
    for await(const chunk of req){}chats++;
    const review={verdict:profile==='accept'?'ACCEPT':'REVISE',instructionScope:'FUTURE_TASK_ONLY',applicationWorkflowQuotes:[],requestedTitleQuote:null,
      issues:profile==='reject'?[{authority:'USER_REQUEST',problem:'Falta la restricción factual.',evidence:'No inventes datos.'}]:[]};
    res.writeHead(200,{'Content-Type':'application/json'});res.end(JSON.stringify({done:true,done_reason:'stop',message:{content:JSON.stringify(review)}}));
  });
  await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(0,'127.0.0.1',resolve);});
  const endpoint='http://127.0.0.1:'+server.address().port;
  for(const scenario of [
    {id:'accept',expected:'ACCEPT',outcome:'REVIEW_ACCEPTED',conforms:true,chats:1,exit:0},
    {id:'reject',expected:'REJECT',outcome:'CONTENT_REJECTED',conforms:true,chats:1,exit:0},
    {id:'invalid',expected:'REJECT',outcome:'REVIEW_INCONCLUSIVE',conforms:false,chats:2,exit:2}
  ]){
    profile=scenario.id;chats=0;
    const request='Crea un prompt para explicar <tema>. No inventes datos.';
    const input=path.join(out,profile+'-fixture.json'),output=path.join(out,profile+'-result.json');
    fs.writeFileSync(input,JSON.stringify({syntheticOnly:true,cases:[{id:profile,message:request,command:{operation:'CREATE_DRAFT',content:request},reviewOnlyBody:profile==='accept'?'Explica <tema>. No inventes datos.':'Explica <tema>.',reviewRequirements:[request],expectedReview:scenario.expected}]}));
    let log='';const child=spawn('dotnet',[executable,'--generation-diagnostic',input,output],{cwd:root,windowsHide:true,env:{...process.env,NOVA_QA_MODEL:'qwen3:8b',NOVA_QA_OLLAMA:endpoint},stdio:['ignore','pipe','pipe']});
    child.stdout.on('data',chunk=>{log+=chunk;});child.stderr.on('data',chunk=>{log+=chunk;});
    const timer=setTimeout(()=>child.kill(),30000);
    const exit=await new Promise((resolve,reject)=>{child.once('error',reject);child.once('exit',resolve);});clearTimeout(timer);
    fs.writeFileSync(path.join(out,profile+'.log'),log);assert.equal(exit,scenario.exit,log);
    const row=JSON.parse(fs.readFileSync(output,'utf8')).rows[0];
    assert.equal(row.caseMode,'CONTROLLED_BODY_REVIEW');assert.equal(row.inferenceEndpoint,endpoint);assert.equal(row.modelIdentityVerified,false);
    assert.equal(row.reviewOutcome,scenario.outcome);assert.equal(row.reviewConforms,scenario.conforms);assert.equal(chats,scenario.chats);
    if(profile==='invalid')assert.equal(row.errorCode,'CONTENT_FIDELITY_REVIEW_INVALID');
    if(profile==='reject')assert.equal(row.errorCode,'CONTENT_FIDELITY_MISMATCH');
    summary.rows.push({id:profile,status:'PASS',outcome:row.reviewOutcome,conforms:row.reviewConforms,errorCode:row.errorCode,chats,exitCode:exit});
  }
  summary.status='PASS';
})().catch(error=>{summary.status='FAIL';summary.error=String(error.stack||error);process.exitCode=1;})
  .finally(async()=>{if(server)await new Promise(resolve=>server.close(resolve));fs.writeFileSync(path.join(out,'results.json'),JSON.stringify(summary,null,2));console.log(JSON.stringify({status:summary.status,evidence:out}));});
