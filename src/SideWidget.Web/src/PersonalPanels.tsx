import {useEffect,useRef,useState,type ReactNode} from 'react';
import type {Snapshot} from './types';
import type {LocalProps,SessionText} from './LocalTools';
import {QrTool} from './LocalTools';
import {CopyButton,Icon,invoke,localDate,nativeAvailable,Notice,Panel,useAction} from './ui';
import {SellyBoardPanel} from './SellyBoardPanel';
import {type TranslationProvider} from './translation';

export function Calendar({state,compact=false,open}:{state:Snapshot;compact?:boolean;open?:()=>void}) {
 const [month,setMonth]=useState(()=>new Date(new Date().getFullYear(),new Date().getMonth(),1));const [selected,setSelected]=useState(localDate());
 const today=localDate();const start=(month.getDay()+6)%7;const days=new Date(month.getFullYear(),month.getMonth()+1,0).getDate();
 const dates=Array.from({length:start+days},(_,index)=>index<start?null:new Date(month.getFullYear(),month.getMonth(),index-start+1));
 function move(delta:number){setMonth(new Date(month.getFullYear(),month.getMonth()+delta,1));}
 if(compact)return <button className="day-glance" onClick={open}><span>Календарь и задачи</span><strong>{new Date().toLocaleDateString('ru-RU',{day:'numeric',month:'long',weekday:'short'})}</strong></button>;
 return <Panel title="Календарь" action={compact&&<button className="quiet" onClick={open}>Открыть</button>}>
  <div className="month-heading"><button className="icon-button" aria-label="Предыдущий месяц" onClick={()=>move(-1)}><Icon name="back" size={16}/></button><strong>{month.toLocaleDateString('ru-RU',{month:'long',year:'numeric'})}</strong><button className="icon-button" aria-label="Следующий месяц" onClick={()=>move(1)}><Icon name="arrow" size={16}/></button></div>
  <div className="calendar-grid" aria-label="Выбор даты">{['Пн','Вт','Ср','Чт','Пт','Сб','Вс'].map(day=><span className="weekday" key={day}>{day}</span>)}{dates.map((date,index)=>date?<button key={localDate(date)} className={`${localDate(date)===today?'today ':''}${localDate(date)===selected?'chosen':''}`} aria-label={date.toLocaleDateString('ru-RU',{day:'numeric',month:'long',year:'numeric'})} aria-pressed={localDate(date)===selected} onClick={()=>setSelected(localDate(date))}>{date.getDate()}</button>:<span key={`blank-${index}`}/>)}</div>
  {!compact&&<><div className="section-heading"><h3>{new Date(`${selected}T12:00:00`).toLocaleDateString('ru-RU',{day:'numeric',month:'long',weekday:'long'})}</h3><button className="quiet" onClick={()=>{setSelected(today);setMonth(new Date(new Date().getFullYear(),new Date().getMonth(),1));}}>Сегодня</button></div><SellyBoardPanel day={selected} start={localDate(month)} end={localDate(new Date(month.getFullYear(),month.getMonth()+1,1))}/></>}
 </Panel>;
}

export function Diary({state,setState}:LocalProps) {
 const [recoveryKey]=useState(()=>{const id=new URLSearchParams(location.search).get('windowId');return 'sidewidget.diary.recovery'+(id?'.'+id:'');});
 const [text,setText]=useState(()=>{
  const recovery=localStorage.getItem(recoveryKey);if(recovery!==null)return recovery;
  const legacy=localStorage.getItem('sidewidget.diary.recovery');
  if(legacy!==null&&recoveryKey!=='sidewidget.diary.recovery'){localStorage.setItem(recoveryKey,legacy);localStorage.removeItem('sidewidget.diary.recovery');}
  return legacy??state.drafts.diary;
 });const [status,setStatus]=useState('');const [preview,setPreview]=useState(false);const latest=useRef(text);const pending=useRef(Promise.resolve());const {error,run}=useAction();
 const lastSaved=useRef(state.drafts.diary);
 useEffect(()=>{if(latest.current===lastSaved.current){lastSaved.current=state.drafts.diary;latest.current=state.drafts.diary;setText(state.drafts.diary);try{localStorage.setItem(recoveryKey,state.drafts.diary);}catch{}}},[state.drafts.diary,recoveryKey]);
 function change(value:string){setText(value);latest.current=value;try{localStorage.setItem(recoveryKey,value);setStatus('Черновик сохранён на этом компьютере.');}catch{setStatus('Не удалось сохранить черновик. Скопируйте текст перед закрытием.');}}
 useEffect(()=>{if(text===lastSaved.current)return;const timer=setTimeout(()=>{const value=text;if(nativeAvailable){pending.current=pending.current.then(async()=>{try {const next=await invoke<Snapshot>('diary.draft',{text:value});lastSaved.current=value;if(latest.current===value){setState(next);setStatus('Черновик сохранён на этом компьютере.');}}catch{if(latest.current===value)setStatus('Текст сохранён локально; хранилище приложения пока недоступно.');}});}else {lastSaved.current=value;setState({...state,drafts:{diary:value}});}},450);return()=>clearTimeout(timer);},[text]);
 return <div className="stack"><p>Личный черновик. Синхронизация с дневником SellyBoard пока недоступна.</p><div className="actions"><button aria-pressed={!preview} onClick={()=>setPreview(false)}>Редактор</button><button aria-pressed={preview} onClick={()=>setPreview(true)}>Предпросмотр</button></div>
  {preview?<div className="markdown-preview">{text?<MarkdownPreview text={text}/>:<p>Здесь появится запись.</p>}</div>:<label>Запись<textarea autoFocus rows={12} value={text} onChange={e=>change(e.target.value)} placeholder={'# Сегодня\n\nЧто важно запомнить?'} maxLength={50000}/></label>}
  <small>Заголовки, списки, чекбоксы, выделение и код. HTML и внешние изображения не исполняются и не загружаются.</small><Notice>{status||'Черновик хранится только на этом компьютере.'}</Notice>
  <div className="actions"><CopyButton text={text}/><button disabled={!nativeAvailable} onClick={()=>void run(async()=>{setState(await invoke<Snapshot>('diary.draft',{text}));setStatus('Черновик сохранён на этом компьютере.');})}>Сохранить черновик</button></div>{error&&<Notice error>{error}</Notice>}
 </div>;
}
function MarkdownPreview({text}:{text:string}) {
 const lines=text.split('\n');const output:ReactNode[]=[];
 for(let index=0;index<lines.length;index++){
  const line=lines[index];const key=index;
  if(line.startsWith('```')){const code:string[]=[];while(++index<lines.length&&!lines[index].startsWith('```'))code.push(lines[index]);output.push(<pre key={key}><code>{code.join('\n')}</code></pre>);continue;}
  if(line.startsWith('### ')){output.push(<h3 key={key}><MarkdownInline text={line.slice(4)}/></h3>);continue;}
  if(line.startsWith('## ')){output.push(<h2 key={key}><MarkdownInline text={line.slice(3)}/></h2>);continue;}
  if(line.startsWith('# ')){output.push(<h1 key={key}><MarkdownInline text={line.slice(2)}/></h1>);continue;}
  const check=line.match(/^[-*] \[([ xX])\] (.*)$/);
  if(check){output.push(<div className="preview-check" key={key}><input type="checkbox" checked={check[1]!==' '} readOnly tabIndex={-1} aria-label={check[2]}/><span><MarkdownInline text={check[2]}/></span></div>);continue;}
  const ordered=/^\d+\. /.test(line);const listPattern=ordered?/^\d+\. /:/^[-*] /;
  if(listPattern.test(line)){const items:ReactNode[]=[];do{items.push(<li key={index}><MarkdownInline text={lines[index].replace(listPattern,'')}/></li>);index++;}while(index<lines.length&&listPattern.test(lines[index])&&!/^[-*] \[/.test(lines[index]));index--;output.push(ordered?<ol key={key}>{items}</ol>:<ul key={key}>{items}</ul>);continue;}
  output.push(<p key={key}><MarkdownInline text={line||'\u00a0'}/></p>);
 }
 return <>{output}</>;
}
function MarkdownInline({text}:{text:string}) {
 return <>{text.split(/(\*\*[^*]+\*\*|\*[^*]+\*|`[^`]+`|(?<!!)\[[^\]]+\]\(https?:\/\/[^\s)]+\))/g).map((part,index)=>{
  const link=part.match(/^\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)$/);if(link)return <DiaryLink key={index} label={link[1]} url={link[2]}/>;
  return part.startsWith('**')&&part.endsWith('**')?<strong key={index}>{part.slice(2,-2)}</strong>:part.startsWith('*')&&part.endsWith('*')?<em key={index}>{part.slice(1,-1)}</em>:part.startsWith('`')&&part.endsWith('`')?<code key={index}>{part.slice(1,-1)}</code>:part;
 })}</>;
}
function DiaryLink({label,url}:{label:string;url:string}) {
 const {error,run}=useAction();
 return <><a href={url} title={url} onClick={event=>{event.preventDefault();void run(()=>invoke('links.open',{url}));}}>{label}</a>{error&&<span className="link-error" role="alert"> {error}</span>}</>;
}

type WeatherState={city:string;temperature:number;apparent:number;wind:number;precipitation:number;code:number;time:string;fetchedAt:string;source:string;stale?:boolean;forecast:{date:string;min:number;max:number;code:number}[]};
function savedWeather():WeatherState|undefined {try{const value=JSON.parse(localStorage.getItem('sidewidget.weather.last')||'null');return value&&typeof value.temperature==='number'&&Array.isArray(value.forecast)?value:undefined;}catch{return undefined;}}
function weatherLabel(code:number){if(code===0)return 'Ясно';if(code<=3)return 'Облачно';if(code<50)return 'Туман';if(code<60)return 'Морось';if(code<70)return 'Дождь';if(code<80)return 'Снег';if(code<90)return 'Ливни';return 'Гроза';}
export function Weather({compact=false,open}:{compact?:boolean;open?:()=>void}) {
 const [city,setCity]=useState(()=>localStorage.getItem('sidewidget.weather.city')||'');const [value,setValue]=useState<WeatherState|undefined>(savedWeather);const {error,busy,run}=useAction();
 async function load(){await run(async()=>{const next=await invoke<WeatherState>('weather.get',{city});setValue(next);localStorage.setItem('sidewidget.weather.city',city);localStorage.setItem('sidewidget.weather.last',JSON.stringify(next));});}
 if(compact)return <button className="day-glance" onClick={open} title={value?.city||city||'Выбрать город'}><span>Погода</span><strong>{value?`${Math.round(value.temperature)}° · ${value.city}`:city||'Выбрать город'}</strong>{value&&<small>{new Date(value.fetchedAt).toLocaleString('ru-RU',{day:'numeric',month:'short',hour:'2-digit',minute:'2-digit'})}</small>}</button>;
 return <div className="stack"><form className="stack" onSubmit={e=>{e.preventDefault();void load();}}><label>Город<input autoFocus required value={city} onChange={e=>setCity(e.target.value)} placeholder="Например, Москва" maxLength={100}/></label><button className="primary" disabled={busy||!city.trim()}>{busy?'Загружаем…':'Получить погоду'}</button></form><small>Город отправляется Open-Meteo по нажатию кнопки. Автоматическая геолокация не используется.</small>{error&&<Notice error>{error}</Notice>}
  {value&&<><div className="weather-current"><strong>{Math.round(value.temperature)}°</strong><div><h2>{value.city}</h2><p>{weatherLabel(value.code)}</p><small>Ощущается как {Math.round(value.apparent)}°</small></div></div>{(value.stale||Date.now()-Date.parse(value.fetchedAt)>3600000)&&<Notice>Сохранённые данные. Обновите прогноз для выбранного города.</Notice>}<p>Ветер {value.wind} км/ч · осадки {value.precipitation} мм</p><div className="forecast">{value.forecast.map(day=><div key={day.date}><span>{new Date(`${day.date}T12:00:00`).toLocaleDateString('ru-RU',{day:'numeric',month:'short'})}</span><strong>{Math.round(day.min)}° / {Math.round(day.max)}°</strong><small>{weatherLabel(day.code)}</small></div>)}</div><small>Источник: {value.source}. Модельные данные на {value.time}. Получено {new Date(value.fetchedAt).toLocaleString('ru-RU')}.</small></>}
 </div>;
}

type CurrencyState={date:string;source:string;fetchedAt:string;stale:boolean;rates:{code:string;name:string;nominal:number;value:number;rate:number}[]};
export function Currency() {
 const [value,setValue]=useState<CurrencyState>();const [amount,setAmount]=useState('100');const [from,setFrom]=useState('USD');const [to,setTo]=useState('RUB');const {error,busy,run}=useAction();
 function load(){void run(async()=>setValue(await invoke<CurrencyState>('currency.get')));}
 useEffect(load,[]);
 const rates=value?.rates||[];const a=rates.find(r=>r.code===from);const b=rates.find(r=>r.code===to);const number=Number(amount.replace(',','.'));const result=a&&b&&amount.trim()&&Number.isFinite(number)?(number*a.rate/b.rate).toLocaleString('ru-RU',{maximumFractionDigits:4}):'';
 return <div className="stack"><label>Сумма<input autoFocus inputMode="decimal" value={amount} onChange={e=>setAmount(e.target.value)}/></label><div className="form-row"><label>Из<select value={from} onChange={e=>setFrom(e.target.value)} disabled={!rates.length}>{rates.map(rate=><option key={rate.code} value={rate.code}>{rate.code} · {rate.name}</option>)}</select></label><label>В<select value={to} onChange={e=>setTo(e.target.value)} disabled={!rates.length}>{rates.map(rate=><option key={rate.code} value={rate.code}>{rate.code} · {rate.name}</option>)}</select></label></div><div className="actions"><button onClick={()=>{setFrom(to);setTo(from);}} disabled={!rates.length}>Поменять местами</button><button onClick={load} disabled={busy}>{busy?'Загружаем…':'Обновить курсы'}</button></div>{result&&<div className="answer"><output>{result} <small>{to}</small></output><CopyButton text={result}/></div>}{error&&<Notice error>{error}</Notice>}
  {value&&<><Notice>{value.stale?'Сохранённые курсы; обновление не удалось.':'Последние опубликованные курсы.'}</Notice><small>{value.source} · котировка на {value.date}. Получено {new Date(value.fetchedAt).toLocaleString('ru-RU')}.</small></>}<small>Номинал учтён. Результат округлён до 4 знаков. Это справочный курс, не цена обмена в банке.</small>
 </div>;
}

const translationLanguages=[['ru','Русский'],['en','Английский'],['de','Немецкий'],['fr','Французский'],['es','Испанский'],['zh','Китайский'],['ja','Японский']];
const recentLanguagesKey='sidewidget.translation.recentLanguages';
const translationSelectionKey='sidewidget.translation.selection';
const defaultTranslationSelection={provider:'mymemory' as TranslationProvider,source:'en',target:'ru'};

function readTranslationSelection():typeof defaultTranslationSelection {
 try {
  const saved=JSON.parse(localStorage.getItem(translationSelectionKey)??'null');
  if(saved){
   const provider:TranslationProvider=saved.provider==='google'||saved.provider==='yandex'||saved.provider==='deepl'?saved.provider:'mymemory';
   const source=(saved.source==='auto'&&provider!=='mymemory')||translationLanguages.some(([code])=>code===saved.source)?saved.source:'en';
   const target=translationLanguages.some(([code])=>code===saved.target)?saved.target:'ru';
   return {provider,source,target};
  }
 }catch{}
 return defaultTranslationSelection;
}

function readRecentLanguages():string[] {
 try {
  const stored:unknown=JSON.parse(localStorage.getItem(recentLanguagesKey)??'[]');
  if(Array.isArray(stored))return [...new Set(stored.filter(code=>translationLanguages.some(([id])=>id===code)))];
 }catch{}
 return [];
}

export function Translation({sessionText,sessionResult}:{sessionText?:SessionText;sessionResult?:SessionText}={}) {
 const [config,setConfig]=useState<{configured:boolean}>();
 const [selection,setSelection]=useState(readTranslationSelection);
 const {provider,source,target}=selection;
 const localText=useState('');const localResult=useState('');const [qr,setQr]=useState(false);
 const [text,setText]=sessionText??localText;
 const [result,setResult]=sessionResult??localResult;
 const [busy,setBusy]=useState(false);const [error,setError]=useState('');const revision=useRef(0);const pending=useRef(false);const configure=useAction();
 const [recentLanguages,setRecentLanguages]=useState(readRecentLanguages);
 const recentOptions=recentLanguages.map(code=>translationLanguages.find(([id])=>id===code)!);
 const otherOptions=translationLanguages.filter(([code])=>!recentLanguages.includes(code));
 const languageOption=([code,name]:string[])=><option key={code} value={code}>{name}</option>;
 const providerName=provider==='mymemory'?'MyMemory':provider==='deepl'?'DeepL':provider==='google'?'Google':'Яндекс';const textLimit=provider==='google'?5000:10000;
 const byteCount=new TextEncoder().encode(text).length;
 const canTranslate=!busy&&!configure.busy&&!!text.trim()&&text.length<=textLimit&&(provider!=='deepl'||config?.configured===true)&&(provider!=='mymemory'||byteCount<=500);

 useEffect(()=>{let alive=true;void invoke<{configured:boolean}>('translation.get').then(value=>{if(alive)setConfig(value);}).catch(()=>{});return()=>{alive=false;revision.current++;if(pending.current)void invoke('translation.cancel').catch(()=>{});};},[]);
 function invalidate(){revision.current++;setResult('');setQr(false);setError('');}
 function select(next:typeof selection){
  invalidate();
  setSelection(next);
  try{localStorage.setItem(translationSelectionKey,JSON.stringify(next));}catch{}
 }
 function rememberLanguages(...codes:string[]){
  const next=[...new Set([...codes,...recentLanguages])].filter(code=>code!=='auto');
  setRecentLanguages(next);
  try{localStorage.setItem(recentLanguagesKey,JSON.stringify(next));}catch{}
 }
 async function translate(){
  if(!canTranslate||pending.current)return;
  const current=++revision.current;pending.current=true;setBusy(true);setError('');setResult('');setQr(false);
  try{const value=await invoke<{text:string;provider:string}>('translation.run',{text,source,target,provider});if(current===revision.current)setResult(value.text);}
  catch(e){if(current===revision.current&&!(e instanceof Error&&'code' in e&&e.code==='cancelled'))setError(e instanceof Error?e.message:'Не удалось выполнить перевод');}
  finally{if(current===revision.current){pending.current=false;setBusy(false);}}
 }
 async function cancel(){revision.current++;try{await invoke('translation.cancel');}catch{setError('Не удалось отменить запрос. Дождитесь завершения.');}finally{pending.current=false;setBusy(false);}}
 return <div className="stack">
  <label>Переводчик<select value={provider} disabled={busy||configure.busy} onChange={e=>{const next=e.target.value as TranslationProvider;select({provider:next,source:next==='mymemory'&&source==='auto'?'en':source,target});}}><option value="mymemory">MyMemory · бесплатно, без ключа</option><option value="google">Google · бесплатно, без ключа</option><option value="yandex">Яндекс · бесплатно, без ключа</option><option value="deepl">DeepL API · свой ключ</option></select></label>
  <small>Текст отправится в {providerName} по кнопке «Перевести» или Ctrl+Enter.</small>
  {provider==='mymemory'&&<small>Без регистрации. До 500 байт за запрос и 5 000 символов в сутки; лимит сервиса общий для IP.</small>}
  {provider==='deepl'&&<>{!config?.configured&&<Notice>Для DeepL нужен API-ключ. MyMemory работает без него.</Notice>}<button disabled={busy||configure.busy} onClick={()=>void configure.run(async()=>{await invoke('translation.configure');setConfig(await invoke('translation.get'));})}>{configure.busy?'Окно настройки открыто…':'Настроить ключ DeepL'}</button>{configure.error&&<Notice error>{configure.error}</Notice>}</>}
  <div className="form-row translation-languages">
   <label>Исходный язык<select disabled={busy} value={source} onChange={e=>{select({...selection,source:e.target.value});rememberLanguages(e.target.value);}}>{recentOptions.map(languageOption)}{provider!=='mymemory'&&<option value="auto">Определить автоматически</option>}{otherOptions.map(languageOption)}</select></label>
   <button type="button" className="translation-swap" aria-label="Поменять языки местами" disabled={busy||source==='auto'} onClick={()=>{select({provider,source:target,target:source});rememberLanguages(source,target);if(result)setText(result);}}><Icon name="swap" size={20}/></button>
   <label>Язык перевода<select disabled={busy} value={target} onChange={e=>{select({...selection,target:e.target.value});rememberLanguages(e.target.value);}}>{recentOptions.map(languageOption)}{otherOptions.map(languageOption)}</select></label>
  </div>
  <div className="form-row translation-texts">
   <label>Исходный текст<textarea autoFocus disabled={busy} rows={5} value={text} maxLength={textLimit} onChange={e=>{invalidate();setText(e.target.value);}} onKeyDown={e=>{
    if(e.key!=='Enter'||!e.ctrlKey||e.altKey||e.shiftKey||e.metaKey||e.nativeEvent.isComposing)return;
    e.preventDefault();
    if(!e.repeat)void translate();
   }}/></label>
   <label>Результат<textarea readOnly rows={5} value={result}/></label>
  </div>
  <small>{provider==='mymemory'?`${byteCount} / 500 байт · кириллица обычно занимает 2 байта`:`${text.length} / ${textLimit.toLocaleString("ru-RU")} символов`}</small>
  {provider==='mymemory'&&byteCount>500&&<Notice>Сократите текст до 500 байт.</Notice>}

  {text.length>textLimit&&<Notice>Сократите текст до {textLimit.toLocaleString("ru-RU")} символов для выбранного переводчика.</Notice>}
  <div className="actions"><button className="primary" aria-keyshortcuts="Control+Enter" disabled={!canTranslate} onClick={()=>void translate()}>{busy?'Переводим…':'Перевести'}</button>{busy&&<button onClick={()=>void cancel()}>Отменить запрос</button>}</div>
  {error&&<Notice error>{error}</Notice>}
  {result&&<><div className="actions"><CopyButton text={result}/><button onClick={()=>setQr(v=>!v)}>{qr?'Скрыть QR':'Показать QR'}</button></div>{qr&&<QrTool initialText={result}/>}</>}
 </div>;
}





