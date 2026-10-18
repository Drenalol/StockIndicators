import { createContext, useCallback, useMemo, useState, type ReactNode } from 'react';
import type { Locale, LocaleContextValue, Translations } from './types';
import { SUPPORTED_LOCALES } from './types';
import en from './en.json';
import ru from './ru.json';
import es from './es.json';
import it from './it.json';
import de from './de.json';
import fr from './fr.json';
import ptBr from './pt-BR.json';
import uk from './uk.json';
import be from './be.json';

const translationsMap: Record<Locale, Translations> = { en, ru, es, it, de, fr, 'pt-BR': ptBr, uk, be };

/**
 * Strings of an arbitrary locale without going through the context. Needed when
 * UI is produced in the same event handler that calls `setLocale` (e.g. the
 * "settings saved" toast after a language switch): the context's `t` is captured
 * by the current render's closure and still holds the OLD locale until React
 * re-renders, so such one-shot strings must be read for the target locale.
 */
export function getTranslations(locale: Locale): Translations {
  return translationsMap[locale];
}

const STORAGE_KEY = 'locale';

// The profile language the UI was last synchronized with. A landing/login language
// switch writes STORAGE_KEY only, so a reload does not let the (unchanged) profile
// language from /auth/me overwrite the local choice; a real profile change (settings
// page or another device) differs from this marker and is applied as before.
const SERVER_LANG_KEY = 'locale.serverLang';

// Browser language prefix -> app locale. "pt" resolves to the only Portuguese
// variant we ship; uk/be arrive as plain prefixes from the OS/browser.
function detectBrowserLocale(): Locale {
  const browserLang = (navigator.language || 'en').toLowerCase();
  if (browserLang.startsWith('ru')) return 'ru';
  if (browserLang.startsWith('es')) return 'es';
  if (browserLang.startsWith('it')) return 'it';
  if (browserLang.startsWith('de')) return 'de';
  if (browserLang.startsWith('fr')) return 'fr';
  if (browserLang.startsWith('pt')) return 'pt-BR';
  if (browserLang.startsWith('uk')) return 'uk';
  if (browserLang.startsWith('be')) return 'be';
  return 'en';
}

function getInitialLocale(): Locale {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored && SUPPORTED_LOCALES.includes(stored as Locale)) return stored as Locale;
  } catch { /* ignore */ }
  return detectBrowserLocale();
}

export const LocaleContext = createContext<LocaleContextValue>({
  locale: 'en',
  setLocale: () => {},
  applyServerLanguage: () => {},
  t: en,
});

interface Props {
  children: ReactNode;
}

export default function LocaleProvider({ children }: Props) {
  const [locale, setLocaleState] = useState<Locale>(getInitialLocale);

  const setLocale = useCallback((next: Locale) => {
    setLocaleState(next);
    try {
      localStorage.setItem(STORAGE_KEY, next);
    } catch { /* ignore */ }
  }, []);

  const applyServerLanguage = useCallback((serverLang: Locale) => {
    try {
      if (localStorage.getItem(SERVER_LANG_KEY) === serverLang) return;
      localStorage.setItem(SERVER_LANG_KEY, serverLang);
      localStorage.setItem(STORAGE_KEY, serverLang);
    } catch { /* ignore */ }
    setLocaleState(serverLang);
  }, []);

  const value = useMemo<LocaleContextValue>(() => ({
    locale,
    setLocale,
    applyServerLanguage,
    t: translationsMap[locale],
  }), [locale, setLocale, applyServerLanguage]);

  return (
    <LocaleContext.Provider value={value}>
      {children}
    </LocaleContext.Provider>
  );
}
