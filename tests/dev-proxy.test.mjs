import test from 'node:test';
import assert from 'node:assert/strict';
import config from '../vite.config.js';

test('development entry serves local assets and all live Dispatch routes', async () => {
  let middleware;
  config.plugins.find(plugin=>plugin.name==='bdvm-development-entry').configureServer({middlewares:{use:callback=>middleware=callback}});
  let body,contentType;
  await middleware({url:'/bdvm-ui/modules/bdvm.management/app.js',method:'GET',headers:{host:'127.0.0.1:5173'}},{setHeader:(name,value)=>{if(name==='Content-Type')contentType=value},end:value=>body=value},()=>assert.fail('Local asset must not reach the game proxy'));
  assert.equal(contentType,'text/javascript');assert.match(String(body),/class ManagementApp/);
  for(const route of ['/api','/legacy-dispatch','/trainset','/track','/junction','/car','/job','/infrastructure','/updates','/res','/bdvm'])assert.ok(config.server.proxy[route],route);
});

test('development mutation proxy rejects foreign origins before rewriting trusted requests',async()=>{
  let middleware;
  config.plugins.find(plugin=>plugin.name==='bdvm-development-entry').configureServer({middlewares:{use:callback=>middleware=callback}});
  let status,passed=false;
  await middleware({url:'/api/modules/bdvm.management/intent',method:'POST',headers:{host:'127.0.0.1:5173',origin:'https://example.com'}},{writeHead:code=>status=code,end(){}},()=>passed=true);
  assert.equal(status,403);assert.equal(passed,false);
  const headers={host:'127.0.0.1:5173',origin:'http://127.0.0.1:5173'};
  await middleware({url:'/api/modules/bdvm.management/intent',method:'POST',headers},{},()=>passed=true);assert.equal(passed,true);
  let hook,rewritten;
  config.server.proxy['/api'].configure({on:(event,callback)=>hook=callback});
  hook({setHeader:(key,value)=>rewritten=value},{headers});assert.equal(rewritten,new URL(config.server.proxy['/api'].target).origin);
});
