import {VERSION,PROVIDERS,trustedSender,destination,validate,ReplayLedger} from './protocol.mjs';
const ledger = new ReplayLedger(chrome.storage.local);
if(chrome.sidePanel?.setPanelBehavior)chrome.sidePanel.setPanelBehavior({openPanelOnActionClick:true}).catch(()=>{});
// Restrict storage to trusted extension contexts; page content gets only its current request result.
chrome.storage.local.setAccessLevel({accessLevel:'TRUSTED_CONTEXTS'});
chrome.runtime.onMessage.addListener((message,sender,reply) => {
  if(message?.kind==='panel-register' && trustedSender(sender,chrome.runtime.id)){
    chrome.storage.session.set({panelTabId:sender.tab.id}).then(()=>reply({registered:true}));return true;
  }
  if(message?.kind==='panel-target' && sender.id===chrome.runtime.id && sender.url===chrome.runtime.getURL('sidepanel/index.html')){
    chrome.storage.session.get('panelTabId').then(value=>reply({tabId:value.panelTabId}));return true;
  }
  if (message?.kind === 'detect' && trustedSender(sender,chrome.runtime.id)) reply({requestId:'00000000-0000-0000-0000-000000000000',state:'Ready',code:'extension-ready',adapterVersion:VERSION});
});
chrome.runtime.onConnect.addListener(port => {
  if (port.name !== 'quickprompt-v1' || !trustedSender(port.sender,chrome.runtime.id)) { port.disconnect(); return; }
  let started=false, cancelled=false, tabId, requestId;
  const cancel = () => { cancelled=true;if(tabId && requestId) chrome.tabs.sendMessage(tabId,{kind:'qp-cancel',requestId}).catch(()=>{}); };
  port.onDisconnect.addListener(cancel);
  port.onMessage.addListener(async message => {
    if (message.kind === 'cancel') { cancel();return; }
    if (started || message.kind !== 'transfer') return;
    started=true;const start=performance.now();const request=message.request;requestId=request?.requestId;
    const result = (state,code) => ({requestId,state,code,durationMs:Math.round(performance.now()-start),adapterVersion:VERSION});
    const send = (r,progress=false) => {try{port.postMessage({result:r,progress});}catch{cancel();}};
    const check = () => {if(cancelled)throw new Error('cancelled');if(Date.now() >= Date.parse(request.expiresAtUtc))throw new Error('expired-request');};
    let final, reserved=false, submissionPossible=false;
    try {
      validate(request); await ledger.reserve(request); reserved=true;check();
      const provider=PROVIDERS[request.provider];
      if(!provider.allowed) { final=result('BlockedByProvider','provider-terms-restriction'); }
      else {
        send(result('ProviderTabOpening','opening-provider'),true);
        // Only root/new-chat pages. Never navigate or inject into an existing conversation.
        const existing=(await chrome.tabs.query({url:provider.url})).find(t=>destination(request.provider,t.url));
        const tab=existing ? await chrome.tabs.update(existing.id,{active:true}) : await chrome.tabs.create({url:provider.url,active:true});tabId=tab.id;
        await chrome.windows.update(tab.windowId,{focused:true});
        await waitTab(tabId,check);check();
        const current=await chrome.tabs.get(tabId);
        if(!destination(request.provider,current.url))throw new Error('provider-navigation');
        send(result('WaitingForEditor','waiting-editor'),true);
        await chrome.scripting.executeScript({target:{tabId,frameIds:[0]},files:['content/providers/editor.js',`content/providers/${provider.adapter}.js`]});check();
        const [prepared]=await chrome.scripting.executeScript({target:{tabId,frameIds:[0]},func:r=>globalThis.QuickPromptProvider.prepare(r),args:[request]});
        check();const value=prepared?.result;
        if(!value)throw new Error('no-adapter-result');
        if(value.state !== 'Prepared' || request.action === 'PrepareOnly') final=result(value.state,value.code);
        else {
          send(result('Prepared','exact-editor-match'),true);check();
          const identity=await chrome.tabs.get(tabId);
          if(!destination(request.provider,identity.url))throw new Error('provider-navigation');
          if(!identity.active || !(await chrome.windows.get(identity.windowId)).focused)throw new Error('focus-lost');
          send(result('SubmissionRequested','one-ui-attempt'),true);
          check();submissionPossible=true;
          const [committed]=await chrome.scripting.executeScript({target:{tabId,frameIds:[0]},func:id=>globalThis.QuickPromptProvider.commit(id),args:[requestId]});
          // A cancellation cannot undo a click. Preserve the adapter's observed result after commit.
          final=result(committed?.result?.state || 'Failed',committed?.result?.code || 'submission-unconfirmed');
        }
      }
    } catch(e) {
      const code=['cancelled','expired-request','replay-request','ledger-full','invalid-request','invalid-prompt','invalid-metadata','provider-navigation','focus-lost','tab-timeout'].includes(e.message)?e.message:'bridge-failed';
      final=submissionPossible?result('NeedsManualPaste','submission-unconfirmed'):result(code==='cancelled'?'Cancelled':code==='tab-timeout'?'Timeout':'Failed',code);
    }
    if(reserved)try{await ledger.finish(requestId,final);}catch{final={...final,code:'audit-write-failed'};}
    send(final);
  });
});
function waitTab(id,check) {
  return new Promise((resolve,reject)=>{
    let done=false;
    const finish=(error)=>{if(done)return;done=true;clearTimeout(timer);clearInterval(cancelTimer);chrome.tabs.onUpdated.removeListener(update);chrome.tabs.onRemoved.removeListener(removed);error?reject(error):resolve();};
    const update=(tabId,change)=>{if(tabId===id && change.status==='complete')finish();};
    const removed=tabId=>{if(tabId===id)finish(new Error('provider-navigation'));};
    const timer=setTimeout(()=>finish(new Error('tab-timeout')),25000);
    const cancelTimer=setInterval(()=>{try{check();}catch(e){finish(e);}},100);
    chrome.tabs.onUpdated.addListener(update);chrome.tabs.onRemoved.addListener(removed);
    chrome.tabs.get(id).then(tab=>{if(tab.status==='complete')finish();}).catch(()=>finish(new Error('provider-navigation')));
  });
}
