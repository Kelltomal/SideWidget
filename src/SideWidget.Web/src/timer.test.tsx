// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {TimerPanel,useFocusTimer} from './TimerPanel';
import {advanceTimer,initialTimer,remaining,startTimer} from './timer';
function Harness(){const timer=useFocusTimer();return <TimerPanel timer={timer}/>;}
it('keeps two timers and two alarms independent across pause, deletion and reload',async()=>{
 render(<Harness/>);
 for(let i=0;i<2;i++)await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Запустить таймер'})));
 expect(screen.getAllByRole('button',{name:'Пауза'})).toHaveLength(2);
 await act(async()=>fireEvent.click(screen.getAllByRole('button',{name:'Пауза'})[0]));
 expect(screen.getAllByRole('button',{name:'Пауза'})).toHaveLength(1);
 fireEvent.click(screen.getByRole('button',{name:'Будильник'}));
 for(let i=0;i<2;i++)await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Поставить будильник'})));
 expect(screen.getAllByRole('region',{name:/Текущий отсчёт/})).toHaveLength(4);
 cleanup();render(<Harness/>);
 expect(screen.getAllByRole('region',{name:/Текущий отсчёт/})).toHaveLength(4);
 await act(async()=>fireEvent.click(screen.getAllByRole('button',{name:'Удалить'})[0]));
 expect(screen.getAllByRole('region',{name:/Текущий отсчёт/})).toHaveLength(3);
 expect(screen.getAllByRole('button',{name:'Пауза'})).toHaveLength(1);
});
afterEach(()=>{cleanup();vi.restoreAllMocks();localStorage.clear();vi.useRealTimers();});
it('retains simultaneous signals until each one is dismissed',async()=>{
 vi.useFakeTimers();render(<Harness/>);
 for(let i=0;i<2;i++)await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Запустить таймер'})));
 await act(async()=>vi.advanceTimersByTime(300000));
 expect(screen.getAllByRole('button',{name:/Остановить сигнал/})).toHaveLength(2);
 await act(async()=>fireEvent.click(screen.getAllByRole('button',{name:/Остановить сигнал/})[0]));
 expect(screen.getAllByRole('button',{name:/Остановить сигнал/})).toHaveLength(1);
 cleanup();render(<Harness/>);
 expect(screen.getAllByRole('button',{name:/Остановить сигнал/})).toHaveLength(1);
});
it('retains the running collection when storage rejects a new timer',async()=>{
 render(<Harness/>);
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Запустить таймер'})));
 vi.spyOn(Storage.prototype,'setItem').mockImplementation(()=>{throw new Error('Нет места');});
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Запустить таймер'})));
 expect(screen.getAllByRole('button',{name:'Пауза'})).toHaveLength(1);
 expect(screen.getByRole('alert').textContent).toContain('Нет места');
});
it('validates countdown and catches expiry after suspended callbacks',()=>{
 expect(()=>startTimer(initialTimer,{mode:'timer',seconds:0},0)).toThrow();
 const timer=startTimer(initialTimer,{mode:'timer',seconds:60},1000);
 expect(remaining(timer,11000)).toBe(50);
 expect(advanceTimer(timer,'tick',90000).status).toBe('ringing');
});
it('dismiss prepares Pomodoro break without starting it',()=>{
 let timer=startTimer(initialTimer,{mode:'pomodoro'},1000);
 timer=advanceTimer(timer,'tick',1501000);timer=advanceTimer(timer,'dismiss',1502000);
 expect(timer.status).toBe('ready');expect(timer.phase).toBe('break');expect(timer.remainingSeconds).toBe(300);expect(timer.deadline).toBeNull();
});
it('retains running timer while switching forms and provides pause/resume/reset',async()=>{
 render(<Harness/>);
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Запустить таймер'})));
 expect(screen.getByRole('button',{name:'Пауза'})).toBeTruthy();
 fireEvent.click(screen.getByRole('button',{name:'Будильник'}));
 expect(screen.getByRole('button',{name:'Пауза'})).toBeTruthy();
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Пауза'})));
 expect(screen.getByText('На паузе')).toBeTruthy();
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Продолжить'})));
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Удалить'})));
 expect(screen.queryByRole('button',{name:'Пауза'})).toBeNull();
});
it('signals silently and requires separate start after dismissal',async()=>{
 vi.useFakeTimers();vi.setSystemTime(new Date('2026-11-05T10:00:00Z'));render(<Harness/>);
 fireEvent.click(screen.getByRole('button',{name:'Pomodoro'}));
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Начать Pomodoro'})));
 await act(async()=>vi.advanceTimersByTime(25*60000));
 expect(screen.getByRole('button',{name:/Остановить сигнал/})).toBeTruthy();
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:/Остановить сигнал/})));
 expect(screen.getByRole('button',{name:'Начать перерыв'})).toBeTruthy();
 expect(screen.queryByRole('button',{name:'Пауза'})).toBeNull();
});
it('restores an overdue persisted session and rejects invalid duration',async()=>{
 const timer=startTimer(initialTimer,{mode:'timer',seconds:1},Date.now()-2000);
 localStorage.setItem('sidewidget.focusTimer.v1',JSON.stringify(timer));render(<Harness/>);
 expect(screen.getByRole('button',{name:/Остановить сигнал/})).toBeTruthy();
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:/Остановить сигнал/})));
 fireEvent.change(screen.getByLabelText('Минуты'),{target:{value:'0'}});
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Запустить таймер'})));
 expect(screen.getByRole('alert').textContent).toMatch(/1 секунды/);
});

it('ignores invalid hidden Pomodoro fields when starting countdown',async()=>{
 render(<Harness/>);fireEvent.click(screen.getByRole('button',{name:'Pomodoro'}));
 fireEvent.change(screen.getByLabelText('Работа, мин'),{target:{value:''}});
 fireEvent.click(screen.getByRole('button',{name:'Таймер'}));
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Запустить таймер'})));
 expect(screen.queryByRole('alert')).toBeNull();expect(screen.getByRole('button',{name:'Пауза'})).toBeTruthy();
});

