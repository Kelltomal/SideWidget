import {useEffect,useState} from 'react';
import {invoke,Notice,Panel,useAction} from './ui';
export type InternetState={
 available:boolean;connectivity:'internet'|'local'|'limited'|'none'|'unknown';source:string;target:string;cooldownSeconds:number;
 lastCheck:null|{method:'icmp'|'https';target:string;checkedAt:string;success:boolean;latencyMs:number|null;httpStatus:number|null;status:string;message:string};
};
const presets=[['cloudflare.com','Cloudflare'],['yandex.ru','Яндекс'],['vk.com','VK'],['google.com','Google']];
const connectivityLabels={internet:'Windows видит доступ к интернету',local:'Доступна локальная сеть',limited:'Подключение ограничено',none:'Нет подключения',unknown:'Состояние неизвестно'};
function savedTarget(){try{return localStorage.getItem('sidewidget.internet.target')||'cloudflare.com';}catch{return 'cloudflare.com';}}
export function Internet({expanded}:{expanded:boolean}) {
 const [state,setState]=useState<InternetState>();const [target,setTarget]=useState(savedTarget);const [nextAllowed,setNextAllowed]=useState(0);const [now,setNow]=useState(Date.now());const [loadError,setLoadError]=useState('');const {error,busy,run}=useAction();
 useEffect(()=>{if(!expanded)return;let active=true;void invoke<InternetState>('internet.get').then(value=>{if(active){setState(value);setLoadError('');}}).catch(error=>{if(active)setLoadError(error.message);});return()=>{active=false;};},[expanded]);
 useEffect(()=>{setNow(Date.now());if(!expanded||nextAllowed<=Date.now())return;const timer=setInterval(()=>{const current=Date.now();setNow(current);if(current>=nextAllowed)clearInterval(timer);},250);return()=>clearInterval(timer);},[expanded,nextAllowed]);
 const remaining=Math.max(0,Math.ceil((nextAllowed-now)/1000));
 function change(value:string){setTarget(value);try{localStorage.setItem('sidewidget.internet.target',value);}catch{}}
 async function check(method:'icmp'|'https') {
  const delay=(state?.cooldownSeconds??5)*1000;setNow(Date.now());setNextAllowed(Date.now()+delay);
  await run(async()=>setState(await invoke<InternetState>('internet.check',{method,target:target.trim()})));
 }
 const last=state?.lastCheck;
 return <Panel title="Интернет"><p>{state?(state.available?connectivityLabels[state.connectivity]:'Состояние Windows недоступно'):loadError||'Получаем состояние Windows…'}</p><small>Источник состояния: {state?.source||'Windows'}. Проверка выбранного адреса запускается только вручную.</small>
  <div className="stack internet-controls"><label>Цель проверки<select value={presets.some(([host])=>host===target)?target:'custom'} onChange={e=>change(e.target.value==='custom'?'':e.target.value)}>{presets.map(([host,name])=><option key={host} value={host}>{name}</option>)}<option value="custom">Свой адрес</option></select></label><label>Домен или IP<input value={target} onChange={e=>change(e.target.value)} maxLength={253} placeholder="example.com" autoComplete="off" spellCheck={false}/></label><small>Без https:// и пути. Для HTTPS будет использовано защищённое соединение.</small><div className="actions"><button disabled={busy||remaining>0||!target.trim()} onClick={()=>void check('icmp')}>ICMP-пинг</button><button disabled={busy||remaining>0||!target.trim()} onClick={()=>void check('https')}>HTTPS-запрос</button></div>{busy?<Notice>Проверяем выбранный адрес…</Notice>:remaining>0&&<Notice>Повторная проверка через {remaining} сек.</Notice>}</div>
  {error&&<Notice error>{error}</Notice>}{last&&<div className="internet-result"><h3>{last.method==='icmp'?'ICMP-пинг':'HTTPS-запрос'} · {last.success?'Ответ получен':'Проверка не прошла'}</h3><p>{last.target}</p><dl className="detail-metrics"><div><dt>{last.method==='icmp'?'Задержка ICMP':'Время HTTPS-запроса'}</dt><dd>{last.latencyMs==null?'Недоступно':`${last.latencyMs.toFixed(0)} мс`}</dd></div>{last.httpStatus!=null&&<div><dt>Ответ HTTP</dt><dd>{last.httpStatus}</dd></div>}</dl><p>{last.message}</p><small>{new Date(last.checkedAt).toLocaleString('ru-RU')}</small></div>}
  <small>Отсутствие ответа ICMP не означает, что весь интернет недоступен. HTTPS измеряет длительность запроса, а не ICMP-пинг.</small>
 </Panel>;
}

