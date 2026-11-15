import {t} from './i18n';
import {useMemo,useState} from 'react';
import {defaultStickerOptions,stickerFormats,stickerLayout,stickerDocument} from './qr-stickers';
import {invoke,nativeAvailable,Notice,useAction} from './ui';
import './qr-stickers.css';

export function QrStickerPrint({text,level}:{text:string;level:'L'|'M'|'Q'|'H'}){
 const [format,setFormat]=useState('60x60');
 const [width,setWidth]=useState('60');const [height,setHeight]=useState('60');
 const [padding,setPadding]=useState('2');const [copies,setCopies]=useState('1');
 const [caption,setCaption]=useState('');const [notice,setNotice]=useState('');
 const {busy,error,run}=useAction();
 const options={...defaultStickerOptions,width:Number(width),height:Number(height),padding:Number(padding),copies:Number(copies),caption,level};
 const preview=useMemo(()=>{
  try{return {layout:stickerLayout(text,options),error:''};}catch(e){return {error:(e as Error).message};}
 },[text,width,height,padding,copies,caption,level]);
 function chooseFormat(id:string){setFormat(id);const selected=stickerFormats.find(item=>item.id===id);if(selected){setWidth(String(selected.width));setHeight(String(selected.height));setPadding(String(selected.padding));}setNotice('');}
 async function print(){
  if(!preview.layout)return;
  setNotice('');await run(async()=>{
   const html=stickerDocument(text,options);
   if(nativeAvailable){const result=await invoke<{opened:boolean}>('qr.print',{html});setNotice(result.opened?'Открыто окно печати. Выберите принтер и размер бумаги.':'Окно печати закрыто.');}
   else{
    const popup=window.open('about:blank','_blank','width=700,height=750');
    if(!popup)throw new Error(t('Браузер заблокировал окно печати. Разрешите всплывающие окна и повторите.'));
    popup.opener=null;popup.document.open();popup.document.write(html);popup.document.close();popup.focus();popup.print();
    setNotice('Диалог печати открыт. После печати или отмены закройте окно стикеров.');
   }
  });
 }
 return <details className="qr-sticker-settings"><summary>{t('Печать на стикерах')}</summary><div className="stack">
  <label>{t('Формат стикера')}<select value={format} onChange={e=>chooseFormat(e.target.value)}>{stickerFormats.map(item=><option key={item.id} value={item.id}>{t('{width} × {height} мм',{width:item.width,height:item.height})}</option>)}<option value="custom">{t('Свой размер')}</option></select></label>
  {format==='custom'&&<div className="form-row"><label>{t('Ширина, мм')}<input type="number" min="20" max="200" step="0.1" value={width} onChange={e=>setWidth(e.target.value)}/></label><label>{t('Высота, мм')}<input type="number" min="16" max="200" step="0.1" value={height} onChange={e=>setHeight(e.target.value)}/></label></div>}
  <div className="form-row"><label>{t('Отступ, мм')}<input type="number" min="1" max="10" step="0.1" value={padding} onChange={e=>setPadding(e.target.value)}/></label><label>{t('Стикеров')}<input type="number" min="1" max="200" step="1" value={copies} onChange={e=>setCopies(e.target.value)}/></label></div>
  <label>{t('Подпись, необязательно')}<input value={caption} maxLength={160} onChange={e=>setCaption(e.target.value)} placeholder={t('Например, название товара')}/></label>
  <small>{t('Подпись не меняет QR. Длинная подпись на стикере сокращается.')}</small>
  {preview.layout&&<figure className="qr-sticker-preview"><div className="qr-sticker-frame"><div className="qr-sticker-paper" style={{aspectRatio:`${options.width}/${options.height}`,padding:`${options.padding/options.width*100}cqw`}} role="img" aria-label={t('Предпросмотр стикера {width} на {height} мм',{width,height})}><div className="qr-sticker-content"><img alt="" src={`data:image/svg+xml;charset=utf-8,${encodeURIComponent(preview.layout.svg)}`} style={{width:`${preview.layout.qrMm/(options.width-2*options.padding)*100}%`}}/>{caption&&<span style={{fontSize:`${2.3/(options.width-2*options.padding)*100}cqw`,height:`${6/(options.width-2*options.padding)*100}cqw`,lineHeight:`${5/(options.width-2*options.padding)*100}cqw`}}>{caption}</span>}</div></div></div><figcaption>{t('{width} × {height} мм · {copies} шт.',{width,height,copies})}</figcaption></figure>}
  {preview.error&&<Notice error>{preview.error}</Notice>}{error&&<Notice error>{error}</Notice>}
  <div className="actions"><button className="primary" disabled={busy||!preview.layout} onClick={()=>void print()}>{busy?t('Открываем печать…'):t('Печатать стикеры')}</button></div>
  <small>{t('Одна этикетка на страницу. В диалоге выберите бумагу {width} × {height} мм, масштаб 100%, поля «Нет», без колонтитулов.',{width,height})}</small>
  {notice&&<Notice>{t(notice)}</Notice>}
 </div></details>;
}
