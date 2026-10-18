// --- Subscriptions ---

/** Active runs; Inactive is the user's switch */
export type SubscriptionStatus = 'Active' | 'Inactive';

export interface SubscriptionOutputDto {
  id: string;
  channel: string;
  notificationTemplateId: string | null;
  pushSubscriptionId: string | null;
  /** Destination Telegram chat id (channel = telegram) */
  chatId?: number | null;
  /** Destination URL (Webhook/Discord) or E.164 phone (WhatsApp) */
  target?: string | null;
  isEnabled: boolean;
}

export interface SubscriptionOutputRequest {
  channel: string;
  notificationTemplateId?: string | null;
  pushSubscriptionId?: string | null;
  /** Destination Telegram chat id (channel = telegram) */
  chatId?: number | null;
  /** Destination URL (Webhook/Discord) or E.164 phone (WhatsApp) */
  target?: string | null;
  isEnabled?: boolean;
}

/**
 * A subscription is a pure container of a rule (cooldown + outputs); it stores no
 * indicator/instrument fields — clients read the "primary" instrument from the first
 * WHEN condition of `rule`.
 */
export interface SubscriptionDto {
  id: string;
  status: SubscriptionStatus;
  cooldownSeconds: number | null;
  outputs: SubscriptionOutputDto[];
  created: string;
  updated: string | null;
  /** When the last alert notification was sent; newer than lastReadAt ⇒ the alert is "new". */
  lastTriggeredAt: string | null;
  /** When the user last opened this subscription's alert (cleared the "new" flag). */
  lastReadAt: string | null;
  userId: string | null;
  /** Rule (root group) id this subscription is a container of. */
  ruleGroupId: string;
  /** False only when the user opted into alerts on extended-session candles. */
  ignoreExtendedSessions?: boolean;
  /** Full rule (WHEN-THEN) — the source of instruments, indicators and parameters. */
  rule: RuleDto | null;
}

export interface CreateSubscriptionRequest {
  cooldownSeconds: number;
  /** The rule the subscription is a container of (its instrument/indicator/parameters). */
  rule: CreateRuleRequest;
  outputs?: SubscriptionOutputRequest[];
  /** Defaults to true: extended-session candles (pre/post/overnight) never alert. */
  ignoreExtendedSessions?: boolean;
}

export interface JsonPatchOperation {
  op: string;
  path: string;
  value: unknown;
}

// --- Indicators ---
export interface IndicatorTemplateDto {
  name: string;
  properties: Record<string, string>;
  /** Localized "what does this parameter mean" hints per property (from /api/indicator/{name}?lang=...) */
  descriptions?: Record<string, string>;
  /** Localized "what is this indicator" description */
  description?: string | null;
  /** Property default values as invariant strings ("14", "70", "Day", "Close"; "0" = no default) */
  defaults?: Record<string, string>;
  /** Option names of custom template enums (e.g. Direction: ["Both","Up","Down"]); TimeFrame/CandlePart have built-in pickers */
  enumOptions?: Record<string, string[]>;
}

/**
 * One row of the public indicator catalog (GET /indicator/catalog): the enum key and
 * the localized "what is this" description. Serves both the dashboard and the public
 * /{locale}/indicators pages.
 */
export interface IndicatorCatalogDto {
  name: string;
  description: string | null;
}

// --- Notifications ---
export type NotificationChannel =
  | 'Telegram'
  | 'Email'
  | 'Webhook'
  | 'SignalR'
  | 'WebPush'
  | 'MobilePush'
  | 'WhatsApp'
  | 'Discord';

export interface NotificationTemplateDto {
  id: string;
  name: string;
  isBundled: boolean;
  template: string;
  /** The caller may edit/delete: their own template or an owner-less legacy row (a first write claims it). Bundled templates are never editable. */
  canEdit: boolean;
}

export interface PushSubscriptionDto {
  id: string;
  platform: string;
  endpoint: string;
  deviceLabel: string | null;
  created: string;
}

// --- Rules ---
export type CandlePart = 'Open' | 'High' | 'Low' | 'Close';
export type LogicalOperator = 'And' | 'Or';
export type RuleActionType = 'SendNotification' | 'DisableSubscription';

export interface RuleConditionDto {
  id: string | null;
  indicator: string;
  templateData: Record<string, unknown> | null;
  operator: string;
  referenceType: string;
  referenceIndicator: string | null;
  referenceTemplateData: Record<string, unknown> | null;
  referenceCandlePart: CandlePart | null;
  referenceValue: number | null;
  deviation: number | null;
  ticker: string;
  exchangeName: string | null;
  timeFrame: number;
}

export interface RuleGroupDto {
  id: string | null;
  operator: LogicalOperator;
  conditions: RuleConditionDto[];
  groups: RuleGroupDto[];
}

export interface RuleActionDto {
  id: string | null;
  type: RuleActionType;
  notificationTemplateId: string | null;
  parameters: Record<string, unknown> | null;
}

export interface RuleDto {
  id: string;
  name: string;
  when: RuleGroupDto;
  thenTrue: RuleActionDto[];
  thenFalse: RuleActionDto[];
}

export interface CreateRuleRequest {
  name: string;
  when: RuleGroupDto;
  thenTrue?: RuleActionDto[] | null;
  thenFalse?: RuleActionDto[] | null;
}

export interface PatchRuleRequest extends CreateRuleRequest {}

/**
 * Built-in one-tap rule preset (GET /rule-preset): `when` is a ready condition tree
 * whose ticker/exchange placeholders the client fills with the selected instrument.
 */
export interface RulePresetDto {
  key: string;
  indicator: string;
  /** Localized into the request language */
  name: string;
  description: string | null;
  when: RuleGroupDto;
  /** Paired presets render as segments of one chip; null on standalone presets */
  family: string | null;
  /** Localized chip prefix shared by the family members */
  familyLabel: string | null;
  /** Localized label of this preset's segment inside the family chip */
  segment: string | null;
}

// --- Instruments ---
export interface InstrumentDto {
  ticker: string;
  exchangeName: string | null;
  classCode: string | null;
  currency: string;
  type: string | null;
  name: string | null;
  isin: string | null;
  sector: string | null;
}

export interface InstrumentDetailDto extends InstrumentDto {
  longName: string | null;
  industry: string | null;
  country: string | null;
  website: string | null;
  employees: number | null;
  summary: string | null;
}

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// --- UI ---
export interface InstrumentSelectItem {
  ticker: string;
  exchangeName: string | null;
  name?: string | null;
}

// --- Trigger history ---
export interface TriggerHistoryDto {
  id: string;
  triggerValue: string;
  triggeredAt: string;
  /** Candle the condition saw (the chart marker anchor); null for rows created before the field existed */
  triggeredCandleAt?: string | null;
  /** Per-channel delivery attempts of this trigger (may be empty for legacy rows) */
  deliveries?: TriggerDeliveryDto[];
}

/** Per-channel delivery result: status Sent | Failed | Skipped, error when not delivered.
 * Known errors carry a stable "delivery_error:<code>" key localized via
 * subscriptions.triggerHistory.errors; anything else (provider text, legacy rows) is shown as-is. */
export interface TriggerDeliveryDto {
  channel: string;
  status: string;
  error: string | null;
  sentAt: string;
}

// --- Subscription snapshot (live chart) ---
export interface SnapshotChartDto {
  id: string;
  ticker: string;
  exchangeName: string | null;
  timeFrame: string;
  indicators: string[];
  lastCandleAt: string | null;
  renderedAt: string;
}

export interface SubscriptionSnapshotDto {
  subscriptionId: string;
  charts: SnapshotChartDto[];
  renderedAt: string | null;
}

// --- Interactive chart data (v2 of the chart endpoints, ?v=2) ---
/**
 * Raw series data of the chart the client plots itself. Times are Unix seconds of the
 * display re-stamped wall clock (already shifted to the user's timezone as if UTC by the
 * backend, exactly what the SVG axis labels show).
 */
export interface ChartCandleData {
  time: number;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
}

export interface ChartTimePointData {
  time: number;
  value: number;
}

/** Horizontal reference line of a series (e.g. RSI 70/30). */
export interface ChartRefLineData {
  value: number;
  label: string | null;
  color: string;
}

export interface ChartSeriesData {
  name: string;
  kind: 'line' | 'area' | 'bar';
  pane: 'price' | 'oscillator';
  color: string;
  points: ChartTimePointData[];
  refLines: ChartRefLineData[];
  dashed: boolean;
  hidden: boolean;
}

export interface ChartTriggerData {
  time: number;
  label: string | null;
  /**
   * Signal direction decided by the backend from the handler semantics: "up" buy-side,
   * "down" sell-side, "neutral" directionless event (dot). Absent (old payloads) leaves
   * the client to decide from the chart geometry.
   */
  direction?: 'up' | 'down' | 'neutral' | null;
}

export interface ChartData {
  title: string;
  candles: ChartCandleData[];
  series: ChartSeriesData[];
  oscMin: number | null;
  oscMax: number | null;
  trigger: ChartTriggerData | null;
  /** UTC date of the newest candle (not the display re-stamp): the live freshness anchor. */
  lastCandleAt: string | null;
  /** Pre/post-market quote line; null outside extended sessions. */
  extended: ExtendedQuoteData | null;
  /**
   * The exchange's market phase when the chart was built; null for venues the session
   * gate does not model (24/7 crypto). While an extended session runs the chart shows
   * the extended quote line instead of any closed-market notice.
   */
  session: ChartSession | null;
}

export type ChartSession = 'regular' | 'pre' | 'post' | 'overnight' | 'closed';

/**
 * The latest extended-hours trade and the regular close it is priced against.
 * Daily bars never carry extended prices (exchange aggregation rules), so the chart
 * shows this as its own "Pre-market/After-hours" line like public quote services.
 */
export interface ExtendedQuoteData {
  session: 'pre' | 'post' | 'overnight';
  price: number;
  /** UTC time of the extended candle. */
  at: string;
  referenceClose: number | null;
  /** Localized session name for the chart price line label (backend, per-user language). */
  label: string;
}

/** One public landing gallery slide: an example trigger chart of an indicator with its instrument. */
export interface LandingExampleChartDto {
  indicator: string;
  ticker: string;
  exchangeName: string | null;
  chart: ChartData;
}

// --- Auth / Account ---
export interface TelegramChatDto {
  id: string;
  chatId: number;
  username: string | null;
  linkedAt: string;
}

export interface TelegramLinkDto {
  deepLink: string | null;
  expiresAt: string;
}

/** Preferred sort of the subscriptions list; null — "alertDate". */
export type SubscriptionsSort = 'alertDate' | 'alertCount' | 'ticker' | 'created';

/**
 * Chip filters of the subscriptions list, sent as query params of GET /api/subscription:
 * q - free text over the instruments of the rule, statuses - subscription status names,
 * type/exchange - resolved through the instruments catalog, indicator - the rule watches
 * it on the instrument, triggered - alerts within the last week.
 */
export interface SubscriptionListFilters {
  q?: string;
  statuses?: string[];
  type?: string;
  indicator?: string;
  exchange?: string;
  triggered?: boolean;
}

/**
 * Interactive-chart display preferences stored per account (partial patch; a null flag
 * falls back to its default: OHLC tooltip on, crosshair magnet off).
 */
export interface ChartPreferencesDto {
  ohlcTooltip: boolean | null;
  magnet: boolean | null;
}

/** Pre-authentication instance status: is there a password to sign in with at all? */
export interface AuthStatusDto {
  passwordConfigured: boolean;
}

/**
 * How this instance is published. `landingOnly` marks the public site: the landing, the
 * indicator catalog and the legal pages, with no sign-in and no dashboard behind them.
 */
export interface SiteConfigDto {
  landingOnly: boolean;
}

/**
 * The one account of this installation: what the UI needs to render itself. There is no
 * email, no external identities and no deletion schedule - the operator is whoever knows
 * the password, and their preferences are the account.
 */
export interface AuthProfileDto {
  id: string;
  displayName: string | null;
  /** Preferred language for notifications/charts: a backend locale code ("ru", "en", "pt-BR", ...), null — not set */
  language: string | null;
  /** IANA time zone id (e.g. "Europe/Moscow"), null — not set */
  timezone: string | null;
  /** Preferred subscriptions-list sort, null — the default ("alertDate") */
  subscriptionsSort: SubscriptionsSort | null;
  /** Interactive-chart display preferences, null — never set (client defaults) */
  chartPreferences: ChartPreferencesDto | null;
  /** First-login onboarding tour was completed or skipped */
  onboardingCompleted: boolean;
  /** Create-subscription page tour was completed or skipped */
  subscriptionTourCompleted?: boolean;
  telegramChats: TelegramChatDto[];
}

// --- Provider settings (BYOK) ---

export interface ProviderFieldDto {
  name: string;
  label: string;
  /**
   * "secret" | "text" | "bool" | "int" | "choice" | "url". A "url" field is an address
   * something outside this process opens (a browser, a Telegram button): the page checks
   * the format and warns when the address cannot be reached from outside.
   */
  kind: string;
  required: boolean;
  /** A value is stored in the database for this field */
  hasStored: boolean;
  /** A value comes from the environment (appsettings/env), the fallback source */
  hasEnv: boolean;
  /** Full effective value (the page is auth-guarded); the UI masks secrets behind a reveal toggle */
  value: string | null;
  /** Choice fields: the accepted values the dropdown offers */
  options?: string[] | null;
}

export interface ProviderDto {
  code: string;
  displayName: string;
  /** "data" | "enricher" | "channel" | "auxiliary" */
  kind: string;
  docsUrl: string;
  markets: string[];
  /** The app generates the credentials itself (Web Push keys) - nothing to obtain */
  autoConfigured: boolean;
  enabled: boolean;
  configured: boolean;
  hasStoredSettings: boolean;
  /** stored config differs from what this process wired at startup - a restart will apply it */
  restartPending: boolean;
  fields: ProviderFieldDto[];
}

/** A blank credential value means "keep what is stored"; clearFields deletes fields outright. */
export interface SaveProviderRequest {
  isEnabled: boolean;
  credentials?: Record<string, string | null>;
  clearFields?: string[];
}

export interface ProviderTestResultDto {
  success: boolean;
  message: string;
  latencyMs: number;
}

/** The settings-page guide's dismissal flag (the copy itself is frontend i18n). */
export interface OnboardingDto {
  hidden: boolean;
}
