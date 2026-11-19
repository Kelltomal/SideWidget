// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {Profiler} from 'react';
import {afterEach,expect,it,vi} from 'vitest';
const {invoke}=vi.hoisted(()=>({invoke:vi.fn()}));
vi.mock('./ui',async()=>({...await vi.importActual<typeof import('./ui')>('./ui'),invoke,nativeAvailable:true}));
import {PowerScheduleForm,PowerScheduleStatus} from './PowerSchedule';
import {QuickActions} from './WindowsPanels';
afterEach(()=>{cleanup();vi.useRealTimers();vi.clearAllMocks();});
it('shows delayed power actions in the power menu',()=>{
 render(<QuickActions/>);fireEvent.click(screen.getByRole('button',{name:'Питание'}));
 expect(screen.getByRole('menuitem',{name:'Выключить через…'})).toBeTruthy();
 fireEvent.click(screen.getByRole('menuitem',{name:'Перезагрузить через…'}));
 expect(screen.getByRole('spinbutton',{name:'Часы'})).toBeTruthy();
 expect(screen.getByRole('spinbutton',{name:'Минуты'})).toBeTruthy();
});
it('warns on zero and empty fields without scheduling',()=>{
 render(<PowerScheduleForm action="shutdown" onClose={()=>{}}/>);
 fireEvent.change(screen.getByRole('spinbutton',{name:'Часы'}),{target:{value:'0'}});
 fireEvent.change(screen.getByRole('spinbutton',{name:'Минуты'}),{target:{value:'0'}});
 fireEvent.click(screen.getByRole('button',{name:'Назначить выключение'}));
 expect(screen.getByRole('alert').textContent).toContain('Укажите хотя бы 1 минуту');
 expect(invoke).not.toHaveBeenCalled();
 fireEvent.change(screen.getByRole('spinbutton',{name:'Часы'}),{target:{value:''}});
 fireEvent.change(screen.getByRole('spinbutton',{name:'Минуты'}),{target:{value:''}});
 fireEvent.click(screen.getByRole('button',{name:'Назначить выключение'}));
 expect(invoke).not.toHaveBeenCalled();
});
it.each([[0,30],[2,0],[1,30]])('schedules %i hours and %i minutes',async(hours,minutes)=>{
 invoke.mockResolvedValue({action:'restart',deadline:new Date(Date.now()+60000).toISOString()});
 const close=vi.fn();render(<PowerScheduleForm action="restart" onClose={close}/>);
 fireEvent.change(screen.getByRole('spinbutton',{name:'Часы'}),{target:{value:String(hours)}});
 fireEvent.change(screen.getByRole('spinbutton',{name:'Минуты'}),{target:{value:String(minutes)}});
 fireEvent.click(screen.getByRole('button',{name:'Назначить перезагрузку'}));
 await waitFor(()=>expect(invoke).toHaveBeenCalledWith('power.schedule',{action:'restart',hours,minutes}));
 await waitFor(()=>expect(close).toHaveBeenCalled());
});
it('treats an empty hours field as zero and rejects invalid ranges',async()=>{
 invoke.mockResolvedValue(null);render(<PowerScheduleForm action="shutdown" onClose={()=>{}}/>);
 fireEvent.change(screen.getByRole('spinbutton',{name:'Часы'}),{target:{value:'24'}});
 fireEvent.change(screen.getByRole('spinbutton',{name:'Минуты'}),{target:{value:'1'}});
 fireEvent.click(screen.getByRole('button',{name:'Назначить выключение'}));
 expect(invoke).not.toHaveBeenCalled();expect(screen.getByRole('alert').textContent).toContain('до 24 часов');
 fireEvent.change(screen.getByRole('spinbutton',{name:'Часы'}),{target:{value:''}});
 fireEvent.click(screen.getByRole('button',{name:'Назначить выключение'}));
 await waitFor(()=>expect(invoke).toHaveBeenCalledWith('power.schedule',{action:'shutdown',hours:0,minutes:1}));
});
it('shows countdown and cancels through the native bridge',async()=>{
 invoke.mockImplementation((command:string)=>Promise.resolve(command==='power.get'?{action:'shutdown',deadline:new Date(Date.now()+1800000).toISOString()}:null));
 render(<PowerScheduleStatus/>);
 await screen.findByText('До выключения ПК');
 expect(screen.getByLabelText('Осталось').textContent).toMatch(/(29:59|30:00)/);
 fireEvent.click(screen.getByRole('button',{name:'Отменить выключение'}));
 await waitFor(()=>expect(invoke).toHaveBeenCalledWith('power.cancel'));
 await waitFor(()=>expect(screen.queryByText('До выключения ПК')).toBeNull());
});
it('uses current time on the first committed frame after a delayed schedule response',async()=>{
 vi.useFakeTimers();
 const started=Date.parse('2026-11-19T10:00:00Z');vi.setSystemTime(started);
 let respond!:(schedule:{action:string;deadline:string})=>void;
 invoke.mockImplementation((command:string)=>command==='power.get'?new Promise(resolve=>{respond=resolve;}):Promise.resolve(null));
 const frames:string[]=[];
 render(<Profiler id="power-countdown" onRender={()=>{
  const value=document.querySelector('[aria-label="Осталось"]')?.textContent;
  if(value)frames.push(value);
 }}><PowerScheduleStatus/></Profiler>);
 vi.setSystemTime(started+1500);
 await act(async()=>respond({action:'shutdown',deadline:new Date(Date.now()+1800000).toISOString()}));
 expect(frames[0]).toBe('30:00');
 await act(async()=>{vi.advanceTimersByTime(1000);});
 expect(screen.getByLabelText('Осталось').textContent).toBe('29:59');
});
