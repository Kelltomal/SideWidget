import {useState} from 'react';
import {invoke, nativeAvailable, Notice, useAction} from './ui';

type UpdateResult = {
 status: 'available' | 'current' | 'unpublished' | 'unavailable';
 currentVersion: string;
 latestVersion?: string;
 releaseUrl?: string;
 hasPortableAsset: boolean;
};

export function UpdateSettings() {
 const [result, setResult] = useState<UpdateResult>();
 const {busy, error, run} = useAction();
 if (!nativeAvailable) return null;

 async function check() {
  setResult(undefined);
  setResult(await invoke<UpdateResult>('updates.check'));
 }

 const message = result?.status === 'available'
  ? `Доступна версия ${result.latestVersion}.${result.hasPortableAsset ? '' : ' Архив для Windows пока не приложен.'}`
  : result?.status === 'current' ? 'Доступных обновлений нет.'
  : result?.status === 'unpublished' ? 'На GitHub пока нет опубликованного стабильного релиза.'
  : result?.status === 'unavailable' ? 'GitHub не ответил. Попробуйте ещё раз позже.' : undefined;

 return <section className="stack" aria-labelledby="update-settings-title" aria-busy={busy}>
  <h2 id="update-settings-title">Обновления</h2>
  <div className="actions">
   <button type="button" disabled={busy} onClick={() => void run(check)}>{busy && !result ? 'Проверяем…' : 'Проверить обновления'}</button>
   {result?.status === 'available' && result.releaseUrl &&
    <button type="button" disabled={busy} onClick={() => void run(() => invoke('links.open', {url: result.releaseUrl}))}>Открыть релиз на GitHub</button>}
  </div>
  {message && <Notice error={result?.status === 'unavailable'}>{message}</Notice>}
  {error && <Notice error>{error}</Notice>}
 </section>;
}
