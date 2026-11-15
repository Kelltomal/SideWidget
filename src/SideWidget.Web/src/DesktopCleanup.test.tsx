// @vitest-environment jsdom
import {cleanup, fireEvent, render, screen} from '@testing-library/react';
import {afterEach, expect, it, vi} from 'vitest';
import {DesktopCleanup} from './DesktopCleanup';
import {invoke} from './ui';
import {setLocale} from './i18n';

vi.mock('./ui', async importOriginal => ({...await importOriginal<typeof import('./ui')>(), nativeAvailable: true, invoke: vi.fn()}));
afterEach(() => { cleanup(); vi.resetAllMocks(); setLocale('ru'); });

it('sends the original prompt and shows the plan in the interface language', async () => {
 setLocale('en');
 const base = {id: 'base', files: [], canUndo: false, hasMore: false, hasFolder: false, modelAvailable: true, answer: ''};
 vi.mocked(invoke).mockResolvedValueOnce(base).mockResolvedValueOnce({...base, id: 'plan', answer: 'Архивы перенесу.',
  summary: {steps: [{subject: 'Архивы', mode: 'move', folder: '備份 2026'}], selected: 1, unclear: 0}});
 render(<DesktopCleanup/>);
 const field = await screen.findByPlaceholderText('Sort invoices by year and leave screenshots on the desktop');
 const prompt = '把压缩包移到文件夹「備份 2026」';
 fireEvent.change(field, {target: {value: prompt}});
 const applyButton = screen.getByRole('button', {name: /apply/i});
 fireEvent.click(applyButton);
 expect(await screen.findByText('Move Archives to “備份 2026”. Selected to move: 1.')).toBeTruthy();
 expect(invoke).toHaveBeenCalledWith('desktop.preview', {prompt});
 expect(invoke).not.toHaveBeenCalledWith('desktop.move', expect.anything());
});

it('opens the native folder picker for a fresh icon plan and reports the changed folder', async () => {
 const base = {id: 'base', files: [], canUndo: false, hasMore: false, hasFolder: false, modelAvailable: true, answer: ''};
 vi.mocked(invoke).mockResolvedValueOnce(base)
  .mockResolvedValueOnce({...base, id: 'icon-plan', folderIcon: 'Фото'})
  .mockResolvedValueOnce({path: null})
  .mockResolvedValueOnce({path: 'C:\\Материалы'});
 render(<DesktopCleanup/>);
 await screen.findByText('На рабочем столе нет файлов для переноса.');
 const prompt = screen.getByRole('textbox');
 fireEvent.change(prompt, {target: {value: 'Поменяй значок папки на фото'}});
 fireEvent.click(screen.getByRole('button', {name: 'Применить промпт'}));
 const picker = await screen.findByRole('button', {name: 'Выбрать папку'});
 expect(screen.queryByText('На рабочем столе нет файлов для переноса.')).toBeNull();
 fireEvent.click(picker);
 await screen.findByRole('button', {name: 'Выбрать папку'});
 fireEvent.click(picker);
 expect(await screen.findByText(/Значок изменён/)).toBeTruthy();
 expect(invoke).toHaveBeenCalledWith('desktop.icon', {previewId: 'icon-plan'});
 expect(screen.queryByRole('button', {name: 'Выбрать папку'})).toBeNull();
});

it('offers a folder picker when the prompt asks SideWidget to choose an icon', async () => {
 const base = {id: 'base', files: [], canUndo: false, hasMore: false, hasFolder: false, modelAvailable: true, answer: ''};
 vi.mocked(invoke).mockResolvedValueOnce(base).mockResolvedValueOnce({...base, id: 'suggest-icon', suggestFolderIcon: true});
 render(<DesktopCleanup/>);
 await screen.findByText('На рабочем столе нет файлов для переноса.');
 fireEvent.change(screen.getByRole('textbox'), {target: {value: 'Поменяй значки на рабочем столе'}});
 fireEvent.click(screen.getByRole('button', {name: 'Применить промпт'}));
 expect(await screen.findByRole('button', {name: 'Выбрать папку'})).toBeTruthy();
 expect(screen.getByText('Подберём значок после выбора папки.')).toBeTruthy();
 expect(screen.queryByText('На рабочем столе нет файлов для переноса.')).toBeNull();
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

it('keeps uncertain files unchecked and requires applying a changed prompt before moving', async () => {
 const base = {id: 'base', canUndo: false, hasMore: false, hasFolder: false, modelAvailable: true, answer: ''};
 vi.mocked(invoke).mockResolvedValueOnce({...base, files: [
  {id: 0, name: 'invoice.txt', category: 'Финансы', selected: true, selectable: true},
  {id: 1, name: 'sample.txt', category: 'Документы', selected: false, selectable: true},
 ]}).mockResolvedValueOnce({...base, id: 'prompt', answer: 'Скриншоты останутся на рабочем столе.', files: [
  {id: 1, name: 'invoice.txt', category: 'Счета/2025', selected: true, selectable: true},
  {id: 2, name: 'Screenshot.png', category: 'На рабочем столе', selected: false, selectable: false, reason: 'Оставить по запросу'},
 ]}).mockResolvedValueOnce({count: 1, issues: []}).mockResolvedValueOnce({...base, files: []});
 render(<DesktopCleanup/>);
 const uncertain = await screen.findByRole('checkbox', {name: /sample.txt/}) as HTMLInputElement;
 expect(uncertain.checked).toBe(false);
 const prompt = 'Разложи счета по годам, а скриншоты оставь на столе';
 fireEvent.change(screen.getByLabelText('Что сделать с файлами и папками?'), {target: {value: prompt}});
 expect((screen.getByRole('button', {name: 'Переместить (1)'}) as HTMLButtonElement).disabled).toBe(true);
 fireEvent.click(screen.getByRole('button', {name: 'Применить промпт'}));
 expect(await screen.findByRole('group', {name: 'Счета / 2025'})).toBeTruthy();
 expect(screen.getByText('Скриншоты останутся на рабочем столе.')).toBeTruthy();
 expect((screen.getByRole('checkbox', {name: /Screenshot.png/}) as HTMLInputElement).disabled).toBe(true);
 expect(invoke).toHaveBeenCalledWith('desktop.preview', {prompt});
 fireEvent.click(screen.getByRole('button', {name: 'Переместить (1)'}));
 expect(await screen.findByText('Перемещено файлов: 1.')).toBeTruthy();
 expect(invoke).toHaveBeenCalledWith('desktop.move', {previewId: 'prompt', ids: [1]});
});

it('changes the document plan on drop and sends the topic only on confirmation', async () => {
 const base = {id: 'base', files: [], canUndo: false, hasMore: false, hasFolder: true, modelAvailable: true, answer: ''};
 vi.mocked(invoke).mockResolvedValueOnce(base).mockResolvedValueOnce({...base, id: 'documents', topics: ['Финансы', 'Проекты'], files: [
  {id: 0, name: 'портфолио.md', category: '', selected: false, selectable: true},
 ]}).mockResolvedValueOnce({count: 1, issues: []}).mockResolvedValueOnce(base);
 render(<DesktopCleanup/>);
 await screen.findByText('На рабочем столе нет файлов для переноса.');
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
