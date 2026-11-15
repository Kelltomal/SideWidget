// @vitest-environment jsdom
import {cleanup, fireEvent, render, screen} from '@testing-library/react';
import {afterEach, expect, it, vi} from 'vitest';
import {UpdateSettings} from './UpdateSettings';
import {invoke} from './ui';

vi.mock('./ui', async original => ({...await original<typeof import('./ui')>(), nativeAvailable: true, invoke: vi.fn()}));
afterEach(() => {cleanup(); vi.resetAllMocks();});

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
