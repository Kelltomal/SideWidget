import {useEffect,useId,useLayoutEffect,useMemo,useRef,useState} from 'react';
import {evaluate,unitCategories,convertUnit,parseColor,makeQr} from './tools';
import type {Snapshot} from './types';
import {CopyButton,Icon,invoke,nativeAvailable,Notice,useAction} from './ui';
import {QrStickerPrint} from './QrStickerPrint';
export type LocalProps={state:Snapshot;setState:(state:Snapshot)=>void};
export type SessionText=[string,(value:string)=>void];

const calculatorKeys=['(',')','%','÷','7','8','9','×','4','5','6','−','1','2','3','+','0',','];
function previewCalculation(text:string){
 if(!text.trim())return '';
 try{return evaluate(text);}catch{return '';}
}

export function Calculator({state,setState,sessionExpression}:LocalProps&{sessionExpression?:SessionText}) {
 const localExpression=useState('');
 const [expression,setExpression]=sessionExpression??localExpression;
 const [selection,setSelection]=useState<{start:number;end:number}|null>(null);
 const [keypadOpen,setKeypadOpen]=useState(false);
 const fieldId=useId();
 const input=useRef<HTMLInputElement>(null);
 const selectionTip=useRef<HTMLSpanElement>(null);
 const caret=useRef<number|null>(null);
 const {error,busy,run,setError}=useAction();
 const [confirmClear,setConfirmClear]=useState(false);
 const preview=useMemo(()=>previewCalculation(expression),[expression]);
 const selectedText=selection?expression.slice(selection.start,selection.end):'';
 const selectionPreview=useMemo(()=>previewCalculation(selectedText),[selectedText]);
 function readSelection(){
  const field=input.current;
  const start=field?.selectionStart??0;
  const end=field?.selectionEnd??0;
  setSelection(current=>start===end?null:current?.start===start&&current.end===end?current:{start,end});
 }
 useEffect(()=>{
  const field=input.current;
  field?.addEventListener('selectionchange',readSelection);
  return()=>field?.removeEventListener('selectionchange',readSelection);
 },[]);
 useLayoutEffect(()=>{
  const field=input.current;
  const tip=selectionTip.current;
  if(!field?.offsetWidth||!tip||!selection||!selectionPreview)return;
  const context=document.createElement('canvas').getContext('2d');
  if(!context)return;
  const {start,end}=selection;
  function positionTip(){
   if(!field||!tip||!context)return;
   const style=getComputedStyle(field);
   context.font=`${style.fontStyle} ${style.fontWeight} ${style.fontSize} ${style.fontFamily}`;
   const contentLeft=field.clientLeft+parseFloat(style.paddingLeft);
   const textLeft=contentLeft-field.scrollLeft;
   const textRight=field.clientLeft+field.clientWidth-parseFloat(style.paddingRight);
   const startX=Math.max(contentLeft,textLeft+context.measureText(expression.slice(0,start)).width);
   const endX=Math.min(textRight,textLeft+context.measureText(expression.slice(0,end)).width);
   const anchor=(startX+endX)/2;
   const left=Math.max(0,Math.min(anchor-tip.offsetWidth/2,field.offsetWidth-tip.offsetWidth));
   tip.style.left=`${left}px`;
   tip.style.setProperty('--selection-arrow',`${anchor-left}px`);
   tip.style.visibility=endX>startX?'visible':'hidden';
  }
  positionTip();
  field.addEventListener('scroll',positionTip);
  const resize=new ResizeObserver(positionTip);
  resize.observe(field);
  const fonts=document.fonts;
  fonts?.addEventListener('loadingdone',positionTip);
  return()=>{
   field.removeEventListener('scroll',positionTip);
   resize.disconnect();
   fonts?.removeEventListener('loadingdone',positionTip);
  };
 },[expression,selection,selectionPreview]);
 function updateExpression(next:string){setExpression(next);setSelection(null);setError('');}
 function focusAt(position:number){input.current?.focus();input.current?.setSelectionRange(position,position);}
 useLayoutEffect(()=>{
  if(caret.current===null)return;
  focusAt(caret.current);caret.current=null;
 },[expression]);
 function replaceExpression(next:string,position=next.length){
  if(next.length>500)return;
  if(next===expression)focusAt(position);
  else caret.current=position;
  updateExpression(next);
 }
 function editKey(symbol:string){
  const start=input.current?.selectionStart??expression.length;
  const end=input.current?.selectionEnd??start;
  const from=!symbol&&start===end?Math.max(0,start-1):start;
  replaceExpression(expression.slice(0,from)+symbol+expression.slice(end),from+symbol.length);
 }
 async function calculate() {
  if(!expression.trim()||busy)return;
  await run(async()=>{
   const answer=evaluate(expression);
   if(nativeAvailable)setState(await invoke<Snapshot>('history.add',{expression,result:answer}));
   else setState({...state,calculatorHistory:[{id:crypto.randomUUID(),expression,result:answer,time:new Date().toISOString()},...state.calculatorHistory].slice(0,100)});
  });
 }
 async function remove(id:string) {
  await run(async()=>setState(nativeAvailable?await invoke<Snapshot>('history.delete',{id}):{...state,calculatorHistory:state.calculatorHistory.filter(item=>item.id!==id)}));
 }
 return <div className="stack">
   <div className="answer calculator-answer">
    <div><small>Результат</small><output>{preview||'—'}</output></div>
    {preview&&<div className="actions"><CopyButton text={preview}/><button type="button" aria-label="Продолжить с результата" onClick={()=>replaceExpression(preview)}><Icon name="arrow"/></button></div>}
   </div>
  <form className="stack" onSubmit={e=>{e.preventDefault();void calculate();}}>
   <div className="calculator-field">
    <div className="calculator-field-label">
     <label htmlFor={fieldId}>Выражение</label>
     {selectionPreview&&<span ref={selectionTip} className="calculator-selection" role="tooltip" id={fieldId+'-selection'}><span className="sr-only">Результат выделения: </span>{selectionPreview}</span>}
    </div>
    <div className="calculator-input-row">
     <input id={fieldId} ref={input} autoFocus className="math-input" aria-describedby={selectionPreview?fieldId+'-selection':undefined} value={expression} onChange={e=>updateExpression(e.target.value)} onSelect={readSelection} onKeyUp={readSelection} onPointerUp={readSelection} placeholder="(1250 + 350) × 0,2" maxLength={500} autoComplete="off" spellCheck={false}/>
     <button type="button" className="calculator-keypad-toggle" aria-label={keypadOpen?'Скрыть клавиатуру':'Показать клавиатуру'} aria-expanded={keypadOpen} aria-controls={fieldId+'-keys'} onMouseDown={event=>event.preventDefault()} onClick={()=>setKeypadOpen(open=>!open)}><Icon name="keypad" size={20}/></button>
    </div>
   </div>
   <div id={fieldId+'-keys'} hidden={!keypadOpen}>
   {keypadOpen&&<div className="calculator-keys" role="group" aria-label="Клавиатура калькулятора">
    {calculatorKeys.map(key=><button type="button" key={key} onMouseDown={event=>event.preventDefault()} onClick={()=>editKey(key)}>{key}</button>)}
    <button type="button" aria-label="Удалить символ" onMouseDown={event=>event.preventDefault()} onClick={()=>editKey('')}><Icon name="backspace"/></button>
    <button className="primary" aria-label="Посчитать" disabled={!expression.trim()||busy}>=</button>
   </div>}</div>
   <div className="actions">{!keypadOpen&&<button className="primary" disabled={!expression.trim()||busy}>Посчитать</button>}<button type="button" className="quiet" onClick={()=>replaceExpression('')}>Очистить</button><small>Выделите часть выражения для быстрого расчёта.</small></div>
  </form>
  {error&&<Notice error>{error}</Notice>}
  <div className="section-heading"><h2>История</h2>{state.calculatorHistory.length>0&&<button className="quiet" onClick={()=>setConfirmClear(true)}>Очистить историю</button>}</div>
  {confirmClear&&<div className="stack"><p>Удалить всю историю вычислений с этого компьютера?</p><div className="actions"><button onClick={()=>void run(async()=>{setState(nativeAvailable?await invoke<Snapshot>('history.clear'):{...state,calculatorHistory:[]});setConfirmClear(false);})}>Удалить историю</button><button onClick={()=>setConfirmClear(false)}>Отмена</button></div></div>}
  {!state.calculatorHistory.length&&<p>Результаты останутся здесь после закрытия панели.</p>}
  <div className="list">{state.calculatorHistory.map(item=><article className="history-row" key={item.id}>
   <button className="history-value" onClick={()=>replaceExpression(item.expression)}><span>{item.expression}</span><strong>{item.result}</strong></button>
   <div className="actions"><CopyButton text={item.result} label="Результат"/><CopyButton text={item.expression} label="Выражение"/><button className="quiet" onClick={()=>void remove(item.id)} aria-label={`Удалить ${item.expression}`}>Удалить</button></div>
  </article>)}</div>
 </div>;
}

export function QrTool({initialText='',sessionText}:{initialText?:string;sessionText?:SessionText}) {
 const localText=useState(initialText);
 const [text,setText]=sessionText??localText;const [qr,setQr]=useState<{svg:string;png:string;jpeg:string}>();
 const [size,setSize]=useState(512);const [level,setLevel]=useState<'L'|'M'|'Q'|'H'>('M');
 const {error,busy,run,setError}=useAction();const [saved,setSaved]=useState('');
 useEffect(()=>{let current=true;setQr(undefined);setError('');setSaved('');if(!text)return;
  const timer=setTimeout(()=>{void Promise.resolve().then(()=>makeQr(text,{size,level})).then(value=>{if(current)setQr(value);}).catch(e=>{if(current)setError(e.message);});},150);
  return()=>{current=false;clearTimeout(timer);};
 },[text,size,level]);
 async function save(format:'svg'|'png'|'jpeg') {
  if(!qr)return;await run(async()=>{
   if(nativeAvailable){const result=await invoke<{saved:boolean}>('qr.save',{format,data:qr[format]});setSaved(result.saved?'Файл сохранён.':'Сохранение отменено.');}
   else {const blob=format==='svg'?new Blob([qr.svg],{type:'image/svg+xml'}):await (await fetch(qr[format])).blob();const url=URL.createObjectURL(blob);const link=document.createElement('a');link.href=url;link.download=`qrcode.${format==='jpeg'?'jpg':format}`;link.click();setTimeout(()=>URL.revokeObjectURL(url),1000);setSaved('Файл передан браузеру для сохранения.');}
  });
 }
 return <div className="stack"><label>Текст или ссылка<textarea autoFocus rows={4} value={text} maxLength={2000} onChange={e=>setText(e.target.value)} placeholder="Введите текст для QR-кода"/></label>
  <div className="form-row"><label>Размер, px<select value={size} onChange={e=>setSize(Number(e.target.value))}>{[256,512,1024,2048].map(value=><option key={value}>{value}</option>)}</select></label><label>Коррекция ошибок<select value={level} onChange={e=>setLevel(e.target.value as typeof level)}><option value="L">L · 7%</option><option value="M">M · 15%</option><option value="Q">Q · 25%</option><option value="H">H · 30%</option></select></label></div>
  <small>Генерация на этом компьютере. Текст никуда не отправляется.</small>
  {error&&<Notice error>{error}</Notice>}
  {qr?<><div className="qr-preview"><img src={qr.png} alt="QR-код введённого текста"/></div><div className="actions">{(['svg','png','jpeg'] as const).map(format=><button key={format} disabled={busy} onClick={()=>void save(format)}>Сохранить {format.toUpperCase()}</button>)}<button disabled={busy||!nativeAvailable} onClick={()=>void run(async()=>{await invoke('clipboard.image',{dataUrl:qr.png});setSaved('Изображение скопировано.');})}>Копировать изображение</button></div>{!nativeAvailable&&<small>Копирование изображения доступно в приложении Windows.</small>}</>:<div className="empty-preview"><Icon name="qr" size={40}/><p>QR-код появится после ввода текста</p></div>}
  {qr&&<CopyButton text={qr.svg} label="Копировать SVG-текст"/>}{saved&&<Notice>{saved}</Notice>}
  {text&&<QrStickerPrint text={text} level={level}/>}
 </div>;
}

export function Units({state,setState}:LocalProps) {
 const [category,setCategory]=useState(unitCategories[0].id);const units=unitCategories.find(c=>c.id===category)!.units;
 const [from,setFrom]=useState(units[0].id);const [to,setTo]=useState(units[1].id);const [amount,setAmount]=useState('1');const {error,run}=useAction();
 const conversion=useMemo(()=>{try {const number=Number(amount.replace(',','.'));if(!amount.trim()||!Number.isFinite(number))return {error:'Введите число.'};return {value:convertUnit(number,from,to).toLocaleString('ru-RU',{maximumSignificantDigits:12})};}catch(e){return {error:(e as Error).message};}},[amount,from,to]);
 const name=(id:string)=>unitCategories.flatMap(c=>c.units).find(u=>u.id===id)?.name||id;
 function selectPair(pair:{from:string;to:string}) {const next=unitCategories.find(c=>c.units.some(u=>u.id===pair.from));if(next){setCategory(next.id);setFrom(pair.from);setTo(pair.to);}}
 return <div className="stack"><label>Величина<select value={category} onChange={e=>{const next=unitCategories.find(c=>c.id===e.target.value)!;setCategory(next.id);setFrom(next.units[0].id);setTo(next.units[1].id);}}>{unitCategories.map(c=><option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
  <label>Значение<input autoFocus inputMode="decimal" value={amount} onChange={e=>setAmount(e.target.value)}/></label>
  <div className="form-row"><label>Из<select value={from} onChange={e=>setFrom(e.target.value)}>{units.map(u=><option key={u.id} value={u.id}>{u.name}</option>)}</select></label><label>В<select value={to} onChange={e=>setTo(e.target.value)}>{units.map(u=><option key={u.id} value={u.id}>{u.name}</option>)}</select></label></div>
  <div className="actions"><button onClick={()=>{setFrom(to);setTo(from);}}>Поменять местами</button><button onClick={()=>void run(async()=>setState(nativeAvailable?await invoke<Snapshot>('units.favorite',{from,to}):{...state,unitFavorites:state.unitFavorites.some(p=>p.from===from&&p.to===to)?state.unitFavorites.filter(p=>p.from!==from||p.to!==to):[...state.unitFavorites,{from,to}]}))}>{state.unitFavorites.some(p=>p.from===from&&p.to===to)?'Убрать из избранного':'В избранное'}</button></div>
  {conversion.value&&<div className="answer"><output>{conversion.value}<small>{name(to)}</small></output><CopyButton text={conversion.value}/></div>}
  {conversion.error&&<Notice>{conversion.error}</Notice>}{error&&<Notice error>{error}</Notice>}
  {state.unitFavorites.length>0&&<><h2>Избранные пары</h2><div className="actions">{state.unitFavorites.map(pair=><button key={`${pair.from}:${pair.to}`} onClick={()=>selectPair(pair)}>{name(pair.from)} / {name(pair.to)}</button>)}</div></>}
 </div>;
}

export function Colors({state,setState}:LocalProps) {
 const [input,setInput]=useState('#B6C3D0');const {error,busy,run}=useAction();
 const color=useMemo(()=>{try{return parseColor(input);}catch{return undefined;}},[input]);
 async function remember(hex:string) {setState(nativeAvailable?await invoke<Snapshot>('color.remember',{hex}):{...state,colorHistory:[hex,...state.colorHistory.filter(c=>c!==hex)].slice(0,30)});}
 return <div className="stack"><label>HEX, RGB или HSL<input autoFocus className="code-input" value={input} onChange={e=>setInput(e.target.value)} spellCheck={false} placeholder="#B6C3D0"/></label>
  <div className="actions"><button disabled={!nativeAvailable||busy} onClick={()=>void run(async()=>{const value=await invoke<{hex:string}>('color.pick');setInput(value.hex);await remember(value.hex);})}>Взять цвет с экрана</button><button disabled={!color||busy} onClick={()=>color&&void run(()=>remember(color.hex))}>Сохранить цвет</button></div><small>Пипетка выбирает пиксель экрана; Escape отменяет выбор. Снимок не сохраняется.</small>
  {!nativeAvailable&&<small>Пипетка доступна в приложении Windows.</small>}{error&&<Notice error>{error}</Notice>}
  {color?<><div className="color-preview" style={{background:color.hex}} aria-label={`Образец ${color.hex}`}/>{(['hex','rgb','hsl'] as const).map(format=><div className="format-row" key={format}><div><small>{format.toUpperCase()}</small><code>{color[format]}</code></div><CopyButton text={color[format]}/></div>)}</>:<Notice>Не удалось распознать цвет. Пример: #B6C3D0, rgb(182, 195, 208).</Notice>}
  {state.colorHistory.length>0&&<><h2>Сохранённые цвета</h2><div className="swatches">{state.colorHistory.map(hex=><button key={hex} className="swatch" onClick={()=>setInput(hex)} aria-label={`Выбрать ${hex}`}><span style={{background:hex}}/>{hex}</button>)}</div></>}
 </div>;
}
