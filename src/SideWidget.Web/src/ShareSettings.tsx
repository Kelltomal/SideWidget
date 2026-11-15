import {t} from './i18n';
import {useState} from 'react';
import {invoke,nativeAvailable,Notice,useAction} from './ui';

export function ShareSettings(){
 const {error,busy,run}=useAction();const [result,setResult]=useState<{path:string;revealed:boolean}>();
 async function share(){setResult(undefined);await run(async()=>setResult(await invoke<{path:string;revealed:boolean}>('app.share')));}
 return <section className="stack" aria-labelledby="share-heading"><h2 id="share-heading">{t("Поделиться SideWidget")}</h2><small id="share-help">{t("ZIP с текущей версией и всем необходимым для запуска на другом ПК с Windows x64. Личные данные, настройки и ключи не включаются.")}</small><button type="button" disabled={!nativeAvailable||busy} aria-describedby="share-help" aria-busy={busy} onClick={()=>void share()}>{busy?t("Упаковка…"):t("Поделиться")}</button>{!nativeAvailable&&<small>{t("Доступно в приложении SideWidget для Windows.")}</small>}{busy&&<Notice>{t("Создаём архив. Это может занять несколько минут.")}</Notice>}{result&&<Notice>{result.revealed?t("Архив готов. Файл выделен в Проводнике."):t("Архив готов: {path}. Не удалось открыть Проводник — откройте папку вручную.",{path:result.path})}</Notice>}{error&&<Notice error>{error}</Notice>}</section>;
}
