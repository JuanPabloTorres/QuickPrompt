const actions=new Set(['navigate','click','fill','press','expectVisible','expectText','screenshot']);
function origin(value){const u=new URL(value);if(u.origin!==value||u.hostname.includes('*')||u.username||u.password||!['http:','https:'].includes(u.protocol)||u.protocol==='http:'&&!['127.0.0.1','localhost','[::1]'].includes(u.hostname))throw Error('Invalid allowed origin');return u.origin;}
function validate(workflow,policy){
  if(!['qa','supervised'].includes(workflow.mode)||!Array.isArray(workflow.steps)||workflow.steps.length<1||workflow.steps.length>20)throw Error('Invalid workflow; 1–20 steps required');
  if(!Array.isArray(policy.origins)||policy.origins.length<1||policy.origins.length>10)throw Error('Origins required');const origins=policy.origins.map(origin);
  if(!Number.isInteger(policy.timeoutMs)||policy.timeoutMs<1000||policy.timeoutMs>120000)throw Error('Invalid time budget');
  for(const s of workflow.steps){
    if(!s||!actions.has(s.action)||Object.keys(s).some(k=>!['action','url','role','name','value','key'].includes(k)))throw Error('Unsupported action or argument');
    for(const key of ['url','role','name','value','key'])if(s[key]!==undefined&&(typeof s[key]!=='string'||s[key].length>8000))throw Error('Invalid argument');
    if(s.action==='navigate'){const u=new URL(s.url);if(!origins.includes(u.origin)||u.username||u.password||u.hash||u.search)throw Error('Destination not allowed');}
    if(['click','fill','expectVisible','expectText','press'].includes(s.action)&&(!s.role||!s.name||s.name.length>160))throw Error('Accessible role and exact name required');
    if(s.action==='fill'&&s.value===undefined||s.action==='expectText'&&s.value===undefined)throw Error('Value required');
    if(s.action==='press'&&!['Tab','Enter','Escape','ArrowDown','ArrowUp','Control+k'].includes(s.key))throw Error('Key not allowed');
  }
  if(workflow.mode==='qa'&&origins.some(o=>!['127.0.0.1','localhost','[::1]'].includes(new URL(o).hostname)))throw Error('QA is loopback only');
  return {...policy,origins};
}
function sensitive(step,url){return !['127.0.0.1','localhost','[::1]'].includes(new URL(url).hostname)&&['click','fill','press'].includes(step.action);}
module.exports={validate,sensitive};
