(()=>{'use strict';
const space=()=>document.getElementById('module-space');
const normalizeKeys=value=>Array.isArray(value)?value.map(normalizeKeys):value&&typeof value==='object'?Object.fromEntries(Object.entries(value).map(([key,item])=>[key.charAt(0).toLowerCase()+key.slice(1),normalizeKeys(item)])):value;
const json=async(response)=>{const body=normalizeKeys(await response.json());if(!response.ok)throw new Error(body?.error||`HTTP ${response.status}`);return body};
const managementTransport={
  snapshot:()=>fetch('/api/modules/bdvm.management/snapshot',{cache:'no-store',credentials:'same-origin',headers:{Accept:'application/json'}}).then(json),
  intent:envelope=>fetch('/api/modules/bdvm.management/intent',{method:'POST',credentials:'same-origin',headers:{Accept:'application/json','Content-Type':'application/json'},body:JSON.stringify(envelope)}).then(json),
  exportLogs:()=>fetch('/bdvm',{cache:'no-store',credentials:'same-origin',headers:{Accept:'application/json'}}).then(json).then(data=>{const link=document.createElement('a');link.href=URL.createObjectURL(new Blob([JSON.stringify(data,null,2)],{type:'application/json'}));link.download=`bdvm-diagnostics-${new Date().toISOString().replace(/[:.]/g,'-')}.json`;link.click();setTimeout(()=>URL.revokeObjectURL(link.href),0)})
};
let management;
function show(path=location.pathname){
  if(path==='/dispatch'){location.assign('/');return}
  const root=space();if(!root)return;
  if(path!=='/management')return;
  root.replaceChildren();const mount=document.createElement('div');root.append(mount);
  management=globalThis.BdvmManagement.create(mount,managementTransport);management.refresh();
}
window.addEventListener('bdvm:navigate',event=>show(event.detail?.path));
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',()=>show());else show();
})();
