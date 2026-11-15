// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {SellyBoardPanel} from './SellyBoardPanel';
import {invoke} from './ui';
vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),nativeAvailable:true,invoke:vi.fn()}));
afterEach(()=>{cleanup();vi.resetAllMocks();});
const props={day:'2026-11-05',start:'2026-11-01',end:'2026-12-01'};
it('keeps credentials in native configuration and loads no task data while disconnected',async()=>{
 vi.mocked(invoke).mockResolvedValue({configured:false,pendingCreate:false});
 await act(async()=>render(<SellyBoardPanel {...props}/>));
 expect(screen.queryByLabelText(/Пароль|API-ключ/)).toBeNull();
 expect(invoke).toHaveBeenCalledTimes(1);
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Подключить SellyBoard'})));
 expect(invoke).toHaveBeenCalledWith('selly.configure');
});
it('uses string IDs and opens a recurring task without offering implicit series edits',async()=>{
 vi.mocked(invoke).mockImplementation(async action=>{
  if(action==='selly.info')return {configured:true,pendingCreate:false,timezone:'Europe/Moscow'} as never;
  if(action==='selly.events')return {events:[{id:'9223372036854775807',title:'Повтор',start:'2026-11-05',end:'2026-11-06',all_day:true,status:'todo',occurrence:'2026-11-05'}],unscheduled:[]} as never;
  return {id:'9223372036854775807',title:'Повтор',notes:'Сохранить',status:'todo',recurrence:{frequency:'daily'},can_write:true} as never;
 });
 await act(async()=>render(<SellyBoardPanel {...props}/>));
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:/Повтор/})));
 expect(invoke).toHaveBeenCalledWith('selly.detail',{id:'9223372036854775807',occurrence:'2026-11-05'});
 expect(screen.queryByRole('button',{name:'Сохранить статус'})).toBeNull();
 expect(screen.getByText(/Повторяющуюся задачу/)).toBeTruthy();
});
it('retains an unsent title when switching the selected calendar date',async()=>{
 vi.mocked(invoke).mockImplementation(async action=>(action==='selly.info'?{configured:true,pendingCreate:false,canWrite:true,timezone:'Europe/Moscow'}:{events:[],unscheduled:[]}) as never);
 const view=await act(async()=>render(<SellyBoardPanel {...props}/>));
 fireEvent.change(screen.getByLabelText('Новая личная задача'),{target:{value:'Не потерять'}});
 view.rerender(<SellyBoardPanel {...props} day="2026-11-06"/>);
 expect((screen.getByLabelText('Новая личная задача') as HTMLInputElement).value).toBe('Не потерять');
});
it('keeps a connection failure visible instead of clearing it with another info load',async()=>{
 vi.mocked(invoke).mockImplementation(async action=>{
  if(action==='selly.configure')throw new Error('Укажите HTTPS-адрес SellyBoard');
  return {configured:false,pendingCreate:false} as never;
 });
 await act(async()=>render(<SellyBoardPanel {...props}/>));
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Подключить SellyBoard'})));
 expect(screen.getByRole('alert').textContent).toContain('Укажите HTTPS-адрес SellyBoard');
});
