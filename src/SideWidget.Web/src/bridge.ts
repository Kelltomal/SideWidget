export type Host = {postMessage(message: unknown): void; postMessageWithAdditionalObjects?(message:unknown,objects:File[]):void; addEventListener(type: string, callback: (e: MessageEvent)=>void): void};
export class BridgeError extends Error {
  constructor(public readonly code:string,message:string){super(message);this.name='BridgeError';}
}
export class Bridge {
  private pending=new Map<string,{resolve:(data:any)=>void;reject:(error:Error)=>void;timer:ReturnType<typeof setTimeout>}>();
  private listeners=new Set<(event:any)=>void>();
  constructor(private host?: Host) {host?.addEventListener('message',event=>{
    const m=event.data;if(!m||m.protocolVersion!==1)return;
    if(m.type){this.listeners.forEach(f=>f(m));return;}
    const pending=this.pending.get(m.requestId);if(!pending)return;
    if(m.status!=='result'&&m.status!=='error')return;
    clearTimeout(pending.timer);this.pending.delete(m.requestId);
    if(m.status==='error')pending.reject(new BridgeError(m.error?.code||'unknown',m.error?.message||'Неизвестная ошибка'));else pending.resolve(m.data);
  });}
  subscribe(listener:(event:any)=>void){this.listeners.add(listener);return()=>{this.listeners.delete(listener);};}
  request<T>(actionId: string, params: object = {}, objects?:File[]): {id:string; promise:Promise<T>} {
    const id=crypto.randomUUID();
    const promise=new Promise<T>((resolve,reject)=>{
      if(!this.host){reject(new Error('Действие доступно в нативном приложении SideWidget'));return;}
      if(objects&&!this.host.postMessageWithAdditionalObjects){reject(new Error('Перенос файлов недоступен. Обновите WebView2 или добавьте приложение кнопкой.'));return;}
      // The native website adapter allows ten minutes for a manual CAPTCHA.
      const websiteTranslation=actionId==='translation.run'&&'provider' in params&&(params.provider==='google'||params.provider==='yandex');
      const timeout=actionId==='updates.install'?31*60000:actionId==='updates.github.connect'?16*60000:actionId==='translation.speakLocal'?30*60000:websiteTranslation?12*60000:actionId==='app.share'?15*60000:180000;
      const timer=setTimeout(()=>{this.pending.delete(id);reject(new Error('Не получен ответ приложения. Проверьте результат перед повтором.'));},timeout);
      this.pending.set(id,{resolve,reject,timer});
      try{const message={protocolVersion:1,requestId:id,actionId,params};if(objects)this.host.postMessageWithAdditionalObjects!(message,objects);else this.host.postMessage(message);}
      catch(error){clearTimeout(timer);this.pending.delete(id);reject(error instanceof Error?error:new Error('Не удалось передать сообщение приложению'));}
    });return{id,promise};
  }
}
declare global {interface Window {chrome?:{webview?:Host}}}
export const bridge=new Bridge(typeof window!=='undefined'?window.chrome?.webview:undefined);

