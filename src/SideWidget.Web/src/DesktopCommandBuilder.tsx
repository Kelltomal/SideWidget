import {t} from './i18n';
import {Icon} from './ui';

const subjects = ['Файлы', 'Документы', 'Счета', 'Скриншоты', 'Изображения', 'Архивы', 'Логи',
 'Видео', 'Медиа', 'Игры', 'Программы', 'Скрипты', 'Запуски серверов'] as const;
const modes = {type: 'Разложить по типам', year: 'Разложить по годам', topic: 'Разложить по темам',
 move: 'Перенести в папку', keep: 'Оставить на месте'} as const;
type Subject = typeof subjects[number];
type Mode = keyof typeof modes;
type Rule = {subject: Subject; mode: Mode; folder?: string};
const initialRule: Rule = {subject: 'Файлы', mode: 'type'};
const ruleLimit = 12;
export const initialRules: Rule[] = [initialRule];
const presets: {label: string; rules: Rule[]}[] = [
 {label: 'По типам файлов', rules: initialRules},
 {label: 'Счета по годам', rules: [{subject: 'Счета', mode: 'year'}]},
 {label: 'Документы по темам', rules: [{subject: 'Документы', mode: 'topic'}]},
];

export function DesktopCommandBuilder({rules, onRules, busy}: {
 rules: Rule[]; onRules: (rules: Rule[]) => void; busy: boolean;
}) {
 function replaceRule(index: number, rule: Rule) {
  onRules(rules.map((current, position) => position === index ? rule : current));
 }

 return <div className="stack">
  <div className="desktop-command-presets">
   <h2>{t('Готовые правила')}</h2>
   <div className="actions">
    {presets.map(preset => <button key={preset.label} type="button" disabled={busy}
     aria-pressed={rules === preset.rules} onClick={() => onRules(preset.rules)}>{t(preset.label)}</button>)}
   </div>
  </div>
   {rules.map((rule, index) => <fieldset className="desktop-command-rule" key={index}>
    <legend>{rules.length === 1 ? t('Правило сортировки') : t('Правило {number}', {number: index + 1})}</legend>
    <label>{t('Файлы для правила')}
     <select value={rule.subject} disabled={busy} onChange={event => {
      const subject = event.target.value as Subject;
      replaceRule(index, {...rule, subject, mode: rule.mode === 'topic' && subject !== 'Документы' ? 'type' : rule.mode});
     }}>
      {subjects.map(subject => <option key={subject} value={subject}>{t(subject)}</option>)}
     </select>
    </label>
    <label>{t('Действие с файлами')}
     <select value={rule.mode} disabled={busy} onChange={event => {
      const mode = event.target.value as Mode;
      replaceRule(index, mode === 'move' ? {subject: rule.subject, mode, folder: ''} : {subject: rule.subject, mode});
     }}>
      {(Object.keys(modes) as Mode[]).filter(mode => mode !== 'topic' || rule.subject === 'Документы').map(mode =>
       <option key={mode} value={mode}>{t(modes[mode])}</option>)}
     </select>
    </label>
    {rule.mode === 'move' && <label>{t('Имя папки')}
     <input value={rule.folder ?? ''} maxLength={64} disabled={busy}
      onChange={event => replaceRule(index, {...rule, folder: event.target.value})}/>
    </label>}
    {rules.length > 1 && <button type="button" disabled={busy} className="quiet desktop-rule-remove"
     aria-label={t('Убрать правило {number}', {number: index + 1})}
     onClick={() => onRules(rules.filter((_, position) => index !== position))}>
     <Icon name="close" size={14}/>{t('Убрать правило')}
    </button>}
   </fieldset>)}
   <div className="actions"><button type="button" disabled={busy || rules.length >= ruleLimit}
    onClick={() => onRules([...rules, initialRule])}><Icon name="plus" size={16}/>{t('Добавить правило')}</button></div>
 </div>;
}
