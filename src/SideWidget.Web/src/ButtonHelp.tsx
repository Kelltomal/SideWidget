import {t,locale} from './i18n';
import {useEffect,useLayoutEffect,useRef,useState} from 'react';
import {createPortal} from 'react-dom';
import {Icon,invoke,nativeAvailable} from './ui';

const descriptions:Record<string,string>={
 'Поделиться':'Упаковать текущую версию SideWidget в ZIP и показать файл в Проводнике.',
 'Калькулятор':'Вычисления и история результатов с копированием.', 'Перевод':'Перевод текста через выбранного поставщика.',
 'QR-код':'Создать QR-код из текста или ссылки.', 'Валюты':'Пересчёт суммы по курсу валют.', 'Единицы':'Перевод величин между единицами измерения.',
 'Цвета':'Выбор цвета и копирование его кода.', 'Дневник':'Записи и заметки в дневнике.', 'Таймер':'Таймер для работы и отдыха.',
 'Настройки':'Размер, положение, поведение и состав панели.', 'Редактировать расположение':'Перестановка модулей на главном экране.',
 'Завершить редактирование':'Выйти из режима перестановки модулей.', 'Закрепить панель':'Оставить панель открытой.', 'Открепить панель':'Вернуть автоматическое скрытие панели.',
 'Вернуться на главную':'Открыть главный экран с модулями и быстрыми действиями.', 'Добавить':'Закрепить приложение для быстрого запуска.',
 'Все':'Открыть полный список и дополнительные возможности раздела.', 'Подробнее':'Открыть подробные данные этого раздела.',
 'Раскрыть музыку':'Показать плеер и управление воспроизведением.', 'Выбрать город':'Указать город для прогноза погоды.',
 'Сохранить настройки':'Применить текущие настройки панели.', 'Посчитать':'Вычислить введённое выражение.',
 'PowerShell':'Открыть консоль PowerShell.', 'CMD':'Открыть командную строку Windows.',
 'Копировать':'Скопировать результат в буфер обмена.', 'Очистить поиск':'Удалить поисковый запрос и результаты.',
 'Добавить приложение':'Выбрать программу или ярлык для закрепления в панели.', 'Выбрать окно':'Выбрать открытое окно и закрепить его приложение.',
 'Все настройки звука':'Открыть устройства, громкость и микшер приложений.',
};
export function ButtonHelp(){
 const [help,setHelp]=useState<{button:HTMLButtonElement;label:string;description:string}|null>(null);
 const preview=useRef<HTMLDivElement>(null);const [nativeHelp,setNativeHelp]=useState(nativeAvailable);
 useEffect(()=>{
  let timer:ReturnType<typeof setTimeout>|undefined;let active:HTMLButtonElement|null=null;let previousTitle:string|null=null;
  function clear(){clearTimeout(timer);}
  function dismiss(){clear();if(active){active.removeAttribute('data-help-open');const ids=(active.getAttribute('aria-describedby')||'').split(' ').filter(id=>id&&id!=='button-help');if(ids.length)active.setAttribute('aria-describedby',ids.join(' '));else active.removeAttribute('aria-describedby');if(previousTitle!==null&&!active.hasAttribute('title'))active.setAttribute('title',previousTitle);}active=null;previousTitle=null;setHelp(null);}
  function show(button:HTMLButtonElement,immediate=false){
   if(button.closest('[inert]')||button===active)return;const instant=!!active;dismiss();active=button;previousTitle=button.getAttribute('title');button.removeAttribute('title');
   const label=button.getAttribute('aria-label')||previousTitle||button.textContent?.trim()||t("Действие");
   const section=button.closest('section,[data-module],form')?.querySelector('h2')?.textContent;
   const descriptionKey=Object.keys(descriptions).find(key=>t(key)===label);
   const description=button.dataset.help||(descriptionKey?t(descriptions[descriptionKey]):"")||(previousTitle&&previousTitle!==label?previousTitle:'')||(button.disabled?t("Сейчас недоступно."):section?t("Действие в разделе «{section}».",{section}):'');
   timer=setTimeout(()=>{if(!button.isConnected){dismiss();return;}button.setAttribute('data-help-open','');button.setAttribute('aria-describedby',`${button.getAttribute('aria-describedby')||''} button-help`.trim());setHelp({button,label,description});},immediate||instant?0:400);
  }
  function over(event:PointerEvent){if(event.pointerType==='touch')return;const target=event.target as Element;const button=target.closest('button');if(button instanceof HTMLButtonElement)show(button);}
  function out(event:PointerEvent){const next=event.relatedTarget as Element|null;if(next?.closest?.('button')===active)return;clear();timer=setTimeout(dismiss,120);}
  function focus(event:FocusEvent){const button=event.target;if(button instanceof HTMLButtonElement)show(button,true);}
  function blur(){dismiss();}
  function key(event:KeyboardEvent){if(event.key==='Escape')dismiss();}
  function scroll(){if(!active?.matches(':hover')&&document.activeElement!==active)dismiss();}
  document.addEventListener('pointerover',over);document.addEventListener('pointerout',out);document.addEventListener('focusin',focus);document.addEventListener('focusout',blur);document.addEventListener('keydown',key);document.addEventListener('pointerdown',dismiss);document.addEventListener('scroll',scroll,true);window.addEventListener('resize',dismiss);
  return()=>{dismiss();if(nativeAvailable)void invoke('shell.tooltip',{visible:false}).catch(()=>{});document.removeEventListener('pointerover',over);document.removeEventListener('pointerout',out);document.removeEventListener('focusin',focus);document.removeEventListener('focusout',blur);document.removeEventListener('keydown',key);document.removeEventListener('pointerdown',dismiss);document.removeEventListener('scroll',scroll,true);window.removeEventListener('resize',dismiss);};
 },[locale()]);
 useLayoutEffect(()=>{
  if(!help)return;
  const source=help.button.querySelector('svg,img');if(source&&preview.current){const clone=source.cloneNode(true) as Element;clone.setAttribute('aria-hidden','true');clone.removeAttribute('id');preview.current.replaceChildren(clone);}
 },[help,nativeHelp]);
 useEffect(()=>{
  if(!nativeAvailable||!nativeHelp)return;
  const source=help?.button.querySelector('svg');
  const path=source?Array.from(source.querySelectorAll('path')).map(node=>node.getAttribute('d')||'').join(' ').slice(0,4096):'';
  const description=help?`${help.description}${help.button.disabled?' '+t("Сейчас недоступно."):''}`.slice(0,800):'';
  void invoke('shell.tooltip',help?{visible:true,label:help.label.slice(0,180),description,path}:{visible:false}).catch(()=>setNativeHelp(false));
 },[help,nativeHelp]);
 if(!help)return null;
 if(nativeHelp)return <span id="button-help" role="tooltip" className="sr-only">{help.label}. {help.description}</span>;
 return createPortal(<div id="button-help" role="tooltip" className="button-help"><div className="button-help-preview" ref={preview} key={help.label}><Icon name="arrow" size={28}/></div><div><strong>{help.label}</strong>{help.description&&<p>{help.description}</p>}{help.button.disabled&&help.description!==t("Сейчас недоступно.")&&<small>{t("Сейчас недоступно.")}</small>}</div></div>,document.body);
}
