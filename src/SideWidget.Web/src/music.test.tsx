// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {Music} from './WindowsPanels';
import {invoke} from './ui';
import type {MediaState} from './types';

vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),invoke:vi.fn(),nativeAvailable:true}));
const media:MediaState={available:true,sessions:[{id:'player',title:'Трек',artist:'Исполнитель',source:'Player',playing:true,positionSeconds:12,durationSeconds:180,canPlay:true,canPause:true,canPrevious:true,canNext:true,canSeek:true}]};
afterEach(()=>{cleanup();vi.resetAllMocks();localStorage.clear();});
async function show(value=media){vi.mocked(invoke).mockImplementation(async action=>action==='media.get'?value:action==='audio.get'?{available:true,volume:.5,muted:false,devices:[],sessions:[]}:undefined as never);await act(async()=>render(<Music expanded openAudio={vi.fn()}/>));}
it('keeps one music layout with timing and no expand or collapse actions',async()=>{
 await show();
 expect(screen.queryByRole('button',{name:/Раскрыть|Свернуть/})).toBeNull();
 expect(screen.getByRole('slider',{name:/Позиция воспроизведения/})).toBeTruthy();
 expect(screen.getByRole('button',{name:'Копировать исполнителя и название трека'})).toBeTruthy();
 expect(Array.from(document.querySelectorAll('.music-controls button')).map(button=>button.getAttribute('aria-label'))).toEqual(['Предыдущая композиция','Пауза','Следующая композиция']);
});
it('copies artist and title directly from the ordinary music card',async()=>{
 await show();
 expect(invoke).not.toHaveBeenCalledWith('clipboard.text',expect.anything());
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Копировать исполнителя и название трека'})));
 expect(invoke).toHaveBeenCalledWith('clipboard.text',{text:'Исполнитель — Трек'});
 expect(screen.getByRole('status').textContent).toBe('Скопировано');
 expect(screen.getByRole('slider',{name:/Позиция воспроизведения/})).toBeTruthy();
});
it('does not copy a source name as the artist and reports clipboard failures',async()=>{
 await show({...media,sessions:[{...media.sessions[0],artist:''}]});
 vi.mocked(invoke).mockRejectedValueOnce(new Error('Буфер обмена недоступен'));
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Копировать исполнителя и название трека'})));
 expect(invoke).toHaveBeenCalledWith('clipboard.text',{text:'Трек'});
 expect(screen.getByRole('alert').textContent).toBe('Буфер обмена недоступен');
 expect(screen.queryByRole('status')).toBeNull();
});
it('shows an empty player without copying placeholder text',async()=>{
 await show({available:true,sessions:[]});
 expect(screen.getByText('Сейчас ничего не играет')).toBeTruthy();
 expect(screen.queryByRole('button',{name:'Копировать исполнителя и название трека'})).toBeNull();
 expect(screen.getByRole('region',{name:'Музыка'}).querySelector('.music-cover')).toBeNull();
 expect(screen.queryByText('Пульт на телефоне')).toBeNull();
 expect(screen.getByRole('button',{name:'Все настройки звука'})).toBeTruthy();
});


it('commits the vertical master volume and edits an app inside the mixer menu',async()=>{
 let audio={available:true,volume:.12,muted:false,devices:[],sessions:[{id:'browser',name:'Браузер',volume:.4,muted:false}]};
 vi.mocked(invoke).mockImplementation(async(action,params)=>{
  if(action==='media.get')return media as never;
  if(action==='audio.set'){
   const change=params as {sessionId?:string;volume?:number;muted?:boolean};
   if(change.sessionId)audio={...audio,sessions:audio.sessions.map(item=>item.id===change.sessionId?{...item,...change}:item)};
   else audio={...audio,...change};
  }
  return audio as never;
 });
 await act(async()=>render(<Music expanded openAudio={vi.fn()}/>));
 const master=screen.getByRole('slider',{name:/Общая громкость/});
 expect(master.getAttribute('aria-orientation')).toBe('vertical');
 fireEvent.change(master,{target:{value:'0.3'}});
 expect(invoke).not.toHaveBeenCalledWith('audio.set',expect.anything());
 await act(async()=>fireEvent.keyUp(master,{key:'ArrowUp'}));
 expect(invoke).toHaveBeenCalledWith('audio.set',{volume:.3});
 fireEvent.click(screen.getByRole('button',{name:'Микшер приложений'}));
 const app=screen.getByRole('slider',{name:/Громкость: Браузер/});
 expect(app.getAttribute('aria-orientation')).toBe('vertical');
 fireEvent.change(app,{target:{value:'0.7'}});
 await act(async()=>fireEvent.pointerUp(app));
 expect(invoke).toHaveBeenCalledWith('audio.set',{sessionId:'browser',volume:.7});
 expect(screen.getByRole('dialog',{name:'Микшер приложений'})).toBeTruthy();
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Выключить звук: Браузер'})));
 expect(invoke).toHaveBeenCalledWith('audio.set',{sessionId:'browser',muted:true});
 expect(screen.getByRole('button',{name:'Включить звук: Браузер'})).toBeTruthy();
 fireEvent.keyDown(screen.getByRole('dialog'),{key:'Escape'});
 expect(screen.queryByRole('dialog')).toBeNull();
 expect(document.activeElement).toBe(screen.getByRole('button',{name:'Микшер приложений'}));
});
it('shows an honest empty mixer and closes on outside click',async()=>{
 await show();
 fireEvent.click(screen.getByRole('button',{name:'Микшер приложений'}));
 expect(screen.getByText('Активных аудиосессий нет. Запустите звук в приложении.')).toBeTruthy();
 fireEvent.pointerDown(document.body);
 expect(screen.queryByRole('dialog')).toBeNull();
});

it('uses the wheel for master and session volume, prevents page scrolling and clamps to 0–100%',async()=>{
 let audio={available:true,volume:.99,muted:false,devices:[],sessions:[{id:'browser',name:'Браузер',volume:.01,muted:false}]};
 vi.mocked(invoke).mockImplementation(async(action,params)=>{
  if(action==='media.get')return media as never;
  if(action==='audio.set'){
   const change=params as {sessionId?:string;volume?:number};
   if(change.sessionId)audio={...audio,sessions:audio.sessions.map(item=>item.id===change.sessionId?{...item,...change}:item)};
   else audio={...audio,...change};
  }
  return audio as never;
 });
 await act(async()=>render(<Music expanded openAudio={vi.fn()}/>));
 const master=screen.getByRole('slider',{name:/Общая громкость/});
 const up=new WheelEvent('wheel',{deltaY:-120,bubbles:true,cancelable:true});
 await act(async()=>master.dispatchEvent(up));
 expect(up.defaultPrevented).toBe(true);
 expect(invoke).toHaveBeenCalledWith('audio.set',{volume:1});
 const callCount=vi.mocked(invoke).mock.calls.filter(([action])=>action==='audio.set').length;
 await act(async()=>fireEvent.wheel(master,{deltaY:-120}));
 expect(vi.mocked(invoke).mock.calls.filter(([action])=>action==='audio.set')).toHaveLength(callCount);
 await act(async()=>fireEvent.wheel(master,{deltaY:120}));
 expect(invoke).toHaveBeenCalledWith('audio.set',{volume:.98});
 fireEvent.click(screen.getByRole('button',{name:'Микшер приложений'}));
 const app=screen.getByRole('slider',{name:/Громкость: Браузер/});
 await act(async()=>fireEvent.wheel(app,{deltaY:120}));
 expect(invoke).toHaveBeenCalledWith('audio.set',{sessionId:'browser',volume:0});
 await act(async()=>fireEvent.wheel(app,{deltaY:-120}));
 expect(invoke).toHaveBeenCalledWith('audio.set',{sessionId:'browser',volume:.02});
});

it('shows elapsed, duration and remaining and seeks only on commit',async()=>{
 await show();
 expect(screen.getByText('0:12 / 3:00')).toBeTruthy();
 expect(screen.getByText('Осталось 2:48')).toBeTruthy();
 const timeline=screen.getByRole('slider',{name:/Позиция воспроизведения/});
 fireEvent.change(timeline,{target:{value:'75'}});
 expect(screen.getByText('1:15 / 3:00')).toBeTruthy();
 expect(screen.getByText('Осталось 1:45')).toBeTruthy();
 expect(invoke).not.toHaveBeenCalledWith('media.command',expect.anything());
 vi.mocked(invoke).mockResolvedValueOnce({...media,sessions:[{...media.sessions[0],positionSeconds:75}]});
 await act(async()=>fireEvent.pointerUp(timeline));
 expect(invoke).toHaveBeenCalledWith('media.command',{id:'player',command:'seek',positionSeconds:75});
 expect(screen.getAllByRole('slider',{name:/Позиция воспроизведения/})).toHaveLength(1);
 expect(screen.getByText('Осталось 1:45')).toBeTruthy();
 expect(screen.queryByText('Player')).toBeNull();
});
it('keeps timing visible for a player that cannot seek and reports missing timing',async()=>{
 await show({...media,sessions:[{...media.sessions[0],canSeek:false}]});
 expect((screen.getByRole('slider',{name:/Позиция воспроизведения/}) as HTMLInputElement).disabled).toBe(true);
 expect(screen.getByText('Осталось 2:48')).toBeTruthy();
 cleanup();
 await show({...media,sessions:[{...media.sessions[0],positionSeconds:null,durationSeconds:null}]});
 expect(screen.getByText('Время трека недоступно')).toBeTruthy();
 expect(screen.queryByRole('slider',{name:/Позиция воспроизведения/})).toBeNull();
});
