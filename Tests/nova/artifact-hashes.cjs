const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const sha256=bytes=>crypto.createHash('sha256').update(bytes).digest('hex').toUpperCase();
function webSha256(root){
 const entries=[];
 function visit(dir){for(const entry of fs.readdirSync(dir,{withFileTypes:true})){
  const file=path.join(dir,entry.name);
  if(entry.isSymbolicLink())throw Error('Frontend snapshot must not contain symbolic links');
  if(entry.isDirectory())visit(file);
  else if(entry.isFile())entries.push([path.relative(root,file).replaceAll('\\','/'),sha256(fs.readFileSync(file))]);
 }}
 visit(root);if(!entries.length)throw Error('Empty frontend snapshot');
 entries.sort(([a],[b])=>a<b?-1:a>b?1:0);
 return sha256(JSON.stringify(entries));
}
function coreArtifacts(sdkRoot,worker){
 const directory=path.join(sdkRoot,'adaptive_core'),sdkHashes={};
 for(const name of fs.readdirSync(directory).filter(n=>n.endsWith('.py')).sort()){
  const file=path.join(directory,name);if(!fs.lstatSync(file).isFile())throw Error('Core files must be regular files');
  sdkHashes[name]=sha256(fs.readFileSync(file));
 }
 if(!sdkHashes['inference.py']||!sdkHashes['host.py']||!fs.lstatSync(worker).isFile())throw Error('Explicit Core runtime required');
 return {workerSha256:sha256(fs.readFileSync(worker)),sdkHashes};
}
function sameCoreArtifacts(actual,expected){
 const valid=v=>v&&/^[a-f0-9]{64}$/i.test(v.workerSha256)&&v.sdkHashes&&Object.keys(v.sdkHashes).length>0
  &&Object.values(v.sdkHashes).every(h=>typeof h==='string'&&/^[a-f0-9]{64}$/i.test(h));
 return !!(valid(actual)&&valid(expected)&&actual.workerSha256.toUpperCase()===expected.workerSha256.toUpperCase()
  &&Object.keys(actual.sdkHashes).length===Object.keys(expected.sdkHashes).length
  &&Object.entries(expected.sdkHashes).every(([key,value])=>actual.sdkHashes[key]?.toUpperCase()===value.toUpperCase()));
}
module.exports={webSha256,coreArtifacts,sameCoreArtifacts};
