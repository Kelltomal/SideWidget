import {useEffect,useLayoutEffect,useRef,useState} from 'react';
import {createRoot} from 'react-dom/client';
import {bridge} from './bridge';
import type {Settings,Snapshot} from './types';
import {Calculator,Colors,QrTool,Units} from './LocalTools';
import {DesktopCleanup} from './DesktopCleanup';
import {Apps,Audio,FavoriteApps,Metrics,Music,QuickActions,Search} from './WindowsPanels';
import {Calendar,Currency,Diary,Translation,Weather} from './PersonalPanels';
import {Internet} from './Internet';
import {Icon,invoke,nativeAvailable,Notice,useAction} from './ui';
import './style.css';
import {installMotionInput} from './motion';
import {UpdateSettings} from './UpdateSettings';
import {defaultVisualizer,VisualizerSettingsPanel} from './AudioVisualization';
import {ModuleLayout} from './ModuleLayout';
import {TimerPanel,useFocusTimer} from './TimerPanel';
import {ButtonHelp} from './ButtonHelp';
import {useTheme} from './theme';
import {StartupSettings} from './StartupSettings';
import {ShareSettings} from './ShareSettings';
import {DisplaySettings,selectedDisplayIds} from './DisplaySettings';
import {PhoneRemoteSettings} from './PhoneRemoteSettings';

const modules=[['search','Поиск'],['pinnedTools','Закреплённые инструменты'],['apps','Приложения'],['music','Музыка'],['calendar','Календарь, задачи и погода'],['tools','Инструменты'],['metrics','Текущие показатели ПК'],['quick','Быстрые действия']];
const defaultPinnedTools=['calculator','translation','qr'];
const tools=[['calculator','Калькулятор','calculator'],['translation','Перевод','translate'],['qr','QR-код','qr'],['currency','Валюты','currency'],['units','Единицы','units'],['colors','Цвета','color'],['diary','Дневник','diary'],['timer','Таймер','timer'],['desktop','Файлы и папки','folder']];
const titles:Record<string,string>={home:'',timer:'Таймер',settings:'Настройки',apps:'Приложения',music:'Музыка',audio:'Звук',calendar:'Календарь',weather:'Погода',system:'Текущие показатели ПК',quick:'Быстрые действия',...Object.fromEntries(tools.map(([id,title])=>[id,title]))};
const defaultSettings:Settings={width:444,height:620,hideDelay:650,openDelay:100,hotkey:'Ctrl+Alt+Space',monitor:0,position:1,largeMode:false,order:modules.map(([id])=>id),disabled:[]};
const emptyState:Snapshot={settings:defaultSettings,calculatorHistory:[],colorHistory:[],unitFavorites:[],apps:[],drafts:{diary:''},selly:{configured:false,message:'SellyBoard не подключён. События и задачи пока недоступны.'}};
function normalizeState(value:Snapshot):Snapshot {
 const order=[...(value.settings?.order??defaultSettings.order)];
 if(!order.includes('pinnedTools'))order.splice(order.indexOf('search')+1,0,'pinnedTools');
 return {...emptyState,...value,settings:{...defaultSettings,...value.settings,order,disabled:value.settings?.disabled??[]}};
}
function readPreviewState():Snapshot {
 if(nativeAvailable){const theme=new URLSearchParams(location.search).get('theme');return {...emptyState,settings:{...defaultSettings,theme:theme==='light'||theme==='system'?theme:'dark'}};}
 try {const saved=JSON.parse(localStorage.getItem('sidewidget.local.preview')||'null');if(saved?.settings&&Array.isArray(saved.calculatorHistory))return normalizeState(saved);}catch{}
 return emptyState;
}
export function App(){
 const focusTimer=useFocusTimer();
 const [layoutEditing,setLayoutEditing]=useState(false);const layoutEditingRef=useRef(false);layoutEditingRef.current=layoutEditing;
 function editLayout(value:boolean){layoutEditingRef.current=value;setLayoutEditing(value);if(nativeAvailable)void invoke('shell.editing',{value}).catch(()=>{});}
 useEffect(()=>installMotionInput(document.documentElement),[]);
 const [state,setRawState]=useState<Snapshot>(readPreviewState);const [page,setPage]=useState('home');const [expanded,setExpanded]=useState(true);const [pinned,setPinned]=useState(false);const [startupError,setStartupError]=useState('');const main=useRef<HTMLElement>(null);const pageRef=useRef(page);pageRef.current=page;
 function setState(value:Snapshot){setRawState(normalizeState(value));}
 const [homeReady,setHomeReady]=useState(!nativeAvailable);
 const [searchRequest,setSearchRequest]=useState<number|null>(null);
 const searchInput=useRef<HTMLInputElement>(null);
 const returnToSearch=useRef(false);
 const [translationText,setTranslationText]=useState('');
 const [translationResult,setTranslationResult]=useState('');
 const [calculatorExpression,setCalculatorExpression]=useState('');
 const [qrText,setQrText]=useState('');
 const [,setMinute]=useState(0);
 const shell=(action:string,params:object={})=>{if(nativeAvailable)void invoke(action,params).catch(()=>{});};
 function go(next:string){if(!(next in titles))return;editLayout(false);setPage(next);shell('shell.editing',{value:next!=='home'});if(main.current)main.current.scrollTop=0;}
 function openFromSearch(next:string){returnToSearch.current=true;go(next);}
 useEffect(()=>{
  if(nativeAvailable)void invoke<Snapshot>('bridge.ready').then(value=>{setState(value);setHomeReady(true);}).catch(e=>setStartupError(e.message));
  return bridge.subscribe(event=>{
   if(event.type==='panel'){setPinned(event.data.pinned);setExpanded(event.data.expanded);if(!event.data.expanded)setSearchRequest(null);}
   if(event.type==='search.focus'){returnToSearch.current=false;go('home');setExpanded(true);setSearchRequest(event.data.id);}
   if(event.type==='settings.changed')setRawState(current=>normalizeState({...current,settings:event.data.settings}));
   if(event.type==='state.changed')setState(event.data);
   if(event.type==='displays.changed')setRawState(current=>({...current,displays:event.data}));
   if(event.type==='navigate')go(event.data.page);
   if(event.type==='notice'&&typeof event.data?.message==='string')setStartupError(event.data.message);
   if(event.type==='launcher.changed')void invoke<Snapshot>('state.get').then(setState).catch(e=>setStartupError(e.message));
  });
 },[]);
 useLayoutEffect(()=>{if(homeReady&&nativeAvailable)shell('shell.homeReady');},[homeReady]);
 useLayoutEffect(()=>{
  if(!homeReady||!expanded||page!=='home'||searchRequest===null)return;
  const input=searchInput.current;
  if(input){
   input.focus();
   input.select();
   if(document.activeElement===input)shell('shell.searchFocused',{id:searchRequest});
  }
  setSearchRequest(null);
 },[homeReady,expanded,page,searchRequest]);
 useLayoutEffect(()=>{
  if(page!=='home'||!returnToSearch.current)return;
  returnToSearch.current=false;
  const input=searchInput.current;
  input?.focus();
  const active=input?.getAttribute('aria-activedescendant');
  if(active)document.getElementById(active)?.scrollIntoView?.({block:'nearest'});
 },[page]);
 useEffect(()=>{if(!nativeAvailable)try{localStorage.setItem('sidewidget.local.preview',JSON.stringify(state));}catch{setStartupError('Локальное хранилище заполнено. Скопируйте нужные данные перед закрытием.');}},[state]);
 useEffect(()=>{if(!expanded)return;const timer=setInterval(()=>setMinute(value=>value+1),60000);return()=>clearInterval(timer);},[expanded]);
 useEffect(()=>{function escape(event:KeyboardEvent){if(event.key!=='Escape'||event.defaultPrevented)return;if(pageRef.current!=='home'){event.preventDefault();go('home');}else shell('shell.collapse');}window.addEventListener('keydown',escape);return()=>window.removeEventListener('keydown',escape);},[]);
 const localProps={state,setState};
 useTheme(state.settings.theme??'dark');
 const visibleTools=tools.filter(([id])=>!state.settings.disabled.includes(id));
 const pinnedTools=state.settings.disabled.includes('tools')?[]:(state.settings.pinnedTools??defaultPinnedTools)
  .flatMap(id=>visibleTools.filter(([tool])=>tool===id));
 function toolButton([id,title,icon]:string[]){return <button key={id} onClick={()=>go(id)}><Icon name={icon} size={21}/><span>{title}</span></button>;}
 function homeModule(id:string){switch(id){
  case 'search':return <Search expanded={expanded&&page==='home'} inputRef={searchInput}
   tools={state.settings.disabled.includes('tools')?[]:visibleTools.map(([id,name,icon])=>({id,name,icon}))}
   apps={state.apps} openTool={openFromSearch}/>;
  case 'pinnedTools':return pinnedTools.length>0&&<div role="group" aria-label="Закреплённые инструменты" className="tool-grid pinned-tools">{pinnedTools.map(toolButton)}</div>;
  case 'apps':return <FavoriteApps {...localProps} openApps={()=>go('apps')}/>;
  case 'music':return <Music expanded={expanded} openAudio={()=>go('audio')}/>;
  case 'calendar':return <div className="day-overview"><Calendar state={state} compact open={()=>go('calendar')}/><Weather compact open={()=>go('weather')}/></div>;
  case 'tools':{
   const remaining=visibleTools.filter(tool=>state.settings.disabled.includes('pinnedTools')||!pinnedTools.includes(tool));
   return remaining.length>0&&<section className="tool-section"><h2>Под рукой</h2><div className="tool-grid">{remaining.map(toolButton)}</div></section>;
  }
  case 'metrics':return <Metrics expanded={expanded} compact open={()=>go('system')}/>;
  case 'quick':return <QuickActions compact open={()=>go('quick')}/>;
  default:return null;
 }}
 return <div className={`app ${state.settings.largeMode?'large-mode':''}`} onPointerDown={()=>shell('shell.interactive')} onFocusCapture={event=>{if((event.target as HTMLElement).matches('input,textarea,select,button[aria-expanded="true"]'))shell('shell.editing',{value:true});}} onBlurCapture={()=>{setTimeout(()=>{const focused=document.activeElement;if(pageRef.current==='home'&&!layoutEditingRef.current&&!(focused instanceof HTMLElement&&focused.matches('input,textarea,select')))shell('shell.editing',{value:false});},0);}}>
  <nav className="toolbar" aria-label="Управление панелью"><div className="toolbar-title">{page!=='home'&&<><button className="icon-button" onClick={()=>go('home')} aria-label="Вернуться на главную"><Icon name="back"/></button><h1>{titles[page]}</h1></>}</div><div className="toolbar-actions">{page==='home'&&<button className="icon-button" aria-label={layoutEditing?'Завершить редактирование':'Редактировать расположение'} aria-pressed={layoutEditing} onClick={()=>editLayout(!layoutEditing)}><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" aria-hidden="true"><path d="m4 16 11-11 4 4L8 20H4Zm9-9 4 4"/></svg></button>}<button className="icon-button" aria-label={pinned?'Открепить панель':'Закрепить панель'} aria-pressed={pinned} disabled={!nativeAvailable} onClick={()=>shell('shell.pin')}><Icon name="pin" size={16}/></button><button className="icon-button" aria-label="Настройки" aria-pressed={page==='settings'} onClick={()=>go(page==='settings'?'home':'settings')}><Icon name="gear" size={17}/></button></div></nav>
  <ButtonHelp/>
  <main ref={main}>
   {startupError&&<Notice error>{startupError} <button className="quiet" onClick={()=>setStartupError('')}>Закрыть сообщение</button></Notice>}
   <div className="page-content" hidden={page!=='home'}><ModuleLayout state={state} setState={setState} editing={layoutEditing} finish={()=>editLayout(false)} modules={modules} renderModule={id=>id==='search'||page==='home'?homeModule(id):null}/></div>
   <div className="page-content" key={page}>
   {page==='timer'&&<TimerPanel timer={focusTimer}/>}
   {page==='desktop'&&<DesktopCleanup/>}
   {page==='calculator'&&<Calculator {...localProps} sessionExpression={[calculatorExpression,setCalculatorExpression]}/>}{page==='qr'&&<QrTool sessionText={[qrText,setQrText]}/>}{page==='units'&&<Units {...localProps}/>}{page==='colors'&&<Colors {...localProps}/>}
   {page==='translation'&&<Translation sessionText={[translationText,setTranslationText]} sessionResult={[translationResult,setTranslationResult]}/>}{page==='currency'&&<Currency/>}{page==='diary'&&<Diary {...localProps}/>}
   {page==='apps'&&<Apps {...localProps}/>}{page==='audio'&&<div className="stack"><Audio expanded={expanded}/><PhoneRemoteSettings active={expanded}/></div>}{page==='calendar'&&<Calendar state={state}/>}{page==='weather'&&<Weather/>}
   {page==='music'&&<Music expanded={expanded} openAudio={()=>go('audio')}/>}
   {page==='system'&&<div className="stack"><Internet expanded={expanded}/><Metrics expanded={expanded}/></div>}{page==='quick'&&<QuickActions/>}{page==='settings'&&<SettingsPage state={state} setState={setState}/>}
   </div>
   {(page==='home'||page==='settings')&&state.appVersion&&<footer className="app-version"><small>Версия {state.appVersion}</small></footer>}
  </main>
 </div>;
}
export function SettingsPage({state,setState}:{state:Snapshot;setState:(state:Snapshot)=>void}) {
 const [settings,setSettings]=useState(state.settings);const {error,busy,run}=useAction();const [saved,setSaved]=useState(false);
 const [positionChanges,setPositionChanges]=useState<Record<string,number>>({});
 const previousSettings=useRef(state.settings);
 useEffect(()=>{
  const before=previousSettings.current;previousSettings.current=state.settings;
  setSettings(current=>Object.fromEntries(Object.keys({...current,...state.settings}).map(key=>{
   const name=key as keyof Settings;
   return [key,JSON.stringify(current[name])===JSON.stringify(before[name])?state.settings[name]:current[name]];
  })) as Settings);
 },[state.settings]);
 const order=[...settings.order,...modules.map(([id])=>id)].filter((id,index,list)=>modules.some(([key])=>key===id)&&list.indexOf(id)===index);
 function field<K extends keyof Settings>(key:K,value:Settings[K]){setSaved(false);setSettings(s=>({...s,[key]:value}));}
 function move(index:number,delta:number){const next=[...order];[next[index],next[index+delta]]=[next[index+delta],next[index]];field('order',next);}
 function enabled(id:string,value:boolean){field('disabled',value?settings.disabled.filter(key=>key!==id):[...settings.disabled,id]);}
 const pinnedTools=settings.pinnedTools??defaultPinnedTools;
 async function save(){await run(async()=>{
  const ids=selectedDisplayIds(settings,state.displays??[]);
  const next={...settings,displayIds:ids.length?ids:settings.displayIds};
  setState(nativeAvailable?await invoke<Snapshot>('settings.save',{...next,displayPositions:positionChanges}):{...state,settings:{...next,displayPositions:{...next.displayPositions,...positionChanges}}});
  setPositionChanges({});setSaved(true);
 });}
 async function changeTheme(theme:NonNullable<Settings['theme']>){await run(async()=>{const next=nativeAvailable?await invoke<Snapshot>('settings.theme',{theme}):{...state,settings:{...state.settings,theme}};setState(next);setSettings(current=>({...current,theme:next.settings.theme??'dark'}));});}
 return <form className="stack settings" onSubmit={e=>{e.preventDefault();void save();}}><StartupSettings/><h2 id="theme-label">Оформление</h2><div className="theme-options" role="group" aria-labelledby="theme-label">{([['dark','Тёмная'],['light','Светлая'],['system','Как в Windows']] as const).map(([theme,label])=><button type="button" key={theme} aria-pressed={(state.settings.theme??'dark')===theme} disabled={busy} onClick={()=>void changeTheme(theme)}>{label}</button>)}</div><small>Тема применяется и сохраняется сразу. «Как в Windows» следует системной теме приложений.</small><VisualizerSettingsPanel value={state.settings.visualizer??defaultVisualizer} onPreviewSaved={visualizer=>{setState({...state,settings:{...state.settings,visualizer}});setSettings(current=>({...current,visualizer}));}} onSaved={next=>{setState(next);setSettings(current=>({...current,visualizer:next.settings.visualizer}));}}/><h2>Размер и положение</h2><div className="form-row"><label>Ширина шапки, DIP<input type="number" min={360} max={900} required value={settings.width} onChange={e=>field('width',Number(e.target.value))}/></label><label>Высота, DIP<input type="number" min={360} max={1200} required value={settings.height} onChange={e=>field('height',Number(e.target.value))}/></label></div><small>При открытии ширина увеличивается на 40% с каждой стороны. У края экрана панель сдвигается внутрь.</small>
  <DisplaySettings settings={{...settings,displayPositions:{...settings.displayPositions,...positionChanges}}} displays={state.displays??[]} onChange={ids=>field('displayIds',ids)} onPositionChange={(id,position)=>{setSaved(false);setPositionChanges(current=>({...current,[id]:position}));}}/>
  <label className="checkbox"><input type="checkbox" checked={settings.largeMode} onChange={e=>field('largeMode',e.target.checked)}/>Крупный текст и элементы управления</label>
  <h2>Раскрытие</h2><div className="form-row"><label>Задержка открытия, мс<input type="number" min={0} max={2000} required value={settings.openDelay} onChange={e=>field('openDelay',Number(e.target.value))}/></label><label>Задержка скрытия, мс<input type="number" min={200} max={5000} required value={settings.hideDelay} onChange={e=>field('hideDelay',Number(e.target.value))}/></label></div><label>Горячая клавиша<input value={settings.hotkey} required maxLength={60} placeholder="Ctrl+Alt+Space" onChange={e=>field('hotkey',e.target.value)}/></label><small>Если сочетание занято, Windows вернёт ошибку при сохранении.</small>
  <h2>Главная</h2><div className="layout-list">{order.map((id,index)=><div className="layout-row" key={id}><label className="checkbox"><input type="checkbox" checked={!settings.disabled.includes(id)} onChange={e=>enabled(id,e.target.checked)}/>{modules.find(([key])=>key===id)![1]}</label><button type="button" className="quiet" disabled={index===0} onClick={()=>move(index,-1)} aria-label={`Выше: ${id}`}>Выше</button><button type="button" className="quiet" disabled={index===order.length-1} onClick={()=>move(index,1)} aria-label={`Ниже: ${id}`}>Ниже</button></div>)}</div>
  <h2>Инструменты на главной</h2>{tools.map(([id,title])=><label className="checkbox" key={id}><input type="checkbox" checked={!settings.disabled.includes(id)} onChange={e=>enabled(id,e.target.checked)}/>{title}</label>)}
  <fieldset className="tool-pin-settings"><legend>Закреплённые инструменты</legend><small>Выберите до четырёх инструментов для быстрого доступа.</small>{tools.map(([id,title])=><label className="checkbox" key={id}><input type="checkbox" aria-label={`Закрепить ${title}`} checked={pinnedTools.includes(id)} disabled={!pinnedTools.includes(id)&&(pinnedTools.length>=4||settings.disabled.includes(id))} onChange={e=>field('pinnedTools',e.target.checked?[...pinnedTools,id]:pinnedTools.filter(tool=>tool!==id))}/>{title}</label>)}</fieldset>
  {error&&<Notice error>{error}</Notice>}{saved&&<Notice>Настройки сохранены{nativeAvailable?'.':' для предпросмотра.'}</Notice>}<button className="primary" disabled={busy}>{busy?'Сохраняем…':'Сохранить настройки'}</button><button type="button" onClick={()=>{setSaved(false);setSettings(s=>({...s,order:modules.map(([id])=>id),disabled:[],pinnedTools:defaultPinnedTools}));}}>Вернуть исходную раскладку</button>
 <ShareSettings/><UpdateSettings/></form>;
}
const rootElement=document.getElementById('root');
if(rootElement)createRoot(rootElement).render(<App/>);

