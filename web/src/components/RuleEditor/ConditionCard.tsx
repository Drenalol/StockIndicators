import { type MutableRefObject, type ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ConditionRow } from './ConditionRow';
import type { Condition, InstrumentSelectItem } from './schema';
import type { Translations } from '@/i18n';
import { api } from '@/services/api';
import { qk } from '@/lib/query';

interface Props {
  model: Condition;
  setModel: (m: Condition) => void;
  indicators: string[];
  locale: string;
  t: Translations;
  disableTicker?: boolean;
  searchInstruments: (query: string) => Promise<InstrumentSelectItem[]>;
  loadDefaultInstruments: () => Promise<InstrumentSelectItem[]>;
  resolveInstrument: (ticker: string, exchange: string | null) => InstrumentSelectItem | null;
  onInstrumentSelect: (item: InstrumentSelectItem) => void;
  /** Points at the baseline condition so auto-applied defaults don't show as "changed" */
  originalRef?: MutableRefObject<{ condition: Condition }>;
  /** Move/delete buttons rendered inline in the instrument label row on sm+ */
  headerActions?: ReactNode;
}

/**
 * A single condition with its own indicator-template loading.
 * Each condition in a rule group may use a different indicator,
 * so the template is fetched per condition instead of being shared.
 */
export function ConditionCard({ model, setModel, indicators, locale, t, disableTicker, searchInstruments, loadDefaultInstruments, resolveInstrument, onInstrumentSelect, originalRef, headerActions }: Props) {
  // Per-condition template through the shared cache: conditions (and the info
  // dialog) using the same indicator share one fetch
  const templateQuery = useQuery({
    queryKey: qk.indicatorTemplate(model.indicator, locale),
    queryFn: () => api.getIndicatorTemplate(model.indicator, locale),
    enabled: !!model.indicator,
  });
  const template = templateQuery.data ?? null;

  return (
    <ConditionRow
      model={model}
      setModel={setModel}
      indicators={indicators}
      template={template?.properties ?? null}
      descriptions={template?.descriptions}
      enumOptions={template?.enumOptions}
      defaults={template?.defaults}
      onIndicatorChanged={() => undefined}
      t={t}
      locale={locale}
      disableTicker={disableTicker}
      searchInstruments={searchInstruments}
      loadDefaultInstruments={loadDefaultInstruments}
      resolveInstrument={resolveInstrument}
      onInstrumentSelect={onInstrumentSelect}
      originalRef={originalRef}
      headerActions={headerActions}
    />
  );
}
