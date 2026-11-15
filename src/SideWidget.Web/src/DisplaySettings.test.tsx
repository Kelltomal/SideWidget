// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {DisplaySettings} from './DisplaySettings';
import type {Settings,Snapshot} from './types';
import {SettingsPage} from './main';
import {Diary} from './PersonalPanels';
const host=vi.hoisted(()=>({enabled:false}));
vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),get nativeAvailable(){return host.enabled;},invoke:vi.fn(async()=>({enabled:false}))}));
const settings:Settings={width:444,height:620,hideDelay:650,openDelay:100,hotkey:'Ctrl+Alt+Space',monitor:0,position:1,largeMode:false,order:[],disabled:[]};
const displays=[{id:'one',name:'Экран 1',primary:true},{id:'two',name:'Экран 2',primary:false}];
const state:Snapshot={settings,displays,calculatorHistory:[],colorHistory:[],unitFavorites:[],apps:[],drafts:{diary:''},selly:{configured:false,message:''}};
afterEach(()=>{cleanup();vi.restoreAllMocks();vi.useRealTimers();localStorage.clear();history.replaceState(null,'',location.pathname);});
it('migrates legacy index and allows adding screens',()=>{
 const change=vi.fn();render(<DisplaySettings settings={{...settings,monitor:1}} displays={displays} onChange={change} onPositionChange={vi.fn()}/>);
 expect((screen.getByRole('button',{name:'Убрать: Экран 2'}) as HTMLButtonElement).disabled).toBe(true);
 fireEvent.click(screen.getByRole('button',{name:/Экран 1/}));expect(change).toHaveBeenCalledWith(['two','one']);
});
it('keeps at least one selected and lets disconnected screens be removed',()=>{
 const change=vi.fn();const view=render(<DisplaySettings settings={{...settings,displayIds:['one']}} displays={displays} onChange={change} onPositionChange={vi.fn()}/>);
 expect((screen.getByRole('button',{name:/Экран 1/}) as HTMLInputElement).disabled).toBe(true);
 view.rerender(<DisplaySettings settings={{...settings,displayIds:['one','gone']}} displays={displays} onChange={change} onPositionChange={vi.fn()}/>);
 fireEvent.click(screen.getByRole('button',{name:/Убрать: Отключённый экран/}));expect(change).toHaveBeenCalledWith(['one']);
});
it('handles an unavailable catalog honestly',()=>{
 render(<DisplaySettings settings={settings} displays={[]} onChange={vi.fn()} onPositionChange={vi.fn()}/>);
 expect(screen.getByText(/Список экранов недоступен/)).toBeTruthy();expect(screen.queryAllByRole('checkbox')).toHaveLength(0);
});
it('saves selected screens with other settings and preserves edits during external updates',async()=>{
 const save=vi.fn();const view=render(<SettingsPage state={state} setState={save}/>);
 fireEvent.change(screen.getByLabelText('Ширина шапки, DIP'),{target:{value:'510'}});
 fireEvent.click(screen.getByRole('button',{name:'Добавить: Экран 2'}));
 view.rerender(<SettingsPage state={{...state,settings:{...settings,width:480,position:0.5,theme:'light'}}} setState={save}/>);
 expect((screen.getByLabelText('Ширина шапки, DIP') as HTMLInputElement).value).toBe('510');
 expect((screen.getByLabelText('Положение: Экран 1') as HTMLSelectElement).value).toBe('0.5');
 fireEvent.change(screen.getByLabelText('Положение: Экран 2'),{target:{value:'0'}});
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Сохранить настройки'})));
 expect(save).toHaveBeenCalledWith(expect.objectContaining({settings:expect.objectContaining({width:510,theme:'light',displayIds:['one','two'],displayPositions:{two:0}})}));
});
it('updates an idle diary from another screen without erasing unsaved text',()=>{
 vi.useFakeTimers();localStorage.clear();const save=vi.fn();
 const view=render(<Diary state={{...state,drafts:{diary:'Исходный'}}} setState={save}/>);
 view.rerender(<Diary state={{...state,drafts:{diary:'С другого экрана'}}} setState={save}/>);
 expect((screen.getByLabelText('Запись') as HTMLTextAreaElement).value).toBe('С другого экрана');
 act(()=>vi.advanceTimersByTime(500));expect(save).not.toHaveBeenCalled();
 fireEvent.change(screen.getByLabelText('Запись'),{target:{value:'Ещё пишу'}});
 view.rerender(<Diary state={{...state,drafts:{diary:'Позднее обновление'}}} setState={save}/>);
 expect((screen.getByLabelText('Запись') as HTMLTextAreaElement).value).toBe('Ещё пишу');
});
it('keeps recovery text per window and migrates the legacy recovery once',()=>{
 vi.useFakeTimers();localStorage.setItem('sidewidget.diary.recovery','Несохранённое из старой версии');
 history.replaceState(null,'','?windowId=one');
 const first=render(<Diary state={state} setState={vi.fn()}/>);
 expect((screen.getByLabelText('Запись') as HTMLTextAreaElement).value).toBe('Несохранённое из старой версии');
 first.unmount();history.replaceState(null,'','?windowId=two');
 render(<Diary state={{...state,drafts:{diary:'Общий сохранённый'}}} setState={vi.fn()}/>);
 expect((screen.getByLabelText('Запись') as HTMLTextAreaElement).value).toBe('Общий сохранённый');
 fireEvent.change(screen.getByLabelText('Запись'),{target:{value:'Второе окно пишет'}});
 expect(localStorage.getItem('sidewidget.diary.recovery.one')).toBe('Несохранённое из старой версии');
 expect(localStorage.getItem('sidewidget.diary.recovery.two')).toBe('Второе окно пишет');
});
