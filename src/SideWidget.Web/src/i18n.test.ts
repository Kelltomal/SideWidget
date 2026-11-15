import {afterEach,expect,it} from 'vitest';
import {languageNames,resolveLocale,setLocale,t,type Locale} from './i18n';

afterEach(()=>setLocale('ru'));

it.each([
 ['ru-RU','ru'],['en-GB','en'],['zh-Hans-CN','zh-CN'],['zh-TW','en'],['ja-JP','ja'],['de-AT','de'],
 ['uk-UA','uk'],['be-BY','be'],['es-MX','es'],['pt-BR','pt-BR'],['pt-PT','en'],['fr-FR','en'],
])('resolves %s as %s', (locale,expected)=>expect(resolveLocale(locale)).toBe(expected));

it('renders the update prompt in every supported language', ()=>{
 for(const language of Object.keys(languageNames) as Locale[]){
  setLocale(language);
  expect(t('Доступна версия {version}',{version:'0.4.0'})).toContain('0.4.0');
  if(language!=='ru')expect(t('Пропустить эту версию')).not.toBe('Пропустить эту версию');
 }
});
