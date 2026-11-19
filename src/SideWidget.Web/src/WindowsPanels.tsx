import {CodexShortcut} from './CodexShortcut';
import {useAppReorder} from './useAppReorder';
import {t} from './i18n';
import {useCallback,useEffect,useId,useLayoutEffect,useRef,useState,type KeyboardEvent,type Ref} from 'react';
import {createPortal} from 'react-dom';
import {bridge} from './bridge';
import type {AppShortcut,AudioState,MediaState,Snapshot,SystemState} from './types';
import type {LocalProps} from './LocalTools';
import {bytes,CopyButton,Icon,invoke,nativeAvailable,Notice,Panel,speed,timeLabel,useAction} from './ui';
import './music.css';
import {SpectrumGraphic,useAudioSpectrum,VisualizerSettingsPanel} from './AudioVisualization';
import {ActionMenu,type MenuAction} from './ActionMenu';
import {PowerScheduleForm} from './PowerSchedule';

function usePoll<T>(command:string,expanded:boolean,period:number) {
 const [value,setRawValue]=useState<T>();const [error,setError]=useState('');const revision=useRef(0);
 const setValue=useCallback((next:T|undefined|((current:T|undefined)=>T|undefined))=>{revision.current++;setRawValue(next);},[]);
 useEffect(()=>{if(!expanded)return;let active=true;let timer:ReturnType<typeof setTimeout>;
  async function read(){const started=revision.current;try{const next=await invoke<T>(command);if(active&&revision.current===started){setRawValue(next);setError('');}}catch(e){if(active)setError((e as Error).message);}finally{if(active&&nativeAvailable)timer=setTimeout(read,period);}}
  if(nativeAvailable)void read();else setError(t("Доступно в приложении Windows."));
  return()=>{active=false;clearTimeout(timer);};
 },[command,expanded,period]);
 return {value,setValue,error,setError};
}

export function Music({expanded,openAudio}:{expanded:boolean;openAudio:()=>void}) {
 const {value,setValue,error}=usePoll<MediaState>('media.get',expanded,1000);
 const spectrum=useAudioSpectrum(expanded);
 const [selected,setSelected]=useState(()=>localStorage.getItem('sidewidget.media.selected')||'');const {run,busy,error:actionError}=useAction();
 const session=value?.sessions.find(s=>s.id===selected)||value?.sessions[0];
 async function command(command:string,positionSeconds?:number){if(!session)return;await run(async()=>setValue(await invoke<MediaState>('media.command',{id:session.id,command,positionSeconds})));}
 if(!session){
  const status=value?.available?t("Сейчас ничего не играет"):error||value?.message?t("Медиаплеер недоступен"):t("Подключаем проигрыватель");
  return <section aria-label={t("Музыка")} className="panel music-idle">
   <Icon name="music" size={18}/><span role="status" title={t(error||value?.message||"")||undefined}>{status}</span>
   <button className="icon-button" aria-label={t("Все настройки звука")} onClick={openAudio}><Icon name="settings" size={18}/></button>
   {actionError&&<Notice error>{actionError}</Notice>}
  </section>;
 }
 return <section aria-label={t("Музыка")} className="panel music-with-volume music-compact music-reference">
  <SpectrumGraphic bands={spectrum.available&&spectrum.rms>.00001?spectrum.bands:[]} settings={spectrum.settings}/>
  <div className="music-body"><Audio expanded={expanded} compact openAudio={openAudio}/>
   <div className="music-cover">{session.artwork?<img src={session.artwork} alt={t("Обложка альбома")}/>:<Icon name="music" size={32}/>}</div>
   <div className="music-content">
    <div className="now-playing">
     <div className="track-meta"><TrackTitle title={session.title} artist={session.artist}/><p title={session.artist||session.source}>{session.artist||session.source}</p></div>
     <div className="music-controls"><button className="icon-button" disabled={!session.canPrevious||busy} onClick={()=>void command('previous')} aria-label={t("Предыдущая композиция")}><Icon name="previous" size={15}/></button><button className="play-button" disabled={busy||(session.playing?!session.canPause:!session.canPlay)} onClick={()=>void command(session.playing?'pause':'play')} aria-label={session.playing?t("Пауза"):t("Воспроизвести")}><Icon name={session.playing?'pause':'play'}/></button><button className="icon-button" disabled={!session.canNext||busy} onClick={()=>void command('next')} aria-label={t("Следующая композиция")}><Icon name="next" size={15}/></button></div>
    </div>
    {value!.sessions.length>1&&<label>{t("Проигрыватель")}<select value={session.id} onChange={e=>{setSelected(e.target.value);localStorage.setItem('sidewidget.media.selected',e.target.value);}}>{value!.sessions.map(s=><option value={s.id} key={s.id}>{s.source} — {s.title||t("Без названия")}</option>)}</select></label>}
    {session.positionSeconds!=null&&session.durationSeconds!=null&&session.durationSeconds>0?<CommitSlider label={t("Позиция воспроизведения")} timeline value={Math.min(session.positionSeconds,session.durationSeconds)} max={session.durationSeconds} step={1} onCommit={position=>void command('seek',position)} disabled={!session.canSeek||busy}/>:<small className="track-time-unavailable">{t("Время трека недоступно")}</small>}
    {actionError&&<Notice error>{actionError}</Notice>}
   </div>
  </div>
 </section>;
}

function TrackTitle({title,artist}:{title:string;artist:string}) {
 const [copied,setCopied]=useState(false);const {error,busy,run,setError}=useAction();const timer=useRef<ReturnType<typeof setTimeout>|undefined>(undefined);
 useEffect(()=>{setCopied(false);setError('');return()=>clearTimeout(timer.current);},[title,artist]);
 const text=[artist.trim(),title.trim()].filter(Boolean).join(' — ');
 async function copy(){setCopied(false);await run(async()=>{if(nativeAvailable)await invoke('clipboard.text',{text});else await navigator.clipboard.writeText(text);setCopied(true);clearTimeout(timer.current);timer.current=setTimeout(()=>setCopied(false),1800);});}
 return <><button className="track-title" disabled={!title.trim()||busy} aria-label={t("Копировать исполнителя и название трека")} title={text?t("{track} · Копировать",{track:text}):undefined} onClick={()=>void copy()}><strong>{title||t("Без названия")}</strong></button>{copied&&<Notice>{t("Скопировано")}</Notice>}{error&&<Notice error>{error}</Notice>}</>;
}
function CommitSlider({label,value,max=1,step=.01,onCommit,disabled=false,valueBelow=false,vertical=false,timeline=false}:{label:string;value:number;max?:number;step?:number;onCommit:(value:number)=>void;disabled?:boolean;valueBelow?:boolean;vertical?:boolean;timeline?:boolean}) {
 const [draft,setDraft]=useState(value);const editing=useRef(false);const dirty=useRef(false);const external=useRef(value);external.current=value;
 const slider=useRef<HTMLLabelElement>(null);const latest=useRef({draft,disabled,onCommit});latest.current={draft,disabled,onCommit};
 useEffect(()=>{
  const element=slider.current;if(!element||max!==1||timeline)return;
  function wheel(event:WheelEvent){
   if(!event.deltaY)return;
   event.preventDefault();event.stopPropagation();
   const current=latest.current;if(current.disabled||editing.current)return;
   const next=Math.min(1,Math.max(0,Math.round((current.draft+(event.deltaY<0?.02:-.02))*100)/100));
   if(next===current.draft)return;
   latest.current={...current,draft:next};setDraft(next);current.onCommit(next);
  }
  element.addEventListener('wheel',wheel,{passive:false});
  return()=>element.removeEventListener('wheel',wheel);
 },[max,timeline]);
 useEffect(()=>{if(!editing.current)setDraft(value);},[value]);
 function cancel(){if(!editing.current)return;editing.current=false;dirty.current=false;latest.current.draft=external.current;setDraft(external.current);}
 useEffect(()=>{window.addEventListener('blur',cancel);return()=>window.removeEventListener('blur',cancel);},[]);
 function commit(){const wasEditing=editing.current;editing.current=false;if(dirty.current){dirty.current=false;onCommit(latest.current.draft);}else if(wasEditing){latest.current.draft=external.current;setDraft(external.current);}}
 const displayValue=max===1&&!timeline?`${Math.round(draft*100)}%`:timeLabel(draft);
 return <label ref={slider} className={`slider-label${vertical?' vertical-volume':''}${timeline?' track-timeline':''}`}><span className={timeline?'sr-only':undefined}>{label}{!timeline&&!valueBelow&&<span>{displayValue}</span>}</span><input type="range" title={max===1&&!timeline?t("Колесо мыши: изменить громкость на 2%"):undefined} aria-orientation={vertical?'vertical':undefined} min={0} max={max} step={step} value={draft} disabled={disabled} onPointerDown={event=>{editing.current=true;try{event.currentTarget.setPointerCapture?.(event.pointerId);}catch{}}} onPointerCancel={cancel} onLostPointerCapture={cancel} onChange={e=>{editing.current=true;dirty.current=true;latest.current.draft=Number(e.target.value);setDraft(latest.current.draft);}} onPointerUp={commit} onKeyUp={commit} onBlur={commit}/>{timeline&&<span className="track-times"><span title={t("Прошло / длительность")}>{displayValue} / {timeLabel(max)}</span><span title={t("Осталось")}>{t("Осталось {time}",{time:timeLabel(Math.max(0,max-draft))})}</span></span>}{valueBelow&&<small className="slider-value">{displayValue}</small>}</label>;
}
export function Audio({expanded,compact=false,openAudio,openSettings}:{expanded:boolean;compact?:boolean;openAudio?:()=>void;openSettings?:()=>void}) {
 const {value,setValue,error}=usePoll<AudioState>('audio.get',expanded,4000);const {run,error:actionError,busy}=useAction();const levelRevision=useRef(0);
 useEffect(()=>bridge.subscribe(event=>{
  if(event.type!=='audio.level.changed')return;
  const volume=event.data?.volume,muted=event.data?.muted;
  if(typeof volume!=='number'||!Number.isFinite(volume)||volume<0||volume>1||typeof muted!=='boolean')return;
  levelRevision.current++;
  setValue(current=>({...current,available:true,devices:current?.devices??[],sessions:current?.sessions??[],volume,muted}));
 }),[setValue]);
 const update=(params:object)=>void run(async()=>{const started=levelRevision.current;const next=await invoke<AudioState>('audio.set',params);setValue(current=>levelRevision.current===started?next:{...next,volume:current?.volume??next.volume,muted:current?.muted??next.muted});});
 if(compact)return <div className="music-audio">
  {value?.volume!=null?<CommitSlider label={t("Общая громкость")} vertical valueBelow value={value.volume} onCommit={volume=>update({volume})} disabled={busy}/>:<div className="volume-unavailable" title={t(error||value?.message||"Получаем громкость…")}><Icon name="soundOff"/><small role="status">{t(error||value?.message||"Получаем громкость…")}</small></div>}
  <div className="volume-actions">
   {typeof value?.muted==='boolean'&&<button className="icon-button" disabled={busy} aria-label={value.muted?t("Включить общий звук"):t("Выключить общий звук")} title={value.muted?t("Включить общий звук"):t("Выключить общий звук")} onClick={()=>update({muted:!value.muted})}><Icon name={value.muted?'soundOff':'sound'}/></button>}
   <AudioMixerMenu sessions={value?.sessions??[]} busy={busy} update={update} error={actionError}/>
   {openAudio&&<button className="icon-button" aria-label={t("Все настройки звука")} title={t("Все настройки звука")} onClick={openAudio}><Icon name="settings"/></button>}
   {openSettings&&<button className="icon-button" aria-label={t("Настройки")} title={t("Настройки панели")} onClick={openSettings}><Icon name="gear"/></button>}
  </div>
  {actionError&&<Notice error>{actionError}</Notice>}
 </div>;

 return <div className={`stack${compact?' music-audio':''}`}><VisualizerSettingsPanel/>{!compact&&<p>{t("Общая громкость Windows и отдельные аудиосессии. Громкость музыки здесь не привязана к выбранному проигрывателю.")}</p>}
  {value&&(value.available||value.devices.length>0)?<>{value.volume!=null?<CommitSlider label={t("Общая громкость")} vertical={compact} valueBelow={compact} value={value.volume} onCommit={volume=>update({volume})} disabled={busy}/>:<Notice>{t("Общая громкость недоступна.")}</Notice>}<div className={compact?'volume-actions':'actions'}>{typeof value.muted==='boolean'&&<button className={compact?'icon-button':undefined} disabled={busy} aria-label={value.muted?t("Включить общий звук"):t("Выключить общий звук")} title={value.muted?t("Включить общий звук"):t("Выключить общий звук")} onClick={()=>update({muted:!value.muted})}>{compact?<Icon name={value.muted?'soundOff':'sound'}/>:value.muted?t("Включить общий звук"):t("Выключить общий звук")}</button>}{compact&&expanded&&<AudioMixerMenu sessions={value.sessions} busy={busy} update={update} error={actionError}/>}{compact&&openAudio&&<button className="icon-button" aria-label={t("Все настройки звука")} title={t("Все настройки звука")} onClick={openAudio}><Icon name="settings"/></button>}{compact&&openSettings&&<button className="icon-button" aria-label={t("Настройки")} title={t("Настройки панели")} onClick={openSettings}><Icon name="gear"/></button>}</div>
  {!compact&&<><h2>{t("Микрофон по умолчанию")}</h2>{typeof value.microphoneMuted==='boolean'?<button disabled={busy} onClick={()=>update({microphoneMuted:!value.microphoneMuted})}>{value.microphoneMuted?t("Включить микрофон"):t("Выключить микрофон")}</button>:<Notice>{t("Состояние микрофона недоступно.")}</Notice>}<small>{t("Меняется только устройство ввода по умолчанию, не все микрофоны системы.")}</small>
  <h2>{t("Устройства")}</h2><div className="list">{value.devices.map(device=><div className="device-row" key={device.id}><span>{device.name}</span><small>{device.flow==='input'?t("Ввод"):t("Вывод")} {device.default?t("· по умолчанию"):''}</small></div>)}</div>
  <h2>{t("Микшер приложений")}</h2>{value.sessions.length?value.sessions.map(session=><div className="mixer-session" key={session.id}><CommitSlider label={session.name} value={session.volume} onCommit={volume=>update({sessionId:session.id,volume})} disabled={busy}/><button className="quiet" disabled={busy} onClick={()=>update({sessionId:session.id,muted:!session.muted})}>{session.muted?t("Включить звук"):t("Выключить звук")}</button></div>):<p>{t("Активных аудиосессий нет.")}</p>}</>}</>:<Notice>{t(error||value?.message||"Получаем устройства…")}</Notice>}
  {actionError&&<Notice error>{actionError}</Notice>}{!compact&&<button onClick={()=>void run(()=>invoke('quick.open',{target:'sound'}))}>{t("Открыть настройки звука Windows")}</button>}
 </div>;
}


function AudioMixerMenu({sessions,busy,update,error}:{sessions:AudioState['sessions'];busy:boolean;update:(params:object)=>void;error:string}) {
 const [opened,setOpened]=useState(false);const trigger=useRef<HTMLButtonElement>(null);const menu=useRef<HTMLDivElement>(null);
 const [position,setPosition]=useState({left:8,top:8});
 function close(focus=false){setOpened(false);if(focus)trigger.current?.focus();}
 useLayoutEffect(()=>{
  if(!opened||!trigger.current||!menu.current)return;
  const anchor=trigger.current.getBoundingClientRect();const box=menu.current.getBoundingClientRect();
  setPosition({left:Math.max(8,Math.min(anchor.left,innerWidth-box.width-8)),top:Math.max(8,Math.min(anchor.bottom+6,innerHeight-box.height-8))});
  (menu.current.querySelector('input')||menu.current.querySelector('button'))?.focus();
 },[opened]);
 useEffect(()=>{
  if(!opened)return;
  const outside=(event:PointerEvent)=>{if(!menu.current?.contains(event.target as Node)&&!trigger.current?.contains(event.target as Node))close();};
  const resize=()=>close();
  const scroll=(event:Event)=>{if(!menu.current?.contains(event.target as Node))close();};
  document.addEventListener('pointerdown',outside);document.addEventListener('scroll',scroll,true);window.addEventListener('resize',resize);
  return()=>{document.removeEventListener('pointerdown',outside);document.removeEventListener('scroll',scroll,true);window.removeEventListener('resize',resize);};
 },[opened]);
 return <><button ref={trigger} className="icon-button" aria-label={t("Микшер приложений")} title={t("Микшер приложений")} aria-haspopup="dialog" aria-expanded={opened} onClick={()=>setOpened(!opened)}><Icon name="apps"/></button>
  {opened&&createPortal(<div ref={menu} role="dialog" aria-label={t("Микшер приложений")} className="audio-mixer-menu" style={position} onBlur={event=>{if(event.relatedTarget&&!event.currentTarget.contains(event.relatedTarget as Node)&&event.relatedTarget!==trigger.current)close();}} onKeyDown={event=>{if(event.key==='Escape'){event.preventDefault();event.stopPropagation();close(true);}}}>
   <div className="section-heading"><h2>{t("Микшер приложений")}</h2><button className="icon-button" aria-label={t("Закрыть микшер")} onClick={()=>close(true)}><Icon name="close"/></button></div>
   {sessions.length?<div className="audio-mixer-columns">{sessions.map(session=><div className="audio-mixer-channel" key={session.id}>
    <strong title={session.name}>{session.name}</strong><CommitSlider label={t("Громкость: {name}",{name:session.name})} vertical valueBelow value={session.volume} disabled={busy} onCommit={volume=>update({sessionId:session.id,volume})}/>
    <button className="icon-button" disabled={busy} aria-label={session.muted?t("Включить звук: {name}",{name:session.name}):t("Выключить звук: {name}",{name:session.name})} title={session.muted?t("Включить звук: {name}",{name:session.name}):t("Выключить звук: {name}",{name:session.name})} onClick={()=>update({sessionId:session.id,muted:!session.muted})}><Icon name={session.muted?'soundOff':'sound'}/></button>
   </div>)}</div>:<p>{t("Активных аудиосессий нет. Запустите звук в приложении.")}</p>}
   {error&&<Notice error>{error}</Notice>}
  </div>,document.body)}
 </>;
}

type FileResult={name:string;path:string;isDirectory:boolean};
type ToolResult={id:string;name:string;icon:string};
type SearchEntry={key:string;name:string;icon:string;detail:string;open:()=>void;file?:FileResult};
export function Search({expanded,inputRef,tools=[],apps=[],openTool}:{expanded:boolean;inputRef?:Ref<HTMLInputElement>;tools?:ToolResult[];apps?:AppShortcut[];openTool?:(id:string)=>void}) {
 const [query,setQuery]=useState('');
 const [files,setFiles]=useState<FileResult[]>([]);
 const [message,setMessage]=useState('');
 const [loading,setLoading]=useState(false);
 const [selectedKey,setSelectedKey]=useState<string>();
 const revision=useRef(0);
 const resultsId=useId();
 const {run,error,busy,setError}=useAction();
 const searchText=query.trim().toLocaleLowerCase().replaceAll('ё','е');
 const matches=(name:string)=>name.toLocaleLowerCase().replaceAll('ё','е').includes(searchText);
 useEffect(()=>{
  const current=++revision.current;
  if(!query.trim()||!expanded||!nativeAvailable)return;
  setLoading(true);setMessage('');
  const timer=setTimeout(()=>{void invoke<{available:boolean;results:FileResult[];message?:string}>('search.query',{text:query}).then(value=>{
   if(current!==revision.current)return;
   setFiles(value.results);
   if(!value.available)setMessage(value.message||t("Everything недоступен. Запустите установленный Everything."));
  }).catch(e=>{if(current===revision.current)setMessage(e.message);}).finally(()=>{if(current===revision.current)setLoading(false);});},250);
  return()=>{clearTimeout(timer);revision.current++;};
 },[query,expanded]);

 function changeQuery(next:string){
  if(next===query)return;
  revision.current++;
  setQuery(next);setFiles([]);setMessage('');setError('');setSelectedKey(undefined);
  setLoading(nativeAvailable&&!!next.trim());
 }
 function openFile(file:FileResult,reveal=false){if(!busy)void run(()=>invoke('search.open',{path:file.path,reveal}));}
 const results:SearchEntry[]=searchText&&expanded?[
  ...tools.filter(tool=>matches(tool.name)).map(tool=>({key:'tool:'+tool.id,name:tool.name,icon:tool.icon,detail:t("Инструмент"),open:()=>openTool?.(tool.id)})),
  ...apps.filter(app=>matches(app.name)).map(app=>({key:'app:'+app.id,name:app.name,icon:'apps',detail:t("Приложение"),open:()=>{if(!busy)void run(()=>invoke('apps.launch',{id:app.id}));}})),
  ...files.map(file=>({key:'file:'+file.path,name:file.name,icon:file.isDirectory?'folder':'diary',detail:file.path,open:()=>openFile(file),file})),
 ]:[];
 const selected=Math.max(0,results.findIndex(result=>result.key===selectedKey));
 const activeId=results.length?resultsId+'-'+selected:undefined;

 function keyboard(event:KeyboardEvent<HTMLInputElement>){
  if(event.nativeEvent.isComposing)return;
  if(event.key==='Escape'&&query){event.preventDefault();event.stopPropagation();changeQuery('');return;}
  if(!results.length)return;
  if(event.key==='ArrowDown'||event.key==='ArrowUp'){
   event.preventDefault();
   const next=Math.max(0,Math.min(results.length-1,selected+(event.key==='ArrowDown'?1:-1)));
   setSelectedKey(results[next].key);
   document.getElementById(resultsId+'-'+next)?.scrollIntoView?.({block:'nearest'});
  }
  if(event.key==='Enter'){
   event.preventDefault();
   if(!event.repeat)results[selected].open();
  }
 }
 return <div className="search-block">
  <div className="search-field"><Icon name="search"/><input ref={inputRef} role="combobox" aria-label={t("Поиск инструментов, приложений и файлов")}
   aria-autocomplete="list" aria-haspopup="grid" aria-expanded={!!results.length} aria-controls={results.length?resultsId:undefined} aria-activedescendant={activeId}
   value={query} onChange={e=>changeQuery(e.target.value)} onKeyDown={keyboard} placeholder={t("Инструмент, приложение или файл")} autoComplete="off"/>
   {query&&<button className="icon-button" onMouseDown={event=>event.preventDefault()} onClick={()=>changeQuery('')} aria-label={t("Очистить поиск")}><Icon name="close" size={16}/></button>}
  </div>
  {!!results.length&&<div id={resultsId} role="grid" className="search-results" aria-label={t("Результаты поиска")}>{results.map((result,index)=><div
   id={resultsId+'-'+index} role="row" aria-selected={selected===index} key={result.key} className="search-result"
   onFocus={()=>setSelectedKey(result.key)} onClick={()=>setSelectedKey(result.key)}>
   <div role="gridcell"><button className="file-result" onClick={result.open}><Icon name={result.icon}/><span>{result.name}<small>{result.detail}</small></span></button></div>
   {result.file&&<div role="gridcell" className="result-actions"><button className="quiet" onClick={()=>openFile(result.file!,true)}>{t("В Проводнике")}</button><CopyButton text={result.file.path} label={t("Путь")}/></div>}
  </div>)}</div>}
  {expanded&&searchText&&loading&&<Notice>{t("Ищем файлы…")}</Notice>}
  {expanded&&searchText&&message&&<Notice>{t(message)}</Notice>}
  {expanded&&searchText&&!results.length&&!loading&&!message&&<Notice>{t("Ничего не найдено. Попробуйте другое имя.")}</Notice>}
  {error&&<Notice error>{error}</Notice>}
 </div>;
}

export function FavoriteApps({state,setState,openApps,openCodex,active=true}:LocalProps&{openApps:()=>void;openCodex?:()=>void;active?:boolean}) {
 const {error,run}=useAction();const reorder=useAppReorder(state,setState);
 return <div className="favorite-apps"><div className="app-shortcuts">{openCodex&&state.settings.codexShortcut!==false&&<CodexShortcut open={openCodex} active={active}/>} {state.apps.map(app=><button {...reorder.handlers(app.id)} className={`app-shortcut${reorder.dragging===app.id?' is-dragging':''}`} key={app.id} onClick={()=>{if(!reorder.dragging)void run(()=>invoke('apps.launch',{id:app.id}));}} title={app.name}>{app.icon?<img src={app.icon} alt=""/>:<Icon name="apps" size={22}/>}<span>{app.name}</span></button>)}<button className="app-shortcut add-shortcut" onClick={openApps}><Icon name="plus" size={22}/><span>{state.apps.length?t("Все"):t("Добавить")}</span></button></div>{(error||reorder.error)&&<Notice error>{error||reorder.error}</Notice>}</div>;
}
export function Apps({state,setState}:LocalProps) {
 const {error,busy,run}=useAction();const reorder=useAppReorder(state,setState);const [editing,setEditing]=useState('');const [name,setName]=useState('');
 const change=(action:string,params:object={})=>void run(async()=>setState(await invoke<Snapshot>(action,params)));
 function move(index:number,delta:number){const ids=state.apps.map(a=>a.id);[ids[index],ids[index+delta]]=[ids[index+delta],ids[index]];change('apps.reorder',{ids});}
 return <div className="stack"><p>{t("Закрепите программу или ярлык. Перенос в панель добавляет приложение, но не запускает его.")}</p><div className="actions"><button className="primary" disabled={busy||!nativeAvailable} onClick={()=>change('apps.add')}>{t("Добавить приложение")}</button><button disabled={busy||!nativeAvailable} onClick={()=>change('apps.pickWindow')}>{t("Выбрать окно")}</button></div><small>{t("При выборе окна Escape или правая кнопка мыши отменяют выбор. Повторный выбор закреплённого окна открепляет приложение.")}</small>{state.launcherUndoAvailable===true&&<button disabled={busy} onClick={()=>change('apps.undo')}>{t("Отменить выбор окна")}</button>}{!nativeAvailable&&<Notice>{t("Добавление и запуск доступны в приложении Windows.")}</Notice>}{error&&<Notice error>{error}</Notice>}
  {reorder.error&&<Notice error>{reorder.error}</Notice>}<div className="list">{state.apps.map((app,index)=><article className={`launcher-row${reorder.dragging===app.id?' is-dragging':''}`} key={app.id} onDragOver={reorder.handlers(app.id).onDragOver} onDrop={reorder.handlers(app.id).onDrop}><div className="launcher-title" {...reorder.handlers(app.id)} tabIndex={0} role="group" aria-label={app.name}>{app.icon?<img src={app.icon} alt=""/>:<Icon name="apps"/>}<div><strong>{app.name}</strong><small>{app.path}</small></div></div>
   {editing===app.id?<form className="actions" onSubmit={e=>{e.preventDefault();change('apps.rename',{id:app.id,name});setEditing('');}}><label>{t("Название")}<input autoFocus required maxLength={80} value={name} onChange={e=>setName(e.target.value)}/></label><button>{t("Сохранить")}</button><button type="button" onClick={()=>setEditing('')}>{t("Отмена")}</button></form>:<div className="actions"><button disabled={busy} onClick={()=>void run(()=>invoke('apps.launch',{id:app.id}))}>{t("Открыть")}</button><button className="quiet" onClick={()=>{setEditing(app.id);setName(app.name);}}>{t("Имя")}</button><button className="quiet" disabled={busy} onClick={()=>change('apps.icon',{id:app.id})}>{t("Значок")}</button><button className="quiet" disabled={!index||busy} aria-label={t("Выше: {name}",{name:app.name})} onClick={()=>move(index,-1)}>{t("Выше")}</button><button className="quiet" disabled={index===state.apps.length-1||busy} aria-label={t("Ниже: {name}",{name:app.name})} onClick={()=>move(index,1)}>{t("Ниже")}</button><button className="quiet" disabled={busy} onClick={()=>change('apps.remove',{id:app.id})}>{t("Открепить")}</button></div>}
  </article>)}</div><div className="actions"><button onClick={()=>void run(()=>invoke('quick.open',{target:'powershell'}))}>PowerShell</button><button onClick={()=>void run(()=>invoke('quick.open',{target:'cmd'}))}>CMD</button></div>
 </div>;
}

export function Metrics({expanded,compact=false,open}:{expanded:boolean;compact?:boolean;open?:()=>void}) {
 const {value,error}=usePoll<SystemState>('system.get',expanded,5000);
 return <Panel title={t("Текущие показатели ПК")} action={compact&&<button className="quiet" onClick={open}>{t("Подробнее")}</button>}>
  {value?<><dl className="metrics"><div><dt>CPU</dt><dd>{value.cpuPercent==null?t("Недоступно"):`${value.cpuPercent.toFixed(0)}%`}</dd></div><div><dt>{t("Память")}</dt><dd>{bytes(value.memoryUsed)} / {bytes(value.memoryTotal)}</dd></div><div><dt>GPU</dt><dd>{value.gpuPercent==null?t("Недоступно"):`${value.gpuPercent.toFixed(0)}%`}</dd></div><div><dt>{t("Видеопамять")}</dt><dd>{bytes(value.gpuMemoryUsed)} / {bytes(value.gpuMemoryTotal)}</dd></div></dl>{!compact&&<><dl className="detail-metrics"><div><dt>{t("Время работы Windows")}</dt><dd>{t("{days} дн. {hours} ч.",{days:Math.floor(value.uptimeSeconds/86400),hours:Math.floor(value.uptimeSeconds%86400/3600)})}</dd></div><div><dt>{t("Сеть")}</dt><dd>{value.network?.name||t("Нет активного интерфейса")}</dd></div><div><dt>{t("Приём / передача")}</dt><dd>{speed(value.network?.receiveBytesPerSecond)} / {speed(value.network?.sendBytesPerSecond)}</dd></div><div><dt>{t("Питание")}</dt><dd>{value.battery.available?`${value.battery.percent??'—'}% · ${value.battery.charging?t("заряжается"):value.battery.ac?t("от сети"):t("от батареи")}`:value.battery.ac===true?t("От сети"):value.battery.ac==null?t("Недоступно"):t("Батарея не обнаружена")}</dd></div></dl><h3>{t("Диски")}</h3>{value.disks.map(disk=><p key={disk.name}><strong>{disk.name}</strong> · {t("свободно {free} из {total}",{free:bytes(disk.free),total:bytes(disk.total)})}</p>)}<small>{t("Активность дисков, температура и мощность недоступны.")}</small></>}</>:<Notice>{t(error||"Получаем показатели…")}</Notice>}
 </Panel>;
}
export function QuickActions({compact=false,open}:{compact?:boolean;open?:()=>void}) {
 const [scheduledAction,setScheduledAction]=useState<'shutdown'|'restart'|null>(null);
 const {error,busy,run}=useAction();
 const settings:MenuAction[]=[
  {id:'notifications',label:t("Не беспокоить"),icon:'notifications',description:t("Открыть настройки уведомлений Windows. Режим не переключается автоматически.")},
  {id:'sound',label:t("Звук"),icon:'sound',description:t("Открыть настройки устройств и громкости звука Windows.")},
  {id:'power',label:t("Параметры питания"),icon:'battery',description:t("Открыть настройки питания и спящего режима Windows.")},
  {id:'taskmanager',label:t("Диспетчер задач"),icon:'taskmanager',description:t("Открыть процессы и показатели производительности Windows.")},
 ];
 const network:MenuAction[]=[
  {id:'wifi',label:'Wi-Fi',icon:'wifi',description:t("Открыть настройки Wi-Fi Windows для выбора сети и управления подключением.")},
  {id:'bluetooth',label:'Bluetooth',icon:'bluetooth',description:t("Открыть настройки Bluetooth Windows для подключения устройств.")},
 ];
 const consoles:MenuAction[]=[
  {id:'powershell',label:'PowerShell',icon:'powershell',description:t("Открыть консоль PowerShell.")},
  {id:'cmd',label:'CMD',icon:'cmd',description:t("Открыть командную строку Windows.")},
 ];
 const power:MenuAction[]=[
  {id:'lock',label:t("Заблокировать"),icon:'lock',description:t("Заблокировать текущий сеанс Windows. Приложения останутся открыты.")},
  {id:'sleep',label:t("Спящий режим…"),icon:'sleep',description:t("Перевести компьютер в спящий режим после подтверждения.")},
  {id:'restart',label:t("Перезагрузить ПК…"),icon:'restart',description:t("Перезапустить Windows после подтверждения. Сначала сохраните работу.")},
  {id:'shutdown',label:t("Выключить ПК…"),icon:'power',description:t("Выключить компьютер после подтверждения. Сначала сохраните работу.")},
  {id:'shutdownLater',label:t('Выключить через…'),icon:'timer',description:t('Разовое выключение с обратным отсчётом и отменой.')},
  {id:'restartLater',label:t('Перезагрузить через…'),icon:'timer',description:t('Разовая перезагрузка с обратным отсчётом и отменой.')},
 ];
 const openAction=(target:string)=>void run(()=>invoke('quick.open',{target}));
 const menuProps={compact,busy};
 return <div className="stack quick-actions"><div className="section-heading"><h2>{t("Быстрые действия")}</h2>{compact&&<button className="quiet" onClick={open}>{t("Все")}</button>}</div>
  <div className={compact?'quick-icon-grid':'actions'} role="group" aria-label={t("Действия компьютера")}>
   <ActionMenu {...menuProps} label={t("Подключения")} icon="connections" description={t("Настройки Wi-Fi и Bluetooth Windows.")} items={network} onAction={openAction}/>
   {settings.map(item=><button key={item.id} disabled={busy} aria-label={item.label} data-help={item.description} onClick={()=>openAction(item.id)}><Icon name={item.icon} size={20}/>{!compact&&<span>{item.label}</span>}</button>)}
   <ActionMenu {...menuProps} label={t("Консоли")} icon="powershell" description={t("Запуск PowerShell или командной строки Windows.")} items={consoles} onAction={openAction}/>
   <ActionMenu {...menuProps} label={t("Питание")} icon="power" description={t("Блокировка, спящий режим, перезагрузка и выключение компьютера.")} items={power} onAction={action=>{if(action==='shutdownLater'||action==='restartLater')setScheduledAction(action==='shutdownLater'?'shutdown':'restart');else void run(()=>invoke('quick.power',{action}));}}/>
  </div>{scheduledAction&&<PowerScheduleForm key={scheduledAction} action={scheduledAction} onClose={()=>setScheduledAction(null)}/>}{error&&<Notice error>{error}</Notice>}</div>;
}
