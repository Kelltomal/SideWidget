import {t} from './i18n';
import {useEffect, useRef, useState, type PointerEvent} from 'react';
import {Icon, invoke, nativeAvailable, Notice, useAction} from './ui';
import {DesktopCommandBuilder, initialRules} from './DesktopCommandBuilder';
import './desktop-cleanup.css';

type DesktopFile = {id: number; name: string; category: string; selected: boolean; selectable: boolean; reason?: string};
type PlanSummary = {steps: {subject: string; mode: keyof typeof planMessages; folder?: string}[]; selected: number; unclear: number};
type CleanupStatus = {canUndo: boolean; hasFolder: boolean; canUndoIcons: boolean};
type Preview = CleanupStatus & {id: string; files: DesktopFile[]; hasMore: boolean; modelAvailable: boolean; topics?: string[]; summary?: PlanSummary};
type Result = {count: number; issues: {name: string; message: string}[]};
type IconResult = Result & {canUndoIcons: boolean};
type FolderIconBatch = {id: string; folders: {id: number; name: string; icon: string}[]; hasMore: boolean;
 iconImages: Record<string, string[]>; styles: {id: number; name: string; lightPreview?: boolean}[]};

const planMessages = {
 keep: '{subject} останутся на рабочем столе.',
 year: '{subject} разложу по годам.',
 type: '{subject} разложу по типам.',
 topic: 'Документы разложу по темам. Файлы с неясной темой останутся на столе.',
 move: '{subject} перенесу в «{folder}».',
};
export function DesktopCleanup() {
 const [preview, setPreview] = useState<Preview>();
 const [selected, setSelected] = useState<number[]>([]);
 const [commandAction, setCommandAction] = useState<'sort' | 'icons'>('sort');
 const [rules, setRules] = useState(initialRules);
 const [status, setStatus] = useState<CleanupStatus>({canUndo: false, hasFolder: false, canUndoIcons: false});
 const [source, setSource] = useState<'desktop' | 'documents'>('desktop');
 const [topics, setTopics] = useState<Record<number, string>>({});
 const dragged = useRef<{id: number; x: number; y: number; active: boolean} | null>(null);
 const [dragging, setDragging] = useState<{name: string; x: number; y: number}>();
 const [dropTarget, setDropTarget] = useState<string>();
 const [result, setResult] = useState<{action: 'move' | 'undo' | 'icons' | 'undo-icons'; data: Result}>();
 const [changedFolder, setChangedFolder] = useState<string>();
 const [folderIcons, setFolderIcons] = useState<FolderIconBatch>();
 const [folderStyles, setFolderStyles] = useState<Record<number, number>>({});
 const [folderIconChoices, setFolderIconChoices] = useState<Record<number, string>>({});
 const [sharedStyle, setSharedStyle] = useState(0);
 const [sharedIcon, setSharedIcon] = useState('');
 const [individualIcons, setIndividualIcons] = useState(false);
 const {busy, error, run, setError} = useAction();
 const actions = useRef<HTMLDivElement>(null);
 const iconList = useRef<HTMLFieldSetElement>(null);
 const reviewHeading = useRef<HTMLHeadingElement>(null);
 const editor = useRef<HTMLDivElement>(null);
 const reviewing = commandAction === 'sort' && source === 'desktop' && !!preview;
 const reviewReady = reviewing && !busy;

 useEffect(() => { if (folderIcons) iconList.current?.focus(); }, [folderIcons]);
 useEffect(() => { if (reviewReady) reviewHeading.current?.focus(); }, [reviewReady]);

 function showPreview(next: Preview, selectSuggested = true) {
  setPreview(next);
  setStatus(next);
  setFolderIcons(undefined);
  setTopics({});
  setSelected(selectSuggested ? next.files.filter(file => file.selected).map(file => file.id) : []);
 }

 useEffect(() => {
  if (!nativeAvailable) return;
  let active = true;
  void run(async () => {
   const next = await invoke<CleanupStatus>('desktop.status');
   if (active) setStatus(next);
  });
  return () => { active = false; };
 }, []);

 const incompleteCommand = rules.some(rule => rule.mode === 'move' && !rule.folder?.trim());

 function requestPreview(from: typeof source) {
  return from === 'documents'
   ? invoke<Preview>('desktop.documents')
   : invoke<Preview>('desktop.command', {action: 'sort', rules});
 }

 function clearPreview() {
  endDrag();
  setPreview(undefined);
  setFolderIcons(undefined);
  setSelected([]);
  setTopics({});
  setResult(undefined);
  setChangedFolder(undefined);
  setError('');
 }

 async function refresh(from = source) {
  clearPreview();
  showPreview(await requestPreview(from));
 }

 function chooseCommand(action: typeof commandAction) {
  if (action === commandAction) return;
  setCommandAction(action);
  if (action === 'sort' && source === 'documents') void run(() => refresh('documents'));
  else clearPreview();
 }

 function editRules() {
  clearPreview();
  requestAnimationFrame(() => editor.current?.focus());
 }

 async function change(action: 'move' | 'undo') {
  if (action === 'move' && (!preview || !selected.length)) return;
  actions.current?.focus();
  setResult(undefined);
  const overrides = Object.fromEntries(selected.filter(id => topics[id]).map(id => [id, topics[id]]));
  const data = action === 'move' && preview
   ? await invoke<Result>('desktop.move', {previewId: preview.id, ids: selected, ...(source === 'documents' ? {topics: overrides} : {})})
   : await invoke<Result>('desktop.undo');
  setResult({action, data});
  if (!preview) {
   setStatus(await invoke<CleanupStatus>('desktop.status'));
   return;
  }
  setSelected([]);
  try {
   showPreview(await requestPreview(source), false);
  } catch (error) {
   setPreview(undefined);
   throw error;
  }
 }

 async function changeFolderIcon() {
  const {id} = await invoke<Preview>('desktop.command', {action: 'icons'});
  const {path, canUndoIcons} = await invoke<{path: string | null; canUndoIcons: boolean}>('desktop.icon', {previewId: id});
  setStatus(current => ({...current, canUndoIcons}));
  if (path) {
   setResult(undefined);
   setChangedFolder(path);
  }
 }

 async function previewDesktopIcons() {
  const {id} = await invoke<Preview>('desktop.command', {action: 'icons'});
  const list = await invoke<FolderIconBatch>('desktop.icons.preview', {previewId: id});
  setChangedFolder(undefined);
  setResult(undefined);
  setSelected([]);
  setFolderStyles({});
  setFolderIconChoices({});
  setSharedStyle(0);
  setSharedIcon('');
  setIndividualIcons(false);
  setFolderIcons(list);
 }

 async function applyDesktopIcons() {
  if (!activeFolderIcons || !selected.length) return;
  const styles = Object.fromEntries(selected.map(id => [id, styleForFolder(id)]));
  const icons = Object.fromEntries(selected.map(id => [id, iconOverride(id)]).filter(([, icon]) => icon));
  const data = await invoke<IconResult>('desktop.icons.apply', {previewId: activeFolderIcons.id, ids: selected, styles, icons});
  setStatus(current => ({...current, canUndoIcons: data.canUndoIcons}));
  setResult({action: 'icons', data});
 }

 function styleForFolder(id: number) { return individualIcons ? folderStyles[id] ?? sharedStyle : sharedStyle; }
 function iconOverride(id: number) { return (individualIcons ? folderIconChoices[id] : '') || sharedIcon; }

 async function undoFolderIcons() {
  const data = await invoke<IconResult>('desktop.icons.undo');
  actions.current?.focus();
  setStatus(current => ({...current, canUndoIcons: data.canUndoIcons}));
  setChangedFolder(undefined);
  setResult({action: 'undo-icons', data});
 }

 function assignTopic(id: number, topic: string) {
  if (busy || !preview?.files.some(file => file.id === id) || (topic && !preview.topics?.includes(topic))) return;
  setTopics(previous => ({...previous, [id]: topic}));
  setSelected(previous => topic ? [...new Set([...previous, id])] : previous.filter(value => value !== id));
 }

 function endDrag() {
  dragged.current = null;
  setDragging(undefined);
  setDropTarget(undefined);
 }

 function targetAt(event: PointerEvent) {
  return document.elementFromPoint(event.clientX, event.clientY)?.closest<HTMLElement>('[data-topic]')?.dataset.topic;
 }

 function trackDrag(event: PointerEvent, file: DesktopFile) {
  const start = dragged.current;
  if (!start || start.id !== file.id) return;
  if (!start.active && Math.hypot(event.clientX - start.x, event.clientY - start.y) < 5) return;
  start.active = true;
  setDragging({name: file.name, x: event.clientX, y: event.clientY});
  setDropTarget(targetAt(event));
 }

 if (!nativeAvailable) return <Notice>{t('Уборка рабочего стола доступна в приложении Windows.')}</Notice>;

 const documents = source === 'documents';
 const groups = new Map<string, DesktopFile[]>();
 const topicFor = (file: DesktopFile) => topics[file.id] ?? file.category;
 for (const file of preview?.files ?? []) {
  const category = topicFor(file);
  const group = groups.get(category) ?? [];
  group.push(file);
  groups.set(category, group);
 }
 const selectable = preview?.files.filter(file => file.selectable && (!documents || topicFor(file))) ?? [];
 const allSelected = selectable.length === selected.length;
 const activeFolderIcons = folderIcons;
 const iconPool = activeFolderIcons ? Object.keys(activeFolderIcons.iconImages) : [];
 const plan = preview?.summary;

 return <section className="stack" aria-label={t('Файлы и папки')} aria-busy={busy}>
  <div className="desktop-command-modes">
   <button type="button" aria-pressed={commandAction === 'sort'} disabled={busy} onClick={() => chooseCommand('sort')}>
    <Icon name="apps" size={16}/>{t('Разобрать файлы')}
   </button>
   <button type="button" aria-pressed={commandAction === 'icons'} disabled={busy} onClick={() => chooseCommand('icons')}>
    <Icon name="folder" size={16}/>{t('Значки папок')}
   </button>
  </div>
  {commandAction === 'sort' && !reviewing && <div className="stack" ref={editor} tabIndex={-1}>
   <label>{t('Где разобрать файлы?')}
    <select value={source} disabled={busy} onChange={event => {
     const next = event.target.value === 'documents' ? 'documents' : 'desktop';
     setSource(next);
     if (next === 'documents') void run(() => refresh(next));
     else clearPreview();
    }}><option value="desktop">{t('Рабочий стол')}</option><option value="documents">{t('Разобранное / Документы')}</option></select>
   </label>
   {documents
    ? <p>{t('Перетащите файл на тему или выберите её в списке рядом с файлом. После подтверждения файлы попадут в папки тем внутри «Разобранное».')}</p>
    : <DesktopCommandBuilder rules={rules} busy={busy} onRules={setRules}/>}
  </div>}
  {commandAction === 'icons' && !activeFolderIcons && <div className="stack">
   <p>{t('Подберём значок после выбора папки.')}</p>
   <div className="actions">
    <button className="primary" disabled={busy} onClick={() => void run(previewDesktopIcons)}>{t('Папки на рабочем столе')}</button>
    <button disabled={busy} onClick={() => void run(changeFolderIcon)}>{t('Выбрать папку')}</button>
   </div>
  </div>}
  {error && <Notice error>{t(error)}</Notice>}
  {result && <Notice>{t({icons: 'Изменено значков: {count}.', 'undo-icons': 'Восстановлено значков: {count}.',
   move: 'Перемещено файлов: {count}.', undo: 'Возвращено файлов: {count}.'}[result.action],{count:result.data.count})}</Notice>}
  {changedFolder && <Notice>{t('Значок изменён. {path}',{path:changedFolder})}</Notice>}
  {!!result?.data.issues.length && <ul className="desktop-cleanup-issues" aria-label={t('Ошибки операции')}>
   {result.data.issues.map(issue => <li key={issue.name}><strong>{issue.name}</strong><small>{t(issue.message)}</small></li>)}
  </ul>}
  {activeFolderIcons && <fieldset className="desktop-cleanup-group" ref={iconList} tabIndex={-1}>
   <legend>{t('Папки на рабочем столе')}</legend>
   {activeFolderIcons.folders.length ? <>
    <div className="desktop-folder-icon-settings">
     <label>{t('Общий стиль')}<select disabled={busy} value={sharedStyle} onChange={event => setSharedStyle(Number(event.target.value))}>
      {activeFolderIcons.styles.map(style => <option key={style.id} value={style.id}>{t(style.name)}</option>)}
     </select></label>
     <label>{t('Общий значок')}<select disabled={busy} value={sharedIcon} onChange={event => setSharedIcon(event.target.value)}>
      <option value="">{t('Подбирать по папке')}</option>
      {iconPool.map(icon => <option key={icon} value={icon}>{t(icon)}</option>)}
     </select></label>
    </div>
    <label className="checkbox"><input type="checkbox" checked={individualIcons} disabled={busy}
     onChange={event => setIndividualIcons(event.target.checked)}/>{t('Настраивать папки по отдельности')}</label>
    <table className="desktop-folder-icons">
    <thead><tr><th scope="col">{t('Папка')}</th><th scope="col">{t('Новый значок')}</th></tr></thead>
    <tbody>{activeFolderIcons.folders.map(folder => {
     const icon = iconOverride(folder.id) || folder.icon;
     const chosen = activeFolderIcons.styles[styleForFolder(folder.id)];
     const images = activeFolderIcons.iconImages[icon];
     return <tr key={folder.id}>
     <td><label className="checkbox"><input type="checkbox" checked={selected.includes(folder.id)} disabled={busy}
      aria-label={folder.name} aria-describedby={`desktop-folder-icon-${folder.id}`}
      onChange={event => setSelected(event.target.checked ? [...selected, folder.id] : selected.filter(id => id !== folder.id))}/>
      <span>{folder.name}<small id={`desktop-folder-icon-${folder.id}`}>{t(icon)}</small></span></label>
      {individualIcons && <select aria-label={t('Значок папки «{name}»',{name:folder.name})} disabled={busy}
       value={folderIconChoices[folder.id] ?? ''}
       onChange={event => {const icon = event.target.value; setFolderIconChoices(current => ({...current, [folder.id]: icon}));}}>
       <option value="">{t('Общий значок')}</option>
       {iconPool.map(option => <option key={option} value={option}>{t(option)}</option>)}
      </select>}</td>
     <td>{individualIcons ? <div className="desktop-folder-icon-styles" role="radiogroup" aria-label={t('Стиль папки «{name}»',{name:folder.name})}>
      {activeFolderIcons.styles.map(style => <label key={style.id} title={t(style.name)} className="desktop-folder-icon-choice">
       <input type="radio" className="sr-only" name={`desktop-folder-style-${folder.id}`} disabled={busy}
        aria-label={`${folder.name} — ${t(style.name)}`} checked={chosen.id === style.id}
        onChange={() => setFolderStyles(current => ({...current, [folder.id]: style.id}))}/>
       <img src={images[style.id]} width={36} height={36} alt=""
        data-light-preview={style.lightPreview || undefined}/>
      </label>)}
     </div> : <span className="desktop-folder-icon-preview">
      <img src={images[chosen.id]} width={36} height={36} alt="" data-light-preview={chosen.lightPreview || undefined}/>
      <small>{t(chosen.name)}</small>
     </span>}</td>
    </tr>;})}</tbody>
   </table></> : <Notice>{t('На рабочем столе нет доступных папок.')}</Notice>}
   {activeFolderIcons.hasMore && <Notice>{t('Показаны первые {count} папок.',{count:activeFolderIcons.folders.length})}</Notice>}
  </fieldset>}
  {reviewing && <div className="desktop-cleanup-answer">
   <h2 ref={reviewHeading} tabIndex={-1}>{t('Предпросмотр')}</h2>
   {plan && <ul>{plan.steps.map((step, index) => <li key={index}>
    {t(planMessages[step.mode], {subject: t(step.subject), folder: step.folder})}
   </li>)}</ul>}
   <small role="status">{selected.length > 0
    ? t('Отмечено для переноса — {count}.', {count: selected.length}) : t('Для переноса пока ничего не отмечено.')}
    {!!plan?.unclear && ' ' + t('Требуют проверки — {count}.', {count: plan.unclear})}</small>
  </div>}
  <div className={reviewing ? 'actions desktop-review-actions' : 'actions'} ref={actions} tabIndex={-1}>
   {activeFolderIcons && !!activeFolderIcons.folders.length && <>
    <button className="primary" disabled={busy || !selected.length} onClick={() => void run(applyDesktopIcons)}>
     {t('Изменить значки ({count})',{count:selected.length})}
    </button>
    <button disabled={busy} onClick={() => setSelected(selected.length === activeFolderIcons.folders.length ? [] : activeFolderIcons.folders.map(folder => folder.id))}>
     {selected.length === activeFolderIcons.folders.length ? t('Снять выбор со всех') : t('Выбрать все')}
    </button>
   </>}
   {activeFolderIcons && <button className="quiet" disabled={busy} onClick={clearPreview}>{t('Назад')}</button>}
   {commandAction === 'sort' && <>
    {!!selectable.length && <button className="primary" disabled={busy || !selected.length} onClick={() => void run(() => change('move'))}>
     {t('Переместить ({count})',{count:selected.length})}
    </button>}
    {reviewing ? <button disabled={busy} onClick={editRules}>{t('Изменить правила')}</button>
     : <button className={documents ? undefined : 'primary'} disabled={busy || (!documents && incompleteCommand)}
       onClick={() => void run(refresh)}>{documents ? t('Обновить список') : t('Предпросмотр')}</button>}
    {status.hasFolder && <button className="quiet" disabled={busy} onClick={() => void run(() => invoke('desktop.open'))}>
     <Icon name="folder" size={16}/>{t('Открыть «Разобранное»')}
    </button>}
    {status.canUndo && <button className="quiet" disabled={busy} onClick={() => void run(() => change('undo'))}>{t('Отменить перенос')}</button>}
   </>}
   {commandAction === 'icons' && status.canUndoIcons && <button disabled={busy} onClick={() => void run(undoFolderIcons)}>{t('Вернуть как было')}</button>}
  </div>
  {commandAction === 'sort' && !preview && busy && <Notice>{t('Разбираем файлы…')}</Notice>}
  {documents && preview && !preview.modelAvailable && <Notice>{t('Модель недоступна. Выберите темы вручную.')}</Notice>}
  {preview?.files.length === 0 && <Notice>{documents ? t('В папке «Разобранное / Документы» нет файлов для разбора.') : t('На рабочем столе нет файлов для переноса.')}</Notice>}
  {preview?.hasMore && <Notice>{t('Показаны первые {count} файлов.',{count:preview.files.length})}</Notice>}
  {!!preview?.files.length && <>
   {!!selectable.length && <button className="quiet desktop-cleanup-select" disabled={busy} onClick={() => setSelected(allSelected ? [] : selectable.map(file => file.id))}>
    {allSelected ? t('Снять выбор со всех') : t('Выбрать все')}
   </button>}
   {documents && <div className="desktop-topic-targets" role="group" aria-label={t('Перетащите файл на тему')}>
    {['', ...(preview.topics ?? [])].map(topic => <div key={topic} className="desktop-topic-target"
     data-topic={topic} data-dragging={!!dragging || undefined} data-drop-target={dropTarget === topic || undefined}>
     {topic ? t(topic) : t('Оставить на месте')}
    </div>)}
   </div>}
   {[...groups].map(([category, files]) => <fieldset className="desktop-cleanup-group" key={category}>
    <legend><Icon name="folder" size={16}/>{category.split('/').map(part => t(part)).join(' / ') || t('Оставить на месте')}</legend>
    {files.map(file => <div className="desktop-cleanup-row" key={file.id}>
    <label className="checkbox">
     <input type="checkbox" checked={selected.includes(file.id)} disabled={busy || !file.selectable || (documents && !category)} onChange={event => {
      setSelected(event.target.checked ? [...selected, file.id] : selected.filter(id => id !== file.id));
     }}/>
     <span className={documents ? 'desktop-drag-handle' : undefined} onPointerDown={event => {
      if (!documents || busy || !event.isPrimary || event.button !== 0) return;
      event.preventDefault();
      dragged.current = {id: file.id, x: event.clientX, y: event.clientY, active: false};
      event.currentTarget.setPointerCapture(event.pointerId);
     }} onPointerMove={event => trackDrag(event, file)} onPointerUp={event => {
      if (!dragged.current) return;
      const topic = targetAt(event);
      if (dragged.current.active && topic !== undefined) assignTopic(file.id, topic);
      endDrag();
     }} onPointerCancel={endDrag} onLostPointerCapture={endDrag} onClick={event => {if (documents) event.preventDefault();}}>
      {file.name}{file.reason && (!documents || !category) && <small>{t(file.reason)}</small>}
     </span>
    </label>
    {documents && <select aria-label={t('Тема файла {name}',{name:file.name})} value={category} disabled={busy} onChange={event => assignTopic(file.id, event.target.value)}>
     <option value="">{t('Оставить на месте')}</option>
     {preview.topics?.map(topic => <option key={topic} value={topic}>{t(topic)}</option>)}
    </select>}
    </div>)}
   </fieldset>)}
  </>}
  {dragging && <div className="desktop-drag-preview" aria-hidden="true" style={{transform: `translate(${dragging.x + 12}px, ${dragging.y + 12}px)`}}>{dragging.name}</div>}
 </section>;
}
