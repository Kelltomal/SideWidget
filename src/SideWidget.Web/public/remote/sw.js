/* Only the application shell is cached. Device trust, metadata and commands always use the network. */
const CACHE='sidewidget-remote-v1';
self.addEventListener('install',event=>{
 event.waitUntil((async()=>{
  const cache=await caches.open(CACHE);
  const response=await fetch('/',{cache:'reload'});
  if(!response.ok)throw new Error('Shell unavailable');
  const html=await response.text();
  const assets=[...html.matchAll(/(?:src|href)="(\/remote-assets\/[^"?#]+)"/g)].map(match=>match[1]);
  await cache.addAll([...new Set([...assets,'/fonts/MedeoSansVariable.woff2','/remote/icon-192.png','/remote/icon-512.png'])]);
  await cache.put('/',new Response(html,{headers:response.headers}));
 })());
});
self.addEventListener('activate',event=>{
 event.waitUntil((async()=>{
  for(const key of await caches.keys())if(key.startsWith('sidewidget-remote-')&&key!==CACHE)await caches.delete(key);
  await self.clients.claim();
 })());
});
self.addEventListener('fetch',event=>{
 const url=new URL(event.request.url);
 if(url.origin!==self.location.origin||event.request.method!=='GET'||url.pathname.startsWith('/api/')||url.pathname==='/remote/config'||url.pathname==='/remote/root.crt')return;
 if(event.request.mode==='navigate'){
  event.respondWith((async()=>{
   try{
    const response=await fetch(event.request);
    if(response.ok){
     const copy=response.clone();const html=await copy.text();
     const assets=[...html.matchAll(/(?:src|href)="(\/remote-assets\/[^"?#]+)"/g)].map(match=>match[1]);
     const cache=await caches.open(CACHE);
     // Replace the offline shell only after every asset of this release is available.
     await cache.addAll([...new Set(assets)]);
     await cache.put('/',new Response(html,{headers:response.headers}));
    }
    return response;
   }catch{return await (await caches.open(CACHE)).match('/')||Response.error();}
  })());return;
 }
 if(url.pathname.startsWith('/remote-assets/')||url.pathname==='/fonts/MedeoSansVariable.woff2'||/^\/remote\/icon-[\w-]+\.png$/.test(url.pathname)){
  event.respondWith((async()=>{
   const cache=await caches.open(CACHE);const saved=await cache.match(event.request);if(saved)return saved;
   const response=await fetch(event.request);if(response.ok)await cache.put(event.request,response.clone());return response;
  })());
 }
});
