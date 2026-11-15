import {useEffect,useRef,useState} from 'react';
import {t} from './i18n';
import type {Snapshot} from './types';
import {invoke,nativeAvailable,Notice,useAction} from './ui';

const ranges=[
 {key:'clockFontSize',label:'Размер текста часов',min:14,max:34,initial:22},
 {key:'compactStripHeight',label:'Высота полоски поверх полноэкранного окна',min:23,max:120,initial:23},
] as const;

type ClockRange=(typeof ranges)[number];

function ClockSlider({range,saved,disabled,save}:{
 range:ClockRange;saved:number;disabled:boolean;save:(value:number)=>Promise<unknown>;
}) {
 const [value,setValue]=useState(saved);
 const pending=useRef(false);
 useEffect(()=>setValue(saved),[saved]);
 async function commit(next:number) {
  if(pending.current||disabled||next===saved)return;
  pending.current=true;
  try {
   if(!await save(next))setValue(saved);
  } finally {
   pending.current=false;
  }
 }
 return <label className="slider-label">
  <span>{t(range.label)}<span aria-hidden="true">{value}</span></span>
  <input type="range" min={range.min} max={range.max} value={value} disabled={disabled}
   aria-label={t(range.label)} onChange={event=>{
    const next=event.target.valueAsNumber;
    setValue(next);
    // Assistive technology can change a range without a pointer or key event.
    if(document.activeElement!==event.currentTarget)void commit(next);
   }}
   onPointerUp={event=>void commit(event.currentTarget.valueAsNumber)}
   onPointerCancel={event=>void commit(event.currentTarget.valueAsNumber)}
   onKeyUp={event=>void commit(event.currentTarget.valueAsNumber)}
   onBlur={event=>void commit(event.currentTarget.valueAsNumber)}/>
 </label>;
}

export function ClockSettings({state,disabled,onSaved}:{
 state:Snapshot;disabled:boolean;onSaved:(state:Snapshot)=>void;
}) {
 const {error,busy,run}=useAction();
 return <section className="stack" aria-labelledby="clock-settings-heading">
  <h2 id="clock-settings-heading">{t('Часы')}</h2>
  {ranges.map(range=><ClockSlider key={range.key} range={range} saved={state.settings[range.key]??range.initial}
   disabled={disabled||busy} save={value=>run(async()=>{
    const change={[range.key]:value};
    const next=nativeAvailable?await invoke<Snapshot>('settings.clock',change):{...state,settings:{...state.settings,...change}};
    onSaved(next);
    return true;
   })}/>)}
  <button type="button" disabled={!nativeAvailable||disabled||busy} onClick={()=>void run(()=>invoke('clock.fullscreen'))}>{t('Часы на весь экран')}</button>
  {error&&<Notice error>{error}</Notice>}
 </section>;
}
