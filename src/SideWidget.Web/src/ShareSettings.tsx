import {useState} from 'react';
import {invoke,nativeAvailable,Notice,useAction} from './ui';

export function ShareSettings(){
 const {error,busy,run}=useAction();const [result,setResult]=useState('');
 async function share(){setResult('');await run(async()=>{const data=await invoke<{path:string;revealed:boolean}>('app.share');setResult(data.revealed?'Архив готов. Файл выделен в Проводнике.':`Архив готов: ${data.path}. Не удалось открыть Проводник — откройте папку вручную.`);});}
 return <section className="stack" aria-labelledby="share-heading"><h2 id="share-heading">Поделиться SideWidget</h2><small id="share-help">ZIP с текущей версией и всем необходимым для запуска на другом ПК с Windows x64. Личные данные, настройки и ключи не включаются.</small><button type="button" disabled={!nativeAvailable||busy} aria-describedby="share-help" aria-busy={busy} onClick={()=>void share()}>{busy?'Упаковка…':'Поделиться'}</button>{!nativeAvailable&&<small>Доступно в приложении SideWidget для Windows.</small>}{busy&&<Notice>Создаём архив. Это может занять несколько минут.</Notice>}{result&&<Notice>{result}</Notice>}{error&&<Notice error>{error}</Notice>}</section>;
}
