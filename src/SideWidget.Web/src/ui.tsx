import {t} from './i18n';
import {useState,type ReactNode} from 'react';
import {bridge} from './bridge';
import type {Invoke} from './types';
export const invoke:Invoke = <T,>(action:string,params:object={})=>bridge.request<T>(action,params).promise;
export const nativeAvailable=!!window.chrome?.webview;
export function Icon({name,size=18}:{name:string;size?:number}) {
 const paths:Record<string,string>={
  codex:'m8 5-6 7 6 7m8-14 6 7-6 7M14 3l-4 18',
  globe:'M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0ZM3 12h18M12 3c4 5 4 13 0 18-4-5-4-13 0-18Z',
  back:'m14 6-6 6 6 6',close:'m6 6 12 12M6 18 18 6',pin:'M8 3h8l-1 7 3 3v2h-5v6h-2v-6H6v-2l3-3-1-7Z',
  backspace:'M8 5h13v14H8l-6-7Zm4 4 6 6m0-6-6 6',
  keypad:'M3 6h18v12H3ZM6.5 9.5h1m4 0h1m4 0h1m-11 3h1m4 0h1m4 0h1M8 15.5h8',
  settings:'M4 7h16M4 17h16M8 4v6M16 14v6',search:'M10 17a7 7 0 1 0 0-14 7 7 0 0 0 0 14Zm5-2 6 6',
  play:'m9 5 11 7-11 7Z',pause:'M8 5v14M16 5v14',stop:'M6 6h12v12H6z',previous:'M5 5v14m14-14L8 12l11 7Z',next:'M19 5v14M5 5l11 7-11 7Z',
  music:'M9 18V5l11-2v13M9 5v4l11-2M9 18c0 3-6 3-6 0s6-3 6 0Zm11-2c0 3-6 3-6 0s6-3 6 0Z',
  timer:'M9 2h6m-3 0v4m6-1 2 2M12 6a8 8 0 1 0 0 16 8 8 0 0 0 0-16Zm0 4v4l3 2',
  plus:'M12 5v14M5 12h14',calculator:'M5 3h14v18H5ZM8 7h8M8 12h1m6 0h1m-8 5h1m6 0h1',
  qr:'M3 3h6v6H3Zm12 0h6v6h-6ZM3 15h6v6H3Zm12 0h2v2h4v4h-6Zm6-4v2M3 12h9V3m0 14v4',
  units:'m4 16 12-12 5 5L9 21ZM8 12l3 3m1-7 3 3',color:'M12 3a9 9 0 1 0 0 18h2a2 2 0 0 0 0-4h-1a2 2 0 0 1 0-4h5a3 3 0 0 0 3-3c0-4-5-7-9-7ZM7 8h.01M11 6h.01M16 7h.01M6 13h.01',
  translate:'M3 5h12M9 3v2M5 5c0 5 3 8 7 10M12 5c0 5-3 8-7 10m9 6 4-11 4 11m-6-4h4',
  diary:'M5 3h14v18H5ZM9 7h6M9 11h6M9 15h4',apps:'M3 3h7v7H3Zm11 0h7v7h-7ZM3 14h7v7H3Zm11 0h7v7h-7Z',
  calendar:'M4 5h16v16H4ZM8 3v4M16 3v4M4 10h16',weather:'M8 17H6a4 4 0 1 1 1-8 6 6 0 0 1 11 2 3 3 0 1 1 0 6H8Z',
  gear:'m9 3-.6 2.2-2 .9-2-.6-1.5 2.6 1.5 1.7-.2 2.2L3 13.5l1.5 2.6 2.2-.4 1.8 1.3L9 20h3l.6-2.2 2-.9 2 .6 1.5-2.6-1.5-1.7.2-2.2L19 9.5l-1.5-2.6-2.2.4L13.5 6 13 3Zm2 5a3.5 3.5 0 1 1 0 7 3.5 3.5 0 0 1 0-7Z',
  soundOff:'M3 9h4l5-4v14l-5-4H3Zm13 0 6 6m0-6-6 6',
  sound:'M3 9h4l5-4v14l-5-4H3Zm13-2a7 7 0 0 1 0 10m3-13a11 11 0 0 1 0 16',system:'M3 4h18v13H3Zm5 17h8m-4-4v4',currency:'M4 7h16M4 17h16m0-10-4-4m4 4-4 4M4 17l4-4m-4 4 4 4',
  check:'m5 12 4 4L19 6',copy:'M8 8h12v13H8ZM4 16H2V2h12v2',arrow:'M5 12h14m-5-5 5 5-5 5',swap:'M4 8h16m-4-4 4 4-4 4M20 16H4m4-4-4 4 4 4',folder:'M3 6h7l2 3h9v11H3Z',
  wifi:'M2 8a16 16 0 0 1 20 0M5 12a11 11 0 0 1 14 0m-11 4a6 6 0 0 1 8 0m-4 4h.01',
  bluetooth:'m7 7 10 10-5 4V3l5 4L7 17',
  notifications:'M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4M3 3l18 18',
  power:'M12 2v10M6 5a9 9 0 1 0 12 0',
  battery:'M3 6h16v12H3Zm19 4v4M7 10v4m4-4v4m4-4v4',
  chevron:'m6 9 6 6 6-6',connections:'M4 7a12 12 0 0 1 16 0M7 11a8 8 0 0 1 10 0m-7 4a3 3 0 0 1 4 0m-2 5h.01',
  taskmanager:'M3 4h18v16H3Zm4 12v-4m5 4V8m5 8v-6',
  powershell:'m6 7 5 5-5 5m7 0h5',cmd:'M3 4h18v16H3Zm4 5 3 3-3 3m6 0h4',
  lock:'M6 10h12v11H6Zm3 0V6a3 3 0 0 1 6 0v4m-3 5v2',
  sleep:'M20 14A9 9 0 0 1 10 3a9 9 0 1 0 10 11Z',
  restart:'M20 7v5h-5m5 0a8 8 0 1 0-2 6',
 };
 return <svg width={size} height={size} style={{display:'block',flexShrink:0}} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={paths[name]||paths.apps}/></svg>;
}
export function Panel({title,children,action}:{title:string;children:ReactNode;action?:ReactNode}) {
 return <section className="panel"><div className="section-heading"><h2>{title}</h2>{action}</div>{children}</section>;
}
export function Notice({children,error=false}:{children:ReactNode;error?:boolean}) {
 return <p className={error?'notice error':'notice'} role={error?'alert':'status'}>{children}</p>;
}
export function useAction() {
 const [error,setError]=useState(''); const [busy,setBusy]=useState(false);
 async function run<T>(work:()=>Promise<T>):Promise<T|undefined> {
  setError('');setBusy(true);
  try{return await work();}catch(e){if(!(e instanceof Error&&'code' in e&&e.code==='cancelled'))setError(e instanceof Error?t(e.message):t("Не удалось выполнить действие. Повторите попытку."));}
  finally{setBusy(false);}
 }
 return {error,busy,run,setError};
}
export function CopyButton({text,label=t("Копировать")}:{text:string;label?:string}) {
 const [copied,setCopied]=useState(false);const {error,run}=useAction();
 return <><button type="button" disabled={!text} onClick={()=>void run(async()=>{
  if(nativeAvailable)await invoke('clipboard.text',{text});else await navigator.clipboard.writeText(text);
  setCopied(true);setTimeout(()=>setCopied(false),1800);
 })}><Icon name={copied?'check':'copy'} size={15}/>{copied?t("Скопировано"):label}</button>{error&&<Notice error>{error}</Notice>}</>;
}
export function localDate(date=new Date()) {return `${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,'0')}-${String(date.getDate()).padStart(2,'0')}`;}
export function timeLabel(seconds:number|null) {return seconds==null?'—':`${Math.floor(seconds/60)}:${String(Math.floor(seconds%60)).padStart(2,'0')}`;}
export function bytes(value:number|null|undefined) {return value==null?t("Недоступно"):value>=1073741824?t("{value} ГБ",{value:(value/1073741824).toFixed(1)}):t("{value} МБ",{value:(value/1048576).toFixed(1)});}
export function speed(value:number|null|undefined) {return value==null?t("Недоступно"):value>=1048576?t("{value} МБ/с",{value:(value/1048576).toFixed(1)}):t("{value} КБ/с",{value:(value/1024).toFixed(1)});}
