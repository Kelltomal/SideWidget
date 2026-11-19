// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {CodexPanel,type CodexSnapshot} from './CodexPanel';
import {invoke} from './ui';
const host=vi.hoisted(()=>({native:true}));
vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),invoke:vi.fn(),get nativeAvailable(){return host.native;}}));
const snapshot=():CodexSnapshot=>({available:true,checkedAt:'2026-11-18T12:00:00Z',threads:[{id:'a',title:'Release build',status:'running',turnId:'turn',updatedAt:null,hasQueuedInput:false},{id:'b',title:'Needs reply',status:'interrupted',turnId:'turn2',updatedAt:null,hasQueuedInput:false}],limits:{available:true,checkedAt:'2026-11-18T12:00:00Z',buckets:[{id:'codex',primary:{usedPercent:25,windowMinutes:300,resetsAt:'2026-11-18T16:00:00Z'}}]},shutdown:null,limitUsedThisBoot:false});
afterEach(()=>{cleanup();vi.useRealTimers();vi.resetAllMocks();host.native=true;});
async function show(value=snapshot()){vi.mocked(invoke).mockResolvedValue(value);await act(async()=>{render(<CodexPanel/>);});}
it('keeps five-hour quota visible when Codex only supplies a weekly window',async()=>{
 const value=snapshot();value.limits.buckets=[{id:'codex',primary:{usedPercent:86,windowMinutes:10080,resetsAt:null}}];await show(value);
 expect(screen.getByText('5 часов')).toBeTruthy();expect(screen.getByText('Неделя')).toBeTruthy();
 expect(screen.getByText('Codex не передал данные')).toBeTruthy();expect(screen.getByText('Осталось 14%')).toBeTruthy();expect(screen.queryByText('Осталось 100%')).toBeNull();
});
it('shows both quotas by duration even when API slots are reversed',async()=>{
 const value=snapshot();value.limits.buckets=[{id:'codex',primary:{usedPercent:86,windowMinutes:10080,resetsAt:null},secondary:{usedPercent:12,windowMinutes:300,resetsAt:null}}];await show(value);
 const rows=document.querySelectorAll('.codex-limit-window');expect(rows[0].textContent).toContain('5 часов');expect(rows[0].textContent).toContain('Осталось 88%');expect(rows[1].textContent).toContain('Неделя');expect(rows[1].textContent).toContain('Осталось 14%');expect(screen.queryByText('Codex не передал данные')).toBeNull();
});
it('shows actual remaining quota, source timestamp and distinct non-success states',async()=>{
 await show();expect(screen.getByText('Осталось 75%')).toBeTruthy();expect(screen.getByText('5 часов')).toBeTruthy();expect(screen.getByText('Прерван')).toBeTruthy();expect(screen.getByText(/Данные лимитов:/)).toBeTruthy();
 expect((screen.getByRole('checkbox',{name:/Needs reply/}) as HTMLInputElement).disabled).toBe(true);
});
it('arms selected IDs and asks backend to capture current active set for all',async()=>{
 await show();fireEvent.click(screen.getByRole('checkbox',{name:/Release build/}));await act(async()=>fireEvent.click(screen.getByRole('button',{name:'После выбранных чатов'})));
 expect(invoke).toHaveBeenCalledWith('codex.arm',{mode:'selected',ids:['a']});await act(async()=>fireEvent.click(screen.getByRole('button',{name:'После всех текущих активных'})));expect(invoke).toHaveBeenCalledWith('codex.arm',{mode:'all'});
});
it('never treats unavailable limits as zero and prevents rearming consumed boot',async()=>{
 const value=snapshot();value.limitUsedThisBoot=true;value.limits={available:false,checkedAt:value.checkedAt,buckets:[]};await show(value);
 expect(screen.getByText('Лимиты недоступны')).toBeTruthy();expect(screen.queryByText('Осталось 0%')).toBeNull();expect((screen.getByRole('checkbox',{name:'Выключить ПК при исчерпании лимита'}) as HTMLInputElement).disabled).toBe(true);
 expect(screen.getByText('Уже использовано до следующей перезагрузки ПК.')).toBeTruthy();
});
it('arms quota switch and shows cancellable countdown after consumption',async()=>{
 await show();const next={...snapshot(),limitUsedThisBoot:true,shutdown:{mode:'limit' as const,phase:'countdown' as const,ids:[],deadline:new Date(Date.now()+60000).toISOString()}};vi.mocked(invoke).mockResolvedValue(next);
 await act(async()=>fireEvent.click(screen.getByRole('checkbox',{name:'Выключить ПК при исчерпании лимита'})));expect(invoke).toHaveBeenCalledWith('codex.arm',{mode:'limit'});expect(screen.getByText(/Выключение ПК через/)).toBeTruthy();
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Отменить выключение'})));expect(invoke).toHaveBeenCalledWith('codex.cancel');
});
it('ignores poll result started before an action',async()=>{
 vi.useFakeTimers();await show();let resolve!:(value:CodexSnapshot)=>void;vi.mocked(invoke).mockImplementation(action=>action==='codex.get'?new Promise<CodexSnapshot>(done=>{resolve=done;}) as never:Promise.resolve({...snapshot(),shutdown:{mode:'chats',phase:'waiting',ids:['a'],deadline:null}}) as never);
 await act(async()=>{vi.advanceTimersByTime(2000);});await act(async()=>fireEvent.click(screen.getByRole('button',{name:'После всех текущих активных'})));await act(async()=>resolve(snapshot()));expect(screen.getByRole('button',{name:'Отменить выключение'})).toBeTruthy();
});
it('preview never calls native commands',async()=>{
 host.native=false;await act(async()=>{render(<CodexPanel/>);});expect(screen.getByText('Мониторинг доступен в SideWidget для Windows.')).toBeTruthy();expect(invoke).not.toHaveBeenCalled();
});
it('keeps queued input selectable and manual refresh distinct from cached polling',async()=>{
 const value=snapshot();value.threads[1].hasQueuedInput=true;await show(value);expect((screen.getByRole('checkbox',{name:/Needs reply/}) as HTMLInputElement).disabled).toBe(false);
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Обновить'})));expect(invoke).toHaveBeenCalledWith('codex.refresh');
});
it('retains observation after failed cancellation and shows recoverable error',async()=>{
 const value=snapshot();value.shutdown={mode:'chats',phase:'waiting',ids:['a'],deadline:null};await show(value);vi.mocked(invoke).mockRejectedValue(new Error('Связь прервана. Повторите попытку.'));
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Отменить выключение'})));expect(screen.getByRole('alert').textContent).toContain('Связь прервана');expect(screen.getByRole('button',{name:'Отменить выключение'})).toBeTruthy();
});
