// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen} from '@testing-library/react';
import {afterEach,beforeEach,expect,it,vi} from 'vitest';
import {CalendarPanel,type CalendarEntry} from './CalendarPanel';
import {invoke,localDate} from './ui';
const host=vi.hoisted(()=>({native:true,listener:undefined as undefined|((event:unknown)=>void)}));
vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),invoke:vi.fn(),get nativeAvailable(){return host.native;}}));
vi.mock('./bridge',()=>({bridge:{subscribe:vi.fn((listener:(event:unknown)=>void)=>{host.listener=listener;return()=>{host.listener=undefined;};})}}));
const entry=():CalendarEntry=>({id:'one',title:'Review release',date:localDate(),time:'12:00',endTime:'13:00',allDay:false,notes:'Bring checks',reminderMinutes:15,done:false});
beforeEach(()=>{localStorage.clear();vi.mocked(invoke).mockResolvedValue({events:[]});});
afterEach(()=>{cleanup();vi.resetAllMocks();host.native=true;});
async function show(events:CalendarEntry[]=[]){vi.mocked(invoke).mockResolvedValue({events});await act(async()=>{render(<CalendarPanel/>);});}
async function add(title='Review release'){fireEvent.click(screen.getByRole('button',{name:'Добавить событие'}));fireEvent.change(screen.getByLabelText('Название'),{target:{value:title}});await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Сохранить событие'})));}
it('starts empty without fabricated events and switches month week list',async()=>{
 await show();expect(screen.getByText('На этот день событий нет.')).toBeTruthy();fireEvent.click(screen.getByRole('button',{name:'Неделя'}));expect(screen.getByRole('group',{name:'Выбор даты'}).querySelectorAll('button').length).toBe(7);fireEvent.click(screen.getByRole('button',{name:'Список'}));expect(screen.getByText('В этом месяце событий нет.')).toBeTruthy();
});
it('saves through native bridge and renders returned event',async()=>{
 await show();vi.mocked(invoke).mockResolvedValue({events:[entry()]});await add();expect(invoke).toHaveBeenCalledWith('calendar.save',{event:expect.objectContaining({title:'Review release',date:localDate(),done:false})});expect(screen.getByText('Review release')).toBeTruthy();
});
it('validates same-day time range before saving',async()=>{
 await show();fireEvent.click(screen.getByRole('button',{name:'Добавить событие'}));fireEvent.change(screen.getByLabelText('Название'),{target:{value:'Call'}});fireEvent.change(screen.getByLabelText('Начало'),{target:{value:'16:00'}});fireEvent.change(screen.getByLabelText('Окончание'),{target:{value:'15:00'}});await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Сохранить событие'})));expect(screen.getByRole('alert').textContent).toContain('Окончание должно быть позже начала.');expect(invoke).not.toHaveBeenCalledWith('calendar.save',expect.anything());
});
it('edits notes and all-day reminders, marks done, then deletes',async()=>{
 await show([entry()]);fireEvent.click(screen.getByRole('button',{name:'Редактировать: Review release'}));fireEvent.click(screen.getByLabelText('Весь день'));fireEvent.change(screen.getByLabelText('Заметки'),{target:{value:'New note'}});await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Сохранить событие'})));expect(invoke).toHaveBeenCalledWith('calendar.save',{event:expect.objectContaining({id:'one',allDay:true,time:'',endTime:'',reminderMinutes:null,notes:'New note'})});
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Отметить выполненным: Review release'})));expect(invoke).toHaveBeenCalledWith('calendar.save',{event:expect.objectContaining({id:'one',done:true})});
 fireEvent.click(screen.getByRole('button',{name:'Редактировать: Review release'}));await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Удалить событие'})));expect(invoke).toHaveBeenCalledWith('calendar.delete',{id:'one'});
});
it('persists preview events across mounts without native calls',async()=>{
 host.native=false;await show();await add('Local note');expect(invoke).not.toHaveBeenCalled();expect(JSON.parse(localStorage.getItem('sidewidget.calendar.v1')!)[0].title).toBe('Local note');cleanup();await act(async()=>{render(<CalendarPanel/>);});expect(screen.getByText('Local note')).toBeTruthy();
});
it('preserves editor after storage failure and reports error',async()=>{
 host.native=false;await show();const write=vi.spyOn(Storage.prototype,'setItem').mockImplementation(()=>{throw new Error('QuotaExceededError');});await add('Keep this');expect(screen.getByRole('alert').textContent).toContain('Не удалось сохранить');expect((screen.getByLabelText('Название') as HTMLInputElement).value).toBe('Keep this');write.mockRestore();
});
it('supports arrow-key date selection and receives calendar changes',async()=>{
 await show();const selected=document.querySelector('.calendar-grid button[aria-pressed=true]') as HTMLButtonElement;const previous=selected.getAttribute('data-date');fireEvent.keyDown(selected,{key:'ArrowRight'});expect(document.querySelector('.calendar-grid button[aria-pressed=true]')?.getAttribute('data-date')).not.toBe(previous);
 fireEvent.click(screen.getByRole('button',{name:'Сегодня'}));await act(async()=>host.listener?.({type:'calendar.changed',data:{events:[entry()]}}));expect(screen.getByText('Review release')).toBeTruthy();
});
