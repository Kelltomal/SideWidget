// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {ButtonHelp} from './ButtonHelp';
import {invoke} from './ui';

vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),nativeAvailable:true,invoke:vi.fn(async()=>({}))}));
afterEach(()=>{cleanup();vi.useRealTimers();vi.clearAllMocks();});

it('shows native help beneath the panel without a visual DOM overlay over menus',async()=>{
 render(<><button aria-label="Подключения" data-help="Настройки Wi-Fi и Bluetooth"><svg><path d="M2 8h20"/></svg></button><ButtonHelp/></>);
 await act(async()=>screen.getByRole('button').focus());
 await act(async()=>{await new Promise(resolve=>setTimeout(resolve,0));});
 expect(invoke).toHaveBeenCalledWith('shell.tooltip',{visible:true,label:'Подключения',description:'Настройки Wi-Fi и Bluetooth',path:'M2 8h20'});
 expect(screen.getByRole('tooltip').className).toBe('sr-only');
 expect(document.querySelector('.button-help')).toBeNull();
});

it('dismisses native help on menu interaction and Escape',async()=>{
 vi.useFakeTimers();render(<><button aria-label="Питание">Питание</button><ButtonHelp/></>);
 await act(async()=>{screen.getByRole('button').focus();vi.runOnlyPendingTimers();});
 vi.mocked(invoke).mockClear();
 await act(async()=>fireEvent.pointerDown(screen.getByRole('button')));
 expect(invoke).toHaveBeenCalledWith('shell.tooltip',{visible:false});
 expect(screen.queryByRole('tooltip')).toBeNull();
 await act(async()=>{fireEvent.pointerOver(screen.getByRole('button'));vi.advanceTimersByTime(400);});
 await act(async()=>fireEvent.keyDown(document,{key:'Escape'}));
 expect(screen.queryByRole('tooltip')).toBeNull();
});

it('previews the main icon without overlaying a menu chevron',async()=>{
 vi.useFakeTimers();
 render(<><button aria-label="Подключения"><svg viewBox="0 0 24 24"><path d="M2 8a16 16 0 0 1 20 0"/></svg><svg viewBox="0 0 24 24"><path d="m6 9 6 6 6-6"/></svg></button><ButtonHelp/></>);
 await act(async()=>{screen.getByRole('button').focus();vi.runOnlyPendingTimers();});
 expect(invoke).toHaveBeenCalledWith('shell.tooltip',{visible:true,label:'Подключения',description:'',path:'M2 8a16 16 0 0 1 20 0'});
});
