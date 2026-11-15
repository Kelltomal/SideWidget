import {useSyncExternalStore} from 'react';

const existingNames: Record<string, string> = {
 ru: 'Русский', en: 'English', 'zh-CN': '简体中文', ja: '日本語', de: 'Deutsch',
 uk: 'Українська', be: 'Беларуская', es: 'Español', 'pt-BR': 'Português (Brasil)',
};
export type Locale = string;
export type LanguagePreference = Locale | 'system';
type Catalog = Record<string, string>;
const files = import.meta.glob<Catalog>('./locales/*.json', {eager: true, import: 'default'});
const catalogs = new Map<string, Catalog>();
const locales = new Map<string, Locale>();

function localeKey(value: string): string {
 const tag = value.replaceAll('_', '-').toLowerCase();
 return tag === 'zh-hans' ? 'zh-cn' : tag;
}

for (const [path, catalog] of Object.entries(files)) {
 const stem = path.slice('./locales/'.length, -'.json'.length).replaceAll('_', '-');
 const tag = stem.toLowerCase() === 'zh-hans' ? 'zh-CN' : stem;
 const key = localeKey(tag);
 locales.set(key, tag);
 catalogs.set(tag, catalog);
}

function displayName(tag: Locale): string {
 if (Object.hasOwn(existingNames, tag)) return existingNames[tag];
 try {
  return new Intl.DisplayNames(tag, {type: 'language', fallback: 'none'}).of(tag) ?? tag;
 } catch (error) {
  if (error instanceof RangeError) return tag;
  throw error;
 }
}

const orderedLocales = [...new Set([...Object.keys(existingNames), ...[...locales.values()].sort()])];
export const languageNames = Object.fromEntries(orderedLocales
 .filter(tag => catalogs.has(tag)).map(tag => [tag, displayName(tag)]));

export function resolveLocale(value?: string): Locale {
 const tag = value?.replaceAll('_', '-').toLowerCase() ?? '';
 for (let candidate = tag; candidate; candidate = candidate.slice(0, Math.max(0, candidate.lastIndexOf('-')))) {
  const locale = locales.get(localeKey(candidate));
  if (locale) return locale;
 }
 if (/^zh(?:-(?:hans|cn|sg)(?:-|$)|$)/.test(tag)) return 'zh-CN';
 if (tag === 'pt') return 'pt-BR';
 return 'en';
}

let current: Locale = import.meta.env.MODE === 'test' ? 'ru' : resolveLocale(globalThis.navigator?.language);
const listeners = new Set<() => void>();
export const locale = () => current;
export function setLocale(next: Locale) {
 if (typeof document !== 'undefined') document.documentElement.lang = next;
 if (current === next) return;
 current = next;
 listeners.forEach(listener => listener());
}
function subscribe(listener: () => void) {
 listeners.add(listener);
 return () => {listeners.delete(listener);};
}
export const useLocale = () => useSyncExternalStore(subscribe, locale);

export function t(source: string, values?: Record<string, unknown>): string {
 const translated = (tag: Locale) => {
  const catalog = catalogs.get(tag);
  return catalog && Object.hasOwn(catalog, source) ? catalog[source] : undefined;
 };
 const message = translated(current) || (current === 'ru' ? undefined : translated('en')) || translated('ru') || source;
 return values ? message.replace(/\{([a-zA-Z_][a-zA-Z_0-9]*)\}/g, (match: string, name: string) =>
  Object.hasOwn(values, name) ? String(values[name] ?? '') : match) : message;
}
