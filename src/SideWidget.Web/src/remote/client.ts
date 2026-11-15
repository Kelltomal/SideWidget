import {t} from '../i18n';
import type {MediaState} from '../types';

export type RemoteState={media:MediaState;audio:{available:boolean;volume:number|null;muted:boolean|null}};
export class RemoteError extends Error {constructor(message:string,public status=0){super(message);}}
export class RemoteClient {
 private artwork=new Map<string,string>();
 private key=(()=>{try{return localStorage.getItem('sidewidget.remote.key')||'';}catch{return '';}})();
 hasKey(){return !!this.key;}
 remember(key:string){this.key=key;try{localStorage.setItem('sidewidget.remote.key',key);return true;}catch{return false;}}
 forget(){this.key='';try{localStorage.removeItem('sidewidget.remote.key');}catch{}}
 async request<T>(path:string,body?:object):Promise<T>{
  const abort=new AbortController();const timer=setTimeout(()=>abort.abort(),10000);
  try{
   const response=await fetch(path,{method:body?'POST':'GET',headers:{'X-SideWidget-Key':this.key,...path==='/api/state'?{'X-SideWidget-Artwork':[...this.artwork.keys()].join(',')}:{},...body?{'Content-Type':'application/json'}:{}},body:body?JSON.stringify(body):undefined,signal:abort.signal,cache:'no-store',credentials:'omit',redirect:'error'});
   if(response.status===401){this.forget();throw new RemoteError(t("Доступ отозван. Подключите телефон заново через QR на ПК."),401);}
   if(!response.ok){const data=await response.json().catch(()=>({}));throw new RemoteError(t(typeof data.message==='string'&&data.message?data.message:"ПК не выполнил запрос. Обновите состояние и повторите вручную."),response.status);}
   return await response.json() as T;
  }catch(error){
   if(error instanceof RemoteError)throw error;
   throw new RemoteError(body?t("Нет ответа ПК. Проверьте результат перед повтором команды."):t("Нет связи с ПК. Проверяем подключение…"));
  }finally{clearTimeout(timer);}
 }
 async state(){
  const state=await this.request<RemoteState>('/api/state');
  for(const session of state.media.sessions){
   if(session.artworkKey){if(session.artwork){this.artwork.set(session.artworkKey,session.artwork);while(this.artwork.size>16)this.artwork.delete(this.artwork.keys().next().value!);}else session.artwork=this.artwork.get(session.artworkKey);}
  }
  return state;
 }
 command(body:object){return this.request('/api/command',{...body,requestId:crypto.randomUUID()});}
}
