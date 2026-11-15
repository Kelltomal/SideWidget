import {useEffect,useRef,useState,type ReactNode} from 'react';
import type {Snapshot} from './types';
import {invoke,nativeAvailable,Notice} from './ui';
import './module-layout.css';

export function ModuleLayout({state,setState,editing,finish,modules,renderModule}:{state:Snapshot;setState:(state:Snapshot)=>void;editing:boolean;finish:()=>void;modules:string[][];renderModule:(id:string)=>ReactNode}) {
 const current=useRef(state);current.current=state;
 const source=useRef<string|null>(null);const dirty=useRef(false);const queue=useRef(Promise.resolve());const [error,setError]=useState('');const [dragging,setDragging]=useState<string|null>(null);
 const order=[...state.settings.order,...modules.map(([id])=>id)].filter((id,index,list)=>modules.some(([key])=>key===id)&&list.indexOf(id)===index);
 const visible=order.filter(id=>!state.settings.disabled.includes(id));
 const callbacks=useRef({finish,save:()=>{}});
 function save(){
  if(!dirty.current)return;
  // Serialize writes; read the latest settings when each queued write starts.
  queue.current=queue.current.then(async()=>{
   if(!dirty.current)return;
   const settings={...current.current.settings};dirty.current=false;
   try {
    if(nativeAvailable)await invoke('settings.save',{...settings,displayPositions:undefined});
    else localStorage.setItem('sidewidget.local.preview',JSON.stringify(current.current));
    setError('');
   }catch(e){dirty.current=true;setError(`Не удалось сохранить порядок: ${e instanceof Error?e.message:'повторите попытку'}`);}
  });
 }
 callbacks.current={finish,save};
 useEffect(()=>{
  function leave(){callbacks.current.save();source.current=null;setDragging(null);callbacks.current.finish();}
  function visibility(){if(document.visibilityState==='hidden')leave();}
  window.addEventListener('blur',leave);window.addEventListener('pagehide',leave);document.addEventListener('visibilitychange',visibility);
  return()=>{callbacks.current.save();window.removeEventListener('blur',leave);window.removeEventListener('pagehide',leave);document.removeEventListener('visibilitychange',visibility);};
 },[]);
 useEffect(()=>{if(!editing){save();source.current=null;setDragging(null);}},[editing]);
 function move(id:string,target:string){
  if(!editing||id===target)return;
  const next=[...order];const from=next.indexOf(id);const to=next.indexOf(target);if(from<0||to<0)return;
  next.splice(from,1);next.splice(to,0,id);
  const updated={...current.current,settings:{...current.current.settings,order:next}};
  current.current=updated;setState(updated);dirty.current=true;save();
 }
 return <div className={`home-stack module-layout${editing?' is-editing':''}`}>
  {editing&&<p className="layout-help">Перетащите модуль. С клавиатуры: Alt + ↑ / ↓. Порядок сохраняется сразу.</p>}
  {error&&<Notice error>{error} <button className="quiet" onClick={save}>Повторить сохранение</button></Notice>}
  {visible.map((id,index)=><div key={id} data-module={id} className={`layout-module${dragging===id?' is-dragging':''}`} draggable={editing}
   onDragStart={event=>{if(!editing){event.preventDefault();return;}source.current=id;setDragging(id);event.dataTransfer.effectAllowed='move';event.dataTransfer.setData('text/plain',id);}}
   onDragOver={event=>{if(editing&&source.current){event.preventDefault();if(event.dataTransfer)event.dataTransfer.dropEffect='move';}}}
   onDrop={event=>{event.preventDefault();if(source.current)move(source.current,id);source.current=null;setDragging(null);}}
   onDragEnd={()=>{source.current=null;setDragging(null);save();}}>
   {editing&&<button className="module-handle" aria-label={`Переместить: ${modules.find(([key])=>key===id)?.[1]}`} aria-describedby="layout-keyboard-help" onKeyDown={event=>{
    if(!event.altKey||!['ArrowUp','ArrowDown'].includes(event.key))return;event.preventDefault();const target=visible[index+(event.key==='ArrowUp'?-1:1)];if(target)move(id,target);
   }}><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" aria-hidden="true"><path d="M8 5h2m4 0h2M8 12h2m4 0h2M8 19h2m4 0h2"/></svg>{modules.find(([key])=>key===id)?.[1]}</button>}
   <div inert={editing}>{renderModule(id)}</div>
  </div>)}
  {editing&&<span id="layout-keyboard-help" className="layout-help">Alt + стрелка вверх или вниз перемещает модуль.</span>}
 </div>;
}
