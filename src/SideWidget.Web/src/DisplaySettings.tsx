import type {DisplayDescriptor,Settings} from './types';
import {Icon} from './ui';

export function selectedDisplayIds(settings:Settings,displays:DisplayDescriptor[]):string[]{
 if(settings.displayIds?.length)return settings.displayIds;
 const display=displays[settings.monitor]??displays.find(display=>display.primary)??displays[0];
 return display?[display.id]:[];
}

export function DisplaySettings({settings,displays,onChange,onPositionChange}:{settings:Settings;displays:DisplayDescriptor[];onChange:(ids:string[])=>void;onPositionChange:(id:string,position:number)=>void}){
 const selected=selectedDisplayIds(settings,displays);
 const missing=selected.filter(id=>!displays.some(display=>display.id===id));
 function toggle(id:string,checked:boolean){onChange(checked?[...selected,id]:selected.filter(value=>value!==id));}
 return <section className="stack" aria-labelledby="display-settings-label">
  <h2 id="display-settings-label">Мониторы</h2>
  {displays.length>0&&<small>Оставьте хотя бы один экран. Положение можно выбрать здесь или перетащить шторку за часы.</small>}
  <div className="display-list">
   {[...displays,...missing.map((id,index)=>({id,name:`Отключённый экран ${index+1}`,primary:false}))].map(display=>{
    const enabled=selected.includes(display.id);
    const disconnected=missing.includes(display.id);
    const position=settings.displayPositions?.[display.id]??settings.position;
    const name=display.name.split(' — ')[0];
    const resolution=display.name.slice(name.length+3);
    return <article className="display-card" key={display.id}>
     <div className="display-row">
      <Icon name="system" size={25}/>
      <div className="display-description"><strong>{name}</strong><small>{[resolution,display.primary?'Основной':'',disconnected?'Не подключён':enabled?'Шторка включена':''].filter(Boolean).join(' · ')}</small></div>
      <button type="button" aria-label={`${enabled?'Убрать':'Добавить'}: ${name}`} disabled={enabled&&selected.length===1} onClick={()=>toggle(display.id,!enabled)}><Icon name={enabled?'close':'plus'} size={16}/>{enabled?'Убрать':'Добавить'}</button>
     </div>
     {enabled&&!disconnected&&<label className="display-position">Положение<select aria-label={`Положение: ${name}`} value={position} onChange={event=>onPositionChange(display.id,Number(event.target.value))}>
      <option value={0}>Слева</option><option value={0.5}>По центру</option><option value={1}>Справа</option>
      {![0,0.5,1].includes(position)&&<option value={position}>Текущее ({Math.round(position*100)}%)</option>}
     </select></label>}
    </article>;
   })}
  </div>
  {!displays.length&&<small>Список экранов недоступен. Откройте настройки в приложении SideWidget.</small>}
  {missing.length>0&&<small>Если выбранные экраны отключены, шторка появится на основном.</small>}
 </section>;
}
