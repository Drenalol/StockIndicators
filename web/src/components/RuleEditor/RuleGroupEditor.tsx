import type { MutableRefObject } from 'react';
import { v7 as uuidv7 } from 'uuid';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { Plus, Trash2, ChevronUp, ChevronDown, GitMerge } from 'lucide-react';
import { ConditionCard } from './ConditionCard';
import { countLeafConditions, createDefaultGroup, findConditionEntry, type Condition, type InstrumentSelectItem, type RuleGroupModel } from './schema';
import type { Translations } from '@/i18n';

// Fallback until /api/Config resolves; the real ceiling arrives as maxGroupDepth
const DEFAULT_MAX_GROUP_DEPTH = 2;

interface Props {
  group: RuleGroupModel;
  onChange: (group: RuleGroupModel) => void;
  depth?: number;
  indicators: string[];
  locale: string;
  t: Translations;
  /** Preselected instrument, applied to every new condition */
  defaultTicker?: string;
  defaultExchange?: string | null;
  /** Lock the instrument (preselected / editing) */
  disableTicker?: boolean;
  /** Baseline tree used to absorb auto-applied defaults (dirty tracking) */
  originalGroup?: RuleGroupModel | null;
  searchInstruments: (query: string) => Promise<InstrumentSelectItem[]>;
  loadDefaultInstruments: () => Promise<InstrumentSelectItem[]>;
  resolveInstrument: (ticker: string, exchange: string | null) => InstrumentSelectItem | null;
  /** Instrument picked in a row */
  onInstrumentSelect: (entryId: string, item: InstrumentSelectItem) => void;
  /** Max leaf conditions across the whole rule tree (from /api/Config) */
  maxConditions?: number;
  /** Ceiling for group nesting depth (from /api/Config) */
  maxGroupDepth?: number;
  /** Leaf conditions across the whole rule tree (root computes, children inherit) */
  totalLeaves?: number;
}

export function RuleGroupEditor({
  group,
  onChange,
  depth = 0,
  indicators,
  locale,
  t,
  defaultTicker,
  defaultExchange,
  disableTicker,
  originalGroup,
  searchInstruments,
  loadDefaultInstruments,
  resolveInstrument,
  onInstrumentSelect,
  maxConditions,
  maxGroupDepth,
  totalLeaves,
}: Props) {
  const isNested = depth > 0;

  const leaves = totalLeaves ?? countLeafConditions(group);
  const atConditionsLimit = maxConditions !== undefined && leaves >= maxConditions;
  const atGroupDepthLimit = depth >= (maxGroupDepth ?? DEFAULT_MAX_GROUP_DEPTH);

  const updateCondition = (index: number, model: Condition) => {
    const conditions = group.conditions.map((entry, i) => (i === index ? { ...entry, condition: model } : entry));
    onChange({ ...group, conditions });
  };

  const addCondition = () => {
    onChange({
      ...group,
      conditions: [...group.conditions, {
        id: uuidv7(),
        condition: {
          indicator: '',
          ticker: defaultTicker ?? '',
          exchange: defaultExchange ?? undefined,
          intValues: {},
          doubleValues: {},
          decimalValues: {},
          enumValues: {},
        },
      }],
    });
  };

  const removeCondition = (index: number) => {
    onChange({ ...group, conditions: group.conditions.filter((_, i) => i !== index) });
  };

  const moveCondition = (index: number, dir: -1 | 1) => {
    const target = index + dir;
    if (target < 0 || target >= group.conditions.length) return;
    const conditions = [...group.conditions];
    const [entry] = conditions.splice(index, 1);
    conditions.splice(target, 0, entry);
    onChange({ ...group, conditions });
  };

  const addGroup = () => {
    onChange({ ...group, groups: [...group.groups, createDefaultGroup(defaultTicker ?? '', defaultExchange)] });
  };

  const removeGroup = (index: number) => {
    onChange({ ...group, groups: group.groups.filter((_, i) => i !== index) });
  };

  const moveGroup = (index: number, dir: -1 | 1) => {
    const target = index + dir;
    if (target < 0 || target >= group.groups.length) return;
    const groups = [...group.groups];
    const [entry] = groups.splice(index, 1);
    groups.splice(target, 0, entry);
    onChange({ ...group, groups });
  };

  return (
    <div className={isNested ? 'rounded-lg border border-l-4 border-l-primary/40 bg-muted/30 p-3' : 'rounded-lg border p-3 sm:p-4'}>
      {/* Group header: AND/OR badge + toggle */}
      <div className="mb-3 flex items-center gap-2">
        <Badge variant={group.operator === 'And' ? 'secondary' : 'outline'} className="uppercase font-semibold">
          {group.operator === 'And' ? t.rules.and : t.rules.or}
        </Badge>
        <Select value={group.operator} onValueChange={(v) => onChange({ ...group, operator: v as 'And' | 'Or' })}>
          <SelectTrigger className="h-8 w-[110px] text-xs" aria-label={t.rules.groupOperator}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="And">{t.rules.and}</SelectItem>
            <SelectItem value="Or">{t.rules.or}</SelectItem>
          </SelectContent>
        </Select>
        {/* Root-only leaf counter {used}/{limit} */}
        {depth === 0 && maxConditions !== undefined && (
          <span className="ml-auto inline-flex items-center gap-1 text-[11px] text-muted-foreground tabular-nums">
            {leaves}/{maxConditions}
          </span>
        )}
      </div>

      {/* Conditions */}
      {group.conditions.map((entry, index) => {
        const origEntry = originalGroup ? findConditionEntry(originalGroup, entry.id) : null;
        const originalRef = origEntry ? ({ current: origEntry } as MutableRefObject<{ condition: Condition }>) : undefined;
        // Rendered twice: as an in-flow row above the fields on mobile, and inline in the
        // instrument label row on sm+ (hidden there on mobile). Never absolute — an overlay
        // over the instrument SelectTrigger cuts through its focus ring.
        const conditionActions = (
          <>
            <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => moveCondition(index, -1)} disabled={index === 0} aria-label={t.rules.moveUp} title={t.rules.moveUp}>
              <ChevronUp className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => moveCondition(index, 1)} disabled={index === group.conditions.length - 1} aria-label={t.rules.moveDown} title={t.rules.moveDown}>
              <ChevronDown className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => removeCondition(index)} disabled={group.conditions.length <= 1 && group.groups.length === 0} aria-label={t.rules.deleteCondition} title={t.rules.deleteCondition}>
              <Trash2 className="h-4 w-4 text-destructive" />
            </Button>
          </>
        );
        return (
          <div key={entry.id} className="relative rounded-lg border bg-background p-3 mb-2">
            <div className="mb-1 flex items-center justify-end gap-0.5 rounded-md bg-background/90 sm:hidden">
              {conditionActions}
            </div>
            <ConditionCard
              headerActions={conditionActions}
              model={entry.condition}
              setModel={(m) => updateCondition(index, m)}
              indicators={indicators}
              locale={locale}
              t={t}
              disableTicker={disableTicker}
              originalRef={originalRef}
              searchInstruments={searchInstruments}
              loadDefaultInstruments={loadDefaultInstruments}
              resolveInstrument={resolveInstrument}
              onInstrumentSelect={(item) => onInstrumentSelect(entry.id, item)}
            />
          </div>
        );
      })}

      {/* Nested groups */}
      {group.groups.map((sub, index) => (
        <div key={sub.id} className="mb-2">
          <div className="mb-1 flex items-center gap-1.5 px-1">
            <GitMerge className="h-3.5 w-3.5 text-muted-foreground" />
            <span className="text-[11px] font-medium text-muted-foreground uppercase tracking-wide">{t.rules.groupLabel}</span>
          </div>
          <RuleGroupEditor
            group={sub}
            onChange={(next) => onChange({ ...group, groups: group.groups.map((g, i) => (i === index ? next : g)) })}
            depth={depth + 1}
            indicators={indicators}
            locale={locale}
            t={t}
            defaultTicker={defaultTicker}
            defaultExchange={defaultExchange}
            disableTicker={disableTicker}
            originalGroup={originalGroup}
            searchInstruments={searchInstruments}
            loadDefaultInstruments={loadDefaultInstruments}
            resolveInstrument={resolveInstrument}
            onInstrumentSelect={onInstrumentSelect}
            maxConditions={maxConditions}
            maxGroupDepth={maxGroupDepth}
            totalLeaves={leaves}
          />
          <div className="mt-1 flex items-center gap-1 pr-1">
            <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => moveGroup(index, -1)} disabled={index === 0} aria-label={t.rules.moveUp} title={t.rules.moveUp}>
              <ChevronUp className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => moveGroup(index, 1)} disabled={index === group.groups.length - 1} aria-label={t.rules.moveDown} title={t.rules.moveDown}>
              <ChevronDown className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => removeGroup(index)} aria-label={t.rules.deleteGroup} title={t.rules.deleteGroup}>
              <Trash2 className="h-4 w-4 text-destructive" />
            </Button>
          </div>
        </div>
      ))}

      {/* Add buttons — same width, stacked on mobile, side by side on sm+ */}
      <div className="mt-2 grid grid-cols-1 gap-2 sm:grid-cols-2">
        <Button variant="outline" size="sm" onClick={addCondition} disabled={atConditionsLimit}>
          <Plus className="h-3.5 w-3.5 mr-1" />
          {t.rules.addCondition}
        </Button>
        {!atGroupDepthLimit && (
          <Button variant="outline" size="sm" onClick={addGroup} disabled={atConditionsLimit}>
            <GitMerge className="h-3.5 w-3.5 mr-1" />
            {t.rules.addGroup}
          </Button>
        )}
        {atConditionsLimit && (
          <span className="text-xs text-muted-foreground">{t.rules.conditionsLimitReached}</span>
        )}
      </div>
    </div>
  );
}
