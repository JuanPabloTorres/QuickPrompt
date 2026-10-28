const pending=new Map();
const origin='http://127.0.0.1:5188';
window.addEventListener('message',e=>{
  if(e.source!==window || e.origin!==origin || e.data?.source!=='qp.extension.v1')return;
  const job=pending.get(e.data.id);if(!job)return;
  if(e.data.progress){job.observer?.invokeMethodAsync('ReportProgress',e.data.result).catch(()=>{});return;}
  clearTimeout(job.timer);pending.delete(e.data.id);job.resolve(e.data.result);
});
function exchange(kind,request,observer,timeout){
  if(location.origin!==origin)return Promise.resolve({requestId:request?.requestId || crypto.randomUUID(),state:'ExtensionMissing',code:'unsupported-local-origin'});
  const id=crypto.randomUUID();
  return new Promise(resolve=>{
    const timer=setTimeout(()=>{pending.delete(id);if(request)cancel(request.requestId);resolve({requestId:request?.requestId || id,state:kind==='detect'?'ExtensionMissing':'Timeout',code:'extension-no-response'});},timeout);
    pending.set(id,{resolve,timer,observer,requestId:request?.requestId});
    window.postMessage({source:'qp.web.v1',id,kind,request},origin);
  });
}
export const detect=()=>exchange('detect',null,null,800);
export const transfer=(request,observer)=>exchange('transfer',request,observer,55000);
export function cancel(requestId){window.postMessage({source:'qp.web.v1',id:crypto.randomUUID(),kind:'cancel',request:{requestId}},origin);}
