// Grammatical plural agreement for the typed JSON catalogs. Counted strings
// carry one/few/many variants (CLDR categories): ru/uk/be really use all three
// ("1 алерт", "2 алерта", "5 алертов"), while one-number languages just repeat
// the plural in few/many; Intl's remaining "other" maps onto many.

export interface PluralForms {
  one: string;
  few: string;
  many: string;
}

const rulesCache = new Map<string, Intl.PluralRules>();

export function pluralForm(locale: string, count: number, forms: PluralForms): string {
  let rules = rulesCache.get(locale);
  if (!rules) rulesCache.set(locale, (rules = new Intl.PluralRules(locale)));
  const category = rules.select(count);
  if (category === 'one') return forms.one;
  if (category === 'few') return forms.few;
  return forms.many;
}

/** pluralForm + {{count}} interpolation in one step, the usual badge/callout path */
export function formatPlural(locale: string, count: number, forms: PluralForms): string {
  return pluralForm(locale, count, forms).replace('{{count}}', String(count));
}
