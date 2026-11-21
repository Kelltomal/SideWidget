import {useEffect,useRef,useState} from 'react';
import {bridge} from './bridge';
import {locale,t} from './i18n';
import {CopyButton, invoke, nativeAvailable, Notice, useAction} from './ui';

type UpdateResult = {
 status: 'available' | 'current' | 'unpublished' | 'unavailable' | 'rate_limited' | 'forbidden' | 'auth_required';
 currentVersion: string;
 latestVersion?: string;
 releaseUrl?: string;
 hasPortableAsset: boolean;
 portableAsset?: object;
 retryAt?: string;
};

export function StartupUpdate() {
 const [offer,setOffer]=useState<UpdateResult>();
 useEffect(()=>bridge.subscribe(event=>{
  if(event.type==='updates.available'&&event.data?.latestVersion)setOffer(event.data);
 }),[]);
 return offer&&<UpdatePrompt offer={offer} dismiss={()=>setOffer(undefined)}/>;
}

function UpdatePrompt({offer,dismiss}:{offer:UpdateResult;dismiss:()=>void}) {
 const dialog=useRef<HTMLDialogElement>(null);
 const later=useRef<HTMLButtonElement>(null);
 const {run,busy,error}=useAction();
 const [installing,setInstalling]=useState(false);
 useEffect(()=>{
  const prompt=dialog.current;
  if(installing)prompt?.close();
  else {
   prompt?.showModal();
   later.current?.focus();
  }
 },[installing]);
 async function choose(action:'later'|'skip'|'install') {
  await run(async()=>{
   const version=offer.latestVersion;
   if(action==='skip')await invoke('updates.skip',{version});
   if(action==='later')await invoke('updates.defer');
   if(action==='install'){
    if(offer.portableAsset){
     setInstalling(true);
     try{await invoke('updates.install',{version});}
     catch(error){setInstalling(false);throw error;}
     dismiss();
     return;
    }
    if(!offer.releaseUrl)return;
    await invoke('links.open',{url:offer.releaseUrl});
    await invoke('updates.defer');
   }
   dismiss();
  });
 }
 return <dialog className="update-dialog" ref={dialog} aria-labelledby="startup-update-title"
  onCancel={event=>{event.preventDefault();if(!busy&&!installing)void choose('later');}}>
  <div className="stack" aria-busy={installing||busy}>
   <h2 id="startup-update-title">{t('Доступна версия {version}',{version:offer.latestVersion})}</h2>
   <p>{t(offer.portableAsset?'SideWidget перезапустится после загрузки.':'Новый выпуск можно скачать с GitHub.')}</p>
   {error&&<Notice error>{t(error)}</Notice>}
   <div className="actions update-dialog-actions">
    <button type="button" className="primary" disabled={busy||installing} onClick={()=>void choose('install')}>{t('Обновить сейчас')}</button>
    <button type="button" ref={later} disabled={busy||installing} onClick={()=>void choose('later')}>{t('В следующий раз')}</button>
    <button type="button" className="quiet" disabled={busy||installing} onClick={()=>void choose('skip')}>{t('Пропустить эту версию')}</button>
   </div>
  </div>
 </dialog>;
}

type Connection = {connected: boolean};

function GitHubSignIn({dismiss,connected}:{dismiss:()=>void;connected:()=>void}) {
 const dialog=useRef<HTMLDialogElement>(null);
 const pending=useRef(false);
 const openBrowser=useRef<HTMLButtonElement>(null);
 const [code,setCode]=useState('');
 const login=useAction();
 const browser=useAction();

 useEffect(()=>{
  dialog.current?.showModal();
  const unsubscribe=bridge.subscribe(event=>{
   if(event.type==='updates.github.code'&&pending.current&&typeof event.data?.code==='string')
    setCode(event.data.code);
  });
  return ()=>{
   unsubscribe();
   if(pending.current){
    pending.current=false;
    void invoke('updates.github.cancel').catch(()=>{});
   }
  };
 },[]);
 useEffect(()=>{if(code)openBrowser.current?.focus();},[code]);

 async function begin(){
  if(pending.current)return;
  pending.current=true;
  setCode('');
  const response=await login.run(()=>invoke<Connection>('updates.github.connect'));
  if(!pending.current)return;
  pending.current=false;
  setCode('');
  if(response?.connected)connected();
 }

 return <dialog className="update-dialog" ref={dialog} aria-labelledby="github-sign-in-title"
  onCancel={event=>{event.preventDefault();dismiss();}}>
  <div className="stack">
   <h2 id="github-sign-in-title">{t('Вход в GitHub')}</h2>
   <p>{t('SideWidget использует вход только для проверки версий и обновлений. Личные файлы и настройки остаются на компьютере. Данные входа передаются только GitHub, токен хранится локально в зашифрованном виде.')}</p>
   <p>{t('Если скачиваете сборки редко, вход можно пропустить.')}</p>
   {code&&<div className="stack">
    <p role="status">{t('Введите этот код на странице GitHub в браузере.')} <code>{code}</code></p>
    <div className="actions update-dialog-actions">
     <button type="button" className="primary" ref={openBrowser} disabled={browser.busy}
      onClick={()=>void browser.run(()=>invoke('links.open',{url:'https://github.com/login/device'}))}>{t('Открыть GitHub в браузере')}</button>
     <CopyButton text={code} label={t('Скопировать код')}/>
    </div>
   </div>}
   {login.busy&&!code&&<Notice>{t('Получаем код GitHub…')}</Notice>}
   {login.error&&<Notice error>{login.error}</Notice>}
   {browser.error&&<Notice error>{browser.error}</Notice>}
   <div className="actions update-dialog-actions">
    {!login.busy&&<button type="button" className="primary" onClick={()=>void begin()}>{t('Получить код')}</button>}
    <button type="button" autoFocus onClick={dismiss}>{t('Отмена')}</button>
   </div>
  </div>
 </dialog>;
}

export function UpdateSettings() {
 const [result, setResult] = useState<UpdateResult>();
 const [showPrompt,setShowPrompt]=useState(false);
 const [showSignIn,setShowSignIn]=useState(false);
 const [connected,setConnected]=useState<boolean>();
 const {busy, error, run} = useAction();

 useEffect(()=>{
  if(!nativeAvailable)return;
  let active=true;
  void invoke<Connection>('updates.github.status').then(status=>{
   if(active)setConnected(status.connected);
  }).catch(()=>{if(active)setConnected(false);});
  return()=>{active=false;};
 },[]);
 const retryUntil=result?.status==='rate_limited'&&result.retryAt ? Date.parse(result.retryAt) : 0;
 useEffect(()=>{
  const delay=retryUntil-Date.now();
  if(!result||!Number.isFinite(delay)||delay<=0)return;
  const timer=setTimeout(()=>setResult(current=>current===result?{...current,retryAt:undefined}:current),delay);
  return()=>clearTimeout(timer);
 },[result,retryUntil]);
 if (!nativeAvailable) return null;

 async function check() {
  setResult(undefined);
  const next=await invoke<UpdateResult>('updates.check');
  if(next.status==='auth_required')setConnected(false);
  setResult(next);
 }

 const retryDate=retryUntil>Date.now() ? new Date(retryUntil) : null;
 const retryTime=retryDate&&!Number.isNaN(retryDate.valueOf())
  ? new Intl.DateTimeFormat(locale(),{hour:'2-digit',minute:'2-digit'}).format(retryDate) : null;
 const message = result?.status === 'available'
  ? [t('Доступна версия {version}',{version:result.latestVersion})+'.', !result.hasPortableAsset&&t('Архив для Windows пока не приложен.')].filter(Boolean).join(' ')
  : result?.status === 'current' ? t('Доступных обновлений нет.')
  : result?.status === 'unpublished' ? t('На GitHub пока нет опубликованного стабильного релиза.')
  : result?.status === 'rate_limited' ? (retryTime
     ? t('Достигнут лимит запросов GitHub. Повторите проверку после {time}.',{time:retryTime})
     : t('Достигнут лимит запросов GitHub.'))
  : result?.status === 'forbidden' ? t('GitHub отклонил запрос на проверку обновлений.')
  : result?.status === 'auth_required' ? t('Вход в GitHub истёк. Войдите снова или проверьте обновления без входа.')
  : result?.status === 'unavailable' ? t('GitHub не ответил. Попробуйте ещё раз позже.') : undefined;

 return <section className="stack" aria-labelledby="update-settings-title" aria-busy={busy}>
  <h2 id="update-settings-title">{t('Обновления')}</h2>
  <div className="actions">
   <button type="button" disabled={busy||retryDate!==null} onClick={() => void run(check)}>{t(busy && !result ? 'Проверяем…' : 'Проверить обновления')}</button>
   {result?.status==='available'&&result.portableAsset&&
    <button type="button" className="primary" disabled={busy} onClick={()=>setShowPrompt(true)}>{t('Обновить сейчас')}</button>}
   {result?.releaseUrl&&
    <button type="button" disabled={busy} onClick={() => void run(() => invoke('links.open', {url: result.releaseUrl}))}>{t('Открыть релиз на GitHub')}</button>}
  </div>
  {message && <Notice error={result?.status === 'unavailable'||result?.status === 'forbidden'}>{message}</Notice>}
  {connected!==undefined&&<div className="actions">
   {connected&&<small>{t('GitHub подключён для обновлений.')}</small>}
   <button type="button" disabled={busy} onClick={()=>{
    if(!connected){setShowSignIn(true);return;}
    void run(async()=>{
     const status=await invoke<Connection>('updates.github.disconnect');
     setConnected(status.connected);
     setResult(undefined);
    });
   }}>{t(connected?'Отключить GitHub':'Войти в GitHub')}</button>
  </div>}
  {error && <Notice error>{t(error)}</Notice>}
  {showPrompt&&result&&<UpdatePrompt offer={result} dismiss={()=>setShowPrompt(false)}/>}
  {showSignIn&&<GitHubSignIn dismiss={()=>setShowSignIn(false)} connected={()=>{
   setShowSignIn(false);
   setConnected(true);
   void run(check);
  }}/>}
 </section>;
}
