import {useEffect,useState} from 'react';
import type {CodexSnapshot} from './CodexPanel';
import {t} from './i18n';
import {invoke,nativeAvailable} from './ui';
import './launcher-shortcuts.css';

export function CodexShortcut({open,active=true}:{open:()=>void;active?:boolean}){
 const [limits,setLimits]=useState<CodexSnapshot['limits']>();
 useEffect(()=>{
  if(!active||!nativeAvailable)return;
  let alive=true;let timer:ReturnType<typeof setTimeout>;
  async function poll(){try{const result=await invoke<CodexSnapshot>('codex.get');if(alive)setLimits(result.limits);}catch{if(alive)setLimits(undefined);}finally{if(alive)timer=setTimeout(poll,10000);}}
  void poll();return()=>{alive=false;clearTimeout(timer);};
 },[active]);
 const bucket=limits?.available?limits.buckets.find(bucket=>bucket.id==='codex'):undefined;
 const rings=[{minutes:10080,radius:13,label:t('Неделя')},{minutes:300,radius:9,label:t('5 часов')}].map(ring=>{
  const window=[bucket?.primary,bucket?.secondary].find(window=>window?.windowMinutes===ring.minutes);
  const remaining=window&&Number.isFinite(window.usedPercent)?Math.round(Math.max(0,Math.min(100,100-window.usedPercent))*10)/10:null;
  return {...ring,remaining};
 });
 const label='Codex · '+rings.map(ring=>`${ring.label}: ${ring.remaining===null?t('Неизвестно'):t('Осталось {percent}%',{percent:ring.remaining})}`).join(' · ');
 const weekly=rings[0].remaining;
 const value=weekly===null?'—':weekly>0&&weekly<1?'<1':String(Math.floor(weekly));
 return <button className="app-shortcut codex-shortcut" onClick={open} aria-label={label} title={label}>
  <svg className="codex-quota-icon" width="30" height="30" viewBox="0 0 30 30" aria-hidden="true" fill="none">
   {rings.map(ring=><g key={ring.minutes}><circle className="quota-track" cx="15" cy="15" r={ring.radius}/><circle className={ring.remaining===null?'quota-unknown':ring.remaining<=15?'quota-low':'quota-remaining'} data-window={ring.minutes} cx="15" cy="15" r={ring.radius} pathLength="100" strokeDasharray={ring.remaining===null?'2 4':`${ring.remaining} 100`} transform="rotate(-90 15 15)"/></g>)}
   <text className={'quota-value'+(value.length>2?' quota-value-wide':'')} x="15" y="15" textAnchor="middle" dominantBaseline="central" stroke="none">{value}</text>
  </svg><span>Codex</span>
 </button>;
}
