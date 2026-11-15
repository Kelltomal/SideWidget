import {useLayoutEffect} from 'react';
import {bridge} from './bridge';
import type {Settings} from './types';

export function useTheme(preference:NonNullable<Settings['theme']>){
 useLayoutEffect(()=>{
  // First paint uses the saved palette without an entrance transition.
  const frame=requestAnimationFrame(()=>{document.documentElement.dataset.themeReady='true';});
  return()=>cancelAnimationFrame(frame);
 },[]);
 useLayoutEffect(()=>{
  const root=document.documentElement;
  const media=window.matchMedia?.('(prefers-color-scheme: light)');
  function apply(systemLight=media?.matches??false){root.dataset.theme=preference==='system'?(systemLight?'light':'dark'):preference;}
  apply();
  const changed=()=>apply();media?.addEventListener('change',changed);
  const unsubscribe=bridge.subscribe(event=>{if(event.type==='theme.system'&&typeof event.data?.light==='boolean')apply(event.data.light);});
  return()=>{media?.removeEventListener('change',changed);unsubscribe();};
 },[preference]);
}
