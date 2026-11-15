import {afterEach,expect,it} from 'vitest';
import {languageNames,resolveLocale,setLocale,t,type Locale} from './i18n';

afterEach(()=>setLocale('ru'));

it.each([
 ['ru-RU','ru'],['en-GB','en'],['zh-Hans-CN','zh-CN'],['ja-JP','ja'],['de-AT','de'],
 ['uk-UA','uk'],['be-BY','be'],['es-MX','es'],['pt-BR','pt-BR'],['qaa','en'],
 ['zh_Hans','zh-CN'],['ZH_cn','zh-CN'],['pt_BR','pt-BR'],
])('resolves %s as %s', (locale,expected)=>expect(resolveLocale(locale)).toBe(expected));

it('renders the update prompt in every supported language', ()=>{
 for(const language of ['ru','en','zh-CN','ja','de','uk','be','es','pt-BR'] satisfies Locale[]){
  expect(languageNames).toHaveProperty(language);
  setLocale(language);
  expect(t('Доступна версия {version}',{version:'0.4.0'})).toContain('0.4.0');
  if(language!=='ru')expect(t('Пропустить эту версию')).not.toBe('Пропустить эту версию');
 }
});

it('falls back to English and then the source text when a catalog has no entry', ()=>{
 setLocale('qaa');
 expect(t('Доступна версия {version}',{version:'0.4.0'})).toBe('Version 0.4.0 is available');
 expect(t('Новая строка {name}',{name:'SideWidget'})).toBe('Новая строка SideWidget');
});
