import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Info } from 'lucide-react';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { api } from '@/services/api';
import { qk } from '@/lib/query';
import type { Translations } from '@/i18n';
import { getIndicatorFullNameWithAbbr, getIndicatorCategoryLabel, getParameterLabel, getParameterEnumOptionsLabel, getTypeLabel, getTimeFrameLabel, getCandlePartLabel } from '@/i18n/indicatorTranslations';
import { formatDeviationPercent } from '@/lib/deviation';

interface Props {
  /** Indicator name; renders nothing when empty (no indicator selected yet) */
  indicator: string;
  locale: string;
  t: Translations;
}

function formatDefault(key: string, raw: string | undefined, t: Translations): string | null {
  if (!raw || raw === '0') return null;
  if (key === 'TimeFrame') return getTimeFrameLabel(raw, t);
  if (key === 'CandlePart') return getCandlePartLabel(raw, t);
  if (key === 'Deviation' && Number.isFinite(parseFloat(raw))) return formatDeviationPercent(parseFloat(raw));
  return raw;
}

/**
 * Quick "what is this indicator" popover used inside the rule editor, where a full
 * navigation would drop the unsaved condition being edited. Fetches the template
 * (description + parameters + defaults) lazily on first open.
 */
export function IndicatorInfoDialog({ indicator, locale, t }: Props) {
  const [open, setOpen] = useState(false);

  // Lazily fetched on first open; the shared cache answers a reopen (and the
  // indicator detail page visit) without a request
  const templateQuery = useQuery({
    queryKey: qk.indicatorTemplate(indicator, locale),
    queryFn: () => api.getIndicatorTemplate(indicator, locale),
    enabled: open && !!indicator,
  });
  const template = templateQuery.data ?? null;
  const loading = open && templateQuery.isPending;

  if (!indicator) return null;

  return (
    <>
      <button
        type="button"
        aria-label={t.indicators.whatIsIt}
        title={t.indicators.whatIsIt}
        onClick={() => setOpen(true)}
        className="inline-flex h-4 w-4 shrink-0 items-center justify-center rounded-full text-muted-foreground/70 transition-colors hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <Info className="h-3.5 w-3.5" />
      </button>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader className="pb-2">
            <DialogTitle className="flex items-center gap-2 flex-wrap leading-snug">
              {getIndicatorFullNameWithAbbr(indicator, t)}
              <Badge variant="secondary" className="text-[11px]">{getIndicatorCategoryLabel(indicator, t)}</Badge>
            </DialogTitle>
          </DialogHeader>
          {loading ? (
            <div className="space-y-4 py-2" role="status">
              <span className="sr-only">{t.indicators.loading}</span>
              <div className="space-y-2">
                <Skeleton className="h-4 w-full" />
                <Skeleton className="h-4 w-5/6" />
              </div>
              <div className="space-y-2.5">
                <Skeleton className="h-3 w-24" />
                {Array.from({ length: 3 }).map((_, i) => (
                  <div key={i} className="space-y-1">
                    <Skeleton className="h-4 w-40 max-w-full" />
                    <Skeleton className="h-3 w-3/4" />
                  </div>
                ))}
              </div>
            </div>
          ) : (
            <div className="space-y-4 max-h-[60dvh] overflow-y-auto">
              {template?.description && (
                <p className="text-sm leading-relaxed">{template.description}</p>
              )}
              {template && Object.keys(template.properties).length > 0 && (
                <div className="space-y-2">
                  <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">{t.indicators.parametersTitle}</p>
                  {Object.entries(template.properties).map(([key, type]) => {
                    const def = formatDefault(key, template.defaults?.[key], t);
                    const hint = template.descriptions?.[key];
                    const enumValues = getParameterEnumOptionsLabel(key, template.enumOptions, t);
                    return (
                      <div key={key} className="text-sm space-y-0.5">
                        <p className="font-medium">
                          {getParameterLabel(key, t)}
                          <span className="ml-1.5 text-xs font-normal text-muted-foreground">{enumValues ?? getTypeLabel(type, t)}</span>
                          {def && <span className="ml-1.5 text-xs font-normal text-muted-foreground">= {def}</span>}
                        </p>
                        {hint && <p className="text-xs text-muted-foreground leading-relaxed">{hint}</p>}
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}
