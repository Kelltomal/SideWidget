// Run against a TEST profile of the real Windows app with
// WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9234.
// Ordinary focus timers only: never schedules shutdown or restart.
import assert from 'node:assert/strict';

const endpoint=process.argv[2]??'http://127.0.0.1:9234';
const pages=await(await fetch(`${endpoint}/json/list`)).json();
const page=pages.find(item=>item.type==='page'&&item.url.startsWith('https://sidewidget.local/'));
assert.ok(page,'SideWidget WebView not found');
const socket=new WebSocket(page.webSocketDebuggerUrl);
await new Promise((resolve,reject)=>{socket.onopen=resolve;socket.onerror=reject;});
let sequence=0;const pending=new Map();
socket.onmessage=event=>{const message=JSON.parse(event.data);const resolve=pending.get(message.id);if(resolve){pending.delete(message.id);resolve(message);}};
async function evaluate(expression){
 const id=++sequence;
 const response=await new Promise((resolve,reject)=>{
  const timeout=setTimeout(()=>{pending.delete(id);reject(new Error('WebView became unresponsive'));},6000);
  pending.set(id,value=>{clearTimeout(timeout);resolve(value);});
  socket.send(JSON.stringify({id,method:'Runtime.evaluate',params:{expression,awaitPromise:true,returnByValue:true}}));
 });
 assert.ok(!response.result?.exceptionDetails,JSON.stringify(response.result?.exceptionDetails));
 return response.result?.result?.value;
}
async function command(actionId,params={}){
 const response=await evaluate(`new Promise(resolve=>{const requestId=crypto.randomUUID();const timeout=setTimeout(()=>{window.chrome.webview.removeEventListener('message',receive);resolve({status:'timeout'});},3000);function receive(event){if(event.data.requestId!==requestId)return;clearTimeout(timeout);window.chrome.webview.removeEventListener('message',receive);resolve(event.data);}window.chrome.webview.addEventListener('message',receive);window.chrome.webview.postMessage({protocolVersion:1,requestId,actionId:${JSON.stringify(actionId)},params:${JSON.stringify(params)}});})`);
 assert.notEqual(response.status,'timeout',`${actionId} did not receive a response`);
 return response;
}
const created=new Set();
async function start(seconds){
 const response=await command('timer.start',{mode:'timer',seconds});assert.equal(response.status,'result');
 const id=response.data.at(-1).id;created.add(id);return id;
}
try{
 await command('shell.collapse');
 const first=await start(120),second=await start(240);
 for(const action of ['timer.pause','timer.resume']){
  const response=await command(action,{id:first});assert.equal(response.status,'result');
  assert.equal(response.data.find(item=>item.id===first).state.status,action==='timer.pause'?'paused':'running');
 }
 const reset=await command('timer.reset',{id:first});assert.equal(reset.status,'result');
 assert.ok(!reset.data.some(item=>item.id===first));assert.ok(reset.data.some(item=>item.id===second));
 assert.equal((await command('timer.reset',{id:second})).status,'result');
 const expired=await start(1);await new Promise(resolve=>setTimeout(resolve,1400));
 const state=await command('timer.get');assert.equal(state.data.find(item=>item.id===expired).state.status,'ringing');
 const dismissed=await command('timer.dismiss',{id:expired});assert.equal(dismissed.status,'result');
 assert.ok(!dismissed.data.some(item=>item.id===expired));
 const invalid=await command('timer.reset',{id:expired,extra:true});assert.equal(invalid.status,'error');assert.equal(invalid.error.code,'schema');
 await command('shell.interactive');
 assert.equal((await command('state.get')).status,'result');
 console.log('PASS real WinUI/WebView2: collapsed start, selected pause/resume/reset, expiry/dismiss, immediate schema error, responsive after reopening');
}finally{
 for(const id of created)await command('timer.reset',{id}).catch(()=>{});
 socket.close();
}
