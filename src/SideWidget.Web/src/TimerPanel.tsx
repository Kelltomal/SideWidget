import {locale, t} from './i18n';
import {useEffect,useRef,useState} from 'react';
import {bridge} from './bridge';
import {invoke,nativeAvailable,Notice} from './ui';
import {advanceTimer,formatTime,initialTimer,phaseName,remaining,restoreTimer,restoreTimers,primaryTimer,startTimer,type TimerEntry,type TimerMode,type TimerStart} from './timer';
import './timer.css';
const storageKey='sidewidget.focusTimers.v2';
function loadTimers():TimerEntry[]{
 if(nativeAvailable)return [];
 try{
  const saved=localStorage.getItem(storageKey);
  if(saved!==null)return restoreTimers(JSON.parse(saved));
  const legacy=restoreTimer(JSON.parse(localStorage.getItem('sidewidget.focusTimer.v1')??'null'));
  return legacy.status==='idle'?[]:[{id:'legacy',state:legacy}];
 }catch{return [];}
}
export function useFocusTimer(){
 const [items,setItems]=useState<TimerEntry[]>(loadTimers);
 const current=useRef(items);current.current=items;const revision=useRef(0);const locked=useRef(false);const [busy,setBusy]=useState(false);const [error,setError]=useState('');const [now,setNow]=useState(Date.now());
 useEffect(()=>{
  let live=true;
  const initialRevision=revision.current;
  if(nativeAvailable)void invoke<TimerEntry[]>('timer.get').then(s=>{if(live&&revision.current===initialRevision){current.current=s;setItems(s);}}).catch(e=>{if(live&&revision.current===initialRevision)setError(e.message);});
  const unsubscribe=bridge.subscribe(event=>{if(event.type==='timer.changed'){revision.current++;current.current=event.data;setItems(event.data);}});
  const tick=()=>{setNow(Date.now());if(!nativeAvailable)setItems(items=>{
   const next=items.map(item=>{const state=advanceTimer(item.state,'tick');return state===item.state?item:{...item,state};});
   return next.some((item,index)=>item!==items[index])?next:items;
  });};
  const id=setInterval(tick,1000);window.addEventListener('focus',tick);
  return()=>{live=false;unsubscribe();clearInterval(id);window.removeEventListener('focus',tick);};
 },[]);
 useEffect(()=>{if(!nativeAvailable)try{localStorage.setItem(storageKey,JSON.stringify(items));}catch{setError('Не удалось сохранить таймер. Проверьте свободное место.');}},[items]);
 async function action(name:string,input?:TimerStart,id?:string){
  if(locked.current)return;locked.current=true;revision.current++;setBusy(true);setError('');
  try{
   let next:TimerEntry[];
   if(nativeAvailable)next=await invoke<TimerEntry[]>(`timer.${name}`,input??(id?{id}:{}));
   else{
    if(name==='start'){
     if(current.current.length>=100)throw new Error(t('Можно создать до 100 отсчётов. Удалите ненужные.'));
     next=[...current.current,{id:crypto.randomUUID(),state:startTimer(initialTimer,input!)}];
    }else next=current.current.map(item=>item.id===id?{...item,state:advanceTimer(item.state,name)}:item).filter(item=>item.state.status!=='idle');
    localStorage.setItem(storageKey,JSON.stringify(next));
   }
   current.current=next;setItems(next);setNow(Date.now());
  }catch(e){setError(e instanceof Error?e.message:'Не удалось изменить таймер');}
  finally{locked.current=false;setBusy(false);}
 }
 return {items,state:primaryTimer(items)?.state??initialTimer,now,busy,error,action};
}
export type TimerControl=ReturnType<typeof useFocusTimer>;
export function TimerSummary({timer,open}:{timer:TimerControl;open:()=>void}){
 const active=timer.state.status!=='idle';const ringing=timer.state.status==='ringing';
 return <section className="panel timer-summary"><button className="quiet timer-summary-open" onClick={open}><strong>{t('Таймер')}</strong><span>{active?`${phaseName(timer.state)} · ${ringing?t('Время вышло'):formatTime(remaining(timer.state,timer.now))}${timer.items.length>1?t(' · Всего: {count}',{count:timer.items.length}):''}`:t('Будильник · Таймер · Pomodoro')}</span></button>{ringing&&<button className="timer-signal timer-summary-signal" disabled={timer.busy} onClick={()=>void timer.action('dismiss',undefined,primaryTimer(timer.items)?.id)}>{t('Остановить сигнал')}</button>}{timer.error&&<Notice error>{t(timer.error)}</Notice>}</section>;
}
export function TimerPanel({timer}:{timer:TimerControl}){
 const {state,items,now,busy,error,action}=timer;
 const [mode,setMode]=useState<TimerMode>(state.mode);const [minutes,setMinutes]=useState('5');const [seconds,setSeconds]=useState('0');const [alarm,setAlarm]=useState(state.alarmTime);const [work,setWork]=useState(String(state.workMinutes));const [rest,setRest]=useState(String(state.breakMinutes));const [longRest,setLongRest]=useState(String(state.longBreakMinutes));
 const alarmDate=(()=>{const d=new Date(now);const [h,m]=alarm.split(':').map(Number);d.setHours(h,m,0,0);if(d.getTime()<=now)d.setDate(d.getDate()+1);return Number.isNaN(d.getTime())?t('Укажите время'):d.toLocaleString(locale(),{day:'numeric',month:'long',hour:'2-digit',minute:'2-digit'});})();
 return <div className="stack timer-page">
  <div className="timer-modes" role="group" aria-label={t('Режим времени')}>{([['alarm','Будильник'],['timer','Таймер'],['pomodoro','Pomodoro']] as const).map(([id,label])=><button key={id} aria-pressed={mode===id} onClick={()=>setMode(id)}>{t(label)}</button>)}</div>
  <form className="stack" noValidate onSubmit={e=>{e.preventDefault();const duration=Number(minutes)*60+Number(seconds);void action('start',{mode,seconds:minutes.trim()&&seconds.trim()&&Number.isInteger(Number(minutes))&&Number.isInteger(Number(seconds))&&Number(minutes)>=0&&Number(seconds)>=0&&Number(seconds)<=59?duration:0,alarmTime:alarm,...(mode==='pomodoro'?{workMinutes:Number(work),breakMinutes:Number(rest),longBreakMinutes:Number(longRest)}:{})});}}>
   <h2>{mode==='alarm'?t('Новый будильник'):mode==='pomodoro'?t('Новый Pomodoro'):t('Новый таймер')}</h2>
   {mode==='timer'&&<><div className="form-row"><label>{t('Минуты')}<input type="number" min="0" max="1440" value={minutes} onChange={e=>setMinutes(e.target.value)}/></label><label>{t('Секунды')}<input type="number" min="0" max="59" value={seconds} onChange={e=>setSeconds(e.target.value)}/></label></div><div className="timer-presets">{[5,15,30].map(n=><button type="button" key={n} onClick={()=>{setMinutes(String(n));setSeconds('0');}}>{t('{minutes} мин',{minutes:n})}</button>)}</div></>}
   {mode==='alarm'&&<><label>{t('Время будильника')}<input type="time" value={alarm} onChange={e=>setAlarm(e.target.value)}/></label><small>{t('Ближайший сигнал: {date}',{date:alarmDate})}</small></>}
   {mode==='pomodoro'&&<><p>{t('Работа и отдых без спешки. После каждого сигнала следующий этап запускаете вы.')}</p><div className="timer-pomodoro-fields"><label>{t('Работа, мин')}<input type="number" min="1" max="180" value={work} onChange={e=>setWork(e.target.value)}/></label><label>{t('Перерыв, мин')}<input type="number" min="1" max="180" value={rest} onChange={e=>setRest(e.target.value)}/></label><label>{t('Длинный, мин')}<input type="number" min="1" max="180" value={longRest} onChange={e=>setLongRest(e.target.value)}/></label></div><small>{t('Длинный перерыв — после четырёх рабочих этапов.')}</small></>}
   {error&&<Notice error>{t(error)}</Notice>}
   <button className="primary" disabled={busy}>{mode==='alarm'?t('Поставить будильник'):mode==='pomodoro'?t('Начать Pomodoro'):t('Запустить таймер')}</button>
   <small>{t('Сигнал без звука. Остаток времени виден в свёрнутой шапке. SideWidget должен быть запущен.')}</small>
  </form>
  {items.map(({id,state},index)=>{
   const ringing=state.status==='ringing';
   const nextLabel=state.phase==='work'?t('Начать работу'):state.phase==='longBreak'?t('Начать длинный перерыв'):t('Начать перерыв');
   return <section key={id} className="panel timer-session" aria-label={t('Текущий отсчёт {index}: {phase}',{index:index+1,phase:phaseName(state)})}>
    <div className="timer-session-heading"><strong>{phaseName(state)}</strong><span>{state.mode==='pomodoro'?t('Цикл {count} из 4',{count:state.phase==='work'?state.cycle+1:state.cycle}):state.mode==='alarm'?state.alarmTime:t('На {duration}',{duration:formatTime(state.durationSeconds)})}</span></div>
    <div className="timer-digits" aria-label={t('Осталось')}>{formatTime(remaining(state,now))}</div>
    <p role="status">{ringing?t('Время вышло'):state.status==='paused'?t('На паузе'):state.status==='ready'?t('Следующий этап готов'):t('Идёт отсчёт')}</p>
    {ringing?<button className="timer-signal" disabled={busy} onClick={()=>void action('dismiss',undefined,id)}>{t('Остановить сигнал')}<span>{state.mode==='pomodoro'?t('Следующий этап запустите вручную'):t('Завершить отсчёт')}</span></button>:<div className="timer-actions">{state.status==='running'&&state.mode!=='alarm'&&<button disabled={busy} onClick={()=>void action('pause',undefined,id)}>{t('Пауза')}</button>}{state.status==='paused'&&<button className="primary" disabled={busy} onClick={()=>void action('resume',undefined,id)}>{t('Продолжить')}</button>}{state.status==='ready'&&<button className="primary" disabled={busy} onClick={()=>void action('resume',undefined,id)}>{nextLabel}</button>}<button className="quiet" disabled={busy} onClick={()=>void action('reset',undefined,id)}>{t('Удалить')}</button></div>}
   </section>;
  })}
 </div>;
}
