// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {ClockSettings} from './ClockSettings';
import {invoke} from './ui';
import type {Snapshot} from './types';

vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),nativeAvailable:true,invoke:vi.fn()}));
const state:Snapshot={settings:{width:444,height:620,hideDelay:650,openDelay:100,hotkey:'Ctrl+Alt+Space',monitor:0,position:1,largeMode:false,order:[],disabled:[]},calculatorHistory:[],colorHistory:[],unitFavorites:[],apps:[],drafts:{diary:''}};
afterEach(()=>{cleanup();vi.resetAllMocks();});

it('saves the compact ring toggle independently from the launcher shortcut',async()=>{
 const saved={...state,settings:{...state.settings,showCodexLimits:false}};
 vi.mocked(invoke).mockResolvedValue(saved);
 const onSaved=vi.fn();render(<ClockSettings state={state} disabled={false} onSaved={onSaved}/>);
 const toggle=screen.getByRole('checkbox',{name:'Кружок лимитов в свёрнутой панели при запущенном Codex'});
 expect((toggle as HTMLInputElement).checked).toBe(true);
 await act(async()=>fireEvent.click(toggle));
 expect(invoke).toHaveBeenCalledWith('settings.clock',{showCodexLimits:false});
 expect(onSaved).toHaveBeenCalledWith(saved);
 cleanup();render(<ClockSettings state={saved} disabled={false} onSaved={onSaved}/>);
 expect((screen.getByRole('checkbox') as HTMLInputElement).checked).toBe(false);
});

it('keeps the last saved choice when saving fails',async()=>{
 vi.mocked(invoke).mockRejectedValue(new Error('Нет доступа к настройкам'));
 const onSaved=vi.fn();render(<ClockSettings state={state} disabled={false} onSaved={onSaved}/>);
 await act(async()=>fireEvent.click(screen.getByRole('checkbox')));
 expect(onSaved).not.toHaveBeenCalled();
 expect((screen.getByRole('checkbox') as HTMLInputElement).checked).toBe(true);
 expect(screen.getByRole('alert').textContent).toContain('Нет доступа к настройкам');
});
