(() => {
  const origin = 'http://127.0.0.1:5188';
  if (location.origin !== origin || window !== top) return;
  chrome.runtime.sendMessage({kind:'panel-register'}).catch(()=>{});
  let gesture;
  const running = new Map();
  let panelPending=false;
  chrome.runtime.onMessage.addListener((message,sender,sendResponse)=>{
    if(sender.id!==chrome.runtime.id || sender.url!==chrome.runtime.getURL('sidepanel/index.html') || !['library','compile'].includes(message?.kind))return;
    if(panelPending){sendResponse({error:'Otra operación está en curso.'});return;}
    panelPending=true;const id=crypto.randomUUID();
    const finish=value=>{clearTimeout(timer);window.removeEventListener('message',receive);panelPending=false;sendResponse(value);};
    const receive=e=>{if(e.source===window && e.origin===origin && e.data?.source==='qp.panel.result' && e.data.id===id)finish(e.data);};
    const timer=setTimeout(()=>finish({error:'QuickPrompt no responde. Comprueba la pestaña local.'}),10000);
    window.addEventListener('message',receive);
    window.postMessage({...message,source:'qp.panel.request',id},origin);return true;
  });
  // Blazor's server roundtrip cannot manufacture a trusted DOM click. One click arms one exact action.
  document.addEventListener('click', e => {
    const button = e.target.closest?.('button[data-qp-transfer]');
    if (e.isTrusted && button && !button.disabled) gesture = {provider:button.dataset.provider, action:button.dataset.qpTransfer, at:Date.now()};
  }, true);
  const reply = (id, result, progress = false) => window.postMessage({source:'qp.extension.v1', id, result, progress}, origin);
  window.addEventListener('message', e => {
    if (e.source !== window || e.origin !== origin || e.data?.source !== 'qp.web.v1' || typeof e.data.id !== 'string') return;
    const {id,kind,request} = e.data;
    if (kind === 'detect') { chrome.runtime.sendMessage({kind:'detect'}, r => { if (!chrome.runtime.lastError && r) reply(id,r); }); return; }
    if (kind === 'cancel') { running.get(request?.requestId)?.postMessage({kind:'cancel'}); return; }
    if (kind !== 'transfer' || !request || !gesture || Date.now()-gesture.at > 10000 || request.provider !== gesture.provider || request.action !== gesture.action) { reply(id,{requestId:request?.requestId,state:'Failed',code:'trusted-click-required'}); return; }
    gesture = null;
    const port = chrome.runtime.connect({name:'quickprompt-v1'});
    running.set(request.requestId,port);
    port.onMessage.addListener(message => { reply(id,message.result,message.progress); if (!message.progress) { running.delete(request.requestId); port.disconnect(); } });
    port.onDisconnect.addListener(() => { if (running.delete(request.requestId)) reply(id,{requestId:request.requestId,state:'NeedsManualPaste',code:'extension-disconnected'}); });
    port.postMessage({kind:'transfer',request});
  });
})();
