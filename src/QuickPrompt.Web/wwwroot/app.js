window.qp = {
  fallbackJobs: new Map(),
  init(language) {
    this.language(language);
    document.getElementById('startup-loading')?.remove();
    if (this.ready) return;
    this.ready = true;
    document.addEventListener('click', e => { this.beginFallback(e); document.querySelectorAll('.prompt-options[open]').forEach(d=>{if(!d.contains(e.target))d.open=false;}); }, true);
    document.addEventListener('close', e => {
      if (!e.target.matches?.('#overlay-host dialog') || document.querySelector('#overlay-host dialog[open]')) return;
      const active = document.activeElement;
      if (active && active !== document.body && active.checkVisibility() && !active.closest('#overlay-host')) return;
      const origin = this.overlayReturnFocus;
      const target = origin?.isConnected && origin.matches('a[href],button,input,textarea,select,[tabindex]') && origin.checkVisibility() ? origin : document.getElementById('search');
      target?.focus({preventScroll:true});
    }, true);
    document.addEventListener('keydown', e => {
      if(e.target.id==='assist-command' && e.key==='Enter' && !e.shiftKey)e.preventDefault();
      if(e.key==='Escape'){
        const assistant=document.getElementById('assist-dialog');
        if(assistant?.open){e.preventDefault();assistant.close();return;}
        document.querySelectorAll('.prompt-options[open]').forEach(d=>d.open=false);
      }
      if ((e.ctrlKey || e.metaKey) && !e.altKey && e.target.id === 'template' && ['z','y'].includes(e.key.toLowerCase())) {
        e.preventDefault();
        const redo=e.key.toLowerCase()==='y'||e.shiftKey;
        const action=e.target.closest('.template-text-editor')?.querySelector(redo?'[data-editor-redo]':'[data-editor-undo]');
        if(action && !action.disabled)action.click();
      }
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault(); this.open('assist-dialog',false);
      }
      if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && e.key === 'Enter' && e.target.closest?.('.completion-form')) {
        const copy = e.target.closest('.completion-form').querySelector('button[data-qp-copy]');
        if (copy && !copy.disabled) { e.preventDefault(); copy.click(); }
      }
    });
  },
  language(value) { document.documentElement.lang = value; },
  panelInit(reference) {
    this.panelReference=reference;
    if(this.panelReady)return;this.panelReady=true;
    window.addEventListener('message',async e=>{
      const m=e.data;
      if(window!==top || e.source!==window || e.origin!==location.origin || m?.source!=='qp.panel.request' || typeof m.id!=='string' || !/^[a-f0-9-]{36}$/.test(m.id))return;
      let result;
      try{
        if(m.kind==='library' && typeof m.search==='string' && m.search.length<=200 && typeof m.favorites==='boolean' && Number.isInteger(m.page) && m.page>=0 && m.page<=10000)
          result=await this.panelReference.invokeMethodAsync('Library',m.search,m.favorites,m.page);
        else if(m.kind==='compile' && typeof m.templateId==='string' && Number.isInteger(m.revision) && m.values && typeof m.values==='object')
          result=await this.panelReference.invokeMethodAsync('Compile',m.templateId,m.revision,m.values);
        else throw Error('invalid');
        window.postMessage({source:'qp.panel.result',id:m.id,result},location.origin);
      }catch{window.postMessage({source:'qp.panel.result',id:m.id,error:'No se pudo completar. Comprueba los campos o actualiza la biblioteca.'},location.origin);}
    });
  },
  open(id, modal=true) {
    const d = document.getElementById(id); if(!d || d.open)return;
    document.querySelectorAll('.prompt-options[open]').forEach(menu=>menu.open=false);
    if (!document.activeElement?.closest('#overlay-host')) this.overlayReturnFocus = document.activeElement;
    for(const other of document.querySelectorAll('#overlay-host dialog[open]'))other.close();
    d.dataset.theme=document.querySelector('.shell')?.dataset.theme || 'light';
    if(modal)d.showModal();else{d.show();d.querySelector('textarea,input,button')?.focus();}
  },
  close(id) { document.getElementById(id).close(); },
  focus(selector) {
    const e = document.querySelector(selector);
    if (!e) return;
    for (let parent = e.parentElement; parent; parent = parent.parentElement) {
      if (parent.matches('details')) parent.open = true;
    }
    if (!e.matches('input,textarea,button,select,a')) e.tabIndex = -1;
    e.focus({preventScroll:true});
    e.scrollIntoView({block:'nearest',behavior:'instant'});
  },
  async copy(text) { await navigator.clipboard.writeText(text); },
  async share(title,text) {
    if(!navigator.share)return 'unavailable';
    try{await navigator.share({title,text});return 'shared';}catch(e){return e.name==='AbortError'?'cancelled':'unavailable';}
  },
  beginFallback(e) {
    const anchor=e.target.closest?.('a[data-qp-fallback]');
    if(!e.isTrusted || !anchor || window!==top)return;
    const destinations={ChatGptWeb:'https://chatgpt.com/',ClaudeWeb:'https://claude.ai/new'};
    const destination=destinations[anchor.dataset.qpFallback];
    const source=anchor.closest('.detail')?.querySelector('[data-testid="compiled"]');
    if(!destination || anchor.href!==destination || !source || !anchor.dataset.qpScope)return;
    e.preventDefault(); // Blazor still receives the event and records the outcome below.
    // Initiate clipboard access while the originating document still has trusted-click focus.
    const writing=navigator.clipboard.writeText(source.textContent).then(()=>true,()=>false);
    let popup;try{popup=window.open('about:blank','_blank');if(popup)popup.opener=null;}catch{}
    const job=Promise.race([writing,new Promise(resolve=>setTimeout(()=>resolve(false),5000))]).then(copied=>{
      let opened=false;try{if(popup && !popup.closed){popup.location.replace(destination);opened=true;}}catch{}
      return {copied,opened};
    });
    this.fallbackJobs.set(anchor.dataset.qpScope,job);
    setTimeout(()=>this.fallbackJobs.delete(anchor.dataset.qpScope),20000);
  },
  finishFallback(scope) {
    const result=this.fallbackJobs.get(scope);this.fallbackJobs.delete(scope);
    return result || {copied:false,opened:false};
  },
  download(name, text) {
    const url = URL.createObjectURL(new Blob([text], {type: 'application/json'}));
    const a = document.createElement('a'); a.href = url; a.download = name; a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  },
  selectVariable(name) {
    const e = document.getElementById('template'); const i = e.value.indexOf('<' + name + '>'); e.focus(); if (i >= 0) e.setSelectionRange(i+1, i+1+name.length);
  },
  editorSelection() { const e=document.getElementById('template');return {start:e.selectionStart,end:e.selectionEnd,text:e.value}; },
  editorFocus() { document.getElementById('template')?.focus({preventScroll:true}); },
  // Compatibility for a previously running server until it is restarted. The new editor uses C# commands.
  wrap() {
    const e=document.getElementById('template'),start=e.selectionStart,end=e.selectionEnd;
    if(start===end)return e.value;
    const text=e.value.slice(0,start)+'<'+e.value.slice(start,end)+'>'+e.value.slice(end);
    e.value=text;e.dispatchEvent(new Event('input',{bubbles:true}));e.focus();return text;
  }
};
setTimeout(()=>{const loader=document.getElementById('startup-loading');if(loader){loader.classList.add('slow');loader.querySelector('.startup-help').hidden=false;}},12000);
