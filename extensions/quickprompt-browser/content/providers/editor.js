(() => {
  if(globalThis.QuickPromptEditor)return;
  const pending=new Map(), cancelled=new Set();
  chrome.runtime.onMessage.addListener(message=>{if(message?.kind==='qp-cancel'){cancelled.add(message.requestId);document.dispatchEvent(new Event('qp-cancel'));}});
  const visible=e=>e && e.isConnected && e.getClientRects().length>0 && getComputedStyle(e).visibility!=='hidden';
  const read=e=>e instanceof HTMLTextAreaElement?e.value:e.innerText;
  const only=selectors=>{const items=[...new Set(selectors.flatMap(s=>[...document.querySelectorAll(s)]))].filter(visible);return items.length===1?items[0]:null;};
  const answer=(state,code)=>({state,code});
  function waitFor(find,request,timeout=15000){
    return new Promise(resolve=>{
      let finished=false;
      const finish=value=>{if(finished)return;finished=true;observer.disconnect();clearTimeout(timer);document.removeEventListener('qp-cancel',check);resolve(value);};
      const check=()=>{if(cancelled.has(request.requestId))finish(answer('Cancelled','cancelled'));else if(Date.now()>=Date.parse(request.expiresAtUtc))finish(answer('Timeout','expired-request'));else{const value=find();if(value)finish(value);}};
      const deadline=Date.parse(request.expiresAtUtc);
      const observer=new MutationObserver(check);const timer=setTimeout(()=>finish(answer('Timeout',Date.now()>=deadline?'expired-request':'editor-timeout')),Math.max(0,Math.min(timeout,deadline-Date.now())));
      observer.observe(document.documentElement,{subtree:true,childList:true,attributes:true,characterData:true});document.addEventListener('qp-cancel',check);check();
    });
  }
  function afterRender(request){
    return new Promise(resolve=>{
      let finished=false,first,second;
      const finish=value=>{if(finished)return;finished=true;clearTimeout(timer);if(first!==undefined)cancelAnimationFrame(first);if(second!==undefined)cancelAnimationFrame(second);document.removeEventListener('qp-cancel',check);resolve(value);};
      const check=()=>{if(cancelled.has(request.requestId))finish(answer('Cancelled','cancelled'));else if(Date.now()>=Date.parse(request.expiresAtUtc))finish(answer('Timeout','expired-request'));};
      const timer=setTimeout(()=>finish(answer('Timeout','editor-render-timeout')),Math.max(0,Math.min(2000,Date.parse(request.expiresAtUtc)-Date.now())));
      document.addEventListener('qp-cancel',check);
      first=requestAnimationFrame(()=>{check();if(!finished)second=requestAnimationFrame(()=>{check();if(!finished)finish(null);});});
      check();
    });
  }
  globalThis.QuickPromptEditor={
    create(config){
      const validUrl=()=>location.protocol==='https:' && location.hostname===config.host && !location.search && !location.hash && config.paths.includes(location.pathname);
      const blocked=()=>document.querySelector('iframe[src*="recaptcha"],iframe[src*="challenge"],#challenge-running')?answer('BlockedByProvider','provider-challenge'):document.querySelector(config.login)?answer('LoginRequired','login-required'):null;
      const gated=()=>blocked() || (only(config.stop)?answer('NeedsManualPaste','provider-busy'):null);
      return {
        async prepare(request){
          if(!validUrl())return answer('BlockedByProvider','provider-navigation');
          const sourceUrl=location.href;
          const found=await waitFor(()=>gated() || only(config.editors),request);
          if(!(found instanceof Element))return found;
          if(!validUrl() || cancelled.has(request.requestId))return answer('Cancelled','cancelled-or-navigated');
          if(Date.now()>=Date.parse(request.expiresAtUtc))return answer('Timeout','expired-request');
          if(read(found) && read(found)!==request.compiledPrompt)return answer('NeedsManualPaste','editor-not-empty');
          if(read(found)!==request.compiledPrompt){
            found.focus();
            if(found instanceof HTMLTextAreaElement){Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype,'value').set.call(found,request.compiledPrompt);}
            else {const range=document.createRange();range.selectNodeContents(found);const selection=getSelection();selection.removeAllRanges();selection.addRange(range);if(!document.execCommand('insertText',false,request.compiledPrompt))found.replaceChildren(document.createTextNode(request.compiledPrompt));}
            found.dispatchEvent(new InputEvent('input',{bubbles:true,inputType:'insertText',data:request.compiledPrompt}));
          }
          // Check after the editor's own state updates, without assuming a fixed loading sleep.
          const renderState=await afterRender(request);if(renderState)return renderState;
          if(cancelled.has(request.requestId))return answer('Cancelled','cancelled');
          if(Date.now()>=Date.parse(request.expiresAtUtc))return answer('Timeout','expired-request');
          if(!validUrl() || location.href!==sourceUrl)return answer('BlockedByProvider','provider-navigation');
          const gate=gated();if(gate)return gate;
          if(!visible(found) || only(config.editors)!==found)return answer('NeedsManualPaste','editor-replaced');
          if(read(found)!==request.compiledPrompt)return answer('NeedsManualPaste','editor-text-mismatch');
          if(request.action==='PrepareAndSend'){
            pending.set(request.requestId,{request,editor:found,url:location.href,attempted:false});
            setTimeout(()=>pending.delete(request.requestId),Math.max(0,Date.parse(request.expiresAtUtc)-Date.now()));
          }
          return answer('Prepared','exact-editor-match');
        },
        async commit(id){
          const transfer=pending.get(id);
          if(!transfer || transfer.attempted)return answer('Failed','replay-or-unprepared');
          const {request,editor,url}=transfer;
          if(request.action!=='PrepareAndSend' || request.explicitUserAction!==true || cancelled.has(id) || Date.now()>=Date.parse(request.expiresAtUtc))return answer('Cancelled','cancelled-or-expired');
          const gate=gated();if(gate)return gate;
          if(!validUrl() || location.href!==url || !document.hasFocus() || !visible(editor) || read(editor)!==request.compiledPrompt)return answer('NeedsManualPaste','editor-changed-or-focus-lost');
          const button=only(config.send);
          if(!button || button.disabled || button.getAttribute('aria-disabled')==='true')return answer('NeedsManualPaste','send-unavailable');
          transfer.attempted=true; // At most one UI interaction, even if no confirmation follows.
          button.click();
          const observed=await waitFor(()=>{
            const gate=blocked();if(gate)return gate;
            return (visible(editor) && read(editor)==='') || only(config.stop)?answer('SubmittedUI','ui-transition-observed'):null;
          },request,5000);
          pending.delete(id);
          if(observed.state==='SubmittedUI')return observed;
          if(observed.state==='LoginRequired' || observed.state==='BlockedByProvider')return answer(observed.state,'provider-blocked-after-ui-attempt');
          return answer('NeedsManualPaste',cancelled.has(id)?'cancelled-after-ui-attempt':'submission-unconfirmed');
        }
      };
    }
  };
})();
