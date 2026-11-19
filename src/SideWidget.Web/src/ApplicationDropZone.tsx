import {useState,type ReactNode,type DragEvent} from 'react';
import type {Snapshot} from './types';
import {bridge} from './bridge';
import {t} from './i18n';
import {Notice} from './ui';
import './launcher-shortcuts.css';
export function ApplicationDropZone({children,onAdded}:{children:ReactNode;onAdded:(state:Snapshot)=>void}){
 const [hover,setHover]=useState(false);const [error,setError]=useState('');
 const files=(event:DragEvent)=>Array.from(event.dataTransfer.types).includes('Files');
 async function drop(event:DragEvent){
  if(!files(event))return;event.preventDefault();event.stopPropagation();setHover(false);setError('');
  const items=Array.from(event.dataTransfer.files);if(!items.length)return;
  if(items.length>10){setError(t('Перетащите не больше 10 приложений за раз'));return;}
  try{onAdded(await bridge.request<Snapshot>('apps.drop',{},items).promise);}catch(error){setError(error instanceof Error?t(error.message):t('Не удалось закрепить файл'));}
 }
 return <div className="application-drop-zone" onDragEnterCapture={event=>{if(files(event)){event.preventDefault();setHover(true);}}} onDragOverCapture={event=>{if(files(event)){event.preventDefault();event.dataTransfer.dropEffect='copy';}}} onDragLeaveCapture={event=>{if(!event.currentTarget.contains(event.relatedTarget as Node|null))setHover(false);}} onDropCapture={event=>void drop(event)}>
  {children}{hover&&<div className="application-drop-hint" role="status">{t('Отпустите, чтобы закрепить приложение')}</div>}{error&&<div className="application-drop-error"><Notice error>{error} <button className="quiet" onClick={()=>setError('')}>{t('Закрыть сообщение')}</button></Notice></div>}
 </div>;
}
