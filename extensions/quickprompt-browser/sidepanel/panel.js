const origin='http://127.0.0.1:5188';
const $=id=>document.getElementById(id);let tabId=null,page=0,total=0,selected=null,compiled=null,busy=false,version=0;
const status=text=>$('status').textContent=text;
async function request(message){
  if(!tabId)throw Error('Conecta primero la biblioteca local.');
  const result=await chrome.tabs.sendMessage(tabId,message);
  if(!result?.result)throw Error(result?.error||'Abre QuickPrompt y vuelve a conectar.');return result.result;
}
async function work(action){if(busy)return;busy=true;for(const b of document.querySelectorAll('button'))b.disabled=true;try{await action();}catch(e){status(e.message||'No se pudo completar. Reintenta.');}finally{busy=false;for(const b of document.querySelectorAll('button'))b.disabled=false;}}
async function load(){
  const result=await request({kind:'library',search:$('search').value,favorites:$('favorites').checked,page});
  $('items').replaceChildren();for(const item of result.items){const b=document.createElement('button');b.className='item';const title=document.createElement('strong');title.textContent=item.title;const meta=document.createElement('small');meta.textContent=(item.favorite?'Favorito · ':'')+item.category+' · '+(item.variables.length?item.variables.length+' variables':'Sin variables');b.append(title,meta);b.addEventListener('click',()=>show(item));$('items').append(b);}
  total=result.total;$('count').textContent=result.total+' plantillas · '+(page+1);$('library').hidden=false;$('detail').hidden=true;status(result.total?'Biblioteca conectada.':'No hay plantillas para esta búsqueda.');
}
function show(item){
  version++;selected=item;compiled=null;$('title').textContent=item.title;$('preview').textContent=item.text;$('variables').replaceChildren();
  for(const [i,name] of item.variables.entries()){const label=document.createElement('label'),input=document.createElement('textarea');label.htmlFor='v-'+i;label.textContent=name;input.id=label.htmlFor;input.rows=2;input.maxLength=8000;input.required=true;input.dataset.variable=name;input.addEventListener('input',()=>{version++;compiled=null;$('preview').textContent=item.text;});$('variables').append(label,input);}
  $('library').hidden=true;$('detail').hidden=false;($('variables').querySelector('textarea')||$('copy')).focus();
}
async function compile(){
  if(!selected)throw Error('Selecciona una plantilla.');if(compiled)return compiled;
  const values=Object.create(null);for(const e of $('variables').querySelectorAll('textarea')){if(!e.value.trim()){e.reportValidity();e.focus();throw Error('Completa '+e.dataset.variable+'.');}values[e.dataset.variable]=e.value;}
  const captured=version,result=await request({kind:'compile',templateId:selected.id,revision:selected.revision,values});
  if(captured!==version)throw Error('Los valores cambiaron. Revisa y vuelve a copiar.');
  compiled=result;$('preview').textContent=compiled.text;return compiled;
}
async function copy(){const p=await compile();await navigator.clipboard.writeText(p.text);status('Copiado al portapapeles.');}
$('connect').addEventListener('click',async()=>{
  // Only tabs registered by the already-authorized loopback content script. No browsing-history permission.
  await work(async()=>{const target=await chrome.runtime.sendMessage({kind:'panel-target'});if(!Number.isInteger(target?.tabId))throw Error('Abre QuickPrompt antes de conectar.');tabId=target.tabId;page=0;await load();$('disconnect').hidden=false;});
});
$('disconnect').addEventListener('click',()=>{tabId=null;selected=null;compiled=null;$('items').replaceChildren();$('variables').replaceChildren();$('preview').textContent='';$('library').hidden=true;$('detail').hidden=true;$('disconnect').hidden=true;status('Desconectado. Datos del panel eliminados de memoria.');});
$('open').addEventListener('click',()=>chrome.tabs.create({url:origin+'/'}));
$('find').addEventListener('click',()=>work(async()=>{page=0;await load();}));
$('search').addEventListener('keydown',e=>{if(e.key==='Enter')$('find').click();});
$('previous').addEventListener('click',()=>work(async()=>{page=Math.max(0,page-1);await load();}));
$('next').addEventListener('click',()=>work(async()=>{if((page+1)*12<total){page++;await load();}}));
$('back').addEventListener('click',()=>work(load));
$('copy').addEventListener('click',()=>work(copy));
for(const [id,url] of [['chatgpt','https://chatgpt.com/'],['claude','https://claude.ai/new']])$(id).addEventListener('click',()=>work(async()=>{await copy();await chrome.tabs.create({url});status('Copiado y abierto. Pega y decide el envío en la otra web.');}));
