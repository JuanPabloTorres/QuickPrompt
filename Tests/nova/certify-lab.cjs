// Read-only acceptance gate. Reuses the release ledger; never changes its states.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const {spawnSync}=require('node:child_process');
const {webSha256,coreArtifacts,sameCoreArtifacts}=require('./artifact-hashes.cjs');
const requiredScenarios=['prepare','actual','read','read_incidental','field_data','paraphrase','adapt_copy','provider_prepare'];
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex').toUpperCase();
function sameRuntime(actual,expected){return actual&&Object.keys(expected).every(k=>actual[k]?.toUpperCase()===expected[k].toUpperCase());}
function identified(model,digest){return typeof model==='string'&&model.trim().length>0&&typeof digest==='string'&&/^[a-f0-9]{64}$/i.test(digest);}
function semanticComplete(report,split,count,hashes,model,digest,configuration={}){
 const ids=configuration.expectedIds;
 return identified(model,digest)&&Array.isArray(ids)&&ids.length===count&&new Set(ids).size===count
  &&ids.every(id=>typeof id==='string'&&id.length>0)
  &&!!report&&report.split===split&&report.model===model&&report.modelDigest===digest
  &&report.profile?.semanticCore==='ADAPTIVE_CORE'&&!report.labArtifacts
  &&Object.entries(configuration.profile||{}).every(([key,value])=>report.profile?.[key]===value)
  &&(!configuration.datasetHash||report.datasetSha256?.toUpperCase()===configuration.datasetHash.toUpperCase())
  &&report.status==='PASS_SEMANTIC_CONTRACTS'&&report.frozenContractGate==='PASS'
  &&report.count===count&&report.rows?.length===count&&new Set(report.rows.map(r=>r.id)).size===count
  &&report.rows.every(r=>ids.includes(r.id)&&r.correct===true&&r.effectScopePass===true&&!r.error)
  &&sameRuntime(report.runtimeHashes,hashes)&&sameCoreArtifacts(report.coreArtifacts,configuration.coreArtifacts);
}
function browserComplete(report,hashes,identity){
 return identified(identity?.model,identity?.digest)&&!!report&&report.status==='PASS'&&report.syntheticOnly===true&&report.realQwen===true
  &&report.sdk?.model===identity?.model&&report.modelDigest===identity?.digest&&report.modelDigestAfter===identity?.digest
  &&requiredScenarios.every(s=>report.selectedSemanticScenarios?.includes(s)
    &&report.checks?.some(c=>c.name==='real-qwen-'+s&&c.status==='PASS')
    &&report.runs?.some(r=>r.taskEvidence?.scenario===s&&r.status==='COMPLETED'&&r.semantic?.validationNotes?.includes('ADAPTIVE_CORE')))
  &&report.checks.length>0&&report.checks.every(c=>c.status==='PASS')&&report.errors?.length===0
  &&sameRuntime(report.hashes,hashes)&&sameCoreArtifacts(report.coreArtifactsBefore,identity.coreArtifacts)
  &&sameCoreArtifacts(report.coreArtifactsAfter,identity.coreArtifacts);
}
// Explicit user decision, not automatic removal of failing requirements.
function releaseScope(matrix,profile){
 if(profile!==undefined&&profile!=='local-v1')throw Error('Unknown release profile');
 const local=profile==='local-v1',excluded=local?['EXT-02','EXT-03']:[],replaced=local?['VAR-04']:[];
 const ids=matrix.rows.map(row=>row[0]);
 if(new Set(ids).size!==ids.length||[...excluded,...replaced].some(id=>!ids.includes(id)))throw Error('Invalid scope references');
 const included=matrix.rows.filter(row=>![...excluded,...replaced].includes(row[0]));
 return {profile:profile??'original',excluded,replaced,textContracts:local?['windows-unicode-crlf','utf8-lf-file']:[],requirements:included.length+(local?2:0),
  open:included.filter(row=>row[3]!=='CERRADO').map(row=>row[0])};
}
function copyContractComplete(report,id,apiHash,webHash){
 const expectedNames={
  'windows-unicode-crlf':'windows-standard-unicode-text-and-lines',
  'utf8-lf-file':'downloaded-utf8-lf-exact-no-side-effects'
 };
 const name=expectedNames[id];
 return !!name&&report?.schema==='ai-delivery-tests/1'&&report.syntheticOnly===true
  &&typeof apiHash==='string'&&/^[a-f0-9]{64}$/i.test(apiHash)&&report.apiSha256===apiHash
  &&typeof webHash==='string'&&/^[a-f0-9]{64}$/i.test(webHash)&&report.webSha256===webHash
  &&(id==='utf8-lf-file'?[name,'downloaded-unsaved-draft-utf8-lf-no-side-effects']:[name]).every(required=>
   report.rows?.filter(row=>row.id===required).length===1
   &&report.rows.some(row=>row.id===required&&row.status==='PASS'&&row.observedAt&&row.location?.sourceSha256));
}
function run(candidate,output,inputsFile){
 if(!/^[a-z0-9-]+$/.test(candidate))throw Error('Explicit candidate identity required');
 if(fs.existsSync(output))throw Error('Preserve prior certification; choose a new report path');
 fs.mkdirSync(path.dirname(path.resolve(output)),{recursive:true});
 const root=path.resolve(__dirname,'../..'),base=path.join(root,'.playwright/closure-lab-20261021',candidate);
 const evidence=path.join(root,'docs/evidence/closure-lab-20261021',candidate),rows=[],sources=[];
 const sourceSnapshot=path.join(path.dirname(output),'certification-source.cjs');
 fs.writeFileSync(sourceSnapshot,fs.readFileSync(__filename),{flag:'wx'});
 const hashHelper=path.join(path.dirname(output),'artifact-hashes.cjs');
 fs.writeFileSync(hashHelper,fs.readFileSync(path.join(__dirname,'artifact-hashes.cjs')),{flag:'wx'});
 sources.push({path:path.relative(root,hashHelper),sha256:hash(hashHelper)});
 const read=relative=>{const file=path.resolve(root,relative);if(!file.startsWith(root+path.sep))throw Error('Evidence outside project');const data=JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,''));sources.push({path:path.relative(root,file),sha256:hash(file)});return data;};
 const receipt={schema:'ai-delivery-tests/1',profile:{semanticCore:'RELEASE_CERTIFICATION_GATE'},candidate,rows,count:0,skipped:0,status:'UNCONFIRMED',toolSha256:hash(sourceSnapshot)};
 function check(id,title,passed,expected,observed,status){
  const frame=new Error().stack.split('\n')[2].match(/:(\d+):\d+\)?$/);
  const row={id,title,status:status||(passed?'PASS':'FAIL'),observedAt:new Date().toISOString(),expected,observed,
   location:{file:sourceSnapshot,originalFile:__filename,line:frame?Number(frame[1]):null,symbol:'run',kind:'assertion_observed',sourceSha256:hash(sourceSnapshot)}};
  row.comparisons=[{field:'result',expected,observed,status:row.status}];rows.push(row);
  if(process.env.NOVA_EVAL_LAB_EVENTS)fs.appendFileSync(process.env.NOVA_EVAL_LAB_EVENTS,JSON.stringify({
   schema:'ai-delivery-event/1',id:crypto.randomUUID(),observedAt:row.observedAt,campaignId:process.env.NOVA_EVAL_LAB_CAMPAIGN,
   runId:process.env.NOVA_EVAL_LAB_CAMPAIGN,caseId:id,kind:'certification_check',phase:'verification',status:row.status,
   owner:'QuickPrompt release acceptance',location:row.location,expected,observed})+'\n');
 }
 const processResult=spawnSync(process.execPath,[path.join(root,'scripts/Check-VersionClosure.cjs')],{encoding:'utf8',windowsHide:true});
 let ledger;try{ledger=JSON.parse(processResult.stdout);}catch{ledger={matrixValid:false,errors:['Ledger unavailable']};}
 check('ledger-valid','Matriz original válida',ledger.matrixValid===true,true,ledger.matrixValid);
 let manifest,inputs;
 try{manifest=read(path.join(evidence,'manifest.json'));}catch{}
 const inputsPath=inputsFile?path.resolve(inputsFile):path.join(evidence,'certification-inputs.json');
 try{inputs=read(inputsPath);}catch{}
 if(inputs){
  const snapshot=path.join(path.dirname(output),'certification-inputs.json');
  fs.writeFileSync(snapshot,fs.readFileSync(inputsPath),{flag:'wx'});
  receipt.inputsSnapshot={path:snapshot,sha256:hash(snapshot)};
 }
 let scope;
 try{scope=releaseScope(read('docs/nova/VERSION_CLOSURE_MATRIX.json'),inputs?.releaseProfile);
  if(scope.profile==='local-v1'){
   const source='docs/nova/LOCAL_RELEASE_SCOPE_20261022.md';sources.push({path:source,sha256:hash(path.join(root,source))});
  }
 }catch(e){scope={profile:'INVALID',open:['INVALID_PROFILE'],error:e.message};}
 receipt.releaseScope=scope;
 check('all-requirements','Requisitos obligatorios del alcance declarado cerrados',ledger.matrixValid===true&&scope.open.length===0,[],scope.open);
 let intact=false;
 try{intact=manifest?.candidate===candidate&&manifest.files?.length>0&&manifest.files.every(f=>{
  const file=path.resolve(base,f.path);return file.startsWith(base+path.sep)&&hash(file)===f.sha256.toUpperCase();});}catch{}
 check('candidate-integrity','Archivos del candidato congelado sin cambios',intact,true,intact);
 let expectedCore;
 try{if(intact)expectedCore=coreArtifacts(path.join(base,'sdk'),path.join(base,'nova_worker.py'));}catch{}
 check('core-runtime-identity','Identidad del SDK Python y worker congelados disponible',!!expectedCore,true,expectedCore??null);
 const validInputs=inputs?.schema==='quickprompt-certification-inputs/1'&&inputs.candidate===candidate&&inputs.profile?.semanticCore==='ADAPTIVE_CORE'&&identified(inputs.model,inputs.modelDigest);
 const hashes=Object.fromEntries(['QuickPrompt.Evaluation.dll','QuickPrompt.Infrastructure.dll','QuickPrompt.Application.dll','QuickPrompt.Domain.dll'].map(f=>[f,fs.existsSync(path.join(base,'eval',f))?hash(path.join(base,'eval',f)):'MISSING']));
 for(const split of ['development','validation','holdout']){
  let report;try{if(validInputs&&inputs.reports?.[split])report=read(inputs.reports[split]);}catch{}
  const expectedIds=read('Tests/nova/datasets/recovery-v1/'+split+'.json').scenarios.map(s=>s.id),count=expectedIds.length;
  check(split,'Semántica completa · '+split,semanticComplete(report,split,count,hashes,inputs?.model,inputs?.modelDigest,
   {profile:inputs?.profile,expectedIds,coreArtifacts:expectedCore,datasetHash:hash(path.join(root,'Tests/nova/datasets/recovery-v1/'+split+'.json'))}),
   {correct:count,candidate,model:inputs?.model},report?{status:report.status,correct:report.rows?.filter(r=>r.correct&&r.effectScopePass).length,count:report.count}:null,
   report?undefined:'NOT_TESTED');
 }
 const uiHashes=Object.fromEntries(['QuickPrompt.Api.dll','QuickPrompt.Application.dll','QuickPrompt.Infrastructure.dll'].map(f=>[f,fs.existsSync(path.join(base,'eval',f))?hash(path.join(base,'eval',f)):'MISSING']));
 let browser;try{if(validInputs&&inputs.reports?.browser)browser=read(inputs.reports.browser);}catch{}
 let frontendHash;
 if(scope.profile==='local-v1'){
  try{const frontend=path.resolve(root,inputs.frontendRoot);
   if(!frontend.startsWith(path.join(root,'.playwright')+path.sep))throw Error('Frozen frontend required');
   frontendHash=webSha256(frontend);
  }catch{}
  check('frontend-integrity','Frontend congelado coincide con las pruebas',!!frontendHash&&frontendHash===inputs?.webSha256,
   inputs?.webSha256??null,frontendHash??null);
 }
 check('browser','Peticiones completas y controles reales de UI',browserComplete(browser,uiHashes,{model:inputs?.model,digest:inputs?.modelDigest,coreArtifacts:expectedCore})
  &&(scope.profile!=='local-v1'||!!frontendHash&&browser?.webSha256===frontendHash),requiredScenarios,
  browser?{status:browser.status,scenarios:browser.selectedSemanticScenarios}:null,browser?undefined:'NOT_TESTED');
 if(scope.profile==='local-v1'){
  let copy;try{if(validInputs&&inputs.reports?.copyContracts)copy=read(inputs.reports.copyContracts);}catch{}
  for(const id of ['windows-unicode-crlf','utf8-lf-file'])check(id,'Contrato local de texto · '+id,
   copyContractComplete(copy,id,uiHashes['QuickPrompt.Api.dll'],frontendHash),
   {candidate,apiSha256:uiHashes['QuickPrompt.Api.dll'],webSha256:inputs?.webSha256},
   copy?{status:copy.rows?.find(row=>row.id===({'windows-unicode-crlf':'windows-standard-unicode-text-and-lines','utf8-lf-file':'downloaded-utf8-lf-exact-no-side-effects'}[id]))?.status}:null,
   copy?undefined:'NOT_TESTED');
 }
 receipt.count=rows.length;receipt.status=rows.every(r=>r.status==='PASS')?'PASS':'FAIL';
 receipt.provenance={sources,matrixSha256:hash(path.join(root,'docs/nova/VERSION_CLOSURE_MATRIX.json'))};
 receipt.limits='Certifica únicamente el alcance declarado con evidencia del candidato. local-v1 aplica la decisión explícita del usuario y conserva la matriz histórica. Un PASS semántico no prueba efectos; estados cerrados de la matriz requieren revisión de evidencia. Nunca modifica estados de la matriz.';
 fs.writeFileSync(output,JSON.stringify(receipt,null,2));return receipt;
}
module.exports={semanticComplete,browserComplete,requiredScenarios,releaseScope,copyContractComplete,run};
if(require.main===module){
 try{const args=process.argv.slice(2),candidate=args[args.indexOf('--candidate')+1],output=args[args.indexOf('--report')+1];
  if(!args.includes('--candidate')||!args.includes('--report'))throw Error('Pass --candidate and --report');
  if(args.includes('--inputs')&&(!args[args.indexOf('--inputs')+1]||args[args.indexOf('--inputs')+1].startsWith('--')))throw Error('Explicit inputs file required; no historical fallback');
  const report=run(candidate,output,args.includes('--inputs')?args[args.indexOf('--inputs')+1]:undefined);console.log(JSON.stringify({candidate,status:report.status,checks:report.count}));process.exitCode=report.status==='PASS'?0:1;
 }catch(e){console.error(e.message);process.exitCode=1;}
}
