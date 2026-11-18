import {useEffect,useRef,useState} from 'react';
import {bridge} from './bridge';
import {locale,t} from './i18n';
import {formatTime} from './timer';
import {invoke,nativeAvailable,Notice,useAction} from './ui';

type Schedule={action:'shutdown'|'restart';deadline:string};
export function usePowerSchedule(){
 const [schedule,setSchedule]=useState<Schedule|null>(null);
 const [error,setError]=useState('');const revision=useRef(0);
 useEffect(()=>{
  if(!nativeAvailable)return;
  let live=true;const start=revision.current;
  const off=bridge.subscribe(event=>{if(event.type==='power.changed'){revision.current++;setSchedule(event.data);setError('');}});
  void invoke<Schedule|null>('power.get').then(value=>{if(live&&start===revision.current)setSchedule(value);}).catch(error=>{if(live)setError(error.message);});
  return()=>{live=false;off();};
 },[]);
 return {schedule,setSchedule,error};
}

export function PowerScheduleForm({action,onClose}:{action:'shutdown'|'restart';onClose:()=>void}){
 const [hours,setHours]=useState('0');const [minutes,setMinutes]=useState('30');
 const {busy,error,run,setError}=useAction();
 const restart=action==='restart';
 return <form className="panel stack" aria-label={restart?t('Перезагрузить через…'):t('Выключить через…')} noValidate onSubmit={event=>{
  event.preventDefault();if(busy)return;
  const h=Number(hours),m=Number(minutes);
  if(!Number.isInteger(h)||!Number.isInteger(m)||h<0||h>24||m<0||m>59||h*60+m>1440){setError(t('Задержка: до 24 часов, минуты — от 0 до 59'));return;}
  if(h===0&&m===0){setError(t('Укажите хотя бы 1 минуту'));return;}
  void run(async()=>{await invoke('power.schedule',{action,hours:h,minutes:m});onClose();});
 }}>
  <h3>{restart?t('Перезагрузить через…'):t('Выключить через…')}</h3>
  <div className="form-row"><label>{t('Часы')}<input autoFocus type="number" min="0" max="24" step="1" value={hours} disabled={busy} onChange={event=>setHours(event.target.value)}/></label><label>{t('Минуты')}<input type="number" min="0" max="59" step="1" value={minutes} disabled={busy} onChange={event=>setMinutes(event.target.value)}/></label></div>
  <small>{t('Разовый запуск. SideWidget должен оставаться запущен. Отсчёт и отмена — в разделе «Таймер».')}</small>
  {error&&<Notice error>{error}</Notice>}
  <div className="actions"><button className="primary" disabled={busy}>{restart?t('Назначить перезагрузку'):t('Назначить выключение')}</button><button type="button" disabled={busy} onClick={onClose}>{t('Отмена')}</button></div>
 </form>;
}

export function PowerScheduleStatus({compact=false}:{compact?:boolean}){
 const {schedule,setSchedule,error:readError}=usePowerSchedule();const {busy,error,run}=useAction();
 const [now,setNow]=useState(Date.now());
 useEffect(()=>{if(!schedule)return;setNow(Date.now());const id=setInterval(()=>setNow(Date.now()),1000);return()=>clearInterval(id);},[schedule]);
 if(!schedule)return readError?<Notice error>{readError}</Notice>:null;
 const restart=schedule.action==='restart';const seconds=Math.max(0,Math.ceil((Date.parse(schedule.deadline)-now)/1000));
 return <section className={compact?'stack power-schedule-summary':'panel timer-session'} aria-label={t('Таймер питания')}>
  <strong>{restart?t('До перезагрузки ПК'):t('До выключения ПК')}</strong>
  <div className={compact?'':'timer-digits'} aria-label={t('Осталось')}>{formatTime(seconds)}</div>
  {!compact&&<small>{new Date(schedule.deadline).toLocaleString(locale(),{day:'numeric',month:'long',hour:'2-digit',minute:'2-digit'})} · {t('Разовый запуск. SideWidget должен оставаться запущен.')}</small>}
  <button className="quiet" disabled={busy} onClick={()=>void run(async()=>{setSchedule(await invoke<Schedule|null>('power.cancel'));})}>{restart?t('Отменить перезагрузку'):t('Отменить выключение')}</button>
  {(error||readError)&&<Notice error>{error||readError}</Notice>}
 </section>;
}
