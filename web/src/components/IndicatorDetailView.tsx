import { useMemo } from 'react';
import { useTranslation, type Translations } from '@/i18n';
import { getParameterLabel, getParameterEnumOptionsLabel, getTypeLabel, getTimeFrameLabel, getCandlePartLabel } from '@/i18n/indicatorTranslations';
import type { ChartData, IndicatorTemplateDto, RulePresetDto } from '@/types/contracts';
import { InteractiveChart } from '@/components/InteractiveChart';
import { PresetPicker } from '@/components/RuleEditor/PresetPicker';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Hint, HintContent, HintTrigger } from '@/components/ui/hint';
import { Skeleton } from '@/components/ui/skeleton';
import { formatDeviationPercent } from '@/lib/deviation';
import { Plus } from 'lucide-react';

function formatDefault(key: string, raw: string | undefined, t: Translations): { value: string; muted: boolean } {
  if (!raw) return { value: '-', muted: true };
  // "0" is the raw C# default for parameters that have no conventional default
  // (SMA periods, Gap percent, ...) — show it as "not set" instead of a misleading 0.
  if (raw === '0') return { value: '-', muted: true };
  if (key === 'TimeFrame') return { value: getTimeFrameLabel(raw, t), muted: false };
  if (key === 'CandlePart') return { value: getCandlePartLabel(raw, t), muted: false };
  if (key === 'Deviation' && Number.isFinite(parseFloat(raw))) return { value: formatDeviationPercent(parseFloat(raw)), muted: false };
  return { value: raw, muted: false };
}

interface Props {
  template: IndicatorTemplateDto | null;
  loading: boolean;
  error: string | null;
  example: { pending: boolean; error: boolean; data: ChartData | undefined };
  /** Built-in presets of this indicator from the shared preset catalog */
  presets: RulePresetDto[];
  onPickPreset: (preset: RulePresetDto) => void;
  onCreate: () => void;
}

/**
 * Indicator documentation view (about, example chart, parameter table, one-tap
 * presets, create CTA), shared by the dashboard detail page and the public
 * landing detail page. Everything is derived from the public schema/example/
 * preset endpoints, so the two surfaces always show identical data.
 */
export function IndicatorDetailView({
  template, loading, error, example, presets, onPickPreset, onCreate,
}: Props) {
  const { t } = useTranslation();

  const paramRows = useMemo(() => {
    if (!template) return [];
    const seen = new Set<string>();
    return Object.entries(template.properties).filter(([key]) => {
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    });
  }, [template]);

  if (loading) {
    return (
      <div className="space-y-6" role="status">
        <span className="sr-only">{t.common.loading}</span>
        <div className="rounded-lg border bg-muted/30 p-4">
          <Skeleton className="h-3 w-16" />
          <div className="mt-3 space-y-2">
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-2/3" />
          </div>
        </div>
        <div className="rounded-lg border bg-muted/30 p-4">
          <Skeleton className="h-3 w-20" />
          <Skeleton className="mt-2 h-3 w-40" />
          <Skeleton className="mt-3 aspect-[2/1] w-full rounded-md border border-border" />
        </div>
        <div className="flex justify-end pb-2">
          <Skeleton className="h-11 w-full sm:w-56" />
        </div>
      </div>
    );
  }

  if (error || !template) {
    return (
      <p className="text-muted-foreground">{error ?? t.errors.failedToLoad}</p>
    );
  }

  return (
    <div className="space-y-6">
      {template.description && (
        <div className="rounded-lg border bg-muted/30 p-4">
          <Label className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
            {t.indicators.detail.about}
          </Label>
          <p className="text-sm mt-2 leading-relaxed">{template.description}</p>
        </div>
      )}

      <div className="rounded-lg border bg-muted/30 p-4">
        <div className="mb-3">
          <Label className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
            {t.indicators.detail.example}
          </Label>
          <p className="text-xs text-muted-foreground mt-1">{t.indicators.detail.exampleHint}</p>
        </div>
        {example.pending ? (
          <div role="status">
            <span className="sr-only">{t.indicators.detail.exampleLoading}</span>
            <Skeleton className="aspect-[2/1] w-full rounded-md border border-border" />
          </div>
        ) : example.error || !example.data ? (
          <p className="text-sm text-muted-foreground text-center py-10">{t.indicators.detail.exampleFailed}</p>
        ) : (
          <InteractiveChart data={example.data} hint={false} />
        )}
      </div>

      <div className="rounded-lg border bg-muted/30 p-4">
        <Label className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
          {t.indicators.parametersTitle}
        </Label>
        <div className="overflow-x-auto mt-3 -mx-1 pb-1">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-[180px]">{t.indicators.detail.parameter}</TableHead>
                <TableHead className="w-[110px]">{t.indicators.detail.default}</TableHead>
                <TableHead>{t.indicators.detail.description}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {paramRows.map(([key, type]) => {
                const def = formatDefault(key, template.defaults?.[key], t);
                const hint = template.descriptions?.[key];
                const enumValues = getParameterEnumOptionsLabel(key, template.enumOptions, t);
                return (
                  <TableRow key={key}>
                    <TableCell>
                      <Hint>
                        <HintTrigger asChild>
                          <button type="button" className="text-sm font-medium cursor-help text-left">
                            {getParameterLabel(key, t)}
                          </button>
                        </HintTrigger>
                        {enumValues
                          ? <HintContent side="top">{enumValues}</HintContent>
                          : <HintContent side="top">{getTypeLabel(type, t)}</HintContent>}
                      </Hint>
                    </TableCell>
                    <TableCell className={`text-sm tabular-nums ${def.muted ? 'text-muted-foreground' : 'font-medium'}`}>{def.value}</TableCell>
                    <TableCell className="text-xs text-muted-foreground leading-relaxed align-top">{hint ?? '-'}</TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>
      </div>

      <PresetPicker
        presets={presets}
        activeKey={null}
        onPick={onPickPreset}
        t={t}
      />

      <div className="flex justify-end pb-2">
        <Button className="w-full sm:w-auto" onClick={onCreate}>
          <Plus className="h-4 w-4 mr-1.5" />
          {t.common.createSubscription}
        </Button>
      </div>
    </div>
  );
}
