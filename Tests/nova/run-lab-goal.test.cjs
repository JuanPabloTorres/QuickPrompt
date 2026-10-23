const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {spawnSync}=require('node:child_process'),{randomUUID}=require('node:crypto');
const root=path.resolve(__dirname,'../..');
const pwsh='C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe';
function setup(){
 const id='adapter-'+randomUUID(),directory=path.join(root,'.playwright/closure-lab-20261021/adapter-contract-tests',id);
 fs.mkdirSync(directory,{recursive:true});
 const goal={schema:'ai-delivery-goal/1',id,hash:'a'.repeat(64),request:'Adapta este informe y prepara su texto para copiar.',context:'selected_report',target:'report',
  required:['PROPOSE_ADAPTATION','COPY_PROMPT'],forbidden:['EXECUTE_LOCAL','COPY_TO_CLIPBOARD'],outcomes:['DRAFT'],inputs:{equipo:'QA'},captureInputs:true};
 const contract=path.join(directory,'goal.json');fs.writeFileSync(contract,JSON.stringify(goal));
 return {goal,contract,directory,id};
}
function prepare(s,extra=[]){return spawnSync(pwsh,['-NoProfile','-File',path.join(__dirname,'run-lab-goal.ps1'),'-Contract',s.contract,
 '-Python','C:/Projects/Repos/Adaptive-AI-Core/.venv/Scripts/python.exe','-SdkRoot','C:/Projects/Repos/Adaptive-AI-Core',
 '-LabRoot','C:/Projects/Repos/AI-Delivery-Lab','-Candidate',s.id,'-PrepareOnly',...extra],{encoding:'utf8',windowsHide:true,timeout:15000});}
function select(s,split,hash=s.goal.hash){fs.writeFileSync(path.join(s.directory,s.id+'.source-fixtures.json'),JSON.stringify({
 schema:'quickprompt-goal-source/1',goalId:s.id,goalHash:hash,fixtureSplit:split}));}
test('goal preflight keeps oracle in its dataset and pins the original-input production candidate',()=>{
 const s=setup();select(s,'validation');const r=prepare(s);assert.equal(r.status,0,r.stderr);
 const plan=JSON.parse(fs.readFileSync(r.stdout.trim(),'utf8').replace(/^\uFEFF/,''));
 assert.equal(plan.modelReceivesOracle,false);assert.equal(plan.fixtureSplit,'validation');assert.equal(plan.uiExecution,'NOT_REQUESTED');
 const data=JSON.parse(fs.readFileSync(plan.dataset,'utf8'));
 assert.equal(data.fixtures[0][0],'Preparar reuniones');assert.equal(data.scenarios[0].message,s.goal.request+'\nequipo: QA');
 assert.deepEqual(data.scenarios[0].required,s.goal.required);assert.equal(data.scenarios.length,1);
 assert.notEqual(prepare(s).status,0,'An existing diagnostic cannot be overwritten');
});
test('goal preflight rejects a source fixture bound to another goal',()=>{
 const s=setup();select(s,'validation','b'.repeat(64));const r=prepare(s);
 assert.notEqual(r.status,0);assert.match(r.stderr,/Invalid goal-bound source/);
});

test('expected provider remains an independent oracle in the diagnostic dataset',()=>{
 const s=setup();s.goal.providerId='claude';fs.writeFileSync(s.contract,JSON.stringify(s.goal));
 const r=prepare(s);assert.equal(r.status,0,r.stderr);
 const plan=JSON.parse(fs.readFileSync(r.stdout.trim(),'utf8').replace(/^\uFEFF/,''));
 const data=JSON.parse(fs.readFileSync(plan.dataset,'utf8'));
 assert.equal(data.scenarios[0].expectedProviderId,'claude');
 assert.equal(data.scenarios[0].message,s.goal.request+'\nequipo: QA');
 assert.equal(plan.modelReceivesOracle,false);
});
test('an explicit frozen candidate pins the goal without using the older Lab reference',()=>{
 const s=setup(),r=prepare(s,['-FrozenCandidate','dual-copy-title-authority-safe']);assert.equal(r.status,0,r.stderr);
 const plan=JSON.parse(fs.readFileSync(r.stdout.trim(),'utf8').replace(/^\uFEFF/,''));
 assert.equal(plan.frozenCandidate,'dual-copy-title-authority-safe');assert.equal(plan.modelReceivesOracle,false);
});
test('ordinary diagnostic goals cannot select holdout fixtures',()=>{
 const s=setup();select(s,'holdout');const r=prepare(s);
 assert.notEqual(r.status,0);assert.match(r.stderr,/Invalid goal-bound source/);
});
test('browser expectations for another request fail before inference even in prepare-only mode',()=>{
 const s=setup();const fixtures=JSON.parse(fs.readFileSync(path.join(__dirname,'datasets/recovery-v1/development.json'))).fixtures;
 fs.writeFileSync(path.join(s.directory,s.id+'.browser-fixture.json'),JSON.stringify({schema:'quickprompt-adapt-copy-fixture/1',syntheticOnly:true,
  goalId:s.id,goalHash:s.goal.hash,request:'A different request',source:{title:fixtures[0][0],templateText:fixtures[0][1]}}));
 const r=prepare(s);assert.notEqual(r.status,0);assert.match(r.stderr,/Browser fixture does not match/);
});
test('a bound browser fixture schedules real UI without starting inference during preflight',()=>{
 const s=setup(),fixtures=JSON.parse(fs.readFileSync(path.join(__dirname,'datasets/recovery-v1/development.json'))).fixtures;
 fs.writeFileSync(path.join(s.directory,s.id+'.browser-fixture.json'),JSON.stringify({schema:'quickprompt-adapt-copy-fixture/1',syntheticOnly:true,
  goalId:s.id,goalHash:s.goal.hash,request:s.goal.request+'\nequipo: QA',source:{title:fixtures[0][0],templateText:fixtures[0][1]},
  requiredText:['riesgos'],preservedText:['No inventes cifras.'],values:{equipo:'QA'},webRoot:'.playwright/closure-lab-20261021/local-copy-v11/web'}));
 const r=prepare(s);assert.equal(r.status,0,r.stderr);
 const plan=JSON.parse(fs.readFileSync(r.stdout.trim(),'utf8').replace(/^\uFEFF/,''));assert.equal(plan.uiExecution,'PLANNED');
 assert.equal(plan.modelReceivesOracle,false);
 assert.ok(fs.existsSync(path.join(path.dirname(plan.dataset),'browser-fixture.json')));
});
