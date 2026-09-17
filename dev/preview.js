// Development-only fixtures. Never imported by the embedded production entry.
const scenario = new URLSearchParams(location.search).get('scenario') || 'populated';
const originalFetch = globalThis.fetch.bind(globalThis);
const wagons = Array.from({length:8},(_,i)=>({assetId:`wagon-${i}`,carGuid:`preview-car-${i}`,displayName:`F-${100+i}`,kind:'FreightWagon',ownerType:i>=6?'Company':'Player',owner:i>=6?'Valley Rail':'Alex',state:i<3?'InService':'Available',lastKnownLocation:'SM-B40'}));
const contracts = [{contractId:'preview-dossier-001',displayName:'Alex · Steel Mill → Goods Factory · Steel Billets',originFacilityId:'SM',destinationFacilityId:'GF',cargoId:'SteelBillets',quantity:3,deliveredQuantity:1,paidAmount:480,state:'Active',assignedWagons:wagons.slice(0,3).map(w=>({assetId:w.assetId})),manifests:wagons.slice(0,3).map(w=>({assetId:w.assetId,onBoardQuantity:1}))}];
const actions = [
  {area:'companies',label:'Create a company',intentType:'bdvm.management.company-governance.v1',payload:{action:'company.create'},fields:[{name:'name',label:'Company name',kind:'text',required:true}]},
  {area:'wallets',label:'Transfer to company',intentType:'bdvm.management.intent.v1',payload:{action:'wallet.transfer'},fields:[{name:'amount',label:'Amount',kind:'number',required:true}],confirmation:'Confirm this simulated transfer.'},
  {area:'fleet',label:'Rename vehicle',intentType:'bdvm.management.intent.v1',payload:{action:'fleet.rename'},fields:[{name:'assetId',label:'Vehicle',kind:'select',options:wagons.map(w=>w.assetId),optionLabels:Object.fromEntries(wagons.map(w=>[w.assetId,w.displayName])),required:true},{name:'displayName',label:'New name',kind:'text',required:true}]},

];
const market = [['Flatbed wagon',12500],['Tank wagon',18750],['Boxcar',15000],['DE2 shunter',65000],['Flatbed wagon',13000]].map(([model,price],i)=>({model,price:'$'+price.toLocaleString('en-US'),state:'Available to buy',technicalDetails:{listingId:'preview-offer-'+i,price}}));
for(const offer of market)actions.push({area:'market',label:'Buy '+offer.model,intentType:'bdvm.management.market.purchase.v1',payload:{listingId:offer.technicalDetails.listingId},confirmation:'Confirm simulated purchase of '+offer.model+' for '+offer.price+'? It will be added to your fleet for deployment.',fields:[{name:'forCompany',label:'Company pays',kind:'checkbox',value:'false'}]});
const snapshot = {
  version:1,correlationId:'preview-001',featureFlags:{},
  companies:[{name:'Valley Rail',leader:'Morgan',members:3,membershipPolicy:'ApprovalRequired'}],
  players:[{playerId:'preview-player',displayName:'Alex'}],
  wallets:[{account:'Alex',balance:'$24,850',currency:'DV dollars'},{account:'Valley Rail',balance:'$86,200',currency:'DV dollars'}],
  fleet:wagons,market,contracts,
  industry:[{site:'Steel Mill',role:'Processor',inputs:['Iron ore','Coal'],outputs:['Steel billets'],supportedCargo:['Iron ore','Coal','Steel billets'],stocks:['Steel billets: 120 / 200','Iron ore: 85 / 200']},{site:'Goods Factory',role:'Manufacturer',inputs:['Steel billets'],outputs:['Goods'],supportedCargo:['Steel billets','Goods'],stocks:['Steel billets: 60 / 200','Goods: 100 / 200']}],
  diagnostics:[{subsystem:'Connection',runtimeState:'Simulated preview',description:'Game physics and authority are not exercised here.'},{subsystem:'Persistence',runtimeState:'Not connected',description:'Preview changes are discarded when the page reloads.'}],
  actions,
  industrialWorkspace:{enabled:true,routes:[{originFacilityId:'SM',destinationFacilityId:'GF',cargoIds:['SteelBillets']}],locations:[{id:'SM',name:'Steel Mill'},{id:'GF',name:'Goods Factory'}],cargoChoices:[{id:'SteelBillets',name:'Steel Billets'}],wagons,contracts,personalWagons:wagons.map(w=>w.assetId),companyWagons:[],tags:wagons.map((w,i)=>({assetId:w.assetId,sourceFacilityId:'SM',cargoId:'SteelBillets',loaded:i%2===0,loadedCargoAmount:i%2===0?1:0,loadedCargoId:i%2===0?'SteelBillets':null,capacity:1,lifetime:'UntilEmpty',dossierId:i<3?'preview-dossier-001':null}))}
};
if(scenario==='empty') {
  for(const key of ['companies','wallets','fleet','market','contracts','industry','diagnostics','actions'])snapshot[key]=[];
  snapshot.industrialWorkspace={enabled:false};
}
function syncFleetPreview(){
 if(scenario==='empty')return;
 snapshot.actions=snapshot.actions.filter(action=>action.payload?.action!=='fleet.transfer');
 for(const wagon of wagons.filter(w=>w.state==='Available'||w.state==='Stored')){
  const targetKind=wagon.ownerType==='Player'?'Company':'Player';
  snapshot.actions.push({area:'fleet',label:(targetKind==='Company'?'Transfer to company':'Transfer to me')+' — '+wagon.displayName,intentType:'bdvm.management.fleet-manage.v1',payload:{action:'fleet.transfer',assetId:wagon.assetId,targetKind},confirmation:'Simulate ownership transfer of '+wagon.displayName+' to '+(targetKind==='Company'?'Valley Rail':'Alex')+'?'});
 }
 snapshot.industrialWorkspace.personalWagons=wagons.filter(w=>w.ownerType==='Player').map(w=>w.assetId);
 snapshot.industrialWorkspace.companyWagons=wagons.filter(w=>w.ownerType==='Company').map(w=>w.assetId);
}
const receipts = new Map();
const response = (value,status=200) => new Response(JSON.stringify(value),{status,headers:{'Content-Type':'application/json'}});
globalThis.fetch = async (input,options={}) => {
  const path = new URL(typeof input==='string'?input:input.url,location.origin).pathname;
  if(path==='/api/web/shell')return scenario==='offline'?response({error:'Preview host unavailable'},503):response({connectionState:'Preview',displayName:'Alex · Preview',principalId:'preview-player',modules:[{id:'BDVM.Management'},{id:'BDVM.Dispatch'}],navigation:[{label:'Dispatch',path:'/dispatch',ownerModuleId:'BDVM.Dispatch'},{label:'Management',path:'/management',ownerModuleId:'BDVM.Management'}]});
  if(path==='/api/modules/bdvm.management/snapshot'){syncFleetPreview();return response(snapshot);}
  if(path==='/api/modules/bdvm.management/intent') {
    const envelope=JSON.parse(options.body);
    if(receipts.has(envelope.idempotencyKey))return response(receipts.get(envelope.idempotencyKey));
    const state=scenario==='refused'?'Refused':scenario==='conflict'?'Conflict':'Succeeded';
    const receipt={state,code:state==='Succeeded'?'Simulated response':state==='Refused'?'Preview permission denied':'Preview state changed; review refreshed data',correlationId:envelope.correlationId};
    if(state==='Succeeded') {
      if(envelope.payload.action==='company.create')snapshot.companies.push({name:envelope.payload.name,leader:'Alex',members:1,membershipPolicy:'ApprovalRequired'});
      if(envelope.payload.action==='fleet.rename'){const wagon=wagons.find(w=>w.assetId===envelope.payload.assetId);if(wagon)wagon.displayName=envelope.payload.displayName;}
      if(envelope.payload.action==='fleet.transfer'){const wagon=wagons.find(w=>w.assetId===envelope.payload.assetId);if(wagon){wagon.ownerType=envelope.payload.targetKind;wagon.owner=wagon.ownerType==='Company'?'Valley Rail':'Alex';}}
      if(envelope.intentType==='bdvm.management.market.purchase.v1'){snapshot.market=snapshot.market.filter(offer=>offer.technicalDetails.listingId!==envelope.payload.listingId);snapshot.actions=snapshot.actions.filter(action=>action.payload?.listingId!==envelope.payload.listingId);}
      snapshot.version++;
    }
    receipts.set(envelope.idempotencyKey,receipt);
    if(scenario==='timeout')throw new DOMException('Simulated lost response. Retry the same request.','AbortError');
    return response(receipt);
  }
  if(path==='/bdvm')return response({preview:true,snapshot});
  if(path.startsWith('/api/')||path.startsWith('/updates'))return response({error:'Unavailable in preview'},503);
  return originalFetch(input,options);
};
