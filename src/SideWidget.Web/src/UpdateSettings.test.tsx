// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {afterEach, expect, it, vi} from 'vitest';
import {StartupUpdate,UpdateSettings} from './UpdateSettings';
import {bridge} from './bridge';
import {invoke} from './ui';

vi.mock('./ui', async original => ({...await original<typeof import('./ui')>(), nativeAvailable: true, invoke: vi.fn()}));
if(!HTMLDialogElement.prototype.showModal)
 HTMLDialogElement.prototype.showModal=function(){this.open=true;};
if(!HTMLDialogElement.prototype.close)
 HTMLDialogElement.prototype.close=function(){this.open=false;};
afterEach(() => {cleanup(); vi.restoreAllMocks(); vi.resetAllMocks();});

it('offers the GitHub release after checking for a newer version', async () => {
 const releaseUrl = 'https://github.com/Kelltomal/SideWidget/releases/tag/v0.3.0';
 vi.mocked(invoke).mockResolvedValueOnce({connected:false}).mockResolvedValueOnce({status: 'available', currentVersion: '0.2.0', latestVersion: '0.3.0',
  hasPortableAsset: true, releaseUrl}).mockResolvedValueOnce({opened: true});
 render(<UpdateSettings/>);
 fireEvent.click(screen.getByRole('button', {name: 'Проверить обновления'}));
 expect(await screen.findByText('Доступна версия 0.3.0.')).toBeTruthy();
 fireEvent.click(screen.getByRole('button', {name: 'Открыть релиз на GitHub'}));
 expect(invoke).toHaveBeenCalledWith('updates.check');
 expect(invoke).toHaveBeenCalledWith('links.open', {url: releaseUrl});
});

it.each([
 ['В следующий раз','updates.defer',[]],
 ['Пропустить эту версию','updates.skip',[{version:'0.4.0'}]],
 ['Обновить сейчас','updates.install',[{version:'0.4.0'}]],
] as const)('handles startup choice %s', async (label,action,parameters) => {
 let emit: ((event:unknown)=>void)|undefined;
 vi.spyOn(bridge,'subscribe').mockImplementation(listener=>{emit=listener; return ()=>{};});
 vi.mocked(invoke).mockResolvedValue({});
 render(<StartupUpdate/>);
 await act(()=>emit?.({type:'updates.available',data:{latestVersion:'0.4.0',portableAsset:{}}}));
 fireEvent.click(screen.getByRole('button',{name:label}));
 await waitFor(()=>expect(invoke).toHaveBeenCalledWith(action,...parameters));
});

it('closes the startup prompt during installation and restores it if installation fails', async () => {
 let emit: ((event:unknown)=>void)|undefined;
 vi.spyOn(bridge,'subscribe').mockImplementation(listener=>{emit=listener; return ()=>{};});
 let rejectInstall: (error:Error)=>void=()=>{};
 vi.mocked(invoke).mockImplementation(()=>new Promise((_,reject)=>{rejectInstall=reject;}));
 render(<StartupUpdate/>);
 await act(()=>emit?.({type:'updates.available',data:{latestVersion:'0.4.0',portableAsset:{}}}));
 const dialog=screen.getByRole('dialog') as HTMLDialogElement;
 fireEvent.click(screen.getByRole('button',{name:'Обновить сейчас'}));
 expect(dialog.open).toBe(false);
 await act(()=>rejectInstall(new Error('Сбой обновления')));
 expect(dialog.open).toBe(true);
 expect(screen.getByRole('alert').textContent).toBe('Сбой обновления');
});

it('offers manual download and optional sign-in when GitHub rate limits checks', async () => {
 const releaseUrl='https://github.com/Kelltomal/SideWidget/releases/latest';
 vi.mocked(invoke).mockImplementation(async action=>(action==='updates.check'
  ? {status:'rate_limited', releaseUrl}
  : {connected:false}) as never);
 render(<UpdateSettings/>);
 fireEvent.click(screen.getByRole('button',{name:'Проверить обновления'}));
 expect(await screen.findByText('Достигнут лимит запросов GitHub.')).toBeTruthy();
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Открыть релиз на GitHub'})));
 expect(invoke).toHaveBeenCalledWith('links.open',{url:releaseUrl});
 fireEvent.click(screen.getByRole('button',{name:'Войти в GitHub'}));
 expect(screen.getByRole('dialog',{name:'Вход в GitHub'})).toBeTruthy();
 expect(invoke).not.toHaveBeenCalledWith('updates.github.connect');
});

it('shows the GitHub device code and cancels polling when the dialog closes',async()=>{
 let emit:((event:unknown)=>void)|undefined;
 vi.spyOn(bridge,'subscribe').mockImplementation(listener=>{emit=listener;return ()=>{};});
 vi.mocked(invoke).mockImplementation(action=>action==='updates.github.connect'
  ? new Promise(()=>{})
  : Promise.resolve({connected:false}) as never);
 render(<UpdateSettings/>);
 fireEvent.click(await screen.findByRole('button',{name:'Войти в GitHub'}));
 fireEvent.click(screen.getByRole('button',{name:'Получить код'}));
 await act(()=>emit?.({type:'updates.github.code',data:{code:'AB12-XY34'}}));
 expect(screen.getByText('AB12-XY34')).toBeTruthy();
 fireEvent.click(screen.getByRole('button',{name:'Открыть GitHub в браузере'}));
 expect(invoke).toHaveBeenCalledWith('links.open',{url:'https://github.com/login/device'});
 fireEvent.click(screen.getByRole('button',{name:'Отмена'}));
 expect(invoke).toHaveBeenCalledWith('updates.github.cancel');
});
