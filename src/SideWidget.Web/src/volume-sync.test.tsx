// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {Audio} from './WindowsPanels';
import {bridge} from './bridge';
import {invoke} from './ui';
vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),invoke:vi.fn(),nativeAvailable:true}));
afterEach(()=>{cleanup();vi.useRealTimers();vi.restoreAllMocks();vi.resetAllMocks();});
const snapshot=(volume:number)=>({available:true,volume,muted:false,devices:[],sessions:[]});
it('follows Windows master-volume notifications without writing volume back',async()=>{
 let notify=(event:unknown)=>{};
 vi.spyOn(bridge,'subscribe').mockImplementation(callback=>{notify=callback;return()=>{};});
 vi.mocked(invoke).mockResolvedValue(snapshot(.2));
 await act(async()=>render(<Audio expanded compact/>));
 await act(async()=>notify({type:'audio.level.changed',data:{volume:.73,muted:false}}));
 expect((screen.getByRole('slider',{name:/Общая громкость/}) as HTMLInputElement).value).toBe('0.73');
 expect(invoke).not.toHaveBeenCalledWith('audio.set',expect.anything());
});
it('resumes external synchronization after a cancelled drag',async()=>{
 vi.useFakeTimers();let volume=.2;
 vi.mocked(invoke).mockImplementation(async()=>snapshot(volume) as never);
 await act(async()=>render(<Audio expanded compact/>));
 const slider=screen.getByRole('slider',{name:/Общая громкость/});
 fireEvent.pointerDown(slider);fireEvent.pointerCancel(slider);
 volume=.8;
 await act(async()=>vi.advanceTimersByTime(4001));
 expect((slider as HTMLInputElement).value).toBe('0.8');
 expect(invoke).not.toHaveBeenCalledWith('audio.set',expect.anything());
});

it('keeps the drag stable but applies a queued Windows value immediately on cancel',async()=>{
 let notify=(event:unknown)=>{};
 vi.spyOn(bridge,'subscribe').mockImplementation(callback=>{notify=callback;return()=>{};});
 vi.mocked(invoke).mockResolvedValue(snapshot(.2));
 await act(async()=>render(<Audio expanded compact/>));
 const slider=screen.getByRole('slider',{name:/Общая громкость/});fireEvent.pointerDown(slider);
 await act(async()=>notify({type:'audio.level.changed',data:{volume:.71,muted:false}}));
 expect((slider as HTMLInputElement).value).toBe('0.2');
 fireEvent.pointerCancel(slider);expect((slider as HTMLInputElement).value).toBe('0.71');
 expect(invoke).not.toHaveBeenCalledWith('audio.set',expect.anything());
});
it('does not let an old poll overwrite a newer Windows notification',async()=>{
 vi.useFakeTimers();let notify=(event:unknown)=>{};let resolvePoll=(value:unknown)=>{};let reads=0;
 vi.spyOn(bridge,'subscribe').mockImplementation(callback=>{notify=callback;return()=>{};});
 vi.mocked(invoke).mockImplementation(async()=>{reads++;return reads===1?snapshot(.2):await new Promise<unknown>(resolve=>{resolvePoll=resolve;}) as never;});
 await act(async()=>render(<Audio expanded compact/>));
 await act(async()=>vi.advanceTimersByTime(4001));
 await act(async()=>notify({type:'audio.level.changed',data:{volume:.73,muted:true}}));
 await act(async()=>resolvePoll(snapshot(.2)));
 expect((screen.getByRole('slider',{name:/Общая громкость/}) as HTMLInputElement).value).toBe('0.73');
 expect(screen.getByRole('button',{name:'Включить общий звук'})).toBeTruthy();
});

it('does not let a delayed audio.set reply overwrite a newer Windows value',async()=>{
 let notify=(event:unknown)=>{},resolveWrite=(value:unknown)=>{};
 vi.spyOn(bridge,'subscribe').mockImplementation(callback=>{notify=callback;return()=>{};});
 vi.mocked(invoke).mockImplementation(async(action)=>action==='audio.set'?await new Promise<unknown>(resolve=>{resolveWrite=resolve;}) as never:snapshot(.2));
 await act(async()=>render(<Audio expanded compact/>));
 const slider=screen.getByRole('slider',{name:/Общая громкость/});
 fireEvent.change(slider,{target:{value:'0.4'}});await act(async()=>fireEvent.pointerUp(slider));
 await act(async()=>notify({type:'audio.level.changed',data:{volume:.73,muted:false}}));
 await act(async()=>resolveWrite(snapshot(.4)));
 expect((slider as HTMLInputElement).value).toBe('0.73');
});
