// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen,within} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {Translation} from './PersonalPanels';
import {bridge} from './bridge';
import {invoke} from './ui';
vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),invoke:vi.fn()}));
afterEach(()=>{cleanup();localStorage.clear();vi.resetAllMocks();});
it('translates with MyMemory without configuring a key and sends only on click',async()=>{
 vi.mocked(invoke).mockImplementation(async action=>(action==='translation.get'?{configured:false,defaultProvider:'mymemory'}:{text:'Привет, мир!',provider:'MyMemory'}) as never);
 await act(async()=>render(<Translation/>));
 expect(screen.queryByRole('button',{name:'Настроить ключ DeepL'})).toBeNull();
 fireEvent.change(screen.getByLabelText('Исходный текст'),{target:{value:'Hello world'}});
 expect(invoke).toHaveBeenCalledTimes(1);
 const button=screen.getByRole('button',{name:'Перевести'}) as HTMLButtonElement;
 expect(button.disabled).toBe(false);
 await act(async()=>fireEvent.click(button));
 expect(invoke).toHaveBeenCalledWith('translation.run',{text:'Hello world',source:'en',target:'ru',provider:'mymemory'});
 expect((screen.getByLabelText('Результат') as HTMLTextAreaElement).value).toBe('Привет, мир!');
});
it('counts UTF-8 bytes and offers Google/Yandex without website navigation',async()=>{
 vi.mocked(invoke).mockResolvedValue({configured:false});await act(async()=>render(<Translation/>));
 fireEvent.change(screen.getByLabelText('Исходный текст'),{target:{value:'я'.repeat(251)}});
 expect((screen.getByRole('button',{name:'Перевести'}) as HTMLButtonElement).disabled).toBe(true);
 expect(screen.getByText(/502 \/ 500 байт/)).toBeTruthy();
 expect(screen.getByRole('option',{name:/Google/})).toBeTruthy();
 expect(screen.getByRole('option',{name:/Яндекс/})).toBeTruthy();
 expect(screen.queryByRole('button',{name:/Открыть/})).toBeNull();
});
it('ignores a late translation after cancellation and permits a new request',async()=>{
 let finish:(value:unknown)=>void=()=>{};
 vi.mocked(invoke).mockImplementation((action)=>action==='translation.run'?new Promise(resolve=>{finish=resolve;}):Promise.resolve({configured:false}) as never);
 await act(async()=>render(<Translation/>));fireEvent.change(screen.getByLabelText('Исходный текст'),{target:{value:'Hello'}});
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Перевести'})));
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Отменить запрос'})));
 await act(async()=>finish({text:'late',provider:'MyMemory'}));
 expect((screen.getByLabelText('Результат') as HTMLTextAreaElement).value).toBe('');expect((screen.getByRole('button',{name:'Перевести'}) as HTMLButtonElement).disabled).toBe(false);
});

it('keeps recent languages at the top of both lists after reopening',async()=>{
 vi.mocked(invoke).mockResolvedValue({configured:false});
 const view=render(<Translation/>);
 fireEvent.change(screen.getByLabelText('Исходный язык'),{target:{value:'de'}});
 fireEvent.change(screen.getByLabelText('Язык перевода'),{target:{value:'ja'}});
 view.unmount();
 await act(async()=>render(<Translation/>));
 expect(screen.getByLabelText<HTMLSelectElement>('Исходный язык').value).toBe('de');
 expect(screen.getByLabelText<HTMLSelectElement>('Язык перевода').value).toBe('ja');
 for(const label of ['Исходный язык','Язык перевода']){
  expect(within(screen.getByLabelText(label)).getAllByRole('option').slice(0,2).map(option=>option.textContent)).toEqual(['Японский','Немецкий']);
 }
});

it('restores automatic source detection with its compatible provider',async()=>{
 vi.mocked(invoke).mockResolvedValue({configured:false});
 const view=render(<Translation/>);
 fireEvent.change(screen.getByLabelText('Переводчик'),{target:{value:'yandex'}});
 fireEvent.change(screen.getByLabelText('Исходный язык'),{target:{value:'auto'}});
 view.unmount();
 await act(async()=>render(<Translation/>));
 expect(screen.getByLabelText<HTMLSelectElement>('Исходный язык').value).toBe('auto');
 expect(screen.getByLabelText<HTMLSelectElement>('Переводчик').value).toBe('yandex');
});

it('translates on Ctrl+Enter in the source field and respects request limits',async()=>{
 vi.mocked(invoke).mockImplementation(async action=>(action==='translation.get'?{configured:false}:{text:'Привет',provider:'MyMemory'}) as never);
 await act(async()=>render(<Translation/>));
 const input=screen.getByLabelText('Исходный текст');
 const shortcut={key:'Enter',ctrlKey:true};
 fireEvent.change(input,{target:{value:'я'.repeat(251)}});
 fireEvent.keyDown(input,shortcut);
 fireEvent.change(input,{target:{value:'Hello'}});
 fireEvent.keyDown(input,{key:'Enter'});
 fireEvent.keyDown(input,{...shortcut,repeat:true});
 expect(invoke).not.toHaveBeenCalledWith('translation.run',expect.anything());
 await act(async()=>fireEvent.keyDown(input,shortcut));
 expect(invoke).toHaveBeenCalledWith('translation.run',{text:'Hello',source:'en',target:'ru',provider:'mymemory'});
 expect(screen.getByLabelText<HTMLTextAreaElement>('Результат').value).toBe('Привет');
});

it('places the SVG swap between the language fields and swaps them',async()=>{
 vi.mocked(invoke).mockResolvedValue({configured:false});
 await act(async()=>render(<Translation/>));
 const source=screen.getByLabelText<HTMLSelectElement>('Исходный язык');
 const target=screen.getByLabelText<HTMLSelectElement>('Язык перевода');
 const swap=screen.getByRole('button',{name:'Поменять языки местами'});
 expect(swap.querySelector('svg')).toBeTruthy();
 expect(swap.compareDocumentPosition(target)&Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
 fireEvent.click(swap);
 expect([source.value,target.value]).toEqual(['ru','en']);
});
it('disables duplicate configuration and recovers when the native form is cancelled',async()=>{
 let close:()=>void=()=>{};
 vi.mocked(invoke).mockImplementation(action=>action==='translation.configure'?new Promise(resolve=>{close=()=>resolve({configured:false});}):Promise.resolve({configured:false}) as never);
 await act(async()=>render(<Translation/>));fireEvent.change(screen.getByLabelText('Переводчик'),{target:{value:'deepl'}});
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Настроить ключ DeepL'})));
 expect((screen.getByRole('button',{name:'Окно настройки открыто…'}) as HTMLButtonElement).disabled).toBe(true);
 await act(async()=>close());expect((screen.getByRole('button',{name:'Настроить ключ DeepL'}) as HTMLButtonElement).disabled).toBe(false);
});


it('keeps input and reports provider failure without opening a site or sending elsewhere',async()=>{
 vi.mocked(invoke).mockImplementation(async action=>{if(action==='translation.run')throw new Error('Дневной лимит MyMemory исчерпан. Повторите завтра.');return {configured:false} as never;});
 await act(async()=>render(<Translation/>));
 fireEvent.change(screen.getByLabelText('Исходный текст'),{target:{value:'Hello'}});
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Перевести'})));
 expect(screen.getByRole('alert').textContent).toContain('лимит MyMemory');
 expect((screen.getByLabelText('Исходный текст') as HTMLTextAreaElement).value).toBe('Hello');
 expect(invoke).toHaveBeenCalledTimes(2);
 expect(invoke).not.toHaveBeenCalledWith('links.open',expect.anything());
 expect(invoke).not.toHaveBeenCalledWith('translation.open',expect.anything());
});

it.each(['google','yandex'])('returns %s translation into our result field without a site',async provider=>{
 vi.mocked(invoke).mockImplementation(async action=>(action==='translation.get'?{configured:false}:{text:'Привет, мир',provider}) as never);
 await act(async()=>render(<Translation/>));
 fireEvent.change(screen.getByLabelText('Переводчик'),{target:{value:provider}});
 fireEvent.change(screen.getByLabelText('Исходный текст'),{target:{value:'Hello world'}});
 expect(invoke).toHaveBeenCalledTimes(1);
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Перевести'})));
 expect(invoke).toHaveBeenCalledWith('translation.run',{text:'Hello world',source:'en',target:'ru',provider});
 expect((screen.getByLabelText('Результат') as HTMLTextAreaElement).value).toBe('Привет, мир');
 expect(invoke).not.toHaveBeenCalledWith('translation.open',expect.anything());
 expect(invoke).not.toHaveBeenCalledWith('links.open',expect.anything());
 expect(document.querySelector('iframe')).toBeNull();
});

it('offers Soniox voice translation and keeps the API key in native settings',async()=>{
 vi.mocked(invoke).mockResolvedValue({configured:false,sonioxConfigured:false});
 await act(async()=>render(<Translation/>));
 fireEvent.change(screen.getByLabelText('Переводчик'),{target:{value:'soniox'}});
 expect(screen.queryByRole('button',{name:'Начать запись'})).toBeNull();
 expect(screen.getByText('Добавьте ключ Soniox API, чтобы начать запись.')).toBeTruthy();
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Настроить ключ Soniox'})));
 expect(invoke).toHaveBeenCalledWith('translation.configureSoniox');
 expect(invoke).not.toHaveBeenCalledWith('translation.voice',expect.anything());
});

it.each(['soniox','mymemory','google','yandex','deepl','local'])('uses the microphone with %s only on explicit submit and stops on collapse',async provider=>{
 const stop=vi.fn();
 const getUserMedia=vi.fn(async()=>({getTracks:()=>[{stop}]}));
 const mediaDevices=Object.getOwnPropertyDescriptor(navigator,'mediaDevices');
 Object.defineProperty(navigator,'mediaDevices',{configurable:true,value:{getUserMedia}});
 class Recorder {
  static isTypeSupported(){return true;}
  state='inactive';
  ondataavailable?: (event:{data:Blob})=>void;
  onstop?:()=>void;
  start(){this.state='recording';}
  stop(){
   this.state='inactive';
   this.ondataavailable?.({data:new Blob([new Uint8Array([0x1a,0x45,0xdf,0xa3])])});
   this.onstop?.();
  }
 }
 const local=provider==='local';
 let modelReady=()=>{};
 vi.stubGlobal('MediaRecorder',Recorder);
 vi.stubGlobal('AudioContext',class{
  async decodeAudioData(){return {sampleRate:16000,length:2,numberOfChannels:1,getChannelData:()=>new Float32Array([0,0.5])};}
  async close(){}
 });
 vi.mocked(invoke).mockImplementation(async action=>{
  if(action==='translation.get')return {configured:false,sonioxConfigured:!local} as never;
  if(action==='translation.voice')return {transcript:'Hello',text:'Привет'} as never;
  if(action==='translation.localMicrophone')await new Promise<void>(resolve=>{modelReady=resolve;});
  if(action==='translation.dictate'||action==='translation.localDictate')return {transcript:'Hello'} as never;
  return {} as never;
 });
 try{
  const view=render(<Translation/>);
  const voice=provider==='soniox';
  const command=voice?'translation.voice':local?'translation.localDictate':'translation.dictate';
  const recordLabel=voice?'Начать запись':'Диктовать текст';
  fireEvent.change(screen.getByLabelText('Переводчик'),{target:{value:local?'google':provider}});
  if(!voice&&!local)fireEvent.change(screen.getByLabelText('Распознавание речи'),{target:{value:'soniox'}});
  if(!voice)fireEvent.change(screen.getByLabelText('Исходный текст'),{target:{value:'Draft'}});
  await act(async()=>{});
  await act(async()=>fireEvent.click(screen.getByRole('button',{name:recordLabel})));
  if(local){
   expect(screen.getByRole('status').textContent).toContain('Загружаем модель');
   expect(getUserMedia).not.toHaveBeenCalled();
   expect(invoke).toHaveBeenCalledWith('translation.localMicrophone');
   expect(invoke).not.toHaveBeenCalledWith('translation.configureSoniox');
   await act(async()=>modelReady());
  }
  expect(getUserMedia).toHaveBeenCalledOnce();
  expect(invoke).not.toHaveBeenCalledWith(command,expect.anything());
  fireEvent.click(screen.getByRole('button',{name:voice?'Стоп и перевести':'Стоп и вставить'}));
  expect(await screen.findByDisplayValue(voice?'Привет':'Draft Hello')).toBeTruthy();
  if(voice){
   expect(screen.getByLabelText<HTMLTextAreaElement>('Распознанная речь').value).toBe('Hello');
   expect(invoke).toHaveBeenCalledWith(command,{audio:'GkXfow==',source:'en',target:'ru'});
   await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Прослушать результат'})));
   expect(invoke).toHaveBeenCalledWith('translation.speakLocal',{text:'Привет',language:'ru',operation:expect.any(String)});
   expect(invoke).not.toHaveBeenCalledWith('translation.speak',expect.anything());
  }else{
   expect(invoke).toHaveBeenCalledWith(command,{audio:local?expect.stringMatching(/^UklGR/):'GkXfow==',source:'en'});
   expect(invoke).not.toHaveBeenCalledWith('translation.run',expect.anything());
  }
  expect(stop).toHaveBeenCalled();

  stop.mockClear();
  await act(async()=>fireEvent.click(screen.getByRole('button',{name:recordLabel})));
  if(local)await act(async()=>modelReady());
  view.rerender(<Translation active={false}/>);
  expect(stop).toHaveBeenCalled();
  expect(vi.mocked(invoke).mock.calls.filter(([action])=>action===command)).toHaveLength(1);
  if(local){
   view.rerender(<Translation active/>);
   getUserMedia.mockClear();
   await act(async()=>fireEvent.click(screen.getByRole('button',{name:recordLabel})));
   fireEvent.click(screen.getByRole('button',{name:'Отмена'}));
   expect(invoke).toHaveBeenCalledWith('translation.cancelSpeech');
   await act(async()=>modelReady());
   expect(getUserMedia).not.toHaveBeenCalled();
   expect(screen.getByRole('status').textContent).toBe('');
  }
 }finally{
  vi.unstubAllGlobals();
  if(mediaDevices)Object.defineProperty(navigator,'mediaDevices',mediaDevices);
  else Reflect.deleteProperty(navigator,'mediaDevices');
 }
});

it('switches from Soniox to typing with the previous text provider and draft',async()=>{
 vi.mocked(invoke).mockImplementation(async action=>(action==='translation.get'?{configured:false}:{text:'Привет',provider:'Google'}) as never);
 await act(async()=>render(<Translation/>));
 fireEvent.change(screen.getByLabelText('Переводчик'),{target:{value:'google'}});
 fireEvent.change(screen.getByLabelText('Исходный текст'),{target:{value:'Hello'}});
 fireEvent.click(screen.getByRole('button',{name:'Голос'}));
 expect(screen.getByLabelText<HTMLSelectElement>('Переводчик').value).toBe('soniox');
 fireEvent.click(screen.getByRole('button',{name:'Текст'}));
 expect(screen.getByLabelText<HTMLSelectElement>('Переводчик').value).toBe('google');
 expect(screen.getByLabelText<HTMLTextAreaElement>('Исходный текст').value).toBe('Hello');
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Перевести'})));
 expect(invoke).toHaveBeenCalledWith('translation.run',{text:'Hello',source:'en',target:'ru',provider:'google'});
});
it('plays the two translation fields in their selected languages and stops to switch fields',async()=>{
 const finish: Array<()=>void>=[];
 vi.mocked(invoke).mockImplementation(action=>{
  if(action==='translation.speakLocal')return new Promise(resolve=>finish.push(()=>resolve({}))) as never;
  return Promise.resolve(action==='translation.run'?{text:'你好'}:{configured:false}) as never;
 });
 const view=render(<Translation/>);
 await act(async()=>{});
 const source=screen.getByRole<HTMLButtonElement>('button',{name:'Прослушать исходный текст'});
 const result=screen.getByRole<HTMLButtonElement>('button',{name:'Прослушать результат'});
 expect(source.querySelector('svg')).toBeTruthy();
 expect(result.querySelector('svg')).toBeTruthy();
 expect(source.disabled&&result.disabled).toBe(true);
 fireEvent.change(screen.getByLabelText('Исходный язык'),{target:{value:'ru'}});
 fireEvent.change(screen.getByLabelText('Язык перевода'),{target:{value:'zh'}});
 fireEvent.change(screen.getByLabelText('Исходный текст'),{target:{value:'Привет'}});
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Перевести'})));
 fireEvent.click(source);
 expect(invoke).toHaveBeenCalledWith('translation.speakLocal',{text:'Привет',language:'ru',operation:expect.any(String)});
 fireEvent.click(result);
 expect(invoke).toHaveBeenCalledWith('translation.cancelSpeech');
 expect(invoke).toHaveBeenCalledWith('translation.speakLocal',{text:'你好',language:'zh',operation:expect.any(String)});
 await act(async()=>finish[0]());
 expect(screen.getByRole('button',{name:'Остановить озвучивание'})).toBe(result);
 view.rerender(<Translation active={false}/>);
 expect(vi.mocked(invoke).mock.calls.filter(([action])=>action==='translation.cancelSpeech')).toHaveLength(2);
 await act(async()=>finish[1]());
});

it('shows first-use Piper download progress, then playback, and ignores stale progress',async()=>{
 let emit:(event:unknown)=>void=()=>{};
 const subscription=vi.spyOn(bridge,'subscribe').mockImplementation(listener=>{
  emit=listener;
  return ()=>{};
 });
 vi.mocked(invoke).mockImplementation(action=>action==='translation.speakLocal'
  ? new Promise(()=>{}) as never
  : Promise.resolve({configured:false,sonioxConfigured:true}) as never);
 try {
  await act(async()=>render(<Translation sessionResult={['Привет!',vi.fn()]}/>));
  fireEvent.click(screen.getByRole('button',{name:'Прослушать результат'}));
  const request=vi.mocked(invoke).mock.calls.find(([action])=>action==='translation.speakLocal');
  const {operation}=request?.[1] as {operation:string};
  await act(()=>emit({type:'translation.speech.progress',data:{operation:'other',phase:'download',received:1,total:2}}));
  expect(screen.queryByRole('progressbar',{name:'Загрузка голоса Piper'})).toBeNull();
  await act(()=>emit({type:'translation.speech.progress',data:{operation,phase:'download',received:20000000,total:80000000}}));
  expect(screen.getByText('Скачиваем голос Piper… 25% · 80 МБ')).toBeTruthy();
  await act(()=>emit({type:'translation.speech.progress',data:{operation,phase:'playing'}}));
  expect(screen.getByText('Идёт озвучивание')).toBeTruthy();
  expect(screen.queryByRole('progressbar',{name:'Загрузка голоса Piper'})).toBeNull();
  expect(invoke).not.toHaveBeenCalledWith('translation.speak',expect.anything());
 }finally { subscription.mockRestore(); }
});
