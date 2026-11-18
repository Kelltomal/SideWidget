import {t} from '../i18n';
import {useEffect,useRef,useState} from 'react';
import type {MediaSession} from '../types';
import {RemoteClient,RemoteError,type RemoteState} from './client';

type InstallPrompt=Event&{prompt:()=>Promise<void>;userChoice:Promise<{outcome:string}>};
type Pair={id:string;secret:string;code:string};
type Config={secureUrl:string;fingerprint:string;lanAccess:boolean;setupUrl:string};
const paths={play:'m9 5 11 7-11 7Z',pause:'M8 5v14M16 5v14',previous:'M5 5v14m14-14L8 12l11 7Z',next:'M19 5v14M5 5l11 7-11 7Z',music:'M9 18V5l11-2v13M9 5v4l11-2M9 18c0 3-6 3-6 0s6-3 6 0Zm11-2c0 3-6 3-6 0s6-3 6 0Z',sound:'M3 9h4l5-4v14l-5-4H3Zm13-2a7 7 0 0 1 0 10',mute:'M3 9h4l5-4v14l-5-4H3Zm13 0 6 6m0-6-6 6',phone:'M7 2h10v20H7Zm4 17h2',download:'M12 3v12m-5-5 5 5 5-5M4 16v5h16v-5'};
function Glyph({name,size=24}:{name:keyof typeof paths;size?:number}){return <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={paths[name]}/></svg>;}
function time(seconds:number){const n=Math.max(0,Math.floor(seconds));return `${Math.floor(n/60)}:${String(n%60).padStart(2,'0')}`;}

export function RemotePlayer({state,connected,busy,send,selected,onSelect}:{state?:RemoteState;connected:boolean;busy:boolean;send:(body:object)=>void;selected:string;onSelect:(id:string)=>void}){
 const sessions=state?.media.sessions??[];
 const track=sessions.find(s=>s.id===selected);
 const disabled=!connected||busy;
 const action=(command:string,positionSeconds?:number)=>{if(track)send({command,id:track.id,...positionSeconds===undefined?{}:{positionSeconds}});};
 return <>
  {(sessions.length>1||sessions.length>0&&!track)&&<label className="source-picker">{t("Проигрыватель")}<select value={track?selected:''} disabled={disabled} onChange={e=>onSelect(e.target.value)}>{!track&&<option value="" disabled>{t("Выберите доступный источник")}</option>}{sessions.map(s=><option key={s.id} value={s.id}>{s.source} — {s.title||t("Без названия")}</option>)}</select></label>}
  <div className={`remote-art${!connected?' stale':''}`}>{track?.artwork?<img src={track.artwork} alt={t("Обложка альбома")}/>:<Glyph name="music" size={72}/>}</div>
  <div className="remote-track"><h1>{track?.title||(sessions.length?t("Выберите проигрыватель"):state?t("Сейчас ничего не играет"):t("Подключаемся к ПК"))}</h1><p>{track?.artist||(track?t("Исполнитель не указан"):t(state?.media.message||"Откройте Яндекс Музыку на компьютере."))}</p>{!connected&&state&&<p className="stale-note">{t("Последнее полученное состояние")}</p>}</div>
  {track?.positionSeconds!=null&&track.durationSeconds!=null&&track.durationSeconds>0?<RemoteSlider key={track.id+track.title} label={t("Позиция воспроизведения")} value={track.positionSeconds} max={track.durationSeconds} step={1} disabled={disabled||!track.canSeek} commit={v=>action('seek',v)} timeline/>:<p className="remote-time">{track?t("Плеер не передал время трека"):t("Управление появится после запуска музыки")}</p>}
  <div className="remote-controls">
   <button aria-label={t("Предыдущая композиция")} disabled={disabled||!track?.canPrevious} onClick={()=>action('previous')}><Glyph name="previous" size={30}/></button>
   <button className="remote-play" aria-label={track?.playing?t("Пауза"):t("Воспроизвести")} disabled={disabled||!track||(track.playing?!track.canPause:!track.canPlay)} onClick={()=>action(track?.playing?'pause':'play')}><Glyph name={track?.playing?'pause':'play'} size={34}/></button>
   <button aria-label={t("Следующая композиция")} disabled={disabled||!track?.canNext} onClick={()=>action('next')}><Glyph name="next" size={30}/></button>
  </div>
  <section className="remote-volume" aria-label={t("Громкость ПК")}><div className="volume-heading"><h2>{t("Громкость ПК")}</h2><button aria-label={state?.audio.muted?t("Включить звук ПК"):t("Выключить звук ПК")} disabled={disabled||typeof state?.audio.muted!=='boolean'} onClick={()=>send({command:'mute',muted:!state?.audio.muted})}><Glyph name={state?.audio.muted?'mute':'sound'}/></button></div>
   {state?.audio.volume!=null?<RemoteSlider label={t("Общая громкость ПК")} value={state.audio.volume} disabled={disabled} commit={v=>send({command:'volume',volume:v})}/>:<p>{t("Громкость недоступна")}</p>}
  </section>
 </>;
}

function RemoteSlider({label,value,max=1,step=.01,disabled,commit,timeline=false}:{label:string;value:number;max?:number;step?:number;disabled:boolean;commit:(v:number)=>void;timeline?:boolean}){
 const [draft,setDraft]=useState(value);const editing=useRef(false);const dirty=useRef(false);const latest=useRef(value);latest.current=draft;
 useEffect(()=>{if(!editing.current)setDraft(value);},[value]);
 useEffect(()=>{if(disabled){editing.current=false;dirty.current=false;setDraft(value);}},[disabled]);
 function done(){editing.current=false;if(dirty.current&&!disabled){dirty.current=false;commit(latest.current);}}
 function cancel(){editing.current=false;dirty.current=false;setDraft(value);}
 return <label className="remote-slider"><span className="sr-only">{label}</span><input type="range" aria-label={label} min={0} max={max} step={step} value={Math.min(draft,max)} disabled={disabled} onPointerDown={()=>{editing.current=true;}} onChange={e=>{editing.current=true;dirty.current=true;latest.current=Number(e.target.value);setDraft(latest.current);}} onPointerUp={done} onPointerCancel={cancel} onKeyUp={done} onBlur={done}/><span className="remote-slider-values">{timeline?<><span>{time(draft)}</span><span>{time(max)}</span></>:<span>{Math.round(draft*100)}%</span>}</span></label>;
}

function SetupPage({config,error,retry}:{config?:Config;error:string;retry:()=>void}){
 return <div className="remote-setup"><Glyph name="phone" size={40}/><h1>{t("Подготовим пульт")}</h1><p>{t("Один раз настройте защищённое подключение. После этого пульт можно установить из Chrome как приложение.")}</p>
  <ol><li><strong>{t("Скачайте сертификат SideWidget")}</strong><p>{t("Перед установкой сравните его SHA-256 с кодом в SideWidget на ПК.")}</p><a className="remote-button secondary" href="/remote/root.crt" download="SideWidget-Root.crt">{t("Скачать сертификат")}</a>{config&&<code className="fingerprint">{config.fingerprint.match(/.{1,4}/g)?.join(' ')}</code>}</li>
  <li><strong>{t("Установите на Android")}</strong><p>{t("Настройки → Безопасность → Шифрование и учётные данные → Установить сертификат → Сертификат центра сертификации. Выберите скачанный файл. Названия пунктов зависят от телефона.")}</p><p>{t("Это добавляет доверенный центр сертификации. Устанавливайте только свой сертификат, после сверки с ПК.")}</p></li>
  <li><strong>{t("Откройте защищённый пульт")}</strong><p>{t("Затем подключите телефон и выберите в меню Chrome «Установить приложение».")}</p>{config&&<a className="remote-button" href={config.secureUrl+location.hash}>{t("Открыть пульт по HTTPS")}</a>}</li></ol>
  {error&&<p role="alert">{t(error)} <button onClick={retry}>{t("Повторить")}</button></p>}
 </div>;
}

export function PhoneRemoteApp(){
 const [client]=useState(()=>new RemoteClient());
 const secure=location.protocol==='https:';
 const [config,setConfig]=useState<Config>();const [authorized,setAuthorized]=useState(()=>client.hasKey());
 const lan=!secure&&config?.lanAccess===true;const ready=lan||secure&&authorized;
 const [state,setState]=useState<RemoteState>();const [connected,setConnected]=useState(false);const [busy,setBusy]=useState(false);
 const [error,setError]=useState('');const [notice,setNotice]=useState('');const [selected,setSelected]=useState('');const [refresh,setRefresh]=useState(0);
 const [pair,setPair]=useState<Pair>();const [name,setName]=useState(t("Мой Android"));const [pairBusy,setPairBusy]=useState(false);
 const [invite]=useState(()=>new URLSearchParams(location.hash.slice(1)).get('invite')||'');
 const [install,setInstall]=useState<InstallPrompt>();const [installed,setInstalled]=useState(()=>window.matchMedia?.('(display-mode: standalone)').matches??false);
 const revision=useRef(0);const sending=useRef(false);
 useEffect(()=>{if(secure&&location.hash)history.replaceState(null,'',location.pathname);},[secure]);
 async function loadConfig(){try{setConfig(await client.request<Config>('/remote/config'));setError('');}catch(e){setError((e as Error).message);}}
 useEffect(()=>{if(!secure)void loadConfig();},[secure]);
 useEffect(()=>{
  const ready=(event:Event)=>{event.preventDefault();setInstall(event as InstallPrompt);};const done=()=>{setInstalled(true);setInstall(undefined);};
  window.addEventListener('beforeinstallprompt',ready);window.addEventListener('appinstalled',done);
  return()=>{window.removeEventListener('beforeinstallprompt',ready);window.removeEventListener('appinstalled',done);};
 },[]);
 useEffect(()=>{
  if(!ready)return;
  let active=true;let timer:ReturnType<typeof setTimeout>;let waiting=false;let delay=1000;
  async function poll(){
   if(!active||waiting)return;waiting=true;const version=revision.current;
   try{const next=await client.state();if(active&&version===revision.current){setState(next);setConnected(true);setError('');delay=1000;setSelected(current=>current||choose(next.media.sessions)?.id||'');}}
   catch(e){if(active){setConnected(false);setError((e as Error).message);delay=Math.min(10000,delay*2);if(e instanceof RemoteError&&e.status===401){setAuthorized(false);setState(undefined);}}}
   finally{waiting=false;if(active)timer=setTimeout(poll,document.hidden?10000:delay);}
  }
  const wake=()=>{clearTimeout(timer);if(!waiting)void poll();};
  void poll();window.addEventListener('online',wake);document.addEventListener('visibilitychange',wake);
  return()=>{active=false;clearTimeout(timer);window.removeEventListener('online',wake);document.removeEventListener('visibilitychange',wake);};
 },[ready,refresh]);
 useEffect(()=>{
  if(!pair)return;let active=true;let timer:ReturnType<typeof setTimeout>;
  async function poll(){try{
   const result=await client.request<{status:string;token?:string}>('/api/pair/poll',{id:pair!.id,secret:pair!.secret});
   if(!active)return;
   if(result.status==='approved'&&result.token){if(!client.remember(result.token))setNotice(t("Браузер запретил сохранение. После закрытия потребуется новый QR."));setAuthorized(true);setPair(undefined);setError('');return;}
   if(result.status==='denied'){setError(t("Подключение отклонено на ПК. Создайте новый QR."));setPair(undefined);return;}
   timer=setTimeout(poll,1500);
  }catch(e){if(active){setError((e as Error).message);if(e instanceof RemoteError&&e.status===400)setPair(undefined);else timer=setTimeout(poll,3000);}}}
  void poll();return()=>{active=false;clearTimeout(timer);};
 },[pair]);
 async function begin(){if(pairBusy)return;setPairBusy(true);setError('');try{setPair(await client.request<Pair>('/api/pair/start',{invite,name}));}catch(e){setError((e as Error).message);}finally{setPairBusy(false);}}
 async function send(body:object){
  if(sending.current||!connected)return;sending.current=true;setBusy(true);setError('');revision.current++;
  try{await client.command(body);setRefresh(v=>v+1);}catch(e){setError((e as Error).message);setConnected(false);if(e instanceof RemoteError&&e.status===401)setAuthorized(false);setRefresh(v=>v+1);}finally{sending.current=false;setBusy(false);}
 }
 async function installApp(){if(!install)return;await install.prompt();await install.userChoice;setInstall(undefined);}
 return <div className="remote-shell"><header className="remote-header"><span className="remote-brand"><Glyph name="music" size={22}/>SideWidget</span><span className="remote-connection" role="status">{ready?(connected?t("Связь с ПК"):t("Нет связи с ПК")):!secure?t("Настройка"):t("Подключение телефона")}</span></header>
  <main className="remote-main">
   {!secure&&!lan?<SetupPage config={config} error={error} retry={()=>void loadConfig()}/>:<>
    {ready?<RemotePlayer state={state} selected={selected} onSelect={setSelected} connected={connected} busy={busy} send={body=>void send(body)}/>:<div className="remote-pair"><Glyph name="phone" size={44}/><h1>{pair?t("Подтвердите на ПК"):t("Музыка на ПК. Пульт — здесь.")}</h1>
     {pair?<><p>{t("Сравните код и нажмите «Разрешить» в SideWidget на компьютере.")}</p><strong className="pair-code">{pair.code}</strong><p>{t("Ожидаем подтверждение…")}</p></>:invite?<><p>{t("Телефон запомнится после первого подтверждения.")}</p><label>{t("Имя телефона")}<input autoComplete="off" maxLength={60} value={name} onChange={e=>setName(e.target.value)}/></label><button className="remote-button" disabled={pairBusy||!name.trim()} onClick={()=>void begin()}>{pairBusy?t("Подключаем…"):t("Подключить телефон")}</button></>:<><p>{t("На компьютере откройте SideWidget → Музыка → Пульт на телефоне. Отсканируйте QR для подключения.")}</p><button onClick={()=>setRefresh(v=>v+1)}>{t("Проверить подключение")}</button></>}
    </div>}
    {error&&<p className="remote-error" role="alert">{t(error)}</p>}{notice&&<p role="status">{notice}</p>}
    {ready&&!connected&&<button className="remote-retry" onClick={()=>setRefresh(v=>v+1)}>{t("Подключиться снова")}</button>}
    <footer className="remote-footer">{lan?<details><summary>{t("Сохранить пульт")}</summary><p>{t("Добавьте эту страницу в закладки браузера. Открывайте её с любого телефона в той же домашней сети.")}</p></details>:!installed&&(install?<button className="remote-button secondary" onClick={()=>void installApp()}><Glyph name="download" size={18}/>{t("Установить приложение")}</button>:<details><summary>{t("Установить пульт")}</summary><p>{t("В меню Chrome выберите «Установить приложение» или «Добавить на главный экран». Нужны HTTPS и установленный сертификат SideWidget.")}</p></details>)}<p>{t("Домашняя сеть · Звук остаётся на ПК")}</p><details><summary>{t("Если ПК недоступен")}</summary><p>{t("Проверьте питание ПК и домашний Wi-Fi. В VPN включите доступ к локальной сети. В Windows разрешите SideWidget в брандмауэре для частной сети. Если IP ПК изменился, откройте новый QR.")}</p></details></footer>
   </>}
  </main>
 </div>;
}
function choose(sessions:MediaSession[]){return sessions.find(s=>s.playing)??sessions[0];}
