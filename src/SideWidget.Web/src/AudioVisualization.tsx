import {useEffect,useId,useRef,useState} from 'react';
import {bridge} from './bridge';
import type {AudioSpectrumFrame,Snapshot,VisualizerOptions} from './types';
import {invoke,nativeAvailable,Notice} from './ui';

export const defaultVisualizer:VisualizerOptions={enabled:true,showInHeader:true,brightness:.35,sensitivity:1,style:'wave'};
const previewKey='sidewidget.visualizer.preview';
function previewSettings():VisualizerOptions {try{return {...defaultVisualizer,...JSON.parse(localStorage.getItem(previewKey)||'{}')};}catch{return defaultVisualizer;}}
export function sanitizeSpectrum(input:unknown):number[] {return Array.from({length:32},(_,i)=>{const value=Array.isArray(input)?input[i]:0;return typeof value==='number'&&Number.isFinite(value)?Math.max(0,Math.min(1,value)):0;});}
const emptyFrame:AudioSpectrumFrame={available:false,bands:Array(32).fill(0),rms:0,sequence:0,settings:defaultVisualizer};
export function useAudioSpectrum(active:boolean) {
 const [frame,setFrame]=useState<AudioSpectrumFrame>(emptyFrame);
 useEffect(()=>{
  if(!active)return;
  let stopped=false,inFlight=false,enabled=true;let timer:ReturnType<typeof setTimeout>|undefined;
  async function read(){
   if(stopped||document.hidden||inFlight)return;
   inFlight=true;
   try {
    if(nativeAvailable){const next=await invoke<AudioSpectrumFrame>('visualizer.get');if(!stopped&&next){enabled=next.settings?.enabled??true;setFrame({...next,bands:sanitizeSpectrum(next.bands),settings:{...defaultVisualizer,...next.settings}});}}
    else setFrame({...emptyFrame,settings:previewSettings()});
   }catch{if(!stopped)setFrame(current=>({...current,available:false,bands:Array(32).fill(0)}));}
   inFlight=false;
   if(!stopped&&nativeAvailable&&!document.hidden)timer=setTimeout(read,!enabled?1000:window.matchMedia?.('(prefers-reduced-motion: reduce)').matches?250:40);
  }
  function visible(){clearTimeout(timer);if(!document.hidden)void read();}
  function settingsChanged(){if(!nativeAvailable)void read();}
  void read();document.addEventListener('visibilitychange',visible);window.addEventListener('sidewidget.visualizer.settings',settingsChanged);
  return()=>{stopped=true;clearTimeout(timer);document.removeEventListener('visibilitychange',visible);window.removeEventListener('sidewidget.visualizer.settings',settingsChanged);};
 },[active]);
 return frame;
}
function wavePath(levels:number[]) {
 const points=levels.map((value,index)=>({x:index*320/31,y:72-value*66}));let path='M 0 72';
 for(let i=0;i<points.length;i++){const point=points[i];if(i===0)path+=` L ${point.x} ${point.y}`;else{const previous=points[i-1],middle=(previous.x+point.x)/2;path+=` C ${middle} ${previous.y} ${middle} ${point.y} ${point.x} ${point.y}`;}}
 return path+' L 320 72 Z';
}
export function SpectrumGraphic({bands,settings}:{bands:number[];settings:VisualizerOptions}) {
 const id=useId().replace(/:/g,'');const path=useRef<SVGPathElement>(null);const bars=useRef<(SVGRectElement|null)[]>([]);
 const targets=useRef(Array(32).fill(0) as number[]),levels=useRef(Array(32).fill(0) as number[]),raf=useRef(0),last=useRef(0);
 const config=useRef(settings);config.current=settings;
 const [motionQuery]=useState(()=>window.matchMedia?.('(prefers-reduced-motion: reduce)'));
 function draw(now:number){
  raf.current=0;const current=config.current;if(!current.enabled||current.brightness<=0)return;
  const dt=last.current?Math.min(40,Math.max(1,now-last.current)):16.67;last.current=now;
  let moving=false,audible=false;
  for(let i=0;i<32;i++){
   const target=targets.current[i],from=levels.current[i];
   const duration=target===0?40:target>from?30:80;
   const next=motionQuery?.matches?target:from+(target-from)*(1-Math.exp(-dt/duration));
   levels.current[i]=Math.abs(next-target)<.001?target:next;
   if(Math.abs(levels.current[i]-target)>.001)moving=true;
   if(levels.current[i]>.002)audible=true;
  }
  if(current.style==='bars')bars.current.forEach((bar,index)=>{if(bar)bar.style.transform=`scaleY(${levels.current[index]})`;});
  else path.current?.setAttribute('d',audible?wavePath(levels.current):'');
  if(moving&&!motionQuery?.matches)raf.current=requestAnimationFrame(draw);else last.current=0;
 }
 useEffect(()=>{
  targets.current=sanitizeSpectrum(bands).map(value=>Math.min(1,value*settings.sensitivity));
  if(!settings.enabled||settings.brightness<=0){cancelAnimationFrame(raf.current);raf.current=0;last.current=0;levels.current.fill(0);return;}
  if(!raf.current)raf.current=requestAnimationFrame(draw);
 },[bands,settings.enabled,settings.sensitivity,settings.brightness,settings.style]);
 useEffect(()=>()=>cancelAnimationFrame(raf.current),[]);
 if(!settings.enabled||settings.brightness<=0)return null;
 return <svg className="audio-spectrum" viewBox="0 0 320 72" preserveAspectRatio="none" aria-hidden="true" style={{opacity:`calc(${settings.brightness} * var(--spectrum-opacity-scale, .35))`}}>
  <defs><linearGradient id={id} x1="0" y1="0" x2="0" y2="1"><stop offset="0" stopColor="#f6634b"/><stop offset=".45" stopColor="#e03e6f"/><stop offset="1" stopColor="#895be1"/></linearGradient></defs>
  {settings.style==='bars'?Array.from({length:32},(_,index)=><rect ref={node=>{bars.current[index]=node;}} key={index} x={index*10+1} y="6" width="7" height="66" rx="1.5" fill={`url(#${id})`} style={{transform:'scaleY(0)',transformOrigin:'center bottom',transformBox:'fill-box'}}/>):<path ref={path} fill={`url(#${id})`}/>}
 </svg>;
}

export function VisualizerSettingsPanel({value,onSaved,onPreviewSaved}:{value?:VisualizerOptions;onSaved?:(snapshot:Snapshot)=>void;onPreviewSaved?:(options:VisualizerOptions)=>void}) {
 const [options,setOptions]=useState<VisualizerOptions>(value??defaultVisualizer);const [error,setError]=useState('');const [busy,setBusy]=useState(false);
 const draft=useRef(options),saved=useRef(options),saving=useRef(false);const mounted=useRef(true);
 function assign(next:VisualizerOptions){draft.current=next;setOptions(next);}
 useEffect(()=>{mounted.current=true;return()=>{mounted.current=false;};},[]);
 useEffect(()=>{if(value){assign({...defaultVisualizer,...value});saved.current={...defaultVisualizer,...value};}},[value]);
 useEffect(()=>{
  if(value)return;
  let active=true;
  if(nativeAvailable)void invoke<AudioSpectrumFrame>('visualizer.get').then(frame=>{if(active&&frame?.settings){assign(frame.settings);saved.current=frame.settings;}}).catch(()=>{});
  else {assign(previewSettings());saved.current=previewSettings();}
  const unsubscribe=bridge.subscribe(event=>{if(active&&event.type==='settings.changed'&&event.data?.settings?.visualizer){assign(event.data.settings.visualizer);saved.current=event.data.settings.visualizer;}});
  return()=>{active=false;unsubscribe();};
 },[value]);
 async function save(next:VisualizerOptions){
  if(saving.current)return;saving.current=true;const previous=saved.current;assign(next);setBusy(true);setError('');
  try{
   if(nativeAvailable){const state=await invoke<Snapshot>('visualizer.settings',next);if(mounted.current){saved.current=state.settings.visualizer??next;assign(saved.current);onSaved?.(state);}}
   else{localStorage.setItem(previewKey,JSON.stringify(next));saved.current=next;onPreviewSaved?.(next);window.dispatchEvent(new Event('sidewidget.visualizer.settings'));}
  }catch(e){if(mounted.current){assign(previous);setError((e as Error).message);}}
  finally{saving.current=false;if(mounted.current)setBusy(false);}
 }
 function commit(){if(JSON.stringify(draft.current)!==JSON.stringify(saved.current))void save(draft.current);}
 return <section className="visualizer-settings stack" aria-label="Визуализация звука">
  <div><h2>Визуализация звука</h2><small>Реагирует на весь звук Windows. Настройки сохраняются сразу.</small></div>
  <label className="checkbox"><input type="checkbox" checked={options.enabled} disabled={busy} onChange={event=>void save({...options,enabled:event.target.checked})}/>Включить визуализацию</label>
  <label className="checkbox"><input type="checkbox" checked={options.showInHeader} disabled={busy} onChange={event=>void save({...options,showInHeader:event.target.checked})}/>Показывать в свёрнутом баре</label>
  <label className="slider-label"><span>Яркость<span>{Math.round(options.brightness*100)}%</span></span><input type="range" min="0" max="100" step="1" value={Math.round(options.brightness*100)} disabled={busy} onChange={event=>assign({...options,brightness:Number(event.target.value)/100})} onPointerUp={commit} onKeyUp={commit} onBlur={commit}/></label>
  <label className="slider-label"><span>Чувствительность<span>{options.sensitivity.toFixed(2)}×</span></span><input type="range" min="25" max="400" step="5" value={Math.round(options.sensitivity*100)} disabled={busy} onChange={event=>assign({...options,sensitivity:Number(event.target.value)/100})} onPointerUp={commit} onKeyUp={commit} onBlur={commit}/></label>
  <label>Вид<select value={options.style} disabled={busy} onChange={event=>void save({...options,style:event.target.value as VisualizerOptions['style']})}><option value="wave">Плавная волна</option><option value="bars">Столбцы</option></select></label>
  {error&&<Notice error>{error}</Notice>}
 </section>;
}
