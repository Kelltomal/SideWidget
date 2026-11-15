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
  <div className="actions"><button disabled={busy||!nativeAvailable} onClick={()=>void configure()}>{info.configured?'Изменить подключение':'Подключить SellyBoard'}</button>{info.configured&&<><button disabled={busy} onClick={()=>void load()}>Обновить задачи</button><button disabled={busy} onClick={()=>void run(async()=>{setInfo(await invoke<Info>('selly.disconnect'));setFeed(undefined);setCard(undefined);})}>Отключить</button></>}</div>
  {!info.configured&&<Notice>{info.message||'Подключите персональный API-ключ с правами календаря. Ключ вводится в защищённой нативной форме.'}</Notice>}
  {busy&&<Notice>Загружаем…</Notice>}{error&&<Notice error>{error}</Notice>}{message&&<Notice>{message}</Notice>}
  {info.configured&&<><small>Часовой пояс календаря: {zone}. Конец события на весь день не включён.</small>
   {feed&&<><h3>Задачи на выбранную дату</h3>{items.length?items.map((item,index)=><button key={`${item.id}:${item.occurrence||index}`} className="task-row" disabled={busy} onClick={()=>void open(item)}><span>{item.title}</span><small>{statusLabels[item.status]||item.status}</small></button>):<p>На эту дату задач нет.</p>}
   {feed.unscheduled.length>0&&<><h3>Без даты</h3>{feed.unscheduled.map(item=><button key={item.id} className="task-row" disabled={busy} onClick={()=>void open(item)}><span>{item.title}</span><small>{statusLabels[item.status]||item.status}</small></button>)}</>}{feed.truncated&&<Notice>Показана часть задач. Уточните период в SellyBoard.</Notice>}</>}
   {card&&<section className="stack" aria-label="Карточка задачи"><div className="section-heading"><h3>{card.title}</h3><button onClick={()=>setCard(undefined)}>Закрыть карточку</button></div>{card.description&&<p>{card.description}</p>}{card.notes&&<p>{card.notes}</p>}
   {card.recurrence?<Notice>Повторяющуюся задачу редактируйте в SellyBoard: там можно выбрать экземпляр или серию.</Notice>:card.can_write&&<><label>Статус задачи<select value={status} disabled={busy} onChange={event=>setStatus(event.target.value)}>{Object.entries(statusLabels).map(([value,label])=><option key={value} value={value}>{label}</option>)}</select></label><button disabled={busy} onClick={()=>void run(async()=>{try{await invoke('selly.status',{id:card.id,status});setCard(undefined);setMessage('Статус сохранён. Обновите список задач.');}catch(e){setCard(undefined);throw e;}})}>Сохранить статус</button></>}
   <small>Плановые даты не являются учётом времени. Таймер SideWidget работает отдельно.</small></section>}
   {info.pendingCreate?<Notice>Результат предыдущего создания не подтверждён. Повтор использует исходные данные и тот же ключ.</Notice>:null}
   {info.pendingCreate?<button disabled={busy} onClick={()=>void run(async()=>{await invoke('selly.retryCreate');setInfo(await invoke<Info>('selly.info'));setMessage('Создание подтверждено. Обновите список задач.');})}>Проверить создание повтором</button>:info.canWrite&&<form className="stack" onSubmit={event=>{event.preventDefault();void run(async()=>{try{await invoke('selly.create',{title,day});setTitle('');setMessage('Личная задача создана. Обновите список задач.');}finally{setInfo(await invoke<Info>('selly.info'));}});}}><label>Новая личная задача<input value={title} onChange={event=>setTitle(event.target.value)} required maxLength={250} disabled={busy}/></label><small>Личная задача на {day}, на весь день. Исполнитель — владелец ключа.</small><button disabled={busy||!title.trim()}>Создать личную задачу</button></form>}
  </>}
 </div>;
}
