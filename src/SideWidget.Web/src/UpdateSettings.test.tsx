// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {afterEach, expect, it, vi} from 'vitest';
import {StartupUpdate,UpdateSettings} from './UpdateSettings';
import {bridge} from './bridge';
import {invoke} from './ui';

vi.mock('./ui', async original => ({...await original<typeof import('./ui')>(), nativeAvailable: true, invoke: vi.fn()}));
afterEach(() => {cleanup(); vi.restoreAllMocks(); vi.resetAllMocks();});

it('offers the GitHub release after checking for a newer version', async () => {
 const releaseUrl = 'https://github.com/Kelltomal/SideWidget/releases/tag/v0.3.0';
 vi.mocked(invoke).mockResolvedValueOnce({status: 'available', currentVersion: '0.2.0', latestVersion: '0.3.0',
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
 if(!HTMLDialogElement.prototype.showModal)
  HTMLDialogElement.prototype.showModal=function(){this.open=true;};
 vi.mocked(invoke).mockResolvedValue({});
 render(<StartupUpdate/>);
 await act(()=>emit?.({type:'updates.available',data:{latestVersion:'0.4.0',portableAsset:{}}}));
 fireEvent.click(screen.getByRole('button',{name:label}));
 await waitFor(()=>expect(invoke).toHaveBeenCalledWith(action,...parameters));
});
