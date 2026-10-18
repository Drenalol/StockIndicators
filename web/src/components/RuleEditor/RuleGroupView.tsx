import { Badge } from '@/components/ui/badge';
import { GitMerge } from 'lucide-react';
import { getIndicatorShortName, getTimeFrameLabel, getCandlePartLabel, getEnumValueLabel, getParameterLabel } from '@/i18n/indicatorTranslations';
import type { Translations } from '@/i18n';
import type { RuleConditionDto, RuleGroupDto } from '@/types/contracts';
import { ParameterHint } from '@/components/ParameterHint';
import { formatDeviationPercent } from '@/lib/deviation';

interface ConditionBlockProps {
  rc: RuleConditionDto;
  parameterHintsByIndicator: Record<string, Record<string, string>>;
  t: Translations;
}

function ConditionBlock({ rc, parameterHintsByIndicator, t }: ConditionBlockProps) {
  const rcEntries: [string, unknown][] = rc.templateData && typeof rc.templateData === 'object'
    ? Object.entries(rc.templateData)
    : [];
  // Each condition may use a different indicator, so the hints must come from
  // that condition's own indicator, not from a shared map
  const parameterHints = parameterHintsByIndicator[rc.indicator] ?? {};
  return (
    <div className="rounded-md border bg-background p-3">
      <p className="text-xs font-semibold text-muted-foreground mb-1">{getIndicatorShortName(rc.indicator)}</p>
      <div className="space-y-1">
        {rcEntries.map(([k, v]) => {
          const display = k === 'TimeFrame' && (typeof v === 'string' || typeof v === 'number')
            ? getTimeFrameLabel(v, t)
            : k === 'CandlePart' && typeof v === 'string'
              ? getCandlePartLabel(v, t)
              : k === 'Deviation' && typeof v === 'number'
                ? formatDeviationPercent(v)
                : typeof v === 'string'
                  ? getEnumValueLabel(k, v, t)
                  : String(v);
          return (
            <div key={k} className="flex items-center justify-between text-sm">
              <span className="flex items-center gap-1 text-muted-foreground">
                {getParameterLabel(k, t)}
                <ParameterHint hint={parameterHints[k]} t={t} />
              </span>
              <span className="font-medium">{display}</span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

interface Props {
  group: RuleGroupDto;
  /** Hints per indicator: indicator name -> parameter key -> localized hint */
  parameterHintsByIndicator: Record<string, Record<string, string>>;
  t: Translations;
  depth?: number;
}

/**
 * Read-only rendering of a rule group tree, mirroring the editor layout:
 * an AND/OR operator badge per group and nested groups wrapped in a labelled
 * accent-bordered box, so the grouping structure is visible at a glance.
 */
export function RuleGroupView({ group, parameterHintsByIndicator, t, depth = 0 }: Props) {
  const isNested = depth > 0;
  return (
    <div className={isNested ? 'rounded-lg border border-l-4 border-l-primary/40 bg-muted/30 p-3' : 'rounded-lg border p-3 sm:p-4'}>
      <div className="mb-3 flex items-center gap-2">
        <Badge variant={group.operator === 'And' ? 'secondary' : 'outline'} className="uppercase font-semibold">
          {group.operator === 'And' ? t.rules.and : t.rules.or}
        </Badge>
      </div>

      <div className="space-y-2">
        {group.conditions.map((rc, i) => (
          <ConditionBlock key={rc.id ?? i} rc={rc} parameterHintsByIndicator={parameterHintsByIndicator} t={t} />
        ))}
      </div>

      {group.groups.map((sub, i) => (
        <div key={sub.id ?? i} className="mt-2">
          <div className="mb-1 flex items-center gap-1.5 px-1">
            <GitMerge className="h-3.5 w-3.5 text-muted-foreground" />
            <span className="text-[11px] font-medium text-muted-foreground uppercase tracking-wide">{t.rules.groupLabel}</span>
          </div>
          <RuleGroupView group={sub} parameterHintsByIndicator={parameterHintsByIndicator} t={t} depth={depth + 1} />
        </div>
      ))}
    </div>
  );
}
