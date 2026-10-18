import en from './en.json';

export type Locale = 'en' | 'ru' | 'es' | 'it' | 'de' | 'fr' | 'pt-BR' | 'uk' | 'be';

export const SUPPORTED_LOCALES: Locale[] = ['en', 'ru', 'es', 'it', 'de', 'fr', 'pt-BR', 'uk', 'be'];

// Language pickers show the native name of every locale (menu + settings)
export const LOCALE_NAMES: Record<Locale, string> = {
  en: 'English',
  ru: 'Русский',
  es: 'Español',
  it: 'Italiano',
  de: 'Deutsch',
  fr: 'Français',
  'pt-BR': 'Português (Brasil)',
  uk: 'Українська',
  be: 'Беларуская',
};

export type Translations = typeof en;

export interface LocaleContextValue {
  locale: Locale;
  setLocale: (locale: Locale) => void;
  applyServerLanguage: (locale: Locale) => void;
  t: Translations;
}
