// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {StartupSettings} from './StartupSettings';
import {invoke} from './ui';
vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),invoke:vi.fn(),nativeAvailable:true}));
afterEach(()=>{cleanup();vi.resetAllMocks();});
it('reads Windows state, saves immediately and preserves state on failure',async()=>{
 vi.mocked(invoke).mockResolvedValueOnce({enabled:false}).mockResolvedValueOnce({enabled:true}).mockRejectedValueOnce(new Error('Нет доступа к автозапуску'));
 await act(async()=>render(<StartupSettings/>));
 const toggle=screen.getByRole('checkbox',{name:'Запускать при входе в Windows'}) as HTMLInputElement;
 expect(toggle.checked).toBe(false);
 await act(async()=>fireEvent.click(toggle));
 expect(toggle.checked).toBe(true);
 expect(invoke).toHaveBeenCalledWith('startup.set',{enabled:true});
 await act(async()=>fireEvent.click(toggle));
 expect(toggle.checked).toBe(true);
 expect(screen.getByRole('alert').textContent).toContain('Нет доступа');
});
it('allows retry after a failed state read without enabling an unknown toggle',async()=>{
 vi.mocked(invoke).mockRejectedValueOnce(new Error('Не удалось прочитать')).mockResolvedValueOnce({enabled:true});
 await act(async()=>render(<StartupSettings/>));
 expect((screen.getByRole('checkbox') as HTMLInputElement).disabled).toBe(true);
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Повторить'})));
 expect((screen.getByRole('checkbox') as HTMLInputElement).checked).toBe(true);
});
