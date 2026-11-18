import {useEffect,useLayoutEffect,useRef,useState,type FormEvent} from 'react';
import {locale,t,useLocale} from './i18n';
import {Icon,invoke,nativeAvailable,Notice} from './ui';
import './site-monitor.css';

type SiteCheck={checkedAt:string;status:'up'|'down'|'restricted'|'unknown';httpStatus:number|null;responseMs:number|null;message:string;reportStatus:'reports'|'none'|'unknown';reportCount:number|null};
export type MonitoredSite={id:string;url:string;reportUrl:string;enabled:boolean;intervalMinutes:number;nextCheckAt:string|null;lastCheck:SiteCheck|null;history:SiteCheck[]};
type SiteDraft=Pick<MonitoredSite,'id'|'url'|'reportUrl'|'enabled'|'intervalMinutes'>;
type SiteData={sites:MonitoredSite[];message?:string};
const key='sidewidget.sites.v1';
const labels={up:'Доступен',down:'Недоступен',restricted:'Доступ ограничен',unknown:'Результат неизвестен'};
const time=(value:string|null)=>value&&Number.isFinite(Date.parse(value))?new Date(value).toLocaleString(locale()):t('Неизвестно');
function hostname(url:string){try{return new URL(url).host;}catch{return url;}}
function webUrl(value:string){const url=new URL(value);if(!['http:','https:'].includes(url.protocol)||url.username||url.password)throw new Error('invalid');return url.href;}
function readPreview():MonitoredSite[]{const value:unknown=JSON.parse(localStorage.getItem(key)||'[]');if(!Array.isArray(value)||value.some(site=>!site||typeof site.id!=='string'||typeof site.url!=='string'||typeof site.reportUrl!=='string'||typeof site.enabled!=='boolean'||!Number.isInteger(site.intervalMinutes)))throw new Error('invalid');return value.map(site=>({...site,nextCheckAt:null,lastCheck:null,history:[]}));}
function Report({check}:{check:SiteCheck|null}){return <small>{!check||check.reportStatus==='unknown'?t('Сведения о жалобах недоступны'):check.reportStatus==='reports'?(check.reportCount===null?t('Есть сообщения о сбоях'):t('Сообщений о сбоях: {count}',{count:check.reportCount})):t('Источник не сообщает о сбоях')}</small>;}

export function SiteMonitorPanel(){
 useLocale();const [sites,setSites]=useState<MonitoredSite[]>([]);const [loaded,setLoaded]=useState(!nativeAvailable);const [draft,setDraft]=useState<SiteDraft|null>(null);const [editing,setEditing]=useState(false);const [error,setError]=useState('');const [busy,setBusy]=useState(false);const [status,setStatus]=useState('');const [serverMessage,setServerMessage]=useState('');
 const mounted=useRef(false);const revision=useRef(0);const acting=useRef(false);const address=useRef<HTMLInputElement>(null);const addButton=useRef<HTMLButtonElement>(null);const returnFocus=useRef<HTMLElement|null>(null);
 useEffect(()=>{
  mounted.current=true;let timer:ReturnType<typeof setTimeout>;
  if(!nativeAvailable){try{setSites(readPreview());}catch{setError(t('Не удалось прочитать список сайтов. Сохранённые данные не изменены.'));}return()=>{mounted.current=false;};}
  async function poll(){const version=revision.current;try{if(!acting.current){const next=await invoke<SiteData>('sites.get');if(mounted.current&&version===revision.current){setSites(next.sites);setServerMessage(next.message||'');setLoaded(true);}}}catch{if(mounted.current&&version===revision.current){setError(t('Не удалось обновить сайты. Повторите проверку.'));setLoaded(true);}}finally{if(mounted.current)timer=setTimeout(poll,2000);}}
  void poll();return()=>{mounted.current=false;revision.current++;clearTimeout(timer);};
 },[]);
 useLayoutEffect(()=>{if(draft)address.current?.focus();else if(returnFocus.current){(returnFocus.current.isConnected?returnFocus.current:addButton.current)?.focus();returnFocus.current=null;}},[draft?.id]);
 function open(site?:MonitoredSite){returnFocus.current=document.activeElement instanceof HTMLElement?document.activeElement:null;setError('');setStatus('');setEditing(!!site);setDraft(site?{id:site.id,url:site.url,reportUrl:site.reportUrl,enabled:site.enabled,intervalMinutes:site.intervalMinutes}:{id:crypto.randomUUID(),url:'',reportUrl:'',enabled:true,intervalMinutes:5});}
 async function action(command:string,params?:object){
  if(acting.current)return false;acting.current=true;setBusy(true);setError('');setStatus('');const version=++revision.current;
  try{
   let next:SiteData;
   if(nativeAvailable){const result=params?await invoke<SiteData>(command,params):await invoke<SiteData>(command);if(command==='links.open')return true;next=result;}
   else{const stored=readPreview();if(command==='sites.save'&&params&&'site' in params){const site=params.site as SiteDraft;next={sites:[...stored.filter(item=>item.id!==site.id),{...site,lastCheck:null,nextCheckAt:null,history:[]}]};}else if(command==='sites.delete'&&params&&'id' in params)next={sites:stored.filter(item=>item.id!==params.id)};else return false;localStorage.setItem(key,JSON.stringify(next.sites));}
   if(mounted.current&&version===revision.current){setSites(next.sites);setServerMessage(next.message||'');setLoaded(true);setStatus(t(command==='sites.delete'?'Сайт удалён.':command==='sites.save'?'Настройки сайта сохранены.':'Проверка завершена.'));}return true;
  }catch{if(mounted.current)setError(t('Не удалось выполнить действие с сайтом. Проверьте адрес и повторите попытку.'));return false;}
  finally{acting.current=false;if(mounted.current)setBusy(false);}
 }
 async function save(event:FormEvent){event.preventDefault();if(!draft)return;let site:SiteDraft;try{site={...draft,url:webUrl(draft.url.trim()),reportUrl:draft.reportUrl.trim()?webUrl(draft.reportUrl.trim()):''};if(!Number.isInteger(site.intervalMinutes)||site.intervalMinutes<1||site.intervalMinutes>1440)throw new Error('invalid');}catch{setError(t('Укажите HTTP(S)-адрес без пароля и интервал от 1 до 1440 минут.'));return;}if(await action('sites.save',{site}))setDraft(null);}
 return <div className="site-monitor stack">
  <p>{t('Проверяем доступность с вашего ПК. Сообщения о массовых сбоях показываем отдельно по публичному источнику.')}</p>
  {!nativeAvailable&&<Notice>{t('В браузере можно сохранить адреса. Проверки и открытие источника доступны в SideWidget для Windows.')}</Notice>}
  <div className="actions site-monitor-actions"><button ref={addButton} type="button" className="primary" disabled={busy||!loaded} onClick={()=>open()}>{t('Добавить сайт')}</button><button type="button" disabled={busy||!nativeAvailable||!sites.length} onClick={()=>void action('sites.check')}>{t('Проверить все')}</button></div>
  {error&&<Notice error>{error}</Notice>}{status&&<Notice>{status}</Notice>}{!loaded&&<Notice>{t('Загружаем сайты…')}</Notice>}
  {serverMessage&&<Notice error>{t(serverMessage)}</Notice>}
  {draft&&<form className="panel site-monitor-editor" aria-label={t(editing?'Настройки сайта':'Новый сайт')} onSubmit={event=>void save(event)}>
   <h2>{t(editing?'Настройки сайта':'Новый сайт')}</h2>
   <label>{t('Адрес сайта')}<input ref={address} type="url" required value={draft.url} disabled={busy} placeholder="https://example.com" onChange={e=>setDraft({...draft,url:e.target.value})}/></label>
   <label>{t('Страница источника жалоб')}<input type="url" value={draft.reportUrl} disabled={busy} placeholder="https://downradar.ru/…" onChange={e=>setDraft({...draft,reportUrl:e.target.value})}/></label>
   <small>{t('Необязательно. Без ссылки подберём страницу DownRadar. Если источник недоступен, количество жалоб неизвестно.')}</small>
   <label>{t('Интервал, мин')}<input type="number" required min={1} max={1440} step={1} value={draft.intervalMinutes} disabled={busy} onChange={e=>setDraft({...draft,intervalMinutes:Number(e.target.value)})}/></label>
   <label className="site-monitor-check"><input type="checkbox" checked={draft.enabled} disabled={busy} onChange={e=>setDraft({...draft,enabled:e.target.checked})}/>{t('Проверять автоматически')}</label>
   <small>{t('Автоматические проверки работают, пока SideWidget запущен.')}</small>
   <div className="actions"><button type="submit" className="primary" disabled={busy}>{t('Сохранить сайт')}</button><button type="button" disabled={busy} onClick={()=>{setDraft(null);setError('');}}>{t('Отмена')}</button>{editing&&<button type="button" disabled={busy} onClick={()=>void action('sites.delete',{id:draft.id}).then(ok=>{if(ok)setDraft(null);})}>{t('Удалить сайт')}</button>}</div>
  </form>}
  {loaded&&!sites.length&&!draft&&<Notice>{t('Добавьте адрес сайта для проверки.')}</Notice>}
  {sites.map(site=>{const check=site.lastCheck;const name=hostname(site.url);return <section className="panel site-monitor-site" key={site.id} aria-label={name}>
   <div className="section-heading"><h2>{name}</h2><button type="button" className="icon-button" disabled={busy} aria-label={t('Настроить {site}',{site:name})} onClick={()=>open(site)}><Icon name="settings" size={17}/></button></div>
   <small className="site-monitor-url">{site.url}</small>
   <div className="site-monitor-result"><strong>{check?t(labels[check.status]||labels.unknown):t('Ещё не проверен')}</strong><small>{t('Проверка с этого ПК')}</small></div>
   {check&&<><div className="actions site-monitor-facts">{check.httpStatus!==null&&<span>{t('HTTP {status}',{status:check.httpStatus})}</span>}{check.responseMs!==null&&<span>{t('{time} мс',{time:Math.round(check.responseMs)})}</span>}</div><small>{t('Проверено: {time}',{time:time(check.checkedAt)})}</small></>}
   <small>{site.enabled?t('Каждые {minutes} мин · следующая проверка: {time}',{minutes:site.intervalMinutes,time:nativeAvailable?time(site.nextCheckAt):t('Только в Windows')}):t('Автоматические проверки выключены')}</small>
   <div className="site-monitor-reports"><h3>{t('Публичные сообщения о сбоях')}</h3><Report check={check}/>{site.reportUrl&&<button type="button" className="quiet" disabled={busy||!nativeAvailable} aria-label={t('Открыть источник жалоб: {site}',{site:name})} onClick={()=>void action('links.open',{url:site.reportUrl})}>{t('Открыть источник: {source}',{source:hostname(site.reportUrl)})}</button>}</div>
   <button type="button" disabled={busy||!nativeAvailable} aria-label={t('Проверить {site}',{site:name})} onClick={()=>void action('sites.check',{id:site.id})}>{t('Проверить сейчас')}</button>
   <details className="site-monitor-history"><summary>{t('История проверок')}</summary>{site.history.length?<ul>{site.history.slice(0,20).map((item,index)=><li key={item.checkedAt+index}><span>{time(item.checkedAt)}</span><strong>{t(labels[item.status]||labels.unknown)}</strong>{item.httpStatus!==null&&<small>{t('HTTP {status}',{status:item.httpStatus})}</small>}<Report check={item}/></li>)}</ul>:<small>{t('История пока пуста.')}</small>}</details>
  </section>;})}
 </div>;
}
