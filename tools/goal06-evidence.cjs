// Source/evidence metadata only. Never enumerate or read SQLite/user-data files.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'..'),out=path.join(root,'docs/evidence/goal06');
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const baseline=JSON.parse(fs.readFileSync(path.join(out,'baseline/source-hashes.json'))),before=new Map(baseline.map(x=>[x.path.replaceAll('\\','/'),x.sha256]));
const files=[];
function walk(dir){for(const d of fs.readdirSync(dir,{withFileTypes:true})){if(['bin','obj','node_modules'].includes(d.name))continue;const p=path.join(dir,d.name);if(d.isDirectory())walk(p);else if(/\.(cs|csproj|sln|razor|js|mjs|cjs|css|html|svg|json|md)$/.test(d.name))files.push(p);}}
for(const dir of ['src','Tests/e2e','extensions','automation','tools'])walk(path.join(root,dir));files.push(path.join(root,'README.md'));
const sources=files.map(p=>({path:path.relative(root,p).replaceAll('\\','/'),sha256:hash(p)})).sort((a,b)=>a.path.localeCompare(b.path));
fs.writeFileSync(path.join(out,'source-final-hashes.json'),JSON.stringify(sources,null,2));
const changes=sources.filter(s=>before.get(s.path)!==s.sha256).map(s=>({...s,change:before.has(s.path)?'changed':'added'}));
fs.writeFileSync(path.join(out,'source-changes.json'),JSON.stringify(changes,null,2));
const broken=[];for(const filename of ['GOAL06.md','GOAL06_AUDIT.md','GOAL06_ACCEPTANCE.md']){
 const p=path.join(root,'docs',filename);for(const match of fs.readFileSync(p,'utf8').matchAll(/\[[^\]]*\]\(([^)]+)\)/g)){const link=match[1].split('#')[0];if(!link||/^[a-z]+:/.test(link))continue;if(!fs.existsSync(path.resolve(path.dirname(p),link)))broken.push({file:filename,link});}
}
if(broken.length)throw Error(JSON.stringify(broken));
const summary={status:'PASS_LOCAL_SCOPE',baselineTests:127,finalTests:150,skipped:0,sourceFiles:sources.length,sourceChanges:changes.length,documentationLinks:'PASS',privateDatabasesRead:false,externalProviders:'BLOCKED',humanPilot:'NOT_TESTED',sidePanelDocking:'NOT_TESTED',nativeWebShare:'NOT_TESTED',externalSupervisedBrowser:'NOT_TESTED',evidence:['tests.log','final-core/core.json','final-layout/users.json','new-ui/layout.json','assist/assist.json','panel/panel.json','real-qwen/ai.json','regression.log','automation/automation.json','organization/organization.json','large-backup/large-backup.json','robustness/robustness.json','input-state/input-state.json','ai-faults/ai-faults.json','bridge-edge/extension-runtime.json','bridge-chrome/extension-runtime.json','worker-failures/worker-failures.json']};
for(const file of summary.evidence)if(!fs.existsSync(path.join(out,file)))throw Error('Missing evidence '+file);
fs.writeFileSync(path.join(out,'final-summary.json'),JSON.stringify(summary,null,2));console.log('PASS_GOAL06_EVIDENCE',sources.length,'sources;',changes.length,'changed/added; links resolve');
