import {useEffect,useLayoutEffect,useRef,useState} from 'react';
import {createPortal} from 'react-dom';
import {Icon} from './ui';

export type MenuAction={id:string;label:string;icon:string;description:string};
export function ActionMenu({label,icon,description,items,compact,busy,onAction}:{label:string;icon:string;description:string;items:MenuAction[];compact:boolean;busy:boolean;onAction:(id:string)=>void}) {
 const [opened,setOpened]=useState(false);const trigger=useRef<HTMLButtonElement>(null);const menu=useRef<HTMLDivElement>(null);
 const [position,setPosition]=useState({left:0,top:0});const last=useRef(false);
 function close(focus=false){setOpened(false);if(focus)trigger.current?.focus();}
 useLayoutEffect(()=>{
  if(!opened||!trigger.current||!menu.current)return;
  const rect=trigger.current.getBoundingClientRect();const box=menu.current.getBoundingClientRect();
  setPosition({left:Math.max(8,Math.min(rect.left,innerWidth-box.width-8)),top:rect.top>=box.height+8?rect.top-box.height-6:Math.min(rect.bottom+6,innerHeight-box.height-8)});
  const buttons=menu.current.querySelectorAll<HTMLButtonElement>('button');buttons[last.current?buttons.length-1:0]?.focus();
 },[opened]);
 useEffect(()=>{
  if(!opened)return;
  function outside(event:PointerEvent){if(!menu.current?.contains(event.target as Node)&&!trigger.current?.contains(event.target as Node))close();}
  function scroll(event:Event){
   if(menu.current?.contains(event.target as Node)||!trigger.current||!menu.current)return;
   const rect=trigger.current.getBoundingClientRect();const box=menu.current.getBoundingClientRect();
   if(rect.bottom<0||rect.top>innerHeight){close();return;}
   setPosition({left:Math.max(8,Math.min(rect.left,innerWidth-box.width-8)),top:rect.top>=box.height+8?rect.top-box.height-6:Math.min(rect.bottom+6,innerHeight-box.height-8)});
  }
  function resize(){close();}
  document.addEventListener('pointerdown',outside);document.addEventListener('scroll',scroll,true);window.addEventListener('resize',resize);
  return()=>{document.removeEventListener('pointerdown',outside);document.removeEventListener('scroll',scroll,true);window.removeEventListener('resize',resize);};
 },[opened]);
 return <><button ref={trigger} disabled={busy} aria-label={label} data-help={description} aria-haspopup="menu" aria-expanded={opened} onClick={()=>{last.current=false;setOpened(!opened);}} onKeyDown={event=>{if(event.key==='ArrowDown'||event.key==='ArrowUp'){event.preventDefault();last.current=event.key==='ArrowUp';setOpened(true);}}}><Icon name={icon} size={20}/>{!compact&&<span>{label}</span>}<Icon name="chevron" size={12}/></button>
  {opened&&createPortal(<div ref={menu} className="action-menu" role="menu" aria-label={label} style={position} onBlur={event=>{if(event.relatedTarget!==trigger.current&&!event.currentTarget.contains(event.relatedTarget as Node))close();}} onKeyDown={event=>{
   if(event.key==='Escape'){event.preventDefault();event.stopPropagation();close(true);return;}
   const buttons=Array.from(event.currentTarget.querySelectorAll<HTMLButtonElement>('button'));const index=buttons.indexOf(document.activeElement as HTMLButtonElement);
   if(['ArrowDown','ArrowUp','Home','End'].includes(event.key)){event.preventDefault();buttons[event.key==='Home'?0:event.key==='End'?buttons.length-1:(index+(event.key==='ArrowDown'?1:-1)+buttons.length)%buttons.length]?.focus();}
  }}>{items.map(item=><button role="menuitem" key={item.id} data-help={item.description} onClick={()=>{close(true);onAction(item.id);}}><Icon name={item.icon} size={20}/><span>{item.label}</span></button>)}</div>,document.body)}
 </>;
}
