import {t} from './i18n';
import {useEffect,useState} from 'react';
import {invoke,nativeAvailable,Notice,useAction} from './ui';

type StartupState={enabled:boolean;message?:string};
export function StartupSettings(){
 const [state,setState]=useState<StartupState|null>(null);
 const {error,busy,run}=useAction();
 async function load(){await run(async()=>setState(await invoke<StartupState>('startup.get')));}
 useEffect(()=>{if(nativeAvailable)void load();},[]);
 async function change(enabled:boolean){await run(async()=>setState(await invoke<StartupState>('startup.set',{enabled})));}
 return <><h2>{t("Автозапуск")}</h2><label className="checkbox"><input type="checkbox" checked={state?.enabled??false} disabled={!nativeAvailable||busy||state===null} aria-describedby="startup-help" onChange={event=>void change(event.target.checked)}/>{t("Запускать при входе в Windows")}</label><small id="startup-help">{nativeAvailable?t("Применяется сразу для текущего пользователя. SideWidget запускается в свёрнутом виде."):t("Доступно в приложении SideWidget для Windows.")}</small>{state?.message&&<Notice>{t(state.message)}</Notice>}{error&&<Notice error>{error} <button type="button" className="quiet" disabled={busy} onClick={()=>void load()}>{t("Повторить")}</button></Notice>}</>;
}
