export type TimerMode='timer'|'alarm'|'pomodoro';
export type TimerState={mode:TimerMode;status:'idle'|'ready'|'running'|'paused'|'ringing';phase:'work'|'break'|'longBreak';cycle:number;deadline:string|null;remainingSeconds:number;durationSeconds:number;alarmTime:string;workMinutes:number;breakMinutes:number;longBreakMinutes:number};
export type TimerStart={mode:TimerMode;seconds?:number;alarmTime?:string;workMinutes?:number;breakMinutes?:number;longBreakMinutes?:number};
export type TimerEntry={id:string;state:TimerState};
export function primaryTimer(items:TimerEntry[]){
 const priority=(s:TimerState)=>s.status==='ringing'?0:s.status==='running'?1:2;
 return [...items].sort((a,b)=>priority(a.state)-priority(b.state)||(Date.parse(a.state.deadline??'')||Infinity)-(Date.parse(b.state.deadline??'')||Infinity))[0];
}
export function restoreTimers(value:unknown):TimerEntry[]{
 if(!Array.isArray(value))return [];
 const ids=new Set<string>();
 return value.flatMap(item=>{
  if(!item||typeof item.id!=='string'||!item.id||ids.has(item.id))return [];
  const state=restoreTimer(item.state);if(state.status==='idle')return [];
  ids.add(item.id);return [{id:item.id,state}];
 });
}
export const initialTimer:TimerState={mode:'timer',status:'idle',phase:'work',cycle:0,deadline:null,remainingSeconds:0,durationSeconds:0,alarmTime:'09:00',workMinutes:25,breakMinutes:5,longBreakMinutes:15};
export function remaining(state:TimerState,now=Date.now()){return state.status==='running'&&state.deadline?Math.max(0,Math.min(90000,Math.ceil((Date.parse(state.deadline)-now)/1000))):state.remainingSeconds;}
export function formatTime(seconds:number){const n=Math.max(0,Math.ceil(seconds));return n>=3600?`${Math.floor(n/3600)}:${String(Math.floor(n/60)%60).padStart(2,'0')}:${String(n%60).padStart(2,'0')}`:`${String(Math.floor(n/60)).padStart(2,'0')}:${String(n%60).padStart(2,'0')}`;}
export function phaseName(state:TimerState){return state.mode==='alarm'?'Будильник':state.mode==='timer'?'Таймер':state.phase==='work'?'Работа':state.phase==='longBreak'?'Длинный перерыв':'Перерыв';}
export function startTimer(current:TimerState,input:TimerStart,now=Date.now()):TimerState{
 const workMinutes=input.mode==='pomodoro'?(input.workMinutes??25):25,breakMinutes=input.mode==='pomodoro'?(input.breakMinutes??5):5,longBreakMinutes=input.mode==='pomodoro'?(input.longBreakMinutes??15):15;
 if(!['timer','alarm','pomodoro'].includes(input.mode))throw new Error('Выберите режим таймера');
 if([workMinutes,breakMinutes,longBreakMinutes].some(n=>!Number.isInteger(n)||n<1||n>180))throw new Error('Длительность этапа: от 1 до 180 минут');
 let seconds=input.seconds??0;let deadline=new Date(now);
 if(input.mode==='alarm'){
  if(!/^([01]\d|2[0-3]):[0-5]\d$/.test(input.alarmTime??''))throw new Error('Укажите время будильника в формате ЧЧ:ММ');
  const [hours,minutes]=input.alarmTime!.split(':').map(Number);deadline.setHours(hours,minutes,0,0);if(deadline.getTime()<=now)deadline.setDate(deadline.getDate()+1);seconds=Math.ceil((deadline.getTime()-now)/1000);
 }else{
  if(input.mode==='pomodoro')seconds=workMinutes*60;
  if(!Number.isInteger(seconds)||seconds<1||seconds>86400)throw new Error('Длительность таймера: от 1 секунды до 24 часов');
  deadline=new Date(now+seconds*1000);
 }
 return {...initialTimer,mode:input.mode,status:'running',remainingSeconds:seconds,durationSeconds:seconds,deadline:deadline.toISOString(),alarmTime:input.alarmTime??current.alarmTime,workMinutes,breakMinutes,longBreakMinutes};
}
export function advanceTimer(state:TimerState,action:string,now=Date.now()):TimerState{
 if(state.status==='running'&&remaining(state,now)===0)state={...state,status:'ringing',remainingSeconds:0,deadline:null};
 if(action==='pause'&&state.status==='running'&&state.mode!=='alarm')return {...state,status:'paused',remainingSeconds:remaining(state,now),deadline:null};
 if(action==='resume'&&['ready','paused'].includes(state.status))return {...state,status:'running',deadline:new Date(now+state.remainingSeconds*1000).toISOString()};
 if(action==='reset')return {...state,status:'idle',deadline:null,remainingSeconds:0,cycle:0,phase:'work'};
 if(action==='dismiss'&&state.status==='ringing'){
  if(state.mode!=='pomodoro')return advanceTimer(state,'reset',now);
  let cycle=state.phase==='work'?state.cycle+1:state.cycle;
  const phase=state.phase==='work'?(cycle===4?'longBreak':'break'):'work';if(state.phase==='longBreak')cycle=0;
  const seconds=(phase==='work'?state.workMinutes:phase==='break'?state.breakMinutes:state.longBreakMinutes)*60;
  return {...state,status:'ready',phase,cycle,remainingSeconds:seconds,durationSeconds:seconds,deadline:null};
 }
 return state;
}
export function restoreTimer(value:unknown):TimerState{
 const s=value as TimerState;
 if(!s||!['timer','alarm','pomodoro'].includes(s.mode)||!['idle','ready','running','paused','ringing'].includes(s.status)||!['work','break','longBreak'].includes(s.phase)||!Number.isInteger(s.cycle)||s.cycle<0||s.cycle>4||[s.remainingSeconds,s.durationSeconds].some(n=>!Number.isInteger(n)||n<0||n>90000)||[s.workMinutes,s.breakMinutes,s.longBreakMinutes].some(n=>!Number.isInteger(n)||n<1||n>180)||(s.status==='running'&&!Number.isFinite(Date.parse(s.deadline??''))))return initialTimer;
 return advanceTimer(s,'tick');
}
