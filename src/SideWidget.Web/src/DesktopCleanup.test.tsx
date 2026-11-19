// @vitest-environment jsdom
import {cleanup, fireEvent, render, screen} from '@testing-library/react';
import {afterEach, expect, it, vi} from 'vitest';
import {DesktopCleanup} from './DesktopCleanup';
import {invoke} from './ui';

vi.mock('./ui', async importOriginal => ({...await importOriginal<typeof import('./ui')>(), nativeAvailable: true, invoke: vi.fn()}));
afterEach(() => { cleanup(); vi.resetAllMocks(); });

const status = {hasFolder: false, canUndo: false, canUndoIcons: false};
const base = {id: 'base', files: [], ...status, hasMore: false, modelAvailable: true, answer: ''};

it('opens the rule editor directly and moves files only after review', async () => {
 vi.mocked(invoke).mockResolvedValueOnce(status).mockResolvedValueOnce({...base, id: 'plan', files: [
  {id: 0, name: 'invoice-2025.txt', category: 'Счета/2025', selected: true, selectable: true},
  {id: 1, name: 'Screenshot.png', category: 'На рабочем столе', selected: false, selectable: false},
 ]}).mockResolvedValueOnce({count: 1, issues: []}).mockResolvedValueOnce(base);
 render(<DesktopCleanup/>);
 expect(screen.queryByRole('textbox')).toBeNull();
 expect(screen.queryByRole('button', {name: 'Текстовый запрос'})).toBeNull();
 fireEvent.click(await screen.findByRole('button', {name: 'Счета по годам'}));
 fireEvent.click(screen.getByRole('button', {name: 'Добавить правило'}));
 fireEvent.change(screen.getAllByLabelText('Файлы для правила')[1], {target: {value: 'Скриншоты'}});
 fireEvent.change(screen.getAllByLabelText('Действие с файлами')[1], {target: {value: 'keep'}});
 fireEvent.click(screen.getByRole('button', {name: 'Предпросмотр'}));
 expect(await screen.findByRole('group', {name: 'Счета / 2025'})).toBeTruthy();
 expect(screen.queryByLabelText('Файлы для правила')).toBeNull();
 expect((screen.getByRole('checkbox', {name: /Screenshot.png/}) as HTMLInputElement).disabled).toBe(true);
 expect(invoke).toHaveBeenCalledWith('desktop.command', {action: 'sort', rules: [
  {subject: 'Счета', mode: 'year'}, {subject: 'Скриншоты', mode: 'keep'},
 ]});
 expect(invoke).not.toHaveBeenCalledWith('desktop.move', expect.anything());
 fireEvent.click(screen.getByRole('button', {name: 'Переместить (1)'}));
 expect(await screen.findByText('Перемещено файлов: 1.')).toBeTruthy();
 expect(invoke).toHaveBeenCalledWith('desktop.move', {previewId: 'plan', ids: [0]});
});

it('returns from review to the same draft and discards its actionable preview', async () => {
 vi.mocked(invoke).mockResolvedValueOnce(status).mockResolvedValueOnce(base);
 render(<DesktopCleanup/>);
 fireEvent.click(await screen.findByRole('button', {name: 'Счета по годам'}));
 fireEvent.click(screen.getByRole('button', {name: 'Предпросмотр'}));
 const back = await screen.findByRole('button', {name: 'Изменить правила'});
 fireEvent.click(back);
 const action = screen.getByLabelText('Действие с файлами') as HTMLSelectElement;
 expect(action.value).toBe('year');
 fireEvent.change(action, {target: {value: 'move'}});
 expect((screen.getByRole('button', {name: 'Предпросмотр'}) as HTMLButtonElement).disabled).toBe(true);
 expect(screen.queryByRole('button', {name: /Переместить/})).toBeNull();
});

it('opens the native folder picker directly and reports the changed folder', async () => {
 vi.mocked(invoke).mockResolvedValueOnce(status)
  .mockResolvedValueOnce({...base, id: 'icon-plan'})
  .mockResolvedValueOnce({path: 'C:\\Материалы', canUndoIcons: true});
 render(<DesktopCleanup/>);
 fireEvent.click(await screen.findByRole('button', {name: 'Значки папок'}));
 fireEvent.click(screen.getByRole('button', {name: 'Выбрать папку'}));
 expect(await screen.findByText(/Значок изменён/)).toBeTruthy();
 expect(invoke).toHaveBeenCalledWith('desktop.command', {action: 'icons'});
 expect(invoke).toHaveBeenCalledWith('desktop.icon', {previewId: 'icon-plan'});
});

it('opens the desktop folder list directly and keeps it open after applying changes', async () => {
 vi.mocked(invoke).mockResolvedValueOnce(status).mockResolvedValueOnce({...base, id: 'suggest-icon'});
 render(<DesktopCleanup/>);
 fireEvent.click(await screen.findByRole('button', {name: 'Значки папок'}));
 vi.mocked(invoke).mockResolvedValueOnce({id: 'batch', folders: [
  {id: 0, name: 'игры', icon: 'Игры'}, {id: 1, name: 'Финансы', icon: 'Документы'},
 ], hasMore: false, styles: ['Классическая', 'Цветная', 'Контурная', 'Современная', 'Ретро', 'Закрытая контурная',
  'Закрытая залитая', 'Открытая контурная', 'Открытая залитая'].map((name, id) => ({id, name})),
  iconImages: {'Игры': [...'ABCDEFGHI'].map(suffix => `data:image/png;base64,AAA${suffix}`),
   'Документы': [...'ABCDEFGHI'].map(suffix => `data:image/png;base64,BAA${suffix}`)}})
  .mockResolvedValueOnce({count: 1, issues: [], canUndoIcons: true})
  .mockResolvedValueOnce({count: 1, issues: [], canUndoIcons: false});
 fireEvent.click(screen.getByRole('button', {name: 'Папки на рабочем столе'}));
 const games = await screen.findByRole('checkbox', {name: 'игры'});
 expect(games.closest('tr')?.textContent).toContain('Игры');
 expect(games.closest('tr')?.querySelector('img')?.getAttribute('src')).toBe('data:image/png;base64,AAAA');
 expect(screen.getByRole('checkbox', {name: 'Финансы'}).closest('tr')?.textContent).toContain('Документы');
 expect((screen.getByRole('button', {name: 'Изменить значки (0)'}) as HTMLButtonElement).disabled).toBe(true);
 fireEvent.change(screen.getByLabelText('Общий стиль'), {target: {value: '3'}});
 fireEvent.change(screen.getByLabelText('Общий значок'), {target: {value: 'Документы'}});
 expect(games.closest('tr')?.querySelector('img')?.getAttribute('src')).toBe('data:image/png;base64,BAAD');
 fireEvent.click(screen.getByRole('checkbox', {name: 'Настраивать папки по отдельности'}));
 fireEvent.click(games);
 fireEvent.change(screen.getByLabelText('Значок папки «игры»'), {target: {value: 'Игры'}});
 expect(screen.getByRole('radio', {name: 'игры — Современная'})).toBeTruthy();
 fireEvent.click(screen.getByRole('radio', {name: 'игры — Открытая залитая'}));
 fireEvent.click(screen.getByRole('button', {name: 'Изменить значки (1)'}));
 expect(await screen.findByText('Изменено значков: 1.')).toBeTruthy();
 expect(invoke).toHaveBeenCalledWith('desktop.icons.preview', {previewId: 'suggest-icon'});
 expect(invoke).toHaveBeenCalledWith('desktop.icons.apply', {previewId: 'batch', ids: [0], styles: {0: 8}, icons: {0: 'Игры'}});
 expect(screen.getByRole('checkbox', {name: 'игры'})).toBe(games);
 fireEvent.click(screen.getByRole('button', {name: 'Вернуть как было'}));
 expect(await screen.findByText('Восстановлено значков: 1.')).toBeTruthy();
 expect(invoke).toHaveBeenCalledWith('desktop.icons.undo');
});

it('changes the document plan on drop and sends the topic only on confirmation', async () => {
 const base = {id: 'base', files: [], canUndo: false, hasMore: false, hasFolder: true, modelAvailable: true, answer: ''};
 vi.mocked(invoke).mockResolvedValueOnce(status).mockResolvedValueOnce({...base, id: 'documents', topics: ['Финансы', 'Проекты'], files: [
  {id: 0, name: 'портфолио.md', category: '', selected: false, selectable: true},
 ]}).mockResolvedValueOnce({count: 1, issues: []}).mockResolvedValueOnce(base);
 render(<DesktopCleanup/>);
 await screen.findByRole('button', {name: 'Предпросмотр'});
 fireEvent.change(screen.getByLabelText('Где разобрать файлы?'), {target: {value: 'documents'}});
 const handle = await screen.findByText('портфолио.md');
 const target = screen.getByText('Проекты', {selector: '.desktop-topic-target'});
 Object.defineProperty(handle, 'setPointerCapture', {value: vi.fn()});
 Object.defineProperty(document, 'elementFromPoint', {value: () => target, configurable: true});
 fireEvent.pointerDown(handle, {isPrimary: true, button: 0, pointerId: 1, clientX: 10, clientY: 10});
 fireEvent.pointerMove(handle, {pointerId: 1, clientX: 60, clientY: 60});
 fireEvent.pointerUp(handle, {pointerId: 1, clientX: 60, clientY: 60});
 expect(screen.getByRole('group', {name: 'Проекты'})).toBeTruthy();
 expect(invoke).toHaveBeenCalledTimes(2);
 fireEvent.click(screen.getByRole('button', {name: 'Переместить (1)'}));
 await screen.findByText('Перемещено файлов: 1.');
 expect(invoke).toHaveBeenCalledWith('desktop.move', {previewId: 'documents', ids: [0], topics: {0: 'Проекты'}});
});
