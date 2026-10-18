import * as z from 'zod';
import { v7 as uuidv7 } from 'uuid';
import { TIMEFRAME_NAMES } from '@/i18n/indicatorTranslations';
import type {
  RuleConditionDto as ApiCondition,
  RuleActionDto,
  CreateRuleRequest,
  LogicalOperator,
  RuleActionType,
  RuleDto,
  RuleGroupDto,
} from '@/types/contracts';

export const conditionSchema = z.object({
  indicator: z.string().min(1, 'Indicator is required'),
  ticker: z.string().min(1, 'Instrument is required'),
  exchange: z.string().nullable().optional(),
  intValues: z.record(z.string(), z.number()).optional(),
  doubleValues: z.record(z.string(), z.number()).optional(),
  decimalValues: z.record(z.string(), z.number()).optional(),
  enumValues: z.record(z.string(), z.string()).optional(),
  periods: z.number().optional(),
  candlePart: z.string().optional(),
});

export const cooldownSchema = z.object({
  cooldownMinutes: z.number().min(1, 'At least 1 minute'),
});

export const notificationSchema = z.object({
  channel: z.string().min(1, 'Channel is required'),
  template: z.string().min(1, 'Template is required'),
});

export const subscriptionFormSchema = z.object({
  condition: conditionSchema,
  cooldown: cooldownSchema,
  notification: notificationSchema,
});

export type SubscriptionForm = z.infer<typeof subscriptionFormSchema>;
export type Condition = z.infer<typeof conditionSchema>;
export type Cooldown = z.infer<typeof cooldownSchema>;
export type Notification = z.infer<typeof notificationSchema>;

export interface InstrumentSelectItem {
  ticker: string;
  exchangeName: string | null;
  name?: string | null;
}

/** Cache/dedup key for an instrument; the exchange part may be empty. */
export const instrumentKey = (ticker: string, exchange: string | null | undefined) =>
  `${ticker}|${exchange ?? ''}`;

/** Resolved ticker/exchange parts for rich rendering (ticker chip + exchange logo). */
export function instrumentParts(
  ticker: string,
  exchange: string | null,
  resolve: (ticker: string, exchange: string | null) => InstrumentSelectItem | null,
): { ticker: string; exchange: string | null } {
  if (!ticker) return { ticker: '', exchange: null };
  const resolved = resolve(ticker, exchange);
  return { ticker, exchange: exchange ?? resolved?.exchangeName ?? null };
}

export interface IndicatorProperty {
  key: string;
  value: string;
}

// --- Rule model (WHEN group + THEN actions) ---

export type { LogicalOperator, RuleActionType };

export interface RuleConditionEntry {
  id: string;
  condition: Condition;
}

export interface RuleGroupModel {
  id: string;
  operator: LogicalOperator;
  conditions: RuleConditionEntry[];
  groups: RuleGroupModel[];
}

export interface RuleActionModel {
  id: string;
  type: RuleActionType;
}

export interface RuleFormModel {
  group: RuleGroupModel;
  thenTrue: RuleActionModel[];
  thenFalse: RuleActionModel[];
  /** Optional human-readable rule name; empty means "auto-generate". */
  name: string;
}

const ruleConditionEntrySchema = z.object({
  id: z.string(),
  condition: conditionSchema,
});

const ruleGroupSchema: z.ZodType<RuleGroupModel> = z.lazy(() => z.object({
  id: z.string(),
  operator: z.enum(['And', 'Or']),
  conditions: z.array(ruleConditionEntrySchema),
  groups: z.array(ruleGroupSchema),
}));

export const ruleFormSchema = z.object({
  group: ruleGroupSchema,
  thenTrue: z.array(z.any()),
  thenFalse: z.array(z.any()),
  name: z.string(),
});

// --- Helpers ---

/** Parse subscription/condition templateData into Condition value buckets. */
export function parseTemplateData(templateData: unknown): Partial<Condition> {
  let obj: Record<string, unknown> | null = null;
  if (typeof templateData === 'string' && templateData.trim()) {
    try { obj = JSON.parse(templateData); } catch { return {}; }
  } else if (templateData && typeof templateData === 'object' && !Array.isArray(templateData)) {
    obj = templateData as Record<string, unknown>;
  }
  if (!obj) return {};

  const result: Partial<Condition> = { intValues: {}, doubleValues: {}, enumValues: {} };
  for (const [key, value] of Object.entries(obj)) {
    if (typeof value === 'number') {
      if (Number.isInteger(value)) (result.intValues as Record<string, number>)[key] = value;
      else (result.doubleValues as Record<string, number>)[key] = value;
    } else if (typeof value === 'string') {
      (result.enumValues as Record<string, string>)[key] = value;
    }
  }
  return result;
}

/** Condition values merged into a single template object (TimeFrame stays as stored). */
export function conditionTemplate(c: Condition): Record<string, unknown> {
  const template: Record<string, unknown> = {};
  if (c.intValues) Object.assign(template, c.intValues);
  if (c.doubleValues) Object.assign(template, c.doubleValues);
  if (c.decimalValues) Object.assign(template, c.decimalValues);
  if (c.enumValues) Object.assign(template, c.enumValues);
  return template;
}

/** Count all leaf conditions in the tree. */
export function countLeafConditions(group: RuleGroupModel): number {
  let count = group.conditions.length;
  for (const sub of group.groups) count += countLeafConditions(sub);
  return count;
}

/** Flat list of leaf conditions. */
export function collectLeafConditions(group: RuleGroupModel): RuleConditionEntry[] {
  const all = [...group.conditions];
  for (const sub of group.groups) all.push(...collectLeafConditions(sub));
  return all;
}

/** Apply fn to every leaf condition, returning a new tree. */
export function mapLeafConditions(
  group: RuleGroupModel,
  fn: (entry: RuleConditionEntry) => RuleConditionEntry,
): RuleGroupModel {
  return {
    ...group,
    conditions: group.conditions.map(fn),
    groups: group.groups.map((sub) => mapLeafConditions(sub, fn)),
  };
}

/** Flat list of leaf conditions (API shape) from a rule group DTO. */
export function collectApiConditions(group: RuleGroupDto): ApiCondition[] {
  const all = [...group.conditions];
  for (const sub of group.groups) all.push(...collectApiConditions(sub));
  return all;
}

/** Count leaf conditions (API shape). */
export function countApiLeafConditions(group: RuleGroupDto): number {
  return group.conditions.length + group.groups.reduce((n, sub) => n + countApiLeafConditions(sub), 0);
}

/**
 * The rule's first leaf condition — the subscription's "primary" instrument/indicator
 * view (the subscription itself stores neither an instrument nor an indicator).
 */
export function primaryApiCondition(rule: RuleDto | null | undefined): ApiCondition | null {
  return rule ? collectApiConditions(rule.when)[0] ?? null : null;
}

/** Find a condition entry by id anywhere in the tree. */
export function findConditionEntry(group: RuleGroupModel, id: string): RuleConditionEntry | null {
  for (const entry of group.conditions) {
    if (entry.id === id) return entry;
  }
  for (const sub of group.groups) {
    const found = findConditionEntry(sub, id);
    if (found) return found;
  }
  return null;
}

/**
 * Redundancy warning (U1.5): the same indicator on the same instrument with identical
 * parameters cannot make a difference — flag it instead of blocking the save.
 */
export function findRedundantConditions(group: RuleGroupModel): number {
  const seen = new Set<string>();
  let redundant = 0;
  for (const entry of collectLeafConditions(group)) {
    const key = JSON.stringify({
      indicator: entry.condition.indicator,
      ticker: entry.condition.ticker,
      exchange: entry.condition.exchange ?? null,
      template: conditionTemplate(entry.condition),
    });
    if (seen.has(key)) redundant++;
    seen.add(key);
  }
  return redundant;
}

// --- API mapping ---

/**
 * Condition → API RuleConditionDto payload (TimeFrame stored as int). Non-mutating.
 *
 * The entry id travels back to the server unchanged: it is the condition's stable identity,
 * and the trigger history points at it (the trigger chart resolves the fired condition from
 * the rule tree), as does the per-condition indicator state (trailing-stop anchors) and the
 * same-candle alert dedup. Sending null makes the server mint a fresh id for every leaf on
 * every save, which orphans the trigger history of an edited alert.
 */
export function conditionToApi(c: Condition, id?: string | null): ApiCondition {
  const template: Record<string, unknown> = {};
  if (c.intValues) Object.assign(template, c.intValues);
  if (c.doubleValues) Object.assign(template, c.doubleValues);
  if (c.decimalValues) Object.assign(template, c.decimalValues);
  let timeFrame = typeof c.intValues?.TimeFrame === 'number' ? c.intValues.TimeFrame : 0;
  if (c.enumValues) {
    for (const [key, value] of Object.entries(c.enumValues)) {
      if (key === 'TimeFrame') {
        const idx = timeFrameIndexByName(value);
        if (idx >= 0) timeFrame = idx;
        template['TimeFrame'] = timeFrame;
      } else {
        template[key] = value;
      }
    }
  }
  return {
    id: id ?? null,
    indicator: c.indicator,
    templateData: template,
    operator: 'GreaterThan',
    referenceType: 'Value',
    referenceIndicator: null,
    referenceTemplateData: null,
    referenceCandlePart: null,
    referenceValue: null,
    deviation: null,
    ticker: c.ticker,
    exchangeName: c.exchange ?? null,
    timeFrame,
  };
}

/** TimeFrame name (UI label key) → stored int, -1 when unknown. */
export function timeFrameIndexByName(name: string): number {
  return (TIMEFRAME_NAMES as readonly string[]).indexOf(name);
}

function groupToApi(group: RuleGroupModel): RuleGroupDto {
  return {
    id: group.id,
    operator: group.operator,
    conditions: group.conditions.map(e => conditionToApi(e.condition, e.id)),
    groups: group.groups.map(sub => groupToApi(sub)),
  };
}

/** Max rule name length (mirrors the API validation). */
export const MAX_RULE_NAME_LENGTH = 50;

/** Default rule name from the tree, e.g. "SMA & StochRSI (SBER)". */
export function buildRuleName(group: RuleGroupModel): string {
  const leaves = collectLeafConditions(group);
  if (leaves.length === 0) return 'Rule';
  const indicators = leaves.map(e => e.condition.indicator || '?').slice(0, 3).join(' & ');
  const more = leaves.length > 3 ? ` +${leaves.length - 3}` : '';
  const ticker = leaves[0].condition.ticker;
  return `${indicators}${more}${ticker ? ` (${ticker})` : ''}`;
}

/** Form model → API create request. */
export function toCreateRuleRequest(model: RuleFormModel): CreateRuleRequest {
  // SendNotification uses the templates configured on the subscription outputs,
  // so the action itself never carries a template.
  const actions = (list: RuleActionModel[]): RuleActionDto[] => list.map(a => ({
    id: null,
    type: a.type,
    notificationTemplateId: null,
    parameters: null,
  }));
  const rawName = model.name.trim() ? model.name.trim() : buildRuleName(model.group);
  return {
    name: rawName.slice(0, MAX_RULE_NAME_LENGTH),
    when: groupToApi(model.group),
    thenTrue: actions(model.thenTrue),
    thenFalse: actions(model.thenFalse),
  };
}

function apiConditionToModel(c: ApiCondition): Condition {
  return {
    indicator: c.indicator,
    ticker: c.ticker,
    exchange: c.exchangeName ?? undefined,
    ...parseTemplateData(c.templateData),
  };
}

function apiGroupToModel(dto: RuleGroupDto): RuleGroupModel {
  return {
    id: dto.id ?? uuidv7(),
    operator: dto.operator,
    conditions: dto.conditions.map(c => ({ id: c.id ?? uuidv7(), condition: apiConditionToModel(c) })),
    groups: dto.groups.map(apiGroupToModel),
  };
}

/** API rule → form model. */
export function toRuleFormModel(rule: RuleDto): RuleFormModel {
  return {
    group: apiGroupToModel(rule.when),
    thenTrue: (rule.thenTrue ?? []).map(a => ({
      id: a.id ?? uuidv7(),
      type: a.type,
    })),
    thenFalse: (rule.thenFalse ?? []).map(a => ({
      id: a.id ?? uuidv7(),
      type: a.type,
    })),
    name: rule.name ?? '',
  };
}

/**
 * Preset WHEN-fragment → form group: regenerates local ids and fills the
 * ticker/exchange placeholders (the preset catalog is instrument-agnostic).
 */
export function presetGroupToModel(when: RuleGroupDto, ticker: string, exchange?: string | null): RuleGroupModel {
  const group = apiGroupToModel(when);
  return mapLeafConditions(group, (entry) => ({
    ...entry,
    condition: { ...entry.condition, ticker, exchange: exchange ?? undefined },
  }));
}

export const DEFAULT_RULE_CONDITION: Condition = {
  indicator: '',
  ticker: '',
  intValues: {},
  doubleValues: {},
  decimalValues: {},
  enumValues: {},
};

export function createDefaultGroup(ticker = '', exchange?: string | null): RuleGroupModel {
  return {
    id: uuidv7(),
    operator: 'And',
    conditions: [{
      id: uuidv7(),
      condition: { ...DEFAULT_RULE_CONDITION, ticker, exchange: exchange ?? undefined, intValues: {}, doubleValues: {}, decimalValues: {}, enumValues: {} },
    }],
    groups: [],
  };
}
