// Read only the explicit public-source/evidence inventories made before GOAL05.
// Never enumerate or open a user database/configuration.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),assert=require('assert/strict');
const root=path.resolve(__dirname,'..');process.chdir(root);
const out=process.env.QP_EVIDENCE_DIR||'docs/evidence/goal05';
assert(path.resolve(out).startsWith(path.resolve('docs/evidence/goal05')+path.sep)||path.resolve(out)===path.resolve('docs/evidence/goal05'));
fs.mkdirSync(out,{recursive:true});
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const previous=JSON.parse(fs.readFileSync('docs/evidence/goal05/baseline/previous-evidence-hashes.json'));
const baseline=JSON.parse(fs.readFileSync('docs/evidence/goal05/baseline/source-hashes.json'));
const normalize=file=>file.replaceAll('\\','/');
const protectedFiles=baseline.filter(({file})=>{
  const f=normalize(file);
  return f==='package.json'||f==='package-lock.json'||f.startsWith('Tests/fixtures/')||
    f.startsWith('src/QuickPrompt.Tests/')||
    (f.startsWith('src/QuickPrompt.Domain/')&&!f.endsWith('/Organization.cs'))||
    (f.startsWith('extensions/')&&!f.endsWith('README.md'))||
    ['src/QuickPrompt.Web/Program.cs','src/QuickPrompt.Web/Infrastructure/SchemaMigrations.cs',
     'src/QuickPrompt.Web/Infrastructure/OllamaClient.cs','src/QuickPrompt.Web/Infrastructure/WebPromptTransfer.cs',
     'src/QuickPrompt.Web/wwwroot/provider-transfer.js'].includes(f);
});
const mismatches=files=>files.filter(v=>!fs.existsSync(v.file)||hash(v.file)!==v.sha256).map(v=>v.file);
const earlierEvidenceMismatches=mismatches(previous),protectedSourceMismatches=mismatches(protectedFiles);
const changedExistingSources=baseline.filter(v=>fs.existsSync(v.file)&&hash(v.file)!==v.sha256).map(v=>({file:normalize(v.file),before:v.sha256,after:hash(v.file)}));
const result={status:earlierEvidenceMismatches.length||protectedSourceMismatches.length?'FAIL':'PASS',
  previousEvidenceFiles:previous.length,earlierEvidenceMismatches,protectedSourceFiles:protectedFiles.length,
  protectedSourceMismatches,changedExistingSources,
  scope:'Explicit pre-change public source/evidence inventory only; no private database read/hash/import',
  schema:'SQLite v2 and JSON v2 unchanged; migrations, deterministic compiler and CompiledPrompt DTO bytes unchanged',
  datasets:'Original v1/v2 datasets and original .NET test files unchanged; UI interactions adapted through disclosures, assertions retained'};
fs.writeFileSync(path.join(out,'preservation.json'),JSON.stringify(result,null,2));
console.log(result.status+'_GOAL05_PRESERVATION',previous.length,'earlier evidence files;',protectedFiles.length,'protected sources');
assert.equal(result.status,'PASS');
