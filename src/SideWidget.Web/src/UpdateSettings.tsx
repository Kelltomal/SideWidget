import {useEffect,useRef,useState} from 'react';
import {bridge} from './bridge';
import {t} from './i18n';
import {invoke, nativeAvailable, Notice, useAction} from './ui';

type UpdateResult = {
 status: 'available' | 'current' | 'unpublished' | 'unavailable';
 currentVersion: string;
 latestVersion?: string;
 releaseUrl?: string;
 hasPortableAsset: boolean;
 portableAsset?: object;
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
 const [progress,setProgress]=useState<number|null>(0);
 useEffect(()=>{
  dialog.current?.showModal();
  later.current?.focus();
  return bridge.subscribe(event=>{
   if(event.type==='updates.progress')setProgress(event.data?.percent??null);
  });
 },[]);
 async function choose(action:'later'|'skip'|'install') {
  await run(async()=>{
   const version=offer.latestVersion;
   if(action==='skip')await invoke('updates.skip',{version});
   if(action==='later')await invoke('updates.defer');
   if(action==='install'){
    if(offer.portableAsset){
     setProgress(0);
     setInstalling(true);
     try{await invoke('updates.install',{version});}
     catch(error){setInstalling(false);throw error;}
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
   {installing&&<div className="stack">
    <progress value={progress??undefined} max={100} aria-label={t('Загрузка обновления')}/>
    <small role="status">{progress===null?t('Проверяем и распаковываем обновление…'):t('Загружаем обновление… {percent}%',{percent:progress})}</small>
   </div>}
   {error&&<Notice error>{t(error)}</Notice>}
   <div className="actions update-dialog-actions">
    <button type="button" className="primary" disabled={busy||installing} onClick={()=>void choose('install')}>{t('Обновить сейчас')}</button>
    <button type="button" ref={later} disabled={busy||installing} onClick={()=>void choose('later')}>{t('В следующий раз')}</button>
    <button type="button" className="quiet" disabled={busy||installing} onClick={()=>void choose('skip')}>{t('Пропустить эту версию')}</button>
   </div>
  </div>
 </dialog>;
}

export function UpdateSettings() {
 const [result, setResult] = useState<UpdateResult>();
 const [showPrompt,setShowPrompt]=useState(false);
 const {busy, error, run} = useAction();
 if (!nativeAvailable) return null;

 async function check() {
  setResult(undefined);
  setResult(await invoke<UpdateResult>('updates.check'));
 }

 const message = result?.status === 'available'
  ? [t('Доступна версия {version}',{version:result.latestVersion})+'.', !result.hasPortableAsset&&t('Архив для Windows пока не приложен.')].filter(Boolean).join(' ')
  : result?.status === 'current' ? t('Доступных обновлений нет.')
  : result?.status === 'unpublished' ? t('На GitHub пока нет опубликованного стабильного релиза.')
  : result?.status === 'unavailable' ? t('GitHub не ответил. Попробуйте ещё раз позже.') : undefined;

 return <section className="stack" aria-labelledby="update-settings-title" aria-busy={busy}>
  <h2 id="update-settings-title">{t('Обновления')}</h2>
  <div className="actions">
   <button type="button" disabled={busy} onClick={() => void run(check)}>{t(busy && !result ? 'Проверяем…' : 'Проверить обновления')}</button>
   {result?.status==='available'&&result.portableAsset&&
    <button type="button" className="primary" disabled={busy} onClick={()=>setShowPrompt(true)}>{t('Обновить сейчас')}</button>}
   {result?.status === 'available' && result.releaseUrl &&
    <button type="button" disabled={busy} onClick={() => void run(() => invoke('links.open', {url: result.releaseUrl}))}>{t('Открыть релиз на GitHub')}</button>}
  </div>
  {message && <Notice error={result?.status === 'unavailable'}>{message}</Notice>}
  {error && <Notice error>{t(error)}</Notice>}
  {showPrompt&&result&&<UpdatePrompt offer={result} dismiss={()=>setShowPrompt(false)}/>}
 </section>;
}
