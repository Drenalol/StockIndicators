import type { Locale } from '@/i18n';

const TS_LOCALES: Record<Locale, string> = {
  ru: 'ru-RU',
  es: 'es-ES',
  en: 'en-US',
  it: 'it-IT',
  de: 'de-DE',
  fr: 'fr-FR',
  'pt-BR': 'pt-BR',
  uk: 'uk-UA',
  be: 'be-BY',
};

/**
 * Compact date for cards and tables: "12 сент., 08:30". The year is added only when it
 * differs from the current one, so a grant until next January reads "08 янв. 2027 г., 14:37".
 */
export function formatDate(dateStr: string, locale: Locale): string {
  const date = new Date(dateStr);
  const sameYear = date.getFullYear() === new Date().getFullYear();

  return date.toLocaleDateString(TS_LOCALES[locale], {
    year: sameYear ? undefined : 'numeric',
    month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit'
  });
}

/** Full timestamp for trigger rows and the trigger chart page */
export function formatTriggerDate(dateStr: string, locale: Locale): string {
  return new Date(dateStr).toLocaleString(TS_LOCALES[locale], {
    year: 'numeric', month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit'
  });
}

/**
 * "5 minutes ago"-style stamp for the session list (a Telegram-like "last seen").
 * Past instants only; anything older than a month falls back to the compact date.
 */
export function formatRelative(dateStr: string, locale: Locale, justNow: string): string {
  const elapsedSec = Math.max(0, Math.round((Date.now() - new Date(dateStr).getTime()) / 1000));
  if (elapsedSec < 60) return justNow;

  const rtf = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' });
  if (elapsedSec < 3600) return rtf.format(-Math.round(elapsedSec / 60), 'minute');
  if (elapsedSec < 86400) return rtf.format(-Math.round(elapsedSec / 3600), 'hour');
  if (elapsedSec < 86400 * 30) return rtf.format(-Math.round(elapsedSec / 86400), 'day');

  return formatDate(dateStr, locale);
}
