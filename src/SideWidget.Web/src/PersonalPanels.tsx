import {locale, t} from './i18n';
import {useEffect,useId,useRef,useState,type ReactNode} from 'react';
import type {Snapshot} from './types';
import type {LocalProps,SessionText} from './LocalTools';
import {QrTool} from './LocalTools';
import {CopyButton,Icon,invoke,localDate,nativeAvailable,Notice,Panel,useAction} from './ui';
import {type TranslationProvider} from './translation';
import {CalendarPanel} from './CalendarPanel';
import {useSonioxVoice} from './sonioxVoice';

export function Calendar({compact=false,open}:{compact?:boolean;open?:()=>void}) {
 if(compact)return <button className="day-glance" onClick={open}><span>{t('Календарь')}</span><strong>{new Date().toLocaleDateString(locale(),{day:'numeric',month:'long',weekday:'short'})}</strong></button>;
 return <CalendarPanel/>;
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
 return <div className="stack"><div className="actions"><button aria-pressed={!preview} onClick={()=>setPreview(false)}>{t('Редактор')}</button><button aria-pressed={preview} onClick={()=>setPreview(true)}>{t('Предпросмотр')}</button></div>
  {preview?<div className="markdown-preview">{text?<MarkdownPreview text={text}/>:<p>{t('Здесь появится запись.')}</p>}</div>:<label>{t('Запись')}<textarea autoFocus rows={12} value={text} onChange={e=>change(e.target.value)} placeholder={t('# Сегодня\n\nЧто важно запомнить?')} maxLength={50000}/></label>}
  <small>{t('Заголовки, списки, чекбоксы, выделение и код. HTML и внешние изображения не исполняются и не загружаются.')}</small><Notice>{t(status||'Черновик хранится только на этом компьютере.')}</Notice>
  <div className="actions"><CopyButton text={text}/><button disabled={!nativeAvailable} onClick={()=>void run(async()=>{setState(await invoke<Snapshot>('diary.draft',{text}));setStatus('Черновик сохранён на этом компьютере.');})}>{t('Сохранить черновик')}</button></div>{error&&<Notice error>{t(error)}</Notice>}
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
function weatherLabel(code:number){if(code===0)return t('Ясно');if(code<=3)return t('Облачно');if(code<50)return t('Туман');if(code<60)return t('Морось');if(code<70)return t('Дождь');if(code<80)return t('Снег');if(code<90)return t('Ливни');return t('Гроза');}
export function Weather({compact=false,open}:{compact?:boolean;open?:()=>void}) {
 const [city,setCity]=useState(()=>localStorage.getItem('sidewidget.weather.city')||'');const [value,setValue]=useState<WeatherState|undefined>(savedWeather);const {error,busy,run}=useAction();
 async function load(){await run(async()=>{const next=await invoke<WeatherState>('weather.get',{city});setValue(next);localStorage.setItem('sidewidget.weather.city',city);localStorage.setItem('sidewidget.weather.last',JSON.stringify(next));});}
 if(compact)return <button className="day-glance" onClick={open} title={value?.city||city||t('Выбрать город')}><span>{t('Погода')}</span><strong>{value?`${Math.round(value.temperature)}° · ${value.city}`:city||t('Выбрать город')}</strong>{value&&<small>{new Date(value.fetchedAt).toLocaleString(locale(),{day:'numeric',month:'short',hour:'2-digit',minute:'2-digit'})}</small>}</button>;
 return <div className="stack"><form className="stack" onSubmit={e=>{e.preventDefault();void load();}}><label>{t('Город')}<input autoFocus required value={city} onChange={e=>setCity(e.target.value)} placeholder={t('Например, Москва')} maxLength={100}/></label><button className="primary" disabled={busy||!city.trim()}>{busy?t('Загружаем…'):t('Получить погоду')}</button></form><small>{t('Город отправляется Open-Meteo по нажатию кнопки. Автоматическая геолокация не используется.')}</small>{error&&<Notice error>{t(error)}</Notice>}
  {value&&<><div className="weather-current"><strong>{Math.round(value.temperature)}°</strong><div><h2>{value.city}</h2><p>{weatherLabel(value.code)}</p><small>{t('Ощущается как {temperature}°',{temperature:Math.round(value.apparent)})}</small></div></div>{(value.stale||Date.now()-Date.parse(value.fetchedAt)>3600000)&&<Notice>{t('Сохранённые данные. Обновите прогноз для выбранного города.')}</Notice>}<p>{t('Ветер {wind} км/ч · осадки {precipitation} мм',{wind:value.wind.toLocaleString(locale()),precipitation:value.precipitation.toLocaleString(locale())})}</p><div className="forecast">{value.forecast.map(day=><div key={day.date}><span>{new Date(`${day.date}T12:00:00`).toLocaleDateString(locale(),{day:'numeric',month:'short'})}</span><strong>{Math.round(day.min)}° / {Math.round(day.max)}°</strong><small>{weatherLabel(day.code)}</small></div>)}</div><small>{t('Источник: {source}. Модельные данные на {time}. Получено {fetchedAt}.',{source:t(value.source),time:value.time,fetchedAt:new Date(value.fetchedAt).toLocaleString(locale())})}</small></>}
 </div>;
}

type CurrencyState={date:string;source:string;fetchedAt:string;stale:boolean;rates:{code:string;name:string;nominal:number;value:number;rate:number}[]};
export function Currency() {
 const [value,setValue]=useState<CurrencyState>();const [amount,setAmount]=useState('100');const [from,setFrom]=useState('USD');const [to,setTo]=useState('RUB');const {error,busy,run}=useAction();
 function load(){void run(async()=>setValue(await invoke<CurrencyState>('currency.get')));}
 useEffect(load,[]);
 const currencyNames=locale()==='ru'?null:new Intl.DisplayNames(locale(),{type:'currency',fallback:'none'});
 const rates=(value?.rates??[]).map(rate=>({...rate,name:(currencyNames && /^[A-Z]{3}$/.test(rate.code) ? currencyNames.of(rate.code) : undefined) ?? rate.name}));const a=rates.find(r=>r.code===from);const b=rates.find(r=>r.code===to);const number=Number(amount.replace(',','.'));const result=a&&b&&amount.trim()&&Number.isFinite(number)?(number*a.rate/b.rate).toLocaleString(locale(),{maximumFractionDigits:4}):'';
 return <div className="stack"><label>{t('Сумма')}<input autoFocus inputMode="decimal" value={amount} onChange={e=>setAmount(e.target.value)}/></label><div className="form-row"><label>{t('Из')}<select value={from} onChange={e=>setFrom(e.target.value)} disabled={!rates.length}>{rates.map(rate=><option key={rate.code} value={rate.code}>{rate.code} · {rate.name}</option>)}</select></label><label>{t('В')}<select value={to} onChange={e=>setTo(e.target.value)} disabled={!rates.length}>{rates.map(rate=><option key={rate.code} value={rate.code}>{rate.code} · {rate.name}</option>)}</select></label></div><div className="actions"><button onClick={()=>{setFrom(to);setTo(from);}} disabled={!rates.length}>{t('Поменять местами')}</button><button onClick={load} disabled={busy}>{busy?t('Загружаем…'):t('Обновить курсы')}</button></div>{result&&<div className="answer"><output>{result} <small>{to}</small></output><CopyButton text={result}/></div>}{error&&<Notice error>{t(error)}</Notice>}
  {value&&<><Notice>{value.stale?t('Сохранённые курсы; обновление не удалось.'):t('Последние опубликованные курсы.')}</Notice><small>{t('{source} · котировка на {date}. Получено {fetchedAt}.',{source:t(value.source),date:value.date,fetchedAt:new Date(value.fetchedAt).toLocaleString(locale())})}</small></>}<small>{t('Номинал учтён. Результат округлён до 4 знаков. Это справочный курс, не цена обмена в банке.')}</small>
 </div>;
}

const translationLanguages=[['ru','Русский'],['en','Английский'],['de','Немецкий'],['fr','Французский'],['es','Испанский'],['zh','Китайский'],['ja','Японский']];
const recentLanguagesKey='sidewidget.translation.recentLanguages';
const translationSelectionKey='sidewidget.translation.selection';
const textTranslationProviderKey='sidewidget.translation.textProvider';
type TextTranslationProvider=Exclude<TranslationProvider,'soniox'>;

function readTextTranslationProvider():TextTranslationProvider {
 try{
  const saved=localStorage.getItem(textTranslationProviderKey);
  if(saved==='google'||saved==='yandex'||saved==='deepl')return saved;
 }catch{}
 return 'mymemory';
}
const defaultTranslationSelection={provider:'mymemory' as TranslationProvider,source:'en',target:'ru'};

function readTranslationSelection():typeof defaultTranslationSelection {
 try {
  const saved=JSON.parse(localStorage.getItem(translationSelectionKey)??'null');
  if(saved){
   const provider:TranslationProvider=saved.provider==='google'||saved.provider==='yandex'||saved.provider==='deepl'||saved.provider==='soniox'?saved.provider:'mymemory';
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

const dictationEngineKey='sidewidget.translation.dictationEngine';

function readDictationEngine(){
 try{if(localStorage.getItem(dictationEngineKey)==='soniox')return 'soniox';}catch{}
 return 'local';
}

type TranslationConfig={configured:boolean;sonioxConfigured?:boolean};

export function Translation({sessionText,sessionResult,active=true}:{sessionText?:SessionText;sessionResult?:SessionText;active?:boolean}={}) {
 const [config,setConfig]=useState<TranslationConfig>();
 const [selection,setSelection]=useState(readTranslationSelection);
 const {provider,source,target}=selection;
 const [textProvider,setTextProvider]=useState(()=>provider==='soniox'?readTextTranslationProvider():provider);
 const localText=useState('');const localResult=useState('');const [qr,setQr]=useState(false);
 const [text,setText]=sessionText??localText;
 const [result,setResult]=sessionResult??localResult;
 const [busy,setBusy]=useState(false);const [error,setError]=useState('');const revision=useRef(0);const pending=useRef(false);const configure=useAction();
 const isSoniox=provider==='soniox';
 const [dictationEngine,setDictationEngine]=useState(readDictationEngine);
 const localDictation=!isSoniox&&dictationEngine==='local';
 const voice=useSonioxVoice(active,source,target);
 const voiceBusy=voice.phase!=='idle';
 const canSubmitVoice=voice.phase==='recording'||voice.phase==='ready';
 const voiceStatus=voice.phase==='opening'?'Подключаем микрофон…':voice.phase==='recording'?'Идёт запись':
  voice.phase==='ready'?(isSoniox?'Запись готова к переводу':'Запись готова к распознаванию'):
  voice.phase==='translating'?(isSoniox?'Переводим…':'Распознаём речь…'):
  voice.phase==='loadingAudio'?'Готовим озвучивание…':voice.phase==='playing'?'Идёт озвучивание':'';
 const locked=busy||voiceBusy||configure.busy;
 const visibleResult=isSoniox?voice.value?.text??'':result;
 const visibleSource=isSoniox?voice.value?.transcript??'':text;
 const sourceId=useId(),resultId=useId();
 const [recentLanguages,setRecentLanguages]=useState(readRecentLanguages);
 const recentOptions=recentLanguages.map(code=>translationLanguages.find(([id])=>id===code)!);
 const otherOptions=translationLanguages.filter(([code])=>!recentLanguages.includes(code));
 const languageOption=([code,name]:string[])=><option key={code} value={code}>{t(name)}</option>;
 const providerName=provider==='mymemory'?'MyMemory':provider==='deepl'?'DeepL':provider==='google'?'Google':t('Яндекс');const textLimit=provider==='google'?5000:10000;
 const byteCount=new TextEncoder().encode(text).length;
 const canTranslate=!isSoniox&&!locked&&!!text.trim()&&text.length<=textLimit&&(provider!=='deepl'||config?.configured===true)&&(provider!=='mymemory'||byteCount<=500);

 useEffect(()=>{let alive=true;void invoke<TranslationConfig>('translation.get').then(value=>{if(alive)setConfig(value);}).catch(()=>{});return()=>{alive=false;revision.current++;if(pending.current)void invoke('translation.cancel').catch(()=>{});};},[]);
 function invalidate(){revision.current++;setResult('');setQr(false);setError('');}
 function select(next:typeof selection){
  invalidate();
  voice.clear();
  setSelection(next);
  try{localStorage.setItem(translationSelectionKey,JSON.stringify(next));}catch{}
 }
 function selectProvider(next:TranslationProvider){
  select({provider:next,source:next==='mymemory'&&source==='auto'?'en':source,target});
  if(next==='soniox')return;
  setTextProvider(next);
  try{localStorage.setItem(textTranslationProviderKey,next);}catch{}
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
 function configureKey(useSoniox=isSoniox){
  return configure.run(async()=>{
   await invoke(useSoniox?'translation.configureSoniox':'translation.configure');
   setConfig(await invoke<TranslationConfig>('translation.get'));
  });
 }
 function appendTranscript(transcript:string){
  invalidate();
  const separator=text&&!/\s$/.test(text)?' ':'';
  setText(text+separator+transcript);
 }
 const recordLabel=isSoniox
  ? (voice.phase==='ready'?'Перевести запись':voice.phase==='recording'?'Стоп и перевести':'Начать запись')
  : (voice.phase==='ready'?'Распознать запись':voice.phase==='recording'?'Стоп и вставить':'Диктовать текст');

 function listenButton(field:'source'|'target',value:string){
  const playing=voice.speakingField===field;
  const label=t(playing?'Остановить озвучивание':field==='target'?'Прослушать результат':
    isSoniox?'Прослушать распознанную речь':'Прослушать исходный текст');
  return <button type="button" className="icon-button translation-listen" data-help={label} aria-label={label}
    disabled={!active||busy||configure.busy||!value.trim()||(voiceBusy&&!voice.speakingField)}
    onClick={()=>{
     if(voice.speakingField)voice.cancel();
     if(!playing)void voice.speak(value,field==='source'?source:target,field,isSoniox&&field==='target'&&!!config?.sonioxConfigured);
    }}><Icon name={playing?'stop':'sound'} size={17}/></button>;
 }

 return <div className="stack">
  <div className="actions" role="group" aria-label={t('Способ ввода')}>
   <button type="button" aria-pressed={!isSoniox} disabled={locked} onClick={()=>{if(isSoniox)selectProvider(textProvider);}}>{t('Текст')}</button>
   <button type="button" aria-pressed={isSoniox} disabled={locked} onClick={()=>{if(!isSoniox)selectProvider('soniox');}}>{t('Голос')}</button>
  </div>
  <label>{t('Переводчик')}<select value={provider} disabled={locked} onChange={e=>selectProvider(e.target.value as TranslationProvider)}><option value="mymemory">{t('MyMemory · бесплатно, без ключа')}</option><option value="google">{t('Google · бесплатно, без ключа')}</option><option value="yandex">{t('Яндекс · бесплатно, без ключа')}</option><option value="deepl">{t('DeepL API · свой ключ')}</option><option value="soniox">{t('Soniox · голосовой перевод')}</option></select></label>
  <small>{isSoniox?t('Запись до 30 секунд отправится в Soniox по кнопке перевода.'):t('Текст отправится в {provider} по кнопке «Перевести» или Ctrl+Enter.',{provider:providerName})}</small>
  {provider==='mymemory'&&<small>{t('Без регистрации. До 500 байт за запрос и 5 000 символов в сутки; лимит сервиса общий для IP.')}</small>}
  {(provider==='deepl'||(isSoniox&&config?.sonioxConfigured))&&<>
   {provider==='deepl'&&!config?.configured&&<Notice>{t('Для DeepL нужен API-ключ. MyMemory работает без него.')}</Notice>}
   <button disabled={locked} onClick={()=>void configureKey()}>{configure.busy?t('Окно настройки открыто…'):isSoniox?t('Настроить ключ Soniox'):t('Настроить ключ DeepL')}</button>
  </>}
  {configure.error&&<Notice error>{configure.error}</Notice>}
  <div className="form-row translation-languages">
   <label>{t('Исходный язык')}<select disabled={locked} value={source} onChange={e=>{select({...selection,source:e.target.value});rememberLanguages(e.target.value);}}>{recentOptions.map(languageOption)}{provider!=='mymemory'&&<option value="auto">{t('Определить автоматически')}</option>}{otherOptions.map(languageOption)}</select></label>
   <button type="button" className="translation-swap" aria-label={t('Поменять языки местами')} disabled={locked||source==='auto'} onClick={()=>{select({provider,source:target,target:source});rememberLanguages(source,target);if(!isSoniox&&result)setText(result);}}><Icon name="swap" size={20}/></button>
   <label>{t('Язык перевода')}<select disabled={locked} value={target} onChange={e=>{select({...selection,target:e.target.value});rememberLanguages(e.target.value);}}>{recentOptions.map(languageOption)}{otherOptions.map(languageOption)}</select></label>
  </div>
  {!isSoniox&&<label>{t('Распознавание речи')}<select value={dictationEngine} disabled={locked} onChange={e=>{
   const engine=e.target.value==='soniox'?'soniox':'local';
   setDictationEngine(engine);
   voice.clear();
   try{localStorage.setItem(dictationEngineKey,engine);}catch{}
  }}><option value="local">{t('На компьютере')}</option><option value="soniox">Soniox</option></select></label>}
  {isSoniox&&!config?.sonioxConfigured&&<small>{t('Добавьте ключ Soniox API, чтобы начать запись.')}</small>}
  <div className="actions">
   {isSoniox&&!config?.sonioxConfigured?
    <button className="primary" disabled={locked} onClick={()=>void configureKey(true)}>{configure.busy?t('Окно настройки открыто…'):t('Настроить ключ Soniox')}</button>:
    <button className={isSoniox?'primary':undefined} disabled={!active||busy||configure.busy}
     data-help={localDictation?t('При первой диктовке скачивается модель (около 60 МБ). Затем распознавание работает без интернета.'):
      !isSoniox?t('Soniox расшифрует запись и добавит её в исходный текст. Для диктовки нужен ключ Soniox API.'):undefined}
     aria-disabled={voiceBusy&&!canSubmitVoice||undefined} onClick={()=>{
      if(canSubmitVoice){setQr(false);void voice.submit(isSoniox?undefined:appendTranscript);}
      else if(!voiceBusy){
       if(localDictation||config?.sonioxConfigured)void voice.start(localDictation);
       else void configureKey(true);
      }
     }}>{t(recordLabel)}</button>}
   {voiceBusy&&!voice.speakingField&&<button onClick={()=>voice.cancel()}>{t('Отмена')}</button>}
  </div>
  <small role="status" className={voiceStatus||voice.phase==='preparing'?undefined:'sr-only'}>{voice.phase==='preparing'?
   t('Загружаем модель распознавания…'):t(voiceStatus)}</small>
  {voice.phase==='preparing'&&voice.progress!==null&&<progress className="translation-model-progress" aria-label={t('Загрузка модели распознавания')} max={100} value={voice.progress}/>}
  {voice.error&&<Notice error>{t(voice.error)}</Notice>}
  <div className="form-row translation-texts">
   <div className="translation-text-field">
    <div className="translation-field-heading">
     <label htmlFor={sourceId}>{t(isSoniox?'Распознанная речь':'Исходный текст')}</label>
     {listenButton('source',visibleSource)}
    </div>
    {isSoniox?<textarea id={sourceId} key="voice" readOnly rows={5} value={visibleSource}/>:
     <textarea id={sourceId} key="text" autoFocus disabled={busy||voiceBusy} rows={5} value={text} maxLength={textLimit}
      onChange={e=>{invalidate();setText(e.target.value);}} onKeyDown={e=>{
       if(e.key!=='Enter'||!e.ctrlKey||e.altKey||e.shiftKey||e.metaKey||e.nativeEvent.isComposing)return;
       e.preventDefault();
       if(!e.repeat)void translate();
      }}/>}
   </div>
   <div className="translation-text-field">
    <div className="translation-field-heading">
     <label htmlFor={resultId}>{t('Результат')}</label>
     {listenButton('target',visibleResult)}
    </div>
    <textarea id={resultId} readOnly rows={5} value={visibleResult}/>
   </div>
  </div>
  {!isSoniox&&<>
  <small>{provider==='mymemory'?t('{count} / 500 байт · кириллица обычно занимает 2 байта',{count:byteCount}):t('{count} / {limit} символов',{count:text.length,limit:textLimit.toLocaleString(locale())})}</small>
  {provider==='mymemory'&&byteCount>500&&<Notice>{t('Сократите текст до 500 байт.')}</Notice>}

  {text.length>textLimit&&<Notice>{t('Сократите текст до {limit} символов для выбранного переводчика.',{limit:textLimit.toLocaleString(locale())})}</Notice>}
  <div className="actions"><button className="primary" aria-keyshortcuts="Control+Enter" disabled={!canTranslate} onClick={()=>void translate()}>{busy?t('Переводим…'):t('Перевести')}</button>{busy&&<button onClick={()=>void cancel()}>{t('Отменить запрос')}</button>}</div>
  {error&&<Notice error>{t(error)}</Notice>}</>}
  {visibleResult&&<><div className="actions"><CopyButton text={visibleResult}/><button onClick={()=>setQr(v=>!v)}>{qr?t('Скрыть QR'):t('Показать QR')}</button></div>{qr&&<QrTool initialText={visibleResult}/>}</>}
 </div>;
}





