// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {ShareSettings} from './ShareSettings';
import {invoke} from './ui';
vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),nativeAvailable:true,invoke:vi.fn()}));
afterEach(()=>{cleanup();vi.resetAllMocks();});
it('packages only once while busy and reports the revealed archive',async()=>{
 let finish:(result:unknown)=>void=()=>{};vi.mocked(invoke).mockImplementation(()=>new Promise(resolve=>{finish=resolve;}));
 render(<form onSubmit={()=>{throw new Error('Share submitted settings');}}><ShareSettings/></form>);
 fireEvent.click(screen.getByRole('button',{name:'Поделиться'}));
 const button=screen.getByRole('button',{name:'Упаковка…'}) as HTMLButtonElement;
 expect(button.disabled).toBe(true);fireEvent.click(button);expect(invoke).toHaveBeenCalledTimes(1);expect(invoke).toHaveBeenCalledWith('app.share');
 await act(async()=>finish({path:'C:\\share.zip',revealed:true}));
 expect(screen.getByText('Архив готов. Файл выделен в Проводнике.')).toBeTruthy();expect((screen.getByRole('button',{name:'Поделиться'}) as HTMLButtonElement).disabled).toBe(false);
});
it('allows retry after an export failure',async()=>{
 vi.mocked(invoke).mockRejectedValueOnce(new Error('Недостаточно места'));
 render(<ShareSettings/>);await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Поделиться'})));
 expect(screen.getByRole('alert').textContent).toContain('Недостаточно места');
 expect((screen.getByRole('button',{name:'Поделиться'}) as HTMLButtonElement).disabled).toBe(false);
});
it('keeps the archive path visible if Explorer could not open',async()=>{
 vi.mocked(invoke).mockResolvedValue({path:'C:\\archive.zip',revealed:false});render(<ShareSettings/>);
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Поделиться'})));
 expect(screen.getByText(/C:\\archive.zip/).textContent).toContain('откройте папку вручную');
});
