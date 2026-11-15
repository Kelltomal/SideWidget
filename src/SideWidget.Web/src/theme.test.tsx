// @vitest-environment jsdom
import {act,cleanup,render} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {useTheme} from './theme';
import {bridge} from './bridge';
function Theme({preference='system'}:{preference?:'dark'|'light'|'system'}){useTheme(preference);return null;}
afterEach(()=>{cleanup();vi.unstubAllGlobals();vi.restoreAllMocks();});
it('follows live Windows changes only in system mode and removes subscriptions',()=>{
 const media=new EventTarget() as EventTarget&{matches:boolean};media.matches=true;
 const remove=vi.spyOn(media,'removeEventListener');vi.stubGlobal('matchMedia',()=>media);
 const view=render(<Theme/>);expect(document.documentElement.dataset.theme).toBe('light');
 act(()=>{media.matches=false;media.dispatchEvent(new Event('change'));});expect(document.documentElement.dataset.theme).toBe('dark');
 view.rerender(<Theme preference="light"/>);
 act(()=>media.dispatchEvent(new Event('change')));expect(document.documentElement.dataset.theme).toBe('light');
 view.unmount();expect(remove).toHaveBeenCalledTimes(2);
});
it('follows native Windows theme events',()=>{
 let receive:(event:any)=>void=()=>{};vi.spyOn(bridge,'subscribe').mockImplementation(callback=>{receive=callback;return()=>{};});
 render(<Theme/>);act(()=>receive({type:'theme.system',data:{light:true}}));expect(document.documentElement.dataset.theme).toBe('light');
 act(()=>receive({type:'theme.system',data:{light:false}}));expect(document.documentElement.dataset.theme).toBe('dark');
});
