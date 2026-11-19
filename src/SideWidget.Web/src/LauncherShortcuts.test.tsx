// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {FavoriteApps} from './WindowsPanels';
import {ApplicationDropZone} from './ApplicationDropZone';
import {bridge,Bridge,type Host} from './bridge';
import {invoke} from './ui';
import type {Snapshot} from './types';
vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),nativeAvailable:true,invoke:vi.fn()}));
const initial:Snapshot={settings:{width:444,height:620,hideDelay:650,openDelay:100,hotkey:'Ctrl+Alt+Space',monitor:0,position:1,largeMode:false,order:[],disabled:[]},apps:['a','b','c'].map(id=>({id,name:id,path:id+'.exe'})),calculatorHistory:[],colorHistory:[],unitFavorites:[],drafts:{diary:''}};
afterEach(()=>{cleanup();vi.restoreAllMocks();vi.resetAllMocks();});
function setup(){vi.mocked(invoke).mockImplementation(async action=>action==='codex.get'?{limits:{available:true,checkedAt:new Date().toISOString(),buckets:[{id:'codex',primary:{usedPercent:86,windowMinutes:10080,resetsAt:null}}]}}:initial);}
it('shows real weekly remaining quota and unknown five-hour ring, and opens Codex',async()=>{
 setup();const open=vi.fn();await act(async()=>{render(<FavoriteApps state={initial} setState={vi.fn()} openApps={vi.fn()} openCodex={open}/>);});
 const button=screen.getByRole('button',{name:/Codex.*14%/});expect(button.getAttribute('aria-label')).toContain('5 часов');expect(button.getAttribute('aria-label')).toContain('Неизвестно');expect(button.querySelector('[data-window="10080"]')?.getAttribute('stroke-dasharray')).toBe('14 100');expect(button.querySelector('[data-window="300"]')?.getAttribute('stroke-dasharray')).toBe('2 4');fireEvent.click(button);expect(open).toHaveBeenCalledOnce();
});
it('saves reordered full app IDs after dragging a shortcut without launching it',async()=>{
 setup();await act(async()=>{render(<FavoriteApps state={initial} setState={vi.fn()} openApps={vi.fn()}/>);});
 const data=new Map<string,string>();const transfer={types:[],effectAllowed:'',dropEffect:'',setData:(key:string,value:string)=>data.set(key,value),getData:(key:string)=>data.get(key)||''};
 fireEvent.dragStart(screen.getByRole('button',{name:'a'}),{dataTransfer:transfer});
 await act(async()=>{fireEvent.drop(screen.getByRole('button',{name:'c'}),{dataTransfer:transfer});});
 expect(invoke).toHaveBeenCalledWith('apps.reorder',{ids:['b','c','a']});expect(invoke).not.toHaveBeenCalledWith('apps.launch',expect.anything());
});
it('passes dropped File objects through the native bridge rather than fake file paths',async()=>{
 const file=new File(['shortcut'],'Example.lnk');const request=vi.spyOn(bridge,'request').mockReturnValue({id:'drop',promise:Promise.resolve(initial)});const changed=vi.fn();
 render(<ApplicationDropZone onAdded={changed}><button>target</button></ApplicationDropZone>);
 await act(async()=>{fireEvent.drop(screen.getByRole('button',{name:'target'}),{dataTransfer:{types:['Files'],files:[file]}});});
 expect(request).toHaveBeenCalledWith('apps.drop',{},[file]);expect(changed).toHaveBeenCalledWith(initial);
});
it('correlates attachment replies and fails clearly on an older file-drop host',async()=>{
 let receive:(event:MessageEvent)=>void=()=>{};let sent:any;let attached:File[]=[];
 const host:Host={postMessage:()=>{throw new Error('wrong transport');},postMessageWithAdditionalObjects:(message,objects)=>{sent=message;attached=objects;},addEventListener:(_,listener)=>{receive=listener;}};
 const file=new File(['shortcut'],'Example.lnk');const native=new Bridge(host);const pending=native.request<Snapshot>('apps.drop',{},[file]);
 expect(attached).toEqual([file]);expect(sent.params).toEqual({});receive({data:{protocolVersion:1,requestId:sent.requestId,status:'result',data:initial}} as MessageEvent);expect(await pending.promise).toEqual(initial);
 await expect(new Bridge({postMessage:vi.fn(),addEventListener:vi.fn()}).request('apps.drop',{},[file]).promise).rejects.toThrow('WebView2');
});
it('hides the optional shortcut and supports keyboard app reordering',async()=>{
 setup();await act(async()=>{render(<FavoriteApps state={{...initial,settings:{...initial.settings,codexShortcut:false}}} setState={vi.fn()} openApps={vi.fn()} openCodex={vi.fn()}/>);});
 expect(screen.queryByRole('button',{name:/Codex/})).toBeNull();await act(async()=>{fireEvent.keyDown(screen.getByRole('button',{name:'b'}),{key:'ArrowLeft',altKey:true});});expect(invoke).toHaveBeenCalledWith('apps.reorder',{ids:['b','a','c']});
});
it('rejects oversized file batches before adding any apps',async()=>{
 const request=vi.spyOn(bridge,'request');render(<ApplicationDropZone onAdded={vi.fn()}><button>target</button></ApplicationDropZone>);
 await act(async()=>{fireEvent.drop(screen.getByRole('button',{name:'target'}),{dataTransfer:{types:['Files'],files:Array.from({length:11},(_,index)=>new File([''],index+'.exe'))}});});expect(request).not.toHaveBeenCalled();expect(screen.getByRole('alert').textContent).toContain('10');
});
