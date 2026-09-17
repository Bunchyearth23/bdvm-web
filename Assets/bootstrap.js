(()=>{'use strict';
const space=()=>document.getElementById('module-content');
const normalizeKeys=value=>Array.isArray(value)?value.map(normalizeKeys):value&&typeof value==='object'?Object.fromEntries(Object.entries(value).map(([key,item])=>[key.charAt(0).toLowerCase()+key.slice(1),normalizeKeys(item)])):value;
const json=async(response)=>{let body;try{body=normalizeKeys(await response.json())}catch{throw new Error('The host returned an unreadable response. Check the connection, then retry.')}if(!response.ok)throw new Error(body?.error||(response.status===401?'Your session requires sign-in. Reconnect to the host.':`Host request failed (${response.status}).`));return body};
const request=async(url,options={})=>{const controller=new AbortController(),timer=setTimeout(()=>controller.abort(),30000);try{return await fetch(url,{...options,signal:controller.signal}).then(json)}finally{clearTimeout(timer)}};
const pendingKey=()=>{const principal=globalThis.BdvmWebShell?.principal();return principal?`bdvm-pending:${location.host}:${principal}`:null};
const pendingStore={
  restorePending:()=>{try{const key=pendingKey(),value=key&&JSON.parse(sessionStorage.getItem(key)||'null');return value?.moduleId==='BDVM.Management'&&value.idempotencyKey&&value.correlationId?value:null}catch{return null}},
  savePending:envelope=>{try{const key=pendingKey();if(key){if(envelope)sessionStorage.setItem(key,JSON.stringify(envelope));else sessionStorage.removeItem(key)}}catch{}}
};
const managementTransport={
  ...pendingStore,
  snapshot:()=>request('/api/modules/bdvm.management/snapshot',{cache:'no-store',credentials:'same-origin',headers:{Accept:'application/json'}}),
  intent:envelope=>request('/api/modules/bdvm.management/intent',{method:'POST',credentials:'same-origin',headers:{Accept:'application/json','Content-Type':'application/json'},body:JSON.stringify(envelope)}),
  exportLogs:()=>fetch('/bdvm',{cache:'no-store',credentials:'same-origin',headers:{Accept:'application/json'}}).then(json).then(data=>{const link=document.createElement('a');link.href=URL.createObjectURL(new Blob([JSON.stringify(data,null,2)],{type:'application/json'}));link.download=`bdvm-diagnostics-${new Date().toISOString().replace(/[:.]/g,'-')}.json`;link.click();setTimeout(()=>URL.revokeObjectURL(link.href),0)})
};
const dispatchTransport={
  snapshot:()=>fetch('/api/modules/bdvm.dispatch/snapshot',{cache:'no-store',credentials:'same-origin',headers:{Accept:'application/json'}}).then(json),
  intent:envelope=>fetch(envelope.intentType==='bdvm.dispatch.junction-control.v1'?'/api/modules/bdvm.dispatch/junction/control':'/api/modules/bdvm.dispatch/route/control',{method:'POST',credentials:'same-origin',headers:{Accept:'application/json','Content-Type':'application/json'},body:JSON.stringify(envelope)}).then(json)
};
let management,dispatch,managementSocket;
let liveGeneration=0;
const liveSession=()=>globalThis.crypto?.randomUUID?.()||`${Date.now()}-${Math.random().toString(16).slice(2)}`;
const managementRefreshTags=new Set(['jobs']);
const managementUpdateNeedsRefresh=update=>Array.isArray(update?.tags)?update.tags.some(tag=>managementRefreshTags.has(tag)):Object.keys(update||{}).some(tag=>managementRefreshTags.has(tag));
const wait=milliseconds=>new Promise(resolve=>setTimeout(resolve,milliseconds));
async function monitorManagement(instance,generation){const session=liveSession();let failures=0;while(generation===liveGeneration&&location.pathname==='/management'){if(typeof WebSocket==='function'){await new Promise(resolve=>{const scheme=location.protocol==='https:'?'wss':'ws';const socket=managementSocket=new WebSocket(`${scheme}://${location.host}/updates-ws/${session}`);socket.onopen=()=>{failures=0};socket.onmessage=event=>{try{const update=JSON.parse(event.data);if(!document.hidden&&managementUpdateNeedsRefresh(update))instance.markStale()}catch{}};socket.onerror=()=>socket.close();socket.onclose=()=>{if(managementSocket===socket)managementSocket=undefined;resolve()}})}else{try{const response=await fetch(`/updates/${session}`,{cache:'no-store',credentials:'same-origin'});if(!response.ok)throw new Error(`HTTP ${response.status}`);const update=await response.json();failures=0;if(!document.hidden&&managementUpdateNeedsRefresh(update))instance.markStale()}catch{failures++}}if(generation===liveGeneration&&location.pathname==='/management'){failures++;await wait(Math.min(5000,250*Math.pow(2,failures)))}}}
function show(path=location.pathname){
  const generation=++liveGeneration;
  if(management){management.dispose();management=undefined;}
  if(managementSocket){managementSocket.close();managementSocket=undefined;}
  const root=space();if(!root)return;
  root.replaceChildren();const mount=document.createElement('div');root.append(mount);
  const preview=new URLSearchParams(location.search).get('preview')==='1'&&location.port==='5173';
  if(path==='/dispatch'&&preview){mount.className='empty-state';const title=document.createElement('h1'),description=document.createElement('p'),link=document.createElement('a');title.textContent='Dispatch requires a live world';description.textContent='The map, vehicle positions and route commands must be tested with the game host. Management preview uses simulated data only.';link.href='/dispatch';link.textContent='Open live Dispatch';mount.append(title,description,link);return;}
  if(path==='/dispatch'){const frame=document.createElement('iframe');frame.src='/legacy-dispatch'+location.search;frame.title='Live railway dispatch';frame.className='bdvm-dispatch-frame';frame.setAttribute('allow','fullscreen');mount.className='bdvm-dispatch-host';mount.append(frame);return}
  if(path==='/management'){const loading=document.createElement('p');loading.textContent='Loading Management…';mount.append(loading);management=globalThis.BdvmManagement.create(mount,managementTransport);management.refresh();if(!preview)monitorManagement(management,generation);return}
  if(path!=='/'){mount.className='empty-state';mount.textContent='This workspace is unavailable. Choose a module from the navigation.';}
}
window.addEventListener('bdvm:navigate',event=>show(event.detail?.path));
window.addEventListener('popstate',()=>show());
window.addEventListener('bdvm:connected',()=>show());
window.addEventListener('bdvm:disconnected',()=>show('/'));
const start=()=>{if(globalThis.BdvmWebShell?.principal())show()};
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start);else start();
})();
