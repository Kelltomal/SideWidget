import {useSyncExternalStore} from 'react';

export const languageNames = {
 ru: 'Русский', en: 'English', 'zh-CN': '简体中文', ja: '日本語', de: 'Deutsch',
 uk: 'Українська', be: 'Беларуская', es: 'Español', 'pt-BR': 'Português (Brasil)',
} as const;
export type Locale = keyof typeof languageNames;
export type LanguagePreference = Locale | 'system';
type Translation = readonly [string, string, string, string, string, string, string, string];
const translatedLanguages = ['en', 'zh-CN', 'ja', 'de', 'uk', 'be', 'es', 'pt-BR'] as const;
const entries = import.meta.glob<Record<string, Translation>>('./locales/*.ts', {eager: true, import: 'default'});
const catalog = Object.assign(Object.create(null) as Record<string, Translation>, ...Object.values(entries));

export function resolveLocale(value?: string): Locale {
 const tag = value?.toLowerCase() ?? '';
 const language = tag.split('-')[0];
 if (language === 'zh') return /^zh(?:-(?:hans|cn|sg)(?:-|$)|$)/.test(tag) ? 'zh-CN' : 'en';
 if (language === 'pt') return /^(?:pt|pt-br(?:-.*)?)$/.test(tag) ? 'pt-BR' : 'en';
 return Object.hasOwn(languageNames, language) ? language as Locale : 'en';
}

let current = import.meta.env.MODE === 'test' ? 'ru' as Locale : resolveLocale(globalThis.navigator?.language);
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
 const index = current === 'ru' ? -1 : translatedLanguages.indexOf(current);
 const message = catalog[source]?.[index] || source;
 return values ? message.replace(/\{([a-zA-Z_][a-zA-Z_0-9]*)\}/g, (match: string, name: string) =>
  Object.hasOwn(values, name) ? String(values[name] ?? '') : match) : message;
}
