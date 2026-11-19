import {t} from './i18n';
import {useRef,useState,type DragEvent,type KeyboardEvent} from 'react';
import type {Snapshot} from './types';
import {invoke,nativeAvailable} from './ui';
const appDragType='application/x-sidewidget-app';
export function useAppReorder(state:Snapshot,setState:(state:Snapshot)=>void){
 const source=useRef<string|null>(null);const current=useRef(state);current.current=state;
 const [busy,setBusy]=useState(false);const saving=useRef(false);const [dragging,setDragging]=useState<string|null>(null);const [error,setError]=useState('');
 async function move(id:string,target:string){
  if(saving.current||id===target)return;
  const latest=current.current;const ids=latest.apps.map(app=>app.id);const from=ids.indexOf(id),to=ids.indexOf(target);if(from<0||to<0)return;
  ids.splice(from,1);ids.splice(to,0,id);saving.current=true;setBusy(true);setError('');
  try{if(nativeAvailable)setState(await invoke<Snapshot>('apps.reorder',{ids}));else setState({...latest,apps:ids.map(id=>latest.apps.find(app=>app.id===id)!)});}
  catch(error){setError(error instanceof Error?t(error.message):t('Не удалось сохранить порядок приложений'));}
  finally{saving.current=false;setBusy(false);}
 }
 function reset(){source.current=null;setDragging(null);}
 function handlers(id:string){return {
  draggable:!busy,
  onDragStart:(event:DragEvent)=>{event.stopPropagation();if(saving.current){event.preventDefault();return;}source.current=id;setDragging(id);event.dataTransfer.effectAllowed='move';event.dataTransfer.setData(appDragType,id);},
  onDragOver:(event:DragEvent)=>{if(!source.current&&!Array.from(event.dataTransfer.types).includes(appDragType))return;event.preventDefault();event.stopPropagation();event.dataTransfer.dropEffect='move';},
  onDrop:(event:DragEvent)=>{const moving=source.current||event.dataTransfer.getData(appDragType);if(!moving)return;event.preventDefault();event.stopPropagation();reset();void move(moving,id);},
  onDragEnd:(event:DragEvent)=>{event.stopPropagation();reset();},
  onKeyDown:(event:KeyboardEvent)=>{if(!event.altKey||!['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(event.key))return;event.preventDefault();const index=current.current.apps.findIndex(app=>app.id===id);const target=current.current.apps[index+(['ArrowLeft','ArrowUp'].includes(event.key)?-1:1)];if(target)void move(id,target.id);},
 };}
 return {handlers,dragging,busy,error};
}
