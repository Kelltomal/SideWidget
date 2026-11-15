// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen} from '@testing-library/react';
import {useState} from 'react';
import {afterEach,describe,expect,it,vi} from 'vitest';
import {Calculator} from './LocalTools';
import {Apps,Search} from './WindowsPanels';
import {Diary} from './PersonalPanels';
import {Internet,type InternetState} from './Internet';
import {invoke,useAction} from './ui';
import {App,SettingsPage} from './main';
import {bridge,BridgeError} from './bridge';
import type {Snapshot} from './types';
const hostAvailability=vi.hoisted(()=>({enabled:false}));
vi.mock('./ui',async importOriginal=>({...await importOriginal<typeof import('./ui')>(),invoke:vi.fn(),get nativeAvailable(){return hostAvailability.enabled;}}));
const snapshot:Snapshot={settings:{width:444,height:620,hideDelay:650,openDelay:100,hotkey:'Ctrl+Alt+Space',monitor:0,position:1,largeMode:false,order:[],disabled:[]},calculatorHistory:[{id:'old',expression:'1+1',result:'2',time:'2026-11-05T00:00:00Z'}],colorHistory:[],unitFavorites:[],apps:[],drafts:{diary:''},selly:{configured:false,message:'Не подключено'}};
afterEach(()=>{cleanup();hostAvailability.enabled=false;vi.useRealTimers();vi.restoreAllMocks();vi.resetAllMocks();});
it('switches theme immediately, persists across restart, and keeps unsaved form fields separate',async()=>{
 localStorage.clear();HTMLElement.prototype.scrollTo=vi.fn();render(<App/>);
 fireEvent.click(screen.getByRole('button',{name:'Настройки'}));
 fireEvent.change(screen.getByLabelText('Ширина шапки, DIP'),{target:{value:'510'}});
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Светлая'})));
 expect(document.documentElement.dataset.theme).toBe('light');
 expect(JSON.parse(localStorage.getItem('sidewidget.local.preview')!).settings).toMatchObject({theme:'light',width:444});
 expect((screen.getByLabelText('Ширина шапки, DIP') as HTMLInputElement).value).toBe('510');
 cleanup();render(<App/>);expect(document.documentElement.dataset.theme).toBe('light');
 fireEvent.click(screen.getByRole('button',{name:'Настройки'}));
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Тёмная'})));
 expect(document.documentElement.dataset.theme).toBe('dark');localStorage.clear();
});
it('retains the current theme and reports a failed native save',async()=>{
 hostAvailability.enabled=true;vi.mocked(invoke).mockImplementation(async action=>{if(action==='startup.get')return {enabled:false} as never;throw new Error('Диск недоступен');});
 render(<SettingsPage state={snapshot} setState={vi.fn()}/>);
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Светлая'})));
 expect(screen.getByRole('alert').textContent).toContain('Диск недоступен');
 expect(screen.getByRole('button',{name:'Тёмная'}).getAttribute('aria-pressed')).toBe('true');
});
describe('local calculator flow',()=>{
 it('previews only the selected expression and restores the full result when deselected',()=>{
  const setState=vi.fn();render(<Calculator state={snapshot} setState={setState}/>);
  const input=screen.getByLabelText<HTMLInputElement>('Выражение');
  fireEvent.change(input,{target:{value:'10 + 20 + 30'}});
  input.setSelectionRange(5,12);
  fireEvent.keyUp(input,{key:'ArrowRight',shiftKey:true});
  expect(screen.getByRole('tooltip').textContent).toContain('50');
  expect(screen.getByRole('status').textContent).toBe('60');
  input.setSelectionRange(12,12);
  fireEvent.keyUp(input,{key:'ArrowRight'});
  expect(screen.queryByRole('tooltip')).toBeNull();
  expect(screen.getByRole('status').textContent).toBe('60');
  expect(setState).not.toHaveBeenCalled();
 });
 it('previews the result without writing history and can continue from it',()=>{
  const setState=vi.fn();render(<Calculator state={snapshot} setState={setState}/>);
  const input=screen.getByLabelText<HTMLInputElement>('Выражение');
  fireEvent.change(input,{target:{value:'7*6'}});
  expect(screen.getByRole('status').textContent).toBe('42');
  expect(setState).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button',{name:'Продолжить с результата'}));
  expect(input.value).toBe('42');
  expect(document.activeElement).toBe(input);
 });
 it('inserts keypad symbols at the selection and removes the character before the caret',()=>{
  render(<Calculator state={snapshot} setState={vi.fn()}/>);
  const input=screen.getByLabelText<HTMLInputElement>('Выражение');
  fireEvent.change(input,{target:{value:'2+4'}});
  input.setSelectionRange(2,3);
  expect(screen.queryByRole('group',{name:'Клавиатура калькулятора'})).toBeNull();
  const toggle=screen.getByRole('button',{name:'Показать клавиатуру'});
  expect(toggle.textContent).toBe('');
  expect(toggle.querySelector('svg')).toBeTruthy();
  fireEvent.click(toggle);
  expect(toggle.getAttribute('aria-expanded')).toBe('true');
  fireEvent.click(screen.getByRole('button',{name:'3'}));
  expect(input.value).toBe('2+3');
  fireEvent.click(screen.getByRole('button',{name:'Удалить символ'}));
  expect(input.value).toBe('2+');
  fireEvent.click(screen.getByRole('button',{name:'Скрыть клавиатуру'}));
  expect(screen.queryByRole('group',{name:'Клавиатура калькулятора'})).toBeNull();
  expect(input.value).toBe('2+');
 });
 it('retains calculation history and requires a separate clear confirmation',async()=>{
  const setState=vi.fn();render(<Calculator state={snapshot} setState={setState}/>);
  fireEvent.change(screen.getByLabelText('Выражение'),{target:{value:'(12,5 + 7,5) * 3'}});
  await act(async()=>{fireEvent.click(screen.getByRole('button',{name:'Посчитать'}));});
  expect(setState).toHaveBeenCalledWith(expect.objectContaining({calculatorHistory:[expect.objectContaining({expression:'(12,5 + 7,5) * 3',result:'60'}),snapshot.calculatorHistory[0]]}));
  setState.mockClear();fireEvent.click(screen.getByRole('button',{name:'Очистить историю'}));expect(setState).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button',{name:'Отмена'}));expect(setState).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button',{name:'Очистить историю'}));
  await act(async()=>{fireEvent.click(screen.getByRole('button',{name:'Удалить историю'}));});
  expect(setState).toHaveBeenCalledWith(expect.objectContaining({calculatorHistory:[]}));
 });
 it('keeps earlier history when an invalid expression is rejected',async()=>{
  const setState=vi.fn();render(<Calculator state={snapshot} setState={setState}/>);
  fireEvent.change(screen.getByLabelText('Выражение'),{target:{value:'1/0'}});
  await act(async()=>{fireEvent.click(screen.getByRole('button',{name:'Посчитать'}));});
  expect(screen.getByRole('alert').textContent).toMatch(/ноль/);expect(setState).not.toHaveBeenCalled();expect(screen.getByText('1+1')).toBeTruthy();
 });
});
it('ignores stale Everything results after a later query has completed',async()=>{
 hostAvailability.enabled=true;
 vi.useFakeTimers();let resolveOld:(value:unknown)=>void=()=>{};let resolveNew:(value:unknown)=>void=()=>{};
 vi.mocked(invoke).mockImplementation((action,params)=>new Promise(resolve=>{if((params as {text:string}).text==='old')resolveOld=resolve;else resolveNew=resolve;}));
 render(<Search expanded/>);const input=screen.getByRole('combobox',{name:'Поиск инструментов, приложений и файлов'});
 fireEvent.change(input,{target:{value:'old'}});await act(async()=>{vi.advanceTimersByTime(300);});
 fireEvent.change(input,{target:{value:'new'}});await act(async()=>{vi.advanceTimersByTime(300);});
 await act(async()=>resolveNew({available:true,results:[{name:'new.txt',path:'C:\\new.txt',isDirectory:false}]}));
 expect(screen.getByText('new.txt')).toBeTruthy();
 await act(async()=>resolveOld({available:true,results:[{name:'old.txt',path:'C:\\old.txt',isDirectory:false}]}));
 expect(screen.queryByText('old.txt')).toBeNull();expect(screen.getByText('new.txt')).toBeTruthy();
});
it('previews local Markdown without executing HTML or loading external images',()=>{
 localStorage.removeItem('sidewidget.diary.recovery');
 const text='# День\n**важно**\n- [x] Готово\n<script>alert(1)</script>\n![private](https://example.com/tracking.png)';
 const view=render(<Diary state={{...snapshot,drafts:{diary:text}}} setState={vi.fn()}/>);
 fireEvent.click(screen.getByRole('button',{name:'Предпросмотр'}));
 expect(screen.getByRole('heading',{name:'День'})).toBeTruthy();
 expect(view.container.querySelector('strong')?.textContent).toBe('важно');
 expect((screen.getByRole('checkbox',{name:'Готово'}) as HTMLInputElement).checked).toBe(true);
 expect(view.container.querySelector('script')).toBeNull();expect(view.container.querySelector('img')).toBeNull();
 expect(screen.getByText('<script>alert(1)</script>')).toBeTruthy();
});
it('opens only explicit HTTP links in the diary through the native bridge',async()=>{
 localStorage.removeItem('sidewidget.diary.recovery');vi.mocked(invoke).mockResolvedValue({opened:true});
 const text='[Документы](https://example.com/docs)\n[Опасная ссылка](javascript:alert)';
 const view=render(<Diary state={{...snapshot,drafts:{diary:text}}} setState={vi.fn()}/>);
 fireEvent.click(screen.getByRole('button',{name:'Предпросмотр'}));
 expect(view.container.querySelectorAll('a')).toHaveLength(1);expect(invoke).not.toHaveBeenCalled();
 await act(async()=>fireEvent.click(screen.getByRole('link',{name:'Документы'})));
 expect(invoke).toHaveBeenCalledWith('links.open',{url:'https://example.com/docs'});
});
it('checks internet only after an explicit action and distinguishes HTTPS from ICMP',async()=>{
 vi.useFakeTimers();localStorage.removeItem('sidewidget.internet.target');
 const initial:InternetState={available:true,connectivity:'internet',source:'Windows',target:'www.cloudflare.com',lastCheck:null,cooldownSeconds:5};
 vi.mocked(invoke).mockImplementation(async action=>action==='internet.get'?initial:{...initial,lastCheck:{method:'https',target:'example.com',checkedAt:'2026-11-05T10:00:00Z',success:true,latencyMs:92,httpStatus:200,status:'ok',message:'Ответ получен'}} as never);
 await act(async()=>render(<Internet expanded/>));expect(invoke).toHaveBeenCalledTimes(1);expect(invoke).toHaveBeenCalledWith('internet.get');
 fireEvent.change(screen.getByLabelText('Домен или IP'),{target:{value:'example.com'}});expect(invoke).toHaveBeenCalledTimes(1);
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'HTTPS-запрос'})));
 expect(invoke).toHaveBeenCalledWith('internet.check',{method:'https',target:'example.com'});
 expect(screen.getByText('Время HTTPS-запроса')).toBeTruthy();expect(screen.getByText('Windows видит доступ к интернету')).toBeTruthy();
 expect((screen.getByRole('button',{name:'ICMP-пинг'}) as HTMLButtonElement).disabled).toBe(true);
 await act(async()=>vi.advanceTimersByTime(5000));expect((screen.getByRole('button',{name:'ICMP-пинг'}) as HTMLButtonElement).disabled).toBe(false);
});
it('shows native shell failures and lets the user dismiss them',async()=>{
 let receive:(event:any)=>void=()=>{};
 vi.spyOn(bridge,'subscribe').mockImplementation(listener=>{receive=listener;return()=>{};});
 render(<App/>);
 await act(async()=>receive({type:'notice',data:{message:'Не удалось сохранить размер панели'}}));
 expect(screen.getByRole('alert').textContent).toContain('Не удалось сохранить размер панели');
 fireEvent.click(screen.getByRole('button',{name:'Закрыть сообщение'}));expect(screen.queryByRole('alert')).toBeNull();
});
it('treats explicit picker cancellation as normal while preserving genuine errors',async()=>{
 function Actions(){const {error,run}=useAction();return <><button onClick={()=>void run(()=>Promise.reject(new BridgeError('cancelled','Выбор цвета отменён')))}>Отменить выбор</button><button onClick={()=>void run(()=>Promise.reject(new BridgeError('permission','Нет доступа к экрану')))}>Ошибка выбора</button>{error&&<p role="alert">{error}</p>}</>;}
 render(<Actions/>);await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Отменить выбор'})));expect(screen.queryByRole('alert')).toBeNull();
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Ошибка выбора'})));expect(screen.getByRole('alert').textContent).toBe('Нет доступа к экрану');
});
it('updates an existing application icon using its saved shortcut id',async()=>{
 const current={...snapshot,apps:[{id:'shortcut-1',name:'Блокнот',path:'C:\\Windows\\notepad.exe'}]};
 const updated={...current,apps:[{...current.apps[0],icon:'data:image/png;base64,test'}]};
 const setState=vi.fn();vi.mocked(invoke).mockResolvedValue(updated);
 render(<Apps state={current} setState={setState}/>);
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Значок'})));
 expect(invoke).toHaveBeenCalledWith('apps.icon',{id:'shortcut-1'});expect(setState).toHaveBeenCalledWith(updated);
});
it('reflects external panel geometry changes without losing other unsaved settings',()=>{
 const view=render(<SettingsPage state={{...snapshot,displays:[{id:'a',name:'Экран 1',primary:true},{id:'b',name:'Экран 2',primary:false},{id:'c',name:'Экран 3',primary:false}]}} setState={vi.fn()}/>);
 fireEvent.change(screen.getByLabelText('Задержка скрытия, мс'),{target:{value:'1200'}});
 fireEvent.change(screen.getByLabelText('Горячая клавиша'),{target:{value:'Ctrl+Shift+F12'}});
 const resized={...snapshot,displays:[{id:'a',name:'Экран 1',primary:true},{id:'b',name:'Экран 2',primary:false},{id:'c',name:'Экран 3',primary:false}],settings:{...snapshot.settings,width:480,height:540,monitor:2,position:0.4}};
 view.rerender(<SettingsPage state={resized} setState={vi.fn()}/>);
 expect((screen.getByLabelText('Ширина шапки, DIP') as HTMLInputElement).value).toBe('480');
 expect((screen.getByLabelText('Высота, DIP') as HTMLInputElement).value).toBe('540');
 expect((screen.getByRole('button',{name:'Убрать: Экран 3'}) as HTMLButtonElement).disabled).toBe(true);
 expect((screen.getByLabelText('Положение: Экран 3') as HTMLSelectElement).value).toBe('0.4');
 expect((screen.getByLabelText('Задержка скрытия, мс') as HTMLInputElement).value).toBe('1200');
 expect((screen.getByLabelText('Горячая клавиша') as HTMLInputElement).value).toBe('Ctrl+Shift+F12');
});
it('shows window-pick undo only when the returned native snapshot permits it',async()=>{
 hostAvailability.enabled=true;
 const cancelled={...snapshot,launcherUndoAvailable:false};const changed={...snapshot,launcherUndoAvailable:true,apps:[{id:'picked',name:'Блокнот',path:'C:\\Windows\\notepad.exe'}]};
 vi.mocked(invoke).mockResolvedValueOnce(cancelled).mockResolvedValueOnce(changed).mockResolvedValueOnce(cancelled);
 function Launcher(){const [state,setState]=useState<Snapshot>(snapshot);return <Apps state={state} setState={setState}/>;}
 render(<Launcher/>);expect(screen.queryByRole('button',{name:'Отменить выбор окна'})).toBeNull();
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Выбрать окно'})));
 expect(screen.queryByRole('button',{name:'Отменить выбор окна'})).toBeNull();
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Выбрать окно'})));
 expect(screen.getByRole('button',{name:'Отменить выбор окна'})).toBeTruthy();
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Отменить выбор окна'})));
 expect(invoke).toHaveBeenLastCalledWith('apps.undo',{});expect(screen.queryByRole('button',{name:'Отменить выбор окна'})).toBeNull();
});
