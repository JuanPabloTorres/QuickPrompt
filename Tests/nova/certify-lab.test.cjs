const test=require('node:test'),assert=require('node:assert/strict');
const {semanticComplete,browserComplete,requiredScenarios,releaseScope,copyContractComplete}=require('./certify-lab.cjs');
const hashes={'sample.dll':'AA'};
const digest='d'.repeat(64);
const core={workerSha256:'a'.repeat(64),sdkHashes:{'host.py':'b'.repeat(64),'inference.py':'c'.repeat(64)}};
const semantic=()=>({split:'validation',model:'qwen3:8b',modelDigest:digest,status:'PASS_SEMANTIC_CONTRACTS',frozenContractGate:'PASS',count:2,
 coreArtifacts:structuredClone(core),
 profile:{semanticCore:'ADAPTIVE_CORE'},
 rows:[{id:'a',correct:true,effectScopePass:true},{id:'b',correct:true,effectScopePass:true}],runtimeHashes:hashes});
function verified(r){return semanticComplete(r,'validation',2,hashes,'qwen3:8b',digest,{expectedIds:['a','b'],coreArtifacts:core});}
test('complete scoped semantic evidence is accepted',()=>assert.equal(verified(semantic()),true));
test('same count and claimed dataset hash cannot replace the independent case identities',()=>{
 const r=semantic();r.rows[1].id='case-from-another-dataset';assert.equal(verified(r),false);
 assert.equal(semanticComplete(semantic(),'validation',2,hashes,'qwen3:8b',digest),false);
});
test('missing identity on both evidence and configuration cannot become a match',()=>{
 const r=semantic();delete r.model;delete r.modelDigest;
 assert.equal(semanticComplete(r,'validation',2,hashes,undefined,undefined,{expectedIds:['a','b']}),false);
 const b=browser();delete b.sdk.model;delete b.modelDigest;delete b.modelDigestAfter;
 assert.equal(browserComplete(b,hashes,{}),false);
});
for(const mutation of ['missing','empty','duplicate','incorrect','extra_effect','other_runtime','other_model','qa_assisted'])
 test('semantic certification rejects '+mutation,()=>{const r=semantic();
  if(mutation==='missing')return assert.equal(verified(null),false);
  if(mutation==='empty')r.rows=[];
  if(mutation==='duplicate')r.rows[1].id='a';
  if(mutation==='incorrect')r.rows[1].correct=false;
  if(mutation==='extra_effect')r.rows[1].effectScopePass=false;
  if(mutation==='other_runtime')r.runtimeHashes={'sample.dll':'BB'};
  if(mutation==='other_model')r.modelDigest='different';
  if(mutation==='qa_assisted')r.profile.semanticCore='ADAPTIVE_CORE_QA_SCOPE_ASSISTED';
  assert.equal(verified(r),false);
 });
const browser=()=>({status:'PASS',syntheticOnly:true,realQwen:true,selectedSemanticScenarios:[...requiredScenarios],
 coreArtifactsBefore:structuredClone(core),coreArtifactsAfter:structuredClone(core),
 checks:requiredScenarios.map(s=>({name:'real-qwen-'+s,status:'PASS'})),errors:[],hashes,sdk:{model:'qwen3:8b'},modelDigest:digest,modelDigestAfter:digest,
 runs:requiredScenarios.map(s=>({taskEvidence:{scenario:s},status:'COMPLETED',semantic:{validationNotes:['ADAPTIVE_CORE']}}))});
test('browser evidence must cover every required scenario on the same runtime',()=>{
 const identity={model:'qwen3:8b',digest,coreArtifacts:core};
 assert.equal(browserComplete(browser(),hashes,identity),true);
 const missing=browser();missing.selectedSemanticScenarios.pop();assert.equal(browserComplete(missing,hashes,identity),false);
 const failed=browser();failed.checks[0].status='FAIL';assert.equal(browserComplete(failed,hashes,identity),false);
 const foreign=browser();foreign.hashes={'sample.dll':'BB'};assert.equal(browserComplete(foreign,hashes,identity),false);
 const swapped=browser();swapped.modelDigestAfter='other';assert.equal(browserComplete(swapped,hashes,identity),false);
});

test('identical DLLs and model cannot certify a different Python SDK or worker',()=>{
 const sdk=semantic();sdk.coreArtifacts.sdkHashes['inference.py']='f'.repeat(64);assert.equal(verified(sdk),false);
 const worker=semantic();worker.coreArtifacts.workerSha256='f'.repeat(64);assert.equal(verified(worker),false);
 const missing=semantic();delete missing.coreArtifacts;assert.equal(verified(missing),false);
 const extra=semantic();extra.coreArtifacts.sdkHashes['unmeasured.py']='f'.repeat(64);assert.equal(verified(extra),false);
 assert.equal(semanticComplete(semantic(),'validation',2,hashes,'qwen3:8b',digest,{expectedIds:['a','b']}),false);
});

test('browser certification requires the same measured Core before and after the run',()=>{
 const identity={model:'qwen3:8b',digest,coreArtifacts:core};
 const changed=browser();changed.coreArtifactsAfter.sdkHashes['inference.py']='f'.repeat(64);
 assert.equal(browserComplete(changed,hashes,identity),false);
 const foreign=browser();foreign.coreArtifactsBefore.workerSha256='f'.repeat(64);foreign.coreArtifactsAfter=structuredClone(foreign.coreArtifactsBefore);
 assert.equal(browserComplete(foreign,hashes,identity),false);
 const missing=browser();delete missing.coreArtifactsBefore;assert.equal(browserComplete(missing,hashes,identity),false);
 assert.equal(browserComplete(browser(),hashes,{model:'qwen3:8b',digest}),false);
});

test('real isolated SDK experiment is distinguishable while all .NET binaries remain identical',t=>{
 const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
 const {coreArtifacts,sameCoreArtifacts}=require('./artifact-hashes.cjs');
 const root=path.resolve(__dirname,'../../.playwright/closure-lab-20261021');
 const base=path.join(root,'input-authority-v9'),variant=path.join(root,'schema-grounding-v10');
 if(!fs.existsSync(variant))return t.skip('Preserved isolated experiment not present');
 for(const file of ['QuickPrompt.Evaluation.dll','QuickPrompt.Infrastructure.dll','QuickPrompt.Application.dll','QuickPrompt.Domain.dll']){
  const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
  assert.equal(sha(path.join(base,'eval',file)),sha(path.join(variant,'eval',file)));
 }
 const expected=coreArtifacts(path.join(base,'sdk'),path.join(base,'nova_worker.py'));
 const foreign=coreArtifacts(path.join(variant,'sdk'),path.join(variant,'nova_worker.py'));
 assert.equal(sameCoreArtifacts(foreign,expected),false);
 const r=semantic();r.coreArtifacts=foreign;
 assert.equal(semanticComplete(r,'validation',2,hashes,'qwen3:8b',digest,{expectedIds:['a','b'],coreArtifacts:expected}),false);
});

test('prior certifier reproduction accepts foreign SDK evidence; corrected gate rejects it',t=>{
 const fs=require('node:fs'),path=require('node:path');
 const old='C:/Projects/Repos/AI-Delivery-Lab/artifacts/closure-20261021/runs/lab-live-b83825e19df5/certification-source.cjs';
 if(!fs.existsSync(old))return t.skip('Historical certifier receipt not present');
 const prior=require(old),r=semantic();r.coreArtifacts.sdkHashes['inference.py']='f'.repeat(64);
 const config={expectedIds:['a','b'],coreArtifacts:core};
 assert.equal(prior.semanticComplete(r,'validation',2,hashes,'qwen3:8b',digest,config),true);
 assert.equal(verified(r),false);
});

test('fresh output directories and explicit campaign inputs preserve the original ledger and never fall back',t=>{
 const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
 const {run}=require('./certify-lab.cjs'),root=path.resolve(__dirname,'../..');
 const base=path.join(root,'docs/evidence/closure-lab-20261021/input-authority-v9/certification-inputs.json');
 if(!fs.existsSync(base))return t.skip('Preserved synthetic candidate is not present');
 const dir=path.join(root,'.playwright/closure-lab-20261021/certifier-input-tests',crypto.randomUUID());fs.mkdirSync(dir,{recursive:true});
 const original=JSON.parse(fs.readFileSync(base,'utf8').replace(/^\uFEFF/,''));
 const override=path.join(dir,'inputs.json');fs.writeFileSync(override,JSON.stringify({...original,reports:{}}));
 const ledger=path.join(root,'docs/nova/VERSION_CLOSURE_MATRIX.json'),before=fs.readFileSync(ledger);
 const output=path.join(dir,'new-output/report.json'),result=run('input-authority-v9',output,override);
 assert(fs.existsSync(output));assert.equal(result.rows.find(r=>r.id==='development').status,'NOT_TESTED');
 assert.equal(result.inputsSnapshot.sha256,crypto.createHash('sha256').update(fs.readFileSync(override)).digest('hex').toUpperCase());
 assert(fs.readFileSync(ledger).equals(before));assert.throws(()=>run('input-authority-v9',output,override),/Preserve prior/);
});
test('local scope preserves local failures and excludes only the authorized external integrations',()=>{
 const matrix={rows:[['EXT-01','','','FALLA'],['EXT-02','','','SIN_VERIFICAR'],['EXT-03','','','PENDIENTE'],['VAR-04','','','FALLA'],['REL-10','','','PENDIENTE']]};
 const original=JSON.stringify(matrix),scope=releaseScope(matrix,'local-v1');
 assert.deepEqual(scope.open,['EXT-01','REL-10']);
 assert.deepEqual(scope.excluded,['EXT-02','EXT-03']);assert.deepEqual(scope.replaced,['VAR-04']);
 assert.equal(scope.requirements,4);assert.equal(JSON.stringify(matrix),original);
 assert.equal(releaseScope(matrix).open.length,5);
 assert.throws(()=>releaseScope(matrix,'local-all-green'));
 assert.throws(()=>releaseScope({rows:matrix.rows.filter(row=>row[0]!=='EXT-02')},'local-v1'));
});
test('the two copy contracts require their own observed row on exactly the tested API and frontend',()=>{
 const api='A'.repeat(64),web='B'.repeat(64);
 const report={schema:'ai-delivery-tests/1',syntheticOnly:true,apiSha256:api,webSha256:web,
  status:'FAIL', // Original raw-LF clipboard assertion remains failed.
  rows:[{id:'windows-standard-unicode-text-and-lines',status:'PASS',observedAt:'2026-10-22T00:00:00Z',location:{sourceSha256:'C'.repeat(64)}}]};
 assert.equal(copyContractComplete(report,'windows-unicode-crlf',api,web),true);
 assert.equal(copyContractComplete(report,'utf8-lf-file',api,web),false);
 const fileRows=['downloaded-utf8-lf-exact-no-side-effects','downloaded-unsaved-draft-utf8-lf-no-side-effects'].map(id=>({...report.rows[0],id}));
 assert.equal(copyContractComplete({...report,rows:fileRows},'utf8-lf-file',api,web),true);
 fileRows[1].status='FAIL';assert.equal(copyContractComplete({...report,rows:fileRows},'utf8-lf-file',api,web),false);
 assert.equal(copyContractComplete(report,'windows-unicode-crlf',api,'D'.repeat(64)),false);
 assert.equal(copyContractComplete(report,'windows-unicode-crlf','D'.repeat(64),web),false);
 assert.equal(copyContractComplete({...report,syntheticOnly:false},'windows-unicode-crlf',api,web),false);
 assert.equal(copyContractComplete({...report,rows:[...report.rows,...report.rows]},'windows-unicode-crlf',api,web),false);
});
