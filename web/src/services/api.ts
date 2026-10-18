import type { SubscriptionDto, CreateSubscriptionRequest, IndicatorTemplateDto, IndicatorCatalogDto, JsonPatchOperation, InstrumentDto, InstrumentDetailDto, PaginatedResult, TriggerHistoryDto, SubscriptionSnapshotDto, ChartData, LandingExampleChartDto, NotificationTemplateDto, PushSubscriptionDto, RuleDto, RulePresetDto, CreateRuleRequest, PatchRuleRequest, AuthProfileDto, AuthStatusDto, SiteConfigDto, TelegramChatDto, TelegramLinkDto, SubscriptionListFilters, ProviderDto, SaveProviderRequest, ProviderTestResultDto, OnboardingDto } from '@/types/contracts';

/**
 * Error types for API requests.
 * "network" — no internet / DNS failure / connection refused before any TCP handshake
 * "api"     — server responded with non-2xx (API is down or returned an error)
 * "unknown" — anything else
 */
export type ApiErrorType = 'network' | 'api' | 'unknown';

/**
 * A machine-readable validation error the backend returns in the envelope's "errorDetails"
 * field: a stable code plus the arguments the client substitutes into its own localized
 * message. The plain English "error" string is still delivered for other API consumers.
 */
export interface ApiValidationError {
  code: string;
  args: Record<string, string | number | boolean | null>;
  message?: string;
}

export class ApiError extends Error {
  public type: ApiErrorType;
  public statusCode?: number;
  public details?: ApiValidationError[];

  constructor(message: string, type: ApiErrorType, statusCode?: number, details?: ApiValidationError[]) {
    super(message);
    this.name = 'ApiError';
    this.type = type;
    this.statusCode = statusCode;
    this.details = details;
  }
}

/**
 * Classify a caught error into network / api / unknown.
 */
export function classifyApiError(e: unknown): ApiErrorType {
  if (e instanceof ApiError) return e.type;

  // DOMException / TypeError from fetch — usually network issues
  if (e instanceof DOMException && (e.name === 'NetworkError' || e.name === 'AbortError')) {
    return 'network';
  }
  if (e instanceof TypeError) {
    const msg = e.message.toLowerCase();
    if (msg.includes('network') || msg.includes('fetch') || msg.includes('failed')) {
      return 'network';
    }
  }

  return 'unknown';
}

/**
 * Check if an error indicates no internet connection.
 */
export function isNetworkError(e: unknown): boolean {
  return classifyApiError(e) === 'network';
}

/**
 * True when a failed auth probe says nothing about the session: the request never
 * reached a server that could adjudicate the cookie (no connection, proxy 502/503,
 * timeout, rate limit). The auth state stays unknown in that case instead of being
 * signed out, and the health-recovery retry resolves it once the API is back.
 * Only a real 4xx answer proves the session is gone.
 */
export function isTransientAuthError(e: unknown): boolean {
  if (e instanceof ApiError) {
    if (e.type === 'network') return true;
    const s = e.statusCode;
    return s !== undefined && (s >= 500 || s === 408 || s === 429);
  }
  return classifyApiError(e) === 'network';
}

/**
 * Global "credentials rejected" hook. A 401 from an authenticated request means the
 * server no longer accepts the session cookie — AuthProvider registers a handler that
 * signs the user out on the spot instead of waiting for a page reload. Kept as a plain
 * module-level callback so api.ts stays free of React dependencies (no import cycle
 * with useAuth).
 */
let unauthorizedHandler: (() => void) | null = null;

export function setOnUnauthorized(handler: (() => void) | null): void {
  unauthorizedHandler = handler;
}

/**
 * Liveness probe of the API (GET /api/health: postgres is required, redis is best-effort).
 * Resolves true only when the endpoint answers 2xx; any failure (no internet, connection
 * refused, proxy error, 5xx) means the service is unusable.
 */
export async function checkApiHealth(): Promise<boolean> {
  try {
    const resp = await fetch(`${BASE_URL}/health`, {
      cache: 'no-store',
      signal: AbortSignal.timeout(5000),
    });
    return resp.ok;
  } catch {
    return false;
  }
}


const BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api';
export const API_BASE_URL = BASE_URL;

/**
 * Absolute URL of an API endpoint, for the places that hand the URL to something other
 * than `fetch`: an `<img src>` or a link that leaves the page.
 */
export function apiUrl(path: string): string {
  const base = /^https?:/i.test(BASE_URL) ? BASE_URL : `${window.location.origin}${BASE_URL}`;
  return `${base}${path.startsWith('/') ? path : `/${path}`}`;
}

interface Envelope {
  statusCode?: number;
  result?: unknown;
  error?: unknown;
  errorDetails?: unknown;
}

/**
 * Coerces the envelope's "errorDetails" (an array of {code, args, message}) into typed errors.
 * Returns undefined when the field is absent, empty or malformed, so callers fall back to the
 * plain error string.
 */
function normalizeErrorDetails(raw: unknown): ApiValidationError[] | undefined {
  if (!Array.isArray(raw) || raw.length === 0) return undefined;

  const details: ApiValidationError[] = [];
  for (const item of raw) {
    if (!item || typeof item !== 'object') continue;
    const o = item as Record<string, unknown>;
    if (typeof o.code !== 'string' || !o.code) continue;
    const args = (o.args && typeof o.args === 'object' ? o.args : {}) as Record<string, string | number | boolean | null>;
    details.push({ code: o.code, args, message: typeof o.message === 'string' ? o.message : undefined });
  }

  return details.length ? details : undefined;
}

function extractErrorDetails(text: string): ApiValidationError[] | undefined {
  if (!text) return undefined;
  try {
    return normalizeErrorDetails((JSON.parse(text) as Envelope)?.errorDetails);
  } catch {
    return undefined;
  }
}

/**
 * Extract a human-readable message from an error response body.
 * Handles plain text, {"error": "msg"}, and {"error": {"Field": ["msg", ...]}} envelopes.
 */
function extractErrorMessage(text: string, fallback: string): string {
  if (!text) return fallback;
  try {
    const parsed = JSON.parse(text) as Envelope;
    const error = parsed?.error;
    if (typeof error === 'string' && error) return error;
    if (error && typeof error === 'object') {
      const parts: string[] = [];
      for (const [key, value] of Object.entries(error as Record<string, unknown>)) {
        const list = Array.isArray(value) ? value : [value];
        for (const item of list) parts.push(typeof item === 'string' ? (list.length > 1 ? `${key}: ${item}` : item) : String(item));
      }
      if (parts.length) return parts.join('; ');
    }
    // A parsed API envelope without a usable error payload (e.g. "error": null plus the
    // dev-only "exception" stack trace) must never dump raw JSON into the UI
    if (parsed && typeof parsed === 'object' && ('statusCode' in parsed || 'result' in parsed || 'error' in parsed)) return fallback;
  } catch {
    /* not JSON — fall through to raw text */
  }
  return text;
}

/**
 * One fetch against the API. Authorization is the httpOnly `si_session` cookie, sent
 * same-origin by the browser itself — there is no token to attach and nothing to rotate:
 * when the cookie dies, the 401 goes to the global handler and the user signs in again.
 */
async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let resp: Response;
  try {
    resp = await fetch(`${BASE_URL}${path}`, {
      ...init,
      headers: { 'Content-Type': 'application/json', ...(init?.headers as Record<string, string>) },
    });
  } catch {
    throw new ApiError('', 'network');
  }

  if (!resp.ok) {
    const text = await resp.text().catch(() => resp.statusText);
    // Fire before throwing: the caller's catch is about rendering the error,
    // signing out is a side effect that must not depend on it.
    if (resp.status === 401) unauthorizedHandler?.();
    throw new ApiError(extractErrorMessage(text, resp.statusText || `HTTP ${resp.status}`), 'api', resp.status, extractErrorDetails(text));
  }

  const ct = resp.headers.get('content-type') || '';
  if (!ct.includes('json')) return resp.text() as unknown as T;

  const envelope: Envelope = await resp.json();
  if (envelope.statusCode != null && envelope.statusCode >= 400) {
    throw new ApiError(extractErrorMessage(JSON.stringify(envelope), `HTTP ${envelope.statusCode}`), 'api', envelope.statusCode, normalizeErrorDetails(envelope.errorDetails));
  }
  if ('result' in envelope) return envelope.result as T;
  return envelope as unknown as T;
}

// Normalize API response: handle both PascalCase and camelCase
function pickField(o: Record<string, unknown>, camel: string, pascal: string): unknown {
  return o[camel] ?? o[pascal] ?? null;
}

function normalizeInstrument(raw: unknown): InstrumentDto {
  if (!raw || typeof raw !== 'object') {
    return { ticker: '', exchangeName: null, classCode: null, currency: '', type: null, name: null, isin: null, sector: null };
  }
  const o = raw as Record<string, unknown>;
  return {
    ticker: (o.ticker ?? o.Ticker ?? '') as string,
    exchangeName: (o.exchangeName ?? o.ExchangeName ?? null) as string | null,
    classCode: (o.classCode ?? o.ClassCode ?? null) as string | null,
    currency: (o.currency ?? o.Currency ?? '') as string,
    type: (o.type ?? o.Type ?? null) as string | null,
    name: pickField(o, 'name', 'Name') as string | null,
    isin: pickField(o, 'isin', 'Isin') as string | null,
    sector: pickField(o, 'sector', 'Sector') as string | null,
  };
}

function normalizeInstrumentDetail(raw: unknown): InstrumentDetailDto {
  const base = normalizeInstrument(raw);
  if (!raw || typeof raw !== 'object') {
    return { ...base, longName: null, industry: null, country: null, website: null, employees: null, summary: null };
  }
  const o = raw as Record<string, unknown>;
  const employeesRaw = pickField(o, 'employees', 'Employees');
  return {
    ...base,
    longName: pickField(o, 'longName', 'LongName') as string | null,
    industry: pickField(o, 'industry', 'Industry') as string | null,
    country: pickField(o, 'country', 'Country') as string | null,
    website: pickField(o, 'website', 'Website') as string | null,
    employees: employeesRaw == null ? null : Number(employeesRaw),
    summary: pickField(o, 'summary', 'Summary') as string | null,
  };
}

function normalizePaginated<T>(raw: unknown, normalize: (item: unknown) => T): PaginatedResult<T> {
  if (!raw || typeof raw !== 'object') return { items: [], totalCount: 0, page: 1, pageSize: 50 };
  const o = raw as Record<string, unknown>;
  const itemsRaw = (o.items ?? o.Items ?? []) as unknown[];
  return {
    items: Array.isArray(itemsRaw) ? itemsRaw.map(normalize) : [],
    totalCount: Number(o.totalCount ?? o.TotalCount ?? 0),
    page: Number(o.page ?? o.Page ?? 1),
    pageSize: Number(o.pageSize ?? o.PageSize ?? 50),
  };
}

export const api = {
  // Subscriptions
  getAllSubscriptions: (filters?: SubscriptionListFilters) => {
    const q = new URLSearchParams();
    if (filters?.q) q.set('q', filters.q);
    filters?.statuses?.forEach(s => q.append('status', s));
    if (filters?.type) q.set('type', filters.type);
    if (filters?.indicator) q.set('indicator', filters.indicator);
    if (filters?.exchange) q.set('exchange', filters.exchange);
    if (filters?.triggered) q.set('triggered', 'true');
    const path = q.toString() ? `/subscription?${q}` : '/subscription';
    return request<SubscriptionDto[]>(path);
  },
  getSubscription: (id: string) => request<SubscriptionDto>(`/subscription/${id}`),
  createSubscription: (ticker: string, exchange: string | null | undefined, body: CreateSubscriptionRequest) => {
    const qs = exchange ? `?exchangeName=${encodeURIComponent(exchange)}` : '';
    return request<string>(`/instrument/${encodeURIComponent(ticker)}/subscription${qs}`, { method: 'POST', body: JSON.stringify(body) });
  },
  deleteSubscription: (id: string) =>
    request<void>(`/subscription/${id}`, { method: 'DELETE' }),
  deleteInstrumentSubscriptions: (ticker: string, exchange: string | null) =>
    request<void>(`/instrument/${encodeURIComponent(ticker)}/subscriptions`, { method: 'DELETE', body: JSON.stringify({ exchangeName: exchange }) }),
  patchSubscription: (id: string, ops: JsonPatchOperation[]) =>
    request<void>(`/subscription/${id}`, { method: 'PATCH', body: JSON.stringify(ops) }),
  setSubscriptionActive: (id: string, isActive: boolean) =>
    request<void>(`/subscription/${id}/active`, { method: 'POST', body: JSON.stringify({ isActive }) }),
  getTriggerHistory: (id: string) =>
    request<TriggerHistoryDto[]>(`/subscription/${id}/trigger-history`),
  markSubscriptionRead: (id: string) =>
    request<void>(`/subscription/${id}/read`, { method: 'POST' }),
  markAllSubscriptionsRead: () =>
    request<void>('/subscription/read-all', { method: 'POST' }),

  /**
   * Trigger chart: raw series data the client plots itself (404 when the candle
   * cache window expired).
   */
  getTriggerChartData: (id: string, triggerId: string) =>
    request<ChartData>(`/subscription/${id}/triggers/${triggerId}/chart`),

  /**
   * Instrument preview chart: raw series data (daily candles, no indicators), the same
   * payload the client chart core plots elsewhere; 404 when the cache has no candles.
   */
  getInstrumentChart: (ticker: string, exchange: string | null | undefined) => {
    const qs = exchange ? `?exchange=${encodeURIComponent(exchange)}` : '';
    return request<ChartData>(`/instrument/${encodeURIComponent(ticker)}/chart${qs}`);
  },

  getSubscriptionSnapshot: (id: string) =>
    request<SubscriptionSnapshotDto>(`/subscription/${id}/snapshot`),

  /**
   * Snapshot chart: raw series data for one stored chart row, computed from the live candle
   * cache (404 when its group has no data).
   */
  getSnapshotChartData: (subId: string, chartId: string) =>
    request<ChartData>(`/subscription/${subId}/snapshot/${chartId}/chart`),

  // Notification templates (channel-agnostic: one template can serve any output channel)
  getNotificationTemplates: () =>
    request<NotificationTemplateDto[]>('/notification-template'),
  createNotificationTemplate: (body: { name: string; template: string }) =>
    request<string>('/notification-template', { method: 'POST', body: JSON.stringify(body) }),
  updateNotificationTemplate: (id: string, body: { name: string; template: string }) =>
    request<void>(`/notification-template/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteNotificationTemplate: (id: string) =>
    request<void>(`/notification-template/${id}`, { method: 'DELETE' }),

  // Web Push
  getVapidPublicKey: () =>
    request<{ key: string }>('/push/vapid-public-key'),
  getPushSubscriptions: () =>
    request<PushSubscriptionDto[]>('/push/subscriptions'),
  savePushSubscription: (body: { endpoint: string; keys: { p256dh: string; auth: string }; deviceLabel?: string }) =>
    request<PushSubscriptionDto>('/push/subscriptions', { method: 'POST', body: JSON.stringify(body) }),
  deletePushSubscription: (id: string) =>
    request<void>(`/push/subscriptions/${id}`, { method: 'DELETE' }),
  sendPushTest: () =>
    request<{ sent: number }>('/push/test', { method: 'POST' }),

  // App config (server-side rule limits, keep the UI in sync with validation)
  getConfig: () => request<AppConfig>('/config'),

  // Provider settings (the BYOK control panel: registry, stored state, connection tests)
  getProviders: () => request<ProviderDto[]>('/providers'),
  saveProvider: (code: string, body: SaveProviderRequest) =>
    request<{ restartRequired: boolean }>(`/providers/${code}`, { method: 'PUT', body: JSON.stringify(body) }),
  getOnboarding: () => request<OnboardingDto>('/onboarding'),
  setOnboarding: (body: OnboardingDto) =>
    request<void>('/onboarding', { method: 'PUT', body: JSON.stringify(body) }),
  testProvider: (code: string, credentials?: Record<string, string>) =>
    request<ProviderTestResultDto>(`/providers/${code}/test`, { method: 'POST', body: JSON.stringify({ credentials }) }),

  // Rules (backend route: api/[controller] → api/Rule)
  getRules: () => request<RuleDto[]>('/rule'),
  getRule: (id: string) => request<RuleDto>(`/rule/${id}`),
  createRule: (body: CreateRuleRequest) =>
    request<string>('/rule', { method: 'POST', body: JSON.stringify(body) }),
  updateRule: (id: string, body: PatchRuleRequest) =>
    request<void>(`/rule/${id}`, { method: 'PATCH', body: JSON.stringify(body) }),
  deleteRule: (id: string) =>
    request<void>(`/rule/${id}`, { method: 'DELETE' }),

  // Built-in one-tap rule presets (GET /api/rule-preset?lang=)
  getRulePresets: (lang?: string) => {
    const qs = lang ? `?lang=${encodeURIComponent(lang)}` : '';
    return request<RulePresetDto[]>(`/rule-preset${qs}`);
  },

  // Indicators (the whole /api/indicator controller is public: catalog data only,
  // so the anonymous landing pages and the dashboard share the same endpoints)
  getIndicators: () => request<string[]>('/indicator'),
  /** Public catalog: name + localized description for the indicator lists. */
  getIndicatorCatalog: (lang?: string) => {
    const qs = lang ? `?lang=${encodeURIComponent(lang)}` : '';
    return request<IndicatorCatalogDto[]>(`/indicator/catalog${qs}`);
  },
  getIndicatorTemplate: (name: string, lang?: string) => {
    const qs = lang ? `?lang=${lang}` : '';
    return request<IndicatorTemplateDto>(`/indicator/${name}${qs}`);
  },
  /**
   * Example chart of an indicator: raw series data (synthetic candles with a trigger
   * marker) the client plots with its own chart core.
   */
  getIndicatorExample: (name: string, lang?: string) => {
    const qs = lang ? `?lang=${encodeURIComponent(lang)}` : '';
    return request<ChartData>(`/indicator/${name}/example${qs}`);
  },

  /** Public (anonymous) landing gallery: example trigger charts as interactive series data. */
  getLandingExamples: (lang: string) =>
    request<LandingExampleChartDto[]>(`/landing/examples?lang=${encodeURIComponent(lang)}`),

  /** Public (anonymous) instance mode: whether the app part exists here at all. */
  getSiteConfig: () => request<SiteConfigDto>('/site/config'),

  // Instruments
  getAllInstruments: (params: { page?: number; pageSize?: number; search?: string; type?: string }) => {
    const q = new URLSearchParams();
    if (params.page) q.set('page', String(params.page));
    if (params.pageSize) q.set('pageSize', String(params.pageSize));
    if (params.search) q.set('search', params.search);
    if (params.type) q.set('type', params.type);
    const path = q.toString() ? `/instrument?${q}` : '/instrument';
    return request<unknown>(path).then(r => normalizePaginated(r, normalizeInstrument));
  },
  getInstrument: (ticker: string, exchange: string | null | undefined) => {
    const qs = exchange ? `?exchange=${encodeURIComponent(exchange)}` : '';
    return request<unknown>(`/instrument/${encodeURIComponent(ticker)}${qs}`).then(normalizeInstrumentDetail);
  },
};

export interface AppConfig {
  maxConditionsPerRule: number;
  maxRuleNameLength: number;
  maxGroupDepth: number;
  telegramBotLink?: string;
  /** Default check cooldown for new subscriptions, seconds */
  defaultCooldownSeconds?: number;
  /** Minimum check cooldown the server accepts, seconds */
  minCooldownSeconds?: number;
  /** Alert output channels per subscription */
  maxOutputs?: number;
  /** WhatsApp delivery is configured on the server (Meta credentials present) */
  whatsappEnabled?: boolean;
}

export const auth = {
  /** What the login screen can say before anyone authenticates: does this instance have a password? */
  status: async (): Promise<AuthStatusDto> => {
    try {
      return await request<AuthStatusDto>('/auth/status');
    } catch {
      return { passwordConfigured: false };
    }
  },

  /** Verifies the instance password and opens the cookie session. Throws (401) on a wrong one. */
  login: (password: string): Promise<AuthProfileDto> =>
    request<AuthProfileDto>('/auth/login', { method: 'POST', body: JSON.stringify({ password }) }),

  /** Current account, or null when the session cookie is absent or expired. */
  me: async (): Promise<AuthProfileDto | null> => {
    try {
      return await request<AuthProfileDto>('/auth/me');
    } catch (e) {
      if (e instanceof ApiError && (e.statusCode === 401 || e.statusCode === 404)) return null;
      throw e;
    }
  },

  /** Clears the session cookie. */
  logout: (): Promise<void> => request<void>('/auth/logout', { method: 'POST' }),
};

/**
 * Account management (linked Telegram chats, preferences). Cookie session only.
 */
export const account = {
  /**
   * Partial update: only provided fields are changed. Returns the updated profile.
   */
  updatePreferences: (body: { language?: string; timezone?: string; completeOnboarding?: boolean; subscriptionsSort?: string; chartPreferences?: { ohlcTooltip?: boolean; magnet?: boolean }; completeSubscriptionTour?: boolean }): Promise<AuthProfileDto> =>
    request<AuthProfileDto>('/account/preferences', { method: 'PATCH', body: JSON.stringify(body) }),

  getTelegramChats: (): Promise<TelegramChatDto[]> => request<TelegramChatDto[]>('/account/telegram-chats'),

  deleteTelegramChat: (chatId: string): Promise<void> =>
    request<void>(`/account/telegram-chats/${chatId}`, { method: 'DELETE' }),

  /**
   * Issues a one-time Telegram chat-link deep link. The chat binds itself to the
   * account when the user opens the bot link and taps Start.
   */
  createTelegramLinkToken: (): Promise<TelegramLinkDto> =>
    request<TelegramLinkDto>('/account/telegram-chats/link-token', { method: 'POST' }),
};
