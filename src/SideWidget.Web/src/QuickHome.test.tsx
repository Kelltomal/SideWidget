// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen,within} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {App} from './main';
import {bridge} from './bridge';
import {invoke} from './ui';
import type {Snapshot} from './types';

vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),nativeAvailable:true,invoke:vi.fn()}));
const state:Snapshot={settings:{width:444,height:620,hideDelay:650,openDelay:100,hotkey:'Ctrl+Alt+Space',monitor:0,position:1,largeMode:false,order:['search','music','tools'],disabled:[]},calculatorHistory:[],colorHistory:[],unitFavorites:[],apps:[],drafts:{diary:''}};
afterEach(()=>{cleanup();vi.restoreAllMocks();vi.resetAllMocks();});

function show(snapshot=state,ready:Promise<Snapshot>=Promise.resolve(snapshot)){
 const listeners=new Set<(event:unknown)=>void>();
 vi.spyOn(bridge,'subscribe').mockImplementation(listener=>{listeners.add(listener);return()=>{listeners.delete(listener);};});
 vi.mocked(invoke).mockImplementation(async(action,params)=>{
  if(action==='bridge.ready')return ready as never;
  if(action==='settings.save')return {...snapshot,settings:params} as never;
  if(action==='media.get')return {available:true,sessions:[]} as never;
  if(action==='timer.get')return [] as never;
  if(action==='translation.get')return {configured:false} as never;
  if(action==='translation.run')return {text:'Готовый перевод'} as never;
  if(action==='search.query')return {available:true,results:[{name:'Таймер.txt',path:'C:\\Таймер.txt',isDirectory:false}]} as never;
  return {} as never;
 });
 render(<App/>);
 return (type:string,data:object)=>act(()=>listeners.forEach(listener=>listener({type,data})));
}

it('focuses file search after the hotkey request and acknowledges its id',async()=>{
 const send=show();
 await act(async()=>{});
 fireEvent.click(screen.getAllByRole('button',{name:'Калькулятор'})[0]);
 send('panel',{expanded:false,pinned:false});
 send('search.focus',{id:7});
 expect(document.activeElement).toBe(screen.getByRole('combobox',{name:'Поиск инструментов, приложений и файлов'}));
 expect(invoke).toHaveBeenCalledWith('shell.searchFocused',{id:7});
});

it('returns with Escape to the same query, selected tool and search focus',async()=>{
 const send=show();
 await act(async()=>{});
 send('search.focus',{id:12});
 const input=screen.getByRole('combobox');
 fireEvent.change(input,{target:{value:'а'}});
 fireEvent.keyDown(input,{key:'ArrowDown'});
 const selected=screen.getByRole('row',{selected:true}).textContent;
 fireEvent.keyDown(input,{key:'Enter'});
 expect(screen.getByRole('heading',{level:1})).toBeTruthy();
 fireEvent.keyDown(document.activeElement??window,{key:'Escape'});
 expect(screen.getByRole<HTMLInputElement>('combobox').value).toBe('а');
 expect(screen.getByRole('row',{selected:true}).textContent).toBe(selected);
 expect(document.activeElement).toBe(screen.getByRole('combobox'));
});

it('retains translation, calculator expression and QR text across tool switches',async()=>{
 show();
 await act(async()=>{});
 const open=(name:string)=>fireEvent.click(within(screen.getByRole('group',{name:'Закреплённые инструменты'})).getByRole('button',{name}));
 const back=()=>fireEvent.click(screen.getByRole('button',{name:'Вернуться на главную'}));
 open('Перевод');
 fireEvent.change(screen.getByLabelText('Исходный текст'),{target:{value:'Hello'}});
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Перевести'})));
 back();open('Калькулятор');
 fireEvent.change(screen.getByLabelText('Выражение'),{target:{value:'13*4'}});
 back();open('QR-код');
 fireEvent.change(screen.getByLabelText('Текст или ссылка'),{target:{value:'https://example.com'}});
 back();open('Перевод');
 expect(screen.getByLabelText<HTMLTextAreaElement>('Исходный текст').value).toBe('Hello');
 expect(screen.getByLabelText<HTMLTextAreaElement>('Результат').value).toBe('Готовый перевод');
 back();open('Калькулятор');
 expect(screen.getByLabelText<HTMLInputElement>('Выражение').value).toBe('13*4');
 back();open('QR-код');
 expect(screen.getByLabelText<HTMLTextAreaElement>('Текст или ссылка').value).toBe('https://example.com');
});

it('opens a matching tool from the hotkey before Everything responds',async()=>{
 const send=show();
 await act(async()=>{});
 send('search.focus',{id:9});
 const input=document.activeElement!;
 fireEvent.change(input,{target:{value:'тайм'}});
 fireEvent.keyDown(input,{key:'Enter'});
 expect(screen.getByRole('heading',{name:'Таймер',level:1})).toBeTruthy();
});

it('moves from a tool to a pinned app and then to an Everything file',async()=>{
 vi.useFakeTimers();
 try {
  const send=show({...state,apps:[{id:'timer-app',name:'Таймер Windows',path:'timer.exe'}]});
  await act(async()=>{});
  send('search.focus',{id:10});
  const input=document.activeElement!;
  fireEvent.change(input,{target:{value:'Таймер'}});
  fireEvent.keyDown(input,{key:'ArrowDown'});
  await act(async()=>vi.advanceTimersByTime(250));
  await act(async()=>fireEvent.keyDown(input,{key:'Enter'}));
  expect(invoke).toHaveBeenCalledWith('apps.launch',{id:'timer-app'});
  fireEvent.keyDown(input,{key:'ArrowDown'});
  await act(async()=>fireEvent.keyDown(input,{key:'Enter'}));
  expect(invoke).toHaveBeenCalledWith('search.open',{path:'C:\\Таймер.txt',reveal:false});
 } finally {vi.useRealTimers();}
});

it('waits for saved settings before focusing and respects disabled search',async()=>{
 let resolve!:(snapshot:Snapshot)=>void;
 const send=show(state,new Promise<Snapshot>(done=>{resolve=done;}));
 send('search.focus',{id:8});
 expect(invoke).not.toHaveBeenCalledWith('shell.searchFocused',{id:8});
 await act(async()=>resolve({...state,settings:{...state.settings,disabled:['search']}}));
 expect(screen.queryByRole('combobox',{name:'Поиск инструментов, приложений и файлов'})).toBeNull();
 expect(invoke).not.toHaveBeenCalledWith('shell.searchFocused',{id:8});
});

it('places default shortcuts above music on saved layouts and honors hidden tools',async()=>{
 show({...state,settings:{...state.settings,disabled:['translation']}});
 await act(async()=>{});
 const shortcuts=screen.getByRole('group',{name:'Закреплённые инструменты'});
 expect(within(shortcuts).getAllByRole('button').map(button=>button.textContent)).toEqual(['Калькулятор','QR-код']);
 expect(shortcuts.compareDocumentPosition(screen.getByRole('region',{name:'Музыка'})) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
});

it('saves selected shortcuts and opens one from the main screen',async()=>{
 show({...state,settings:{...state.settings,pinnedTools:[]}});
 await act(async()=>{});
 fireEvent.click(screen.getByRole('button',{name:'Настройки'}));
 fireEvent.click(screen.getByRole('checkbox',{name:'Закрепить Таймер'}));
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Сохранить настройки'})));
 expect(invoke).toHaveBeenCalledWith('settings.save',expect.objectContaining({pinnedTools:['timer']}));
 fireEvent.click(screen.getByRole('button',{name:'Вернуться на главную'}));
 fireEvent.click(within(screen.getByRole('group',{name:'Закреплённые инструменты'})).getByRole('button',{name:'Таймер'}));
 expect(screen.getByRole('heading',{name:'Таймер',level:1})).toBeTruthy();
});
