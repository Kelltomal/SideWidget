import {locale, t} from './i18n';
import {useEffect,useRef,useState} from 'react';
import {invoke,nativeAvailable,Notice,useAction} from './ui';
type Info={configured:boolean;message?:string;canWrite?:boolean;pendingCreate:boolean;timezone?:string};
type TaskCard={id:string;title:string;status:string;start?:string;end?:string;all_day?:boolean;occurrence?:string;recurrence?:object|null;description?:string;notes?:string;can_write?:boolean};
type Feed={events:TaskCard[];unscheduled:TaskCard[];truncated?:boolean};
const statusLabels:Record<string,string>={todo:'Запланирована',in_progress:'В работе',done:'Завершена',cancelled:'Отменена'};
export function SellyBoardPanel({day,start,end}:{day:string;start:string;end:string}){
 const [info,setInfo]=useState<Info>({configured:false,pendingCreate:false});const [feed,setFeed]=useState<Feed>();const [card,setCard]=useState<TaskCard>();const [status,setStatus]=useState('todo');const [title,setTitle]=useState('');
 const {busy,error,run}=useAction();const revision=useRef(0);const [message,setMessage]=useState('');
 async function load(){const current=++revision.current;await run(async()=>{const next=await invoke<Info>('selly.info');if(current!==revision.current)return;setInfo(next);if(next.configured){const data=await invoke<Feed>('selly.events',{start,end});if(current===revision.current)setFeed(data);}else setFeed(undefined);});}
 useEffect(()=>{if(nativeAvailable)void load();return()=>{revision.current++;};},[start,end]);
 async function configure(){revision.current++;setCard(undefined);setFeed(undefined);setMessage('');const next=await run(()=>invoke<Info>('selly.configure'));if(next){setInfo(next);await load();}}
 async function open(item:TaskCard){const current=revision.current;await run(async()=>{const next=await invoke<TaskCard>('selly.detail',{id:item.id,occurrence:item.occurrence||''});if(current===revision.current){setCard(next);setStatus(next.status);setMessage('');}});}
 const zone=info.timezone||'Europe/Moscow';
 function date(value:string){return new Intl.DateTimeFormat('en-CA',{timeZone:zone,year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date(value));}
 const items=feed?.events.filter(item=>item.start&&item.end&&(item.all_day?item.start<=day&&day<item.end:date(item.start)<=day&&day<=date(new Date(Date.parse(item.end)-1).toISOString())))||[];
 return <div className="stack">
  <div className="actions"><button disabled={busy||!nativeAvailable} onClick={()=>void configure()}>{info.configured?t('Изменить подключение'):t('Подключить SellyBoard')}</button>{info.configured&&<><button disabled={busy} onClick={()=>void load()}>{t('Обновить задачи')}</button><button disabled={busy} onClick={()=>void run(async()=>{setInfo(await invoke<Info>('selly.disconnect'));setFeed(undefined);setCard(undefined);})}>{t('Отключить')}</button></>}</div>
  {!info.configured&&<Notice>{(info.message && t(info.message))||t('Подключите персональный API-ключ с правами календаря. Ключ вводится в защищённой нативной форме.')}</Notice>}
  {busy&&<Notice>{t('Загружаем…')}</Notice>}{error&&<Notice error>{error}</Notice>}{message&&<Notice>{t(message)}</Notice>}
  {info.configured&&<><small>{t('Часовой пояс календаря: {zone}. Конец события на весь день не включён.',{zone})}</small>
   {feed&&<><h3>{t('Задачи на выбранную дату')}</h3>{items.length?items.map((item,index)=><button key={`${item.id}:${item.occurrence||index}`} className="task-row" disabled={busy} onClick={()=>void open(item)}><span>{item.title}</span><small>{statusLabels[item.status] ? t(statusLabels[item.status]) : item.status}</small></button>):<p>{t('На эту дату задач нет.')}</p>}
   {feed.unscheduled.length>0&&<><h3>{t('Без даты')}</h3>{feed.unscheduled.map(item=><button key={item.id} className="task-row" disabled={busy} onClick={()=>void open(item)}><span>{item.title}</span><small>{statusLabels[item.status] ? t(statusLabels[item.status]) : item.status}</small></button>)}</>}{feed.truncated&&<Notice>{t('Показана часть задач. Уточните период в SellyBoard.')}</Notice>}</>}
   {card&&<section className="stack" aria-label={t('Карточка задачи')}><div className="section-heading"><h3>{card.title}</h3><button onClick={()=>setCard(undefined)}>{t('Закрыть карточку')}</button></div>{card.description&&<p>{card.description}</p>}{card.notes&&<p>{card.notes}</p>}
   {card.recurrence?<Notice>{t('Повторяющуюся задачу редактируйте в SellyBoard: там можно выбрать экземпляр или серию.')}</Notice>:card.can_write&&<><label>{t('Статус задачи')}<select value={status} disabled={busy} onChange={event=>setStatus(event.target.value)}>{Object.entries(statusLabels).map(([value,label])=><option key={value} value={value}>{t(label)}</option>)}</select></label><button disabled={busy} onClick={()=>void run(async()=>{try{await invoke('selly.status',{id:card.id,status});setCard(undefined);setMessage('Статус сохранён. Обновите список задач.');}catch(e){setCard(undefined);throw e;}})}>{t('Сохранить статус')}</button></>}
   <small>{t('Плановые даты не являются учётом времени. Таймер SideWidget работает отдельно.')}</small></section>}
   {info.pendingCreate?<Notice>{t('Результат предыдущего создания не подтверждён. Повтор использует исходные данные и тот же ключ.')}</Notice>:null}
   {info.pendingCreate?<button disabled={busy} onClick={()=>void run(async()=>{await invoke('selly.retryCreate');setInfo(await invoke<Info>('selly.info'));setMessage('Создание подтверждено. Обновите список задач.');})}>{t('Проверить создание повтором')}</button>:info.canWrite&&<form className="stack" onSubmit={event=>{event.preventDefault();void run(async()=>{try{await invoke('selly.create',{title,day});setTitle('');setMessage('Личная задача создана. Обновите список задач.');}finally{setInfo(await invoke<Info>('selly.info'));}});}}><label>{t('Новая личная задача')}<input value={title} onChange={event=>setTitle(event.target.value)} required maxLength={250} disabled={busy}/></label><small>{t('Личная задача на {day}, на весь день. Исполнитель — владелец ключа.',{day:locale()==='ru'?day:new Date(`${day}T12:00:00`).toLocaleDateString(locale())})}</small><button disabled={busy||!title.trim()}>{t('Создать личную задачу')}</button></form>}
  </>}
 </div>;
}
