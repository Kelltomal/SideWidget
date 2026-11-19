import {useEffect,useRef,useState} from 'react';
import {locale,t,useLocale} from './i18n';
import {invoke,nativeAvailable,Notice,timeLabel} from './ui';
import './codex.css';

type CodexThread={id:string;title:string;status:'running'|'completed'|'failed'|'interrupted'|'unknown';turnId:string|null;updatedAt:string|null;hasQueuedInput:boolean};
type LimitWindow={usedPercent:number;windowMinutes:number;resetsAt:string|null};
export type CodexSnapshot={available:boolean;checkedAt:string;threads:CodexThread[];message?:string;limits:{available:boolean;checkedAt:string;buckets:{id:string;name?:string;primary?:LimitWindow;secondary?:LimitWindow}[];message?:string};shutdown:null|{mode:'chats'|'limit';phase:'waiting'|'countdown';ids:string[];deadline:string|null};limitUsedThisBoot:boolean};
const statusLabels:Record<CodexThread['status'],string>={running:'Работает',completed:'Завершён',failed:'Ошибка',interrupted:'Прерван',unknown:'Статус неизвестен'};
const active=(thread:CodexThread)=>thread.status==='running'||thread.hasQueuedInput;
function dateLabel(value:string|null){if(!value||!Number.isFinite(Date.parse(value))||new Date(value).getFullYear()<2000)return t('Неизвестно');return new Date(value).toLocaleString(locale());}
function windowLabel(minutes:number){return minutes===300?t('5 часов'):minutes===10080?t('Неделя'):t('{minutes} мин',{minutes});}
function quotaWindows(bucket:CodexSnapshot['limits']['buckets'][number]){
 const supplied=[bucket.primary,bucket.secondary].filter((window):window is LimitWindow=>!!window);
 const durations=bucket.id==='codex'?[300,10080,...supplied.map(window=>window.windowMinutes)]:supplied.map(window=>window.windowMinutes);
 return [...new Set(durations)].map(minutes=>({minutes,window:supplied.find(window=>window.windowMinutes===minutes)}));
}

export function CodexPanel({shortcutEnabled=true,onShortcutChange}:{shortcutEnabled?:boolean;onShortcutChange?:(enabled:boolean)=>Promise<void>}={}){
 useLocale();
 const [info,setInfo]=useState<CodexSnapshot>();const [error,setError]=useState('');const [busy,setBusy]=useState(false);const [selected,setSelected]=useState<string[]>([]);const [now,setNow]=useState(Date.now());
 const revision=useRef(0);const acting=useRef(false);const mounted=useRef(false);
 useEffect(()=>{
  mounted.current=true;if(!nativeAvailable)return()=>{mounted.current=false;};let timer:ReturnType<typeof setTimeout>;
  async function poll(){const version=revision.current;try{if(!acting.current){const next=await invoke<CodexSnapshot>('codex.get');if(mounted.current&&version===revision.current){setInfo(next);setError('');}}}catch(e){if(mounted.current&&version===revision.current)setError(e instanceof Error?e.message:t('Не удалось обновить Codex. Повторите попытку.'));}finally{if(mounted.current)timer=setTimeout(poll,2000);}}
  void poll();return()=>{mounted.current=false;revision.current++;clearTimeout(timer);};
 },[]);
 useEffect(()=>{if(info)setSelected(ids=>ids.filter(id=>info.threads.some(thread=>thread.id===id&&active(thread))));},[info]);
 useEffect(()=>{if(info?.shutdown?.phase!=='countdown')return;setNow(Date.now());const timer=setInterval(()=>setNow(Date.now()),1000);return()=>clearInterval(timer);},[info?.shutdown?.phase,info?.shutdown?.deadline]);
 async function action(command:string,params?:object){
  if(!nativeAvailable||acting.current)return;acting.current=true;setBusy(true);setError('');const version=++revision.current;
  try{const next=params?await invoke<CodexSnapshot>(command,params):await invoke<CodexSnapshot>(command);if(mounted.current&&version===revision.current){setInfo(next);setNow(Date.now());}}
  catch(e){if(mounted.current&&!(e instanceof Error&&'code' in e&&e.code==='cancelled'))setError(e instanceof Error?e.message:t('Не удалось выполнить действие. Повторите попытку.'));}
  finally{acting.current=false;if(mounted.current)setBusy(false);}
 }
 if(!nativeAvailable)return <Notice>{t('Мониторинг доступен в SideWidget для Windows.')}</Notice>;
 const watching=info?.shutdown;const limitOn=watching?.mode==='limit';const activeIds=info?.threads.filter(active).map(thread=>thread.id)??[];const chosen=selected.filter(id=>activeIds.includes(id));
 const deadline=watching?.deadline?Date.parse(watching.deadline):NaN;const remaining=Number.isFinite(deadline)?Math.max(0,Math.ceil((deadline-now)/1000)):null;
 return <div className="codex-panel stack">
  <div className="section-heading"><h2>{t('Чаты Codex')}</h2><button type="button" className="quiet" disabled={busy} onClick={()=>void action('codex.refresh')}>{busy?t('Обновляем…'):t('Обновить')}</button></div>
  {error&&<Notice error>{t(error)}</Notice>}
  {!info&&!error&&<Notice>{t('Получаем данные Codex…')}</Notice>}
  {watching&&<section className="panel codex-shutdown" aria-label={t('Выключение ПК')}>
   <p role="status">{watching.phase==='countdown'?(remaining===null?t('Подготовка выключения ПК'):t('Выключение ПК через {time}',{time:timeLabel(remaining)})):limitOn?t('Ожидаем исчерпания лимита Codex'):t('Ожидаем завершения выбранных чатов')}</p>
   {watching.mode==='chats'&&<small>{t('Отслеживаем чатов: {count}',{count:watching.ids.length})}</small>}
   <button type="button" disabled={busy} onClick={()=>void action('codex.cancel')}>{t('Отменить выключение')}</button>
  </section>}
  {info&&<>
   {info.available&&info.message&&<Notice>{t(info.message)}</Notice>}
   {!info.available?<Notice>{t(info.message||'Данные Codex недоступны. Откройте Codex и обновите список.')}</Notice>:<>
    {info.threads.length===0?<Notice>{t('Локальные чаты Codex не найдены.')}</Notice>:<ul className="codex-threads" aria-label={t('Чаты Codex')}>{info.threads.map(thread=><li key={thread.id}>
     <label className="codex-thread"><input type="checkbox" checked={chosen.includes(thread.id)} disabled={busy||!active(thread)} onChange={e=>setSelected(ids=>e.target.checked?[...ids,thread.id]:ids.filter(id=>id!==thread.id))}/><span><strong>{thread.title||t('Без названия')}</strong><small>{t(statusLabels[thread.status]||statusLabels.unknown)}{thread.hasQueuedInput&&<> · {t('В очереди есть запрос')}</>}</small></span></label>
    </li>)}</ul>}
    <div className="codex-arm actions"><button type="button" className="primary" disabled={busy||!chosen.length} onClick={()=>void action('codex.arm',{mode:'selected',ids:chosen})}>{t('После выбранных чатов')}</button><button type="button" disabled={busy||!activeIds.length} onClick={()=>void action('codex.arm',{mode:'all'})}>{t('После всех текущих активных')}</button></div>
    <small>{t('Только текущие активные чаты. Новые чаты не добавляются. Отслеживается завершение текущего ответа Codex. Ошибки, прерывание и активные запросы не считаются завершением. Перед выключением — 60 секунд на отмену.')}</small>
   </>}
   <small>{t('Проверено: {time}',{time:dateLabel(info.checkedAt)})}</small>
   <section className="panel codex-limits" aria-label={t('Лимиты Codex')}>
    <h2>{t('Лимиты Codex')}</h2>
    {onShortcutChange&&<label className="codex-thread"><input type="checkbox" checked={shortcutEnabled} disabled={busy} onChange={async event=>{const enabled=event.target.checked;setBusy(true);try{await onShortcutChange(enabled);}catch(error){setError(error instanceof Error?error.message:t('Не удалось сохранить настройки'));}finally{setBusy(false);}}}/><span>{t('Показывать кружок Codex рядом с приложениями')}</span></label>}
    {info.limits.available&&info.limits.message&&<Notice>{t(info.limits.message)}</Notice>}
    {!info.limits.available||!info.limits.buckets.length?<Notice>{t(info.limits.message||'Лимиты недоступны')}</Notice>:info.limits.buckets.map(bucket=><div className="codex-bucket" key={bucket.id}><h3>{bucket.name||bucket.id}</h3>{quotaWindows(bucket).map(({minutes,window})=><div className="codex-limit-window" key={minutes}><div><span>{windowLabel(minutes)}</span><strong>{window?(Number.isFinite(window.usedPercent)?t('Осталось {percent}%',{percent:Math.round(Math.max(0,Math.min(100,100-window.usedPercent))*10)/10}):t('Неизвестно')):t('Неизвестно')}</strong></div><small>{window?t('Сброс: {time}',{time:dateLabel(window.resetsAt)}):t('Codex не передал данные')}</small></div>)}</div>)}
    <small>{t('Данные лимитов: {time}',{time:dateLabel(info.limits.checkedAt)})}</small>
    <label className="codex-thread codex-limit-toggle"><input type="checkbox" checked={limitOn} disabled={busy||info.limitUsedThisBoot||(!limitOn&&(!info.limits.available||!info.limits.buckets.length))} onChange={e=>void action(e.target.checked?'codex.arm':'codex.cancel',e.target.checked?{mode:'limit'}:undefined)}/><span>{t('Выключить ПК при исчерпании лимита')}</span></label>
    <small>{info.limitUsedThisBoot?t('Уже использовано до следующей перезагрузки ПК.'):t('Один раз до перезагрузки ПК. При нулевом лимите — 60 секунд на отмену. Новый сценарий заменит предыдущее выключение.')}</small>
   </section>
  </>}
 </div>;
}
