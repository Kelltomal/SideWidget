// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {TimerPanel,useFocusTimer} from './TimerPanel';
import {invoke} from './ui';
import {initialTimer,type TimerState} from './timer';
const events=vi.hoisted(()=>({receive:(_event:any)=>{}}));
vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),invoke:vi.fn(),nativeAvailable:true}));
vi.mock('./bridge',()=>({bridge:{subscribe:vi.fn(callback=>{events.receive=callback;return()=>{};})}}));
function Harness(){const timer=useFocusTimer();return <TimerPanel timer={timer}/>;}
afterEach(()=>{cleanup();vi.resetAllMocks();});
it('ignores an initial snapshot that arrives after a newer native event',async()=>{
 let finish:(value:any)=>void=()=>{};
 vi.mocked(invoke).mockImplementation(()=>new Promise(resolve=>{finish=resolve;}) as never);
 await act(async()=>render(<Harness/>));
 const running={...initialTimer,status:'running',remainingSeconds:300,deadline:new Date(Date.now()+300000).toISOString()};
 act(()=>events.receive({type:'timer.changed',data:[{id:'newer',state:running}]}));
 await act(async()=>finish([]));
 expect(screen.getByRole('button',{name:'Пауза'})).toBeTruthy();
});
it('submits native timer commands and retains live state when command fails',async()=>{
 const running:TimerState={...initialTimer,status:'running',remainingSeconds:300,deadline:new Date(Date.now()+300000).toISOString()};
 vi.mocked(invoke).mockImplementation(async action=>action==='timer.get'?[]:[{id:'first',state:running}] as never);
 await act(async()=>render(<Harness/>));
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Запустить таймер'})));
 expect(invoke).toHaveBeenLastCalledWith('timer.start',{mode:'timer',seconds:300,alarmTime:'09:00'});
 vi.mocked(invoke).mockRejectedValueOnce(new Error('Нет места на диске'));
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Пауза'})));
 expect(screen.getByRole('alert').textContent).toContain('Нет места');
 expect(screen.getByRole('button',{name:'Пауза'})).toBeTruthy();
});
it('receives native completion and dismiss only prepares the next stage',async()=>{
 vi.mocked(invoke).mockResolvedValue([]);
 await act(async()=>render(<Harness/>));
 const ringing:TimerState={...initialTimer,mode:'pomodoro',status:'ringing',remainingSeconds:0};
 act(()=>events.receive({type:'timer.changed',data:[{id:'pomodoro',state:ringing}]}));
 vi.mocked(invoke).mockResolvedValueOnce([{id:'pomodoro',state:{...ringing,status:'ready',phase:'break',cycle:1,remainingSeconds:300}}]);
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:/Остановить сигнал/})));
 expect(invoke).toHaveBeenLastCalledWith('timer.dismiss',{id:'pomodoro'});
 expect(screen.getByRole('button',{name:'Начать перерыв'})).toBeTruthy();
 expect(invoke).not.toHaveBeenCalledWith('timer.resume',expect.anything());
});
