(()=>{'use strict';
const space=()=>document.getElementById('module-space');
const normalizeKeys=value=>Array.isArray(value)?value.map(normalizeKeys):value&&typeof value==='object'?Object.fromEntries(Object.entries(value).map(([key,item])=>[key.charAt(0).toLowerCase()+key.slice(1),normalizeKeys(item)])):value;
const json=async(response)=>{const body=normalizeKeys(await response.json());if(!response.ok)throw new Error(body?.error||`HTTP ${response.status}`);return body};
const managementTransport={
  snapshot:()=>fetch('/api/modules/bdvm.management/snapshot',{cache:'no-store',credentials:'same-origin',headers:{Accept:'application/json'}}).then(json),
  intent:envelope=>fetch('/api/modules/bdvm.management/intent',{method:'POST',credentials:'same-origin',headers:{Accept:'application/json','Content-Type':'application/json'},body:JSON.stringify(envelope)}).then(json),
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
  if(managementSocket){managementSocket.close();managementSocket=undefined;}
  const root=space();if(!root)return;
  root.replaceChildren();const mount=document.createElement('div');root.append(mount);
  if(path==='/dispatch'){const frame=document.createElement('iframe');frame.src='/legacy-dispatch';frame.title='Live railway dispatch';frame.className='bdvm-dispatch-frame';frame.setAttribute('allow','fullscreen');mount.className='bdvm-dispatch-host';mount.append(frame);return}
  if(path==='/management'){management=globalThis.BdvmManagement.create(mount,managementTransport);management.refresh();monitorManagement(management,generation);return}
  const heading=document.createElement('h1'),message=document.createElement('p');heading.textContent='Operations overview';message.textContent='Choose Dispatch or Management from the navigation.';mount.className='empty-state';mount.append(heading,message);
}
window.addEventListener('bdvm:navigate',event=>show(event.detail?.path));
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',()=>show());else show();
})();
