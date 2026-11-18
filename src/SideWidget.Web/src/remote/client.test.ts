// @vitest-environment jsdom
import {afterEach,expect,it,vi} from 'vitest';
import {RemoteClient} from './client';

afterEach(()=>{vi.unstubAllGlobals();localStorage.clear();});
it('sends saved key and drops revoked trust',async()=>{
 localStorage.setItem('sidewidget.remote.key','saved-key');
 const fetcher=vi.fn().mockResolvedValue(new Response('',{status:401}));vi.stubGlobal('fetch',fetcher);
 const client=new RemoteClient();
 await expect(client.state()).rejects.toMatchObject({status:401});
 expect(fetcher.mock.calls[0][1].headers['X-SideWidget-Key']).toBe('saved-key');
 expect(client.hasKey()).toBe(false);expect(localStorage.getItem('sidewidget.remote.key')).toBeNull();
});
it('never retries a command after an unknown network outcome',async()=>{
 const fetcher=vi.fn().mockRejectedValue(new TypeError('Failed to fetch'));vi.stubGlobal('fetch',fetcher);
 const client=new RemoteClient();client.remember('key');
 await expect(client.command({command:'next',id:'session'})).rejects.toThrow();
 expect(fetcher).toHaveBeenCalledTimes(1);
 const data=JSON.parse(fetcher.mock.calls[0][1].body);expect(data.requestId).toMatch(/^[\da-f-]{36}$/);
});
it('remembers pairing key for later openings',()=>{
 new RemoteClient().remember('paired-key');expect(new RemoteClient().hasKey()).toBe(true);
});
it('generates command IDs on HTTP where randomUUID is unavailable',async()=>{
 const original=crypto.getRandomValues.bind(crypto);
 vi.stubGlobal('crypto',{getRandomValues:original});
 const fetcher=vi.fn().mockResolvedValue(new Response('{}'));vi.stubGlobal('fetch',fetcher);
 await new RemoteClient().command({command:'next',id:'session'});
 expect(JSON.parse(fetcher.mock.calls[0][1].body).requestId).toMatch(/^[\da-f]{8}-[\da-f]{4}-4[\da-f]{3}-[89ab][\da-f]{3}-[\da-f]{12}$/);
});
it('reuses known artwork without requesting its bytes every poll',async()=>{
 const fetcher=vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({media:{sessions:[{id:'p',artworkKey:'hash',artwork:'data:image/png;base64,dGVzdA=='}]}}))).mockResolvedValueOnce(new Response(JSON.stringify({media:{sessions:[{id:'p',artworkKey:'hash'}]}})));vi.stubGlobal('fetch',fetcher);
 const client=new RemoteClient();await client.state();const second=await client.state();
 expect(fetcher.mock.calls[1][1].headers['X-SideWidget-Artwork']).toBe('hash');expect(second.media.sessions[0].artwork).toBe('data:image/png;base64,dGVzdA==');
});
