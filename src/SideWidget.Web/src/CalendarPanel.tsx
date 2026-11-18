import {useEffect,useLayoutEffect,useRef,useState,type FormEvent,type KeyboardEvent} from 'react';
import {bridge} from './bridge';
import {locale,t,useLocale} from './i18n';
import {Icon,invoke,localDate,nativeAvailable,Notice} from './ui';
import './calendar-panel.css';

export type CalendarEntry={id:string;title:string;date:string;time:string;endTime:string;allDay:boolean;notes:string;reminderMinutes:number|null;done:boolean};
type CalendarData={events:CalendarEntry[]};
type View='month'|'week'|'list';
const storageKey='sidewidget.calendar.v1';
const dateOf=(value:string)=>new Date(`${value}T12:00:00`);
const offset=(value:string,days:number)=>{const date=dateOf(value);date.setDate(date.getDate()+days);return localDate(date);};
const monthOf=(value:string)=>new Date(dateOf(value).getFullYear(),dateOf(value).getMonth(),1);
const dateValid=(value:unknown):value is string=>typeof value==='string'&&/^\d{4}-\d{2}-\d{2}$/.test(value)&&Number.isFinite(dateOf(value).getTime())&&localDate(dateOf(value))===value;
const timeValid=(value:string)=>/^([01]\d|2[0-3]):[0-5]\d$/.test(value);
function validEntries(value:unknown):value is CalendarEntry[]{return Array.isArray(value)&&value.every(event=>event&&typeof event.id==='string'&&typeof event.title==='string'&&dateValid(event.date)&&typeof event.time==='string'&&typeof event.endTime==='string'&&typeof event.allDay==='boolean'&&typeof event.notes==='string'&&typeof event.done==='boolean'&&(event.reminderMinutes===null||[5,15].includes(event.reminderMinutes)));}
const dayName=(value:string)=>dateOf(value).toLocaleDateString(locale(),{day:'numeric',month:'long',year:'numeric',weekday:'long'});
function newEntry(date:string):CalendarEntry{return {id:crypto.randomUUID(),title:'',date,time:'',endTime:'',allDay:false,notes:'',reminderMinutes:null,done:false};}

export function CalendarPanel(){
 useLocale();
 const [events,setEvents]=useState<CalendarEntry[]>([]);const [selected,setSelected]=useState(localDate());const [view,setView]=useState<View>('month');const [draft,setDraft]=useState<CalendarEntry|null>(null);const [editing,setEditing]=useState(false);const [busy,setBusy]=useState(false);const [loading,setLoading]=useState(nativeAvailable);const [error,setError]=useState('');const [status,setStatus]=useState('');
 const mounted=useRef(false);const revision=useRef(0);const acting=useRef(false);const grid=useRef<HTMLDivElement>(null);const focusDate=useRef(false);const title=useRef<HTMLInputElement>(null);const addButton=useRef<HTMLButtonElement>(null);const returnFocus=useRef<HTMLElement|null>(null);const today=localDate();const month=monthOf(selected);
 useEffect(()=>{
  mounted.current=true;const version=revision.current;
  if(!nativeAvailable){try{const saved=localStorage.getItem(storageKey);if(saved){const parsed:unknown=JSON.parse(saved);if(!validEntries(parsed))throw new Error('invalid');setEvents(parsed);}}catch{setError(t('Не удалось прочитать календарь. Сохранённые данные не изменены.'));}return()=>{mounted.current=false;};}
  void invoke<CalendarData>('calendar.get').then(next=>{if(mounted.current&&version===revision.current){setEvents(next.events);setLoading(false);}}).catch(e=>{if(mounted.current&&version===revision.current){setError(e instanceof Error?e.message:t('Не удалось загрузить календарь. Откройте его снова.'));setLoading(false);}});
  const unsubscribe=bridge.subscribe(event=>{if(event.type==='calendar.changed'&&validEntries(event.data?.events)){revision.current++;setEvents(event.data.events);setLoading(false);}});
  return()=>{mounted.current=false;revision.current++;unsubscribe();};
 },[]);
 useLayoutEffect(()=>{if(draft)title.current?.focus();else if(returnFocus.current){(returnFocus.current.isConnected?returnFocus.current:addButton.current)?.focus();returnFocus.current=null;}},[draft?.id]);
 useLayoutEffect(()=>{if(focusDate.current){grid.current?.querySelector<HTMLButtonElement>(`button[data-date="${selected}"]`)?.focus();focusDate.current=false;}},[selected,view]);
 async function change(command:'calendar.save'|'calendar.delete',event:CalendarEntry){
  if(acting.current)return false;acting.current=true;setBusy(true);setError('');setStatus('');const version=++revision.current;
  try{
   let next:CalendarData;
   if(nativeAvailable)next=await invoke<CalendarData>(command,command==='calendar.save'?{event}:{id:event.id});
   else{const current=localStorage.getItem(storageKey);const stored:unknown=current?JSON.parse(current):[];if(!validEntries(stored))throw new Error('invalid');const changed=command==='calendar.delete'?stored.filter(item=>item.id!==event.id):[...stored.filter(item=>item.id!==event.id),event];localStorage.setItem(storageKey,JSON.stringify(changed));next={events:changed};}
   if(mounted.current){if(version===revision.current)setEvents(next.events);setStatus(t(command==='calendar.delete'?'Событие удалено.':'Событие сохранено.'));}return true;
  }catch(e){if(mounted.current)setError(nativeAvailable&&e instanceof Error?e.message:t('Не удалось сохранить календарь. Скопируйте данные и повторите попытку.'));return false;}
  finally{acting.current=false;if(mounted.current)setBusy(false);}
 }
 async function save(event:FormEvent){
  event.preventDefault();if(!draft||busy)return;const normalized={...draft,title:draft.title.trim(),time:draft.allDay?'':draft.time,endTime:draft.allDay?'':draft.endTime,reminderMinutes:draft.allDay||!draft.time?null:draft.reminderMinutes};
  if(!normalized.title){setError(t('Введите название события.'));return;}
  if(normalized.title.length>120||normalized.notes.length>4000){setError(t('Название — до 120 знаков, заметки — до 4000.'));return;}
  if(!dateValid(normalized.date)){setError(t('Укажите корректную дату.'));return;}
  if((normalized.time&&!timeValid(normalized.time))||(normalized.endTime&&(!timeValid(normalized.endTime)||!normalized.time||normalized.endTime<=normalized.time))){setError(t('Окончание должно быть позже начала.'));return;}
  if(await change('calendar.save',normalized)){setSelected(normalized.date);setDraft(null);}
 }
 function navigate(delta:number){if(view==='week')setSelected(offset(selected,delta*7));else{const next=new Date(month.getFullYear(),month.getMonth()+delta,1);setSelected(localDate(next));}}
 function keyDate(event:KeyboardEvent<HTMLButtonElement>,date:string){const step:Record<string,number>={ArrowLeft:-1,ArrowRight:1,ArrowUp:-7,ArrowDown:7,Home:-((dateOf(date).getDay()+6)%7),End:6-((dateOf(date).getDay()+6)%7)};if(event.key in step){event.preventDefault();focusDate.current=true;setSelected(offset(date,step[event.key]));}else if(event.key==='PageUp'||event.key==='PageDown'){event.preventDefault();focusDate.current=true;const next=dateOf(date);next.setDate(1);next.setMonth(next.getMonth()+(event.key==='PageUp'?-1:1));setSelected(localDate(next));}}
 function open(event?:CalendarEntry){returnFocus.current=document.activeElement instanceof HTMLElement?document.activeElement:null;setError('');setStatus('');setEditing(!!event);setDraft(event?{...event}:newEntry(selected));}
 const dates=view==='week'?Array.from({length:7},(_,index)=>offset(selected,index-((dateOf(selected).getDay()+6)%7))):Array.from({length:42},(_,index)=>offset(localDate(month),index-((month.getDay()+6)%7)));
 const agenda=events.filter(event=>view==='list'?event.date.slice(0,7)===selected.slice(0,7):event.date===selected).sort((a,b)=>a.date.localeCompare(b.date)||Number(b.allDay)-Number(a.allDay)||a.time.localeCompare(b.time)||a.title.localeCompare(b.title));
 const heading=view==='week'?`${dateOf(dates[0]).toLocaleDateString(locale(),{day:'numeric',month:'short'})} – ${dateOf(dates[6]).toLocaleDateString(locale(),{day:'numeric',month:'short',year:'numeric'})}`:month.toLocaleDateString(locale(),{month:'long',year:'numeric'});
 return <div className="calendar-panel stack">
  <div className="actions calendar-views" role="group" aria-label={t('Вид календаря')}>{(['month','week','list'] as const).map(mode=><button type="button" key={mode} aria-pressed={view===mode} onClick={()=>setView(mode)}>{t(mode==='month'?'Месяц':mode==='week'?'Неделя':'Список')}</button>)}</div>
  <section className="panel">
   <div className="month-heading"><button type="button" className="icon-button" aria-label={t(view==='week'?'Предыдущая неделя':'Предыдущий месяц')} onClick={()=>navigate(-1)}><Icon name="back" size={16}/></button><strong aria-live="polite">{heading}</strong><button type="button" className="icon-button" aria-label={t(view==='week'?'Следующая неделя':'Следующий месяц')} onClick={()=>navigate(1)}><Icon name="arrow" size={16}/></button></div>
   {view!=='list'&&<div ref={grid} className="calendar-grid calendar-event-grid" role="group" aria-label={t('Выбор даты')}>
    {[0,1,2,3,4,5,6].map(index=><span className="weekday" key={index}>{new Date(2024,0,1+index).toLocaleDateString(locale(),{weekday:'short'})}</span>)}
    {dates.map(date=>{const count=events.filter(event=>event.date===date).length;return <button type="button" key={date} data-date={date} className={`${date===today?'today ':''}${date===selected?'chosen ':''}${date.slice(0,7)!==selected.slice(0,7)?'calendar-other-month':''}`} aria-pressed={date===selected} aria-current={date===today?'date':undefined} aria-label={count?t('{date}, событий: {count}',{date:dayName(date),count}):dayName(date)} tabIndex={date===selected?0:-1} onClick={()=>setSelected(date)} onKeyDown={event=>keyDate(event,date)}><span>{dateOf(date).getDate()}</span><span className="calendar-event-mark" aria-hidden="true">{count>0&&<i/>}</span></button>;})}
   </div>}
   <div className="actions calendar-date-actions"><button type="button" className="quiet" onClick={()=>setSelected(localDate())}>{t('Сегодня')}</button><button ref={addButton} type="button" className="primary" disabled={busy||loading} onClick={()=>open()}>{t('Добавить событие')}</button></div>
  </section>
  {error&&<Notice error>{t(error)}</Notice>}{status&&<Notice>{status}</Notice>}{loading&&<Notice>{t('Загружаем календарь…')}</Notice>}
  {draft&&<form className="panel calendar-editor" onSubmit={event=>void save(event)} aria-label={t(editing?'Редактировать событие':'Новое событие')}>
   <h2>{t(editing?'Редактировать событие':'Новое событие')}</h2>
   <label>{t('Название')}<input ref={title} value={draft.title} maxLength={120} disabled={busy} onChange={e=>setDraft({...draft,title:e.target.value})}/></label>
   <label>{t('Дата')}<input type="date" value={draft.date} required disabled={busy} onChange={e=>setDraft({...draft,date:e.target.value})}/></label>
   <label className="calendar-check"><input type="checkbox" checked={draft.allDay} disabled={busy} onChange={e=>setDraft({...draft,allDay:e.target.checked})}/>{t('Весь день')}</label>
   {!draft.allDay&&<div className="form-row"><label>{t('Начало')}<input type="time" value={draft.time} disabled={busy} onChange={e=>setDraft({...draft,time:e.target.value})}/></label><label>{t('Окончание')}<input type="time" value={draft.endTime} disabled={busy} onChange={e=>setDraft({...draft,endTime:e.target.value})}/></label></div>}
   <label>{t('Напоминание')}<select value={draft.allDay||!draft.time?'none':draft.reminderMinutes??'none'} disabled={busy||draft.allDay||!draft.time} onChange={e=>setDraft({...draft,reminderMinutes:e.target.value==='none'?null:Number(e.target.value)})}><option value="none">{t('Без напоминания')}</option><option value="5">{t('За 5 минут')}</option><option value="15">{t('За 15 минут')}</option></select></label>
   <small>{t('Для напоминаний SideWidget должен оставаться запущенным. В браузере напоминания недоступны.')}</small>
   <label>{t('Заметки')}<textarea rows={3} value={draft.notes} maxLength={4000} disabled={busy} onChange={e=>setDraft({...draft,notes:e.target.value})}/></label>
   <div className="actions"><button type="submit" className="primary" disabled={busy}>{t('Сохранить событие')}</button><button type="button" disabled={busy} onClick={()=>{setDraft(null);setError('');}}>{t('Отмена')}</button>{editing&&<button type="button" disabled={busy} onClick={()=>void change('calendar.delete',draft).then(ok=>{if(ok)setDraft(null);})}>{t('Удалить событие')}</button>}</div>
  </form>}
  <section className="calendar-agenda" aria-label={t('События')}>
   <h2>{view==='list'?t('События за месяц'):dayName(selected)}</h2>
   {!loading&&agenda.length===0&&<p>{t(view==='list'?'В этом месяце событий нет.':'На этот день событий нет.')}</p>}
   <ul>{agenda.map(event=><li key={event.id} className={event.done?'calendar-event is-done':'calendar-event'}>
    <button type="button" className="icon-button calendar-done" aria-label={t(event.done?'Вернуть в план: {title}':'Отметить выполненным: {title}',{title:event.title})} aria-pressed={event.done} disabled={busy} onClick={()=>void change('calendar.save',{...event,done:!event.done})}><Icon name={event.done?'check':'calendar'} size={17}/></button>
    <div className="calendar-event-body"><strong>{event.title}</strong><small>{view==='list'&&<>{dateOf(event.date).toLocaleDateString(locale(),{day:'numeric',month:'short'})} · </>}{event.allDay?t('Весь день'):event.time?`${event.time}${event.endTime?` – ${event.endTime}`:''}`:t('Без времени')}{event.done&&<> · {t('Выполнено')}</>}</small>{event.notes&&<p className="calendar-event-notes">{event.notes}</p>}</div>
    <button type="button" className="icon-button" aria-label={t('Редактировать: {title}',{title:event.title})} disabled={busy} onClick={()=>open(event)}><Icon name="settings" size={17}/></button>
   </li>)}</ul>
  </section>
 </div>;
}
