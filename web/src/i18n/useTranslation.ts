import { useContext } from 'react';
import { LocaleContext } from './LocaleProvider';

export function useTranslation() {
  const { t, locale, setLocale, applyServerLanguage } = useContext(LocaleContext);
  return { t, locale, setLocale, applyServerLanguage };
}
