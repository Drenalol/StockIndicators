import { useState, useEffect, useCallback, useMemo, useRef, type RefObject } from 'react';
import { v7 as uuidv7 } from 'uuid';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { useQuery, useQueries, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/hooks/useAuth';
import { useTranslation } from '@/i18n';
import { useGoBackOr } from '@/hooks/useGoBackOr';
import { useToast } from '@/hooks/use-toast';
import { api, classifyApiError, ApiError } from '@/services/api';
import { qk } from '@/lib/query';
import type { Cooldown, InstrumentSelectItem, RuleFormModel, RuleConditionEntry } from '@/components/RuleEditor/schema';
import {
  createDefaultGroup,
  toCreateRuleRequest,
  toRuleFormModel,
  presetGroupToModel,
  collectLeafConditions,
  conditionToApi,
  findRedundantConditions,
  buildRuleName,
  mapLeafConditions,
  instrumentKey,
  MAX_RULE_NAME_LENGTH,
} from '@/components/RuleEditor/schema';
import type { RulePresetDto } from '@/types/contracts';
import { RuleGroupEditor } from '@/components/RuleEditor/RuleGroupEditor';
import { PresetPicker } from '@/components/RuleEditor/PresetPicker';
import { CooldownField } from '@/components/CooldownField';
import { useLimits, maxConditionsPerRuleOf, maxOutputsOf, defaultCooldownSecondsOf } from '@/lib/limits';
import { localizeValidationErrors } from '@/lib/validationErrors';
import { dashboardPrefix } from '@/lib/paths';
import NotificationsSection, { type OutputChannel, type OutputDraft } from '@/components/NotificationsSection';
import { isValidTargetUrl, normalizeWhatsAppPhone, isUrlChannel, isWhatsApp } from '@/lib/alertChannels';
import { useNavigationGuard } from '@/components/layout/NavigationGuardProvider';
import { useKeyboardOpen } from '@/hooks/useKeyboardOpen';
import OnboardingTour from '@/components/layout/OnboardingTour';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
import { Loader2, ArrowLeft } from 'lucide-react';

// Default until /api/Config answers with the server's own default
const DEFAULT_COOLDOWN: Cooldown = { cooldownMinutes: 60 };

// keep a gap between the focused field and the soft keyboard top
const KEYBOARD_REVEAL_GAP = 12;

// Indicator template parameter errors (IndicatorTemplateValidation / TemplateValidator on
// the API) — they refer to fields inside the trigger section, so they are shown there
// instead of the bottom alert and the view scrolls up to the trigger
const PARAM_VALIDATION_ERROR = /must be at least 1|must be greater than 0|must not be negative|Period out of range|Template property has incorrect format/;

function deepEqual(a: unknown, b: unknown): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

function defaultRuleModel(ticker = '', exchange?: string | null): RuleFormModel {
  return {
    group: createDefaultGroup(ticker, exchange),
    thenTrue: [{ id: uuidv7(), type: 'SendNotification' }],
    thenFalse: [],
    name: '',
  };
}

export default function SubscriptionFormPage() {
  const params = useParams();
  const isEdit = !!(params.subId || params.id);
  const subId = params.subId || params.id;
  const navigate = useNavigate();
  const { loaded, isAuthenticated, profile, completeSubscriptionTour } = useAuth();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { t, locale } = useTranslation();
  const config = useLimits();
  const goBackOr = useGoBackOr();
  const prefix = dashboardPrefix(locale);

  // First-create walkthrough (once per account, server flag): spotlight the form
  // sections. It waits for the nav tour: a brand-new user gets the nav tour first
  // (MainLayout), and this one opens as soon as that flag flips.
  const createTourSteps = useMemo(
    () => (['presets', 'conditions', 'cooldown', 'notifications', 'create'] as const)
      .map(key => ({ anchor: key, ...t.onboarding.subscriptionSteps[key] })),
    [t]
  );
  const showCreateTour = !isEdit
    && profile?.onboardingCompleted === true
    && profile?.subscriptionTourCompleted === false;

  // Output count limit (from /api/Config)
  const maxOutputs = maxOutputsOf(config);

  // Soft keyboard: the chrome tokens collapse (index.css) and the mobile action bar
  // hides; here the focused field is additionally kept above the keys inside the
  // page's own scroll container (native scrollIntoView ignores the visual viewport)
  const keyboardOpen = useKeyboardOpen();
  const scrollAreaRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!keyboardOpen) return;
    const vv = window.visualViewport;
    if (!vv) return;
    const reveal = () => {
      // undo any iOS keyboard reveal scroll of the layout viewport (the document is
      // already sized to the visible band, so there is nothing valid to scroll)
      if (window.scrollY !== 0) window.scrollTo(0, 0);
      const container = scrollAreaRef.current;
      const active = document.activeElement;
      if (!container || !active || !container.contains(active)) return;
      if (!active.matches('input, textarea')) return;
      const limit = vv.height - KEYBOARD_REVEAL_GAP;
      const box = active.getBoundingClientRect();
      if (box.bottom > limit) container.scrollTop += box.bottom - limit;
      else if (box.top < 0) container.scrollTop += box.top;
    };
    reveal();
    // fires when the keyboard finishes opening/animating and when focus moves
    // between fields of different keyboard types while it stays up
    vv.addEventListener('resize', reveal);
    document.addEventListener('focusin', reveal);
    return () => {
      vv.removeEventListener('resize', reveal);
      document.removeEventListener('focusin', reveal);
    };
  }, [keyboardOpen]);

  // Pre-selected instrument via query string (from instrument detail page)
  const [searchParams] = useSearchParams();
  const preselectedTicker = isEdit ? null : searchParams.get('ticker');
  const preselectedExchange = preselectedTicker ? searchParams.get('exchange') : null;
  const lockInstrument = !!preselectedTicker;
  // Pre-selected indicator via query string (from the indicator detail page)
  const preselectedIndicator = isEdit ? null : searchParams.get('indicator');
  // Preset to pre-fill on open (deep link from the indicator page preset chips)
  const presetParam = isEdit ? null : searchParams.get('preset');
  // Entry point marker: the form can be opened with a locked instrument from the instruments
  // detail page (default) or from the subscriptions list context menu
  const fromSubscriptions = !isEdit && searchParams.get('from') === 'subscriptions';
  const fromIndicators = !isEdit && searchParams.get('from') === 'indicators';

  // Cancel URL: back to subscription detail if editing, the entry point otherwise
  // (indicator page if pre-filled from there, instrument detail if pre-filled, else subscriptions list)
  const cancelUrl = params.subId
    ? `${prefix}/subscriptions/${params.ticker}/${params.subId}`
    : params.id
      ? `${prefix}/subscriptions`
      : fromIndicators && preselectedIndicator
        ? `${prefix}/indicators/${encodeURIComponent(preselectedIndicator)}`
        : lockInstrument && preselectedTicker && !fromSubscriptions
          ? `${prefix}/instruments?ticker=${encodeURIComponent(preselectedTicker)}${preselectedExchange ? `&exch=${encodeURIComponent(preselectedExchange)}` : ''}`
          : `${prefix}/subscriptions`;

  const [ruleModel, setRuleModel] = useState<RuleFormModel>(() => {
    const model = defaultRuleModel(preselectedTicker ?? '', preselectedExchange);
    if (preselectedIndicator) model.group.conditions[0].condition.indicator = preselectedIndicator;
    return model;
  });
  const [cooldown, setCooldown] = useState<Cooldown>(DEFAULT_COOLDOWN);
  // Extended-session opt-out (a subscription field): on by default, the checkbox simply
  // hides on 24/7 crypto instruments where no extended session exists
  const [ignoreExtendedSessions, setIgnoreExtendedSessions] = useState(true);
  // The create form adopts the server default (1 h free plan) when /api/Config resolves;
  // editing keeps the subscription's own cooldown (a legacy fast one included).
  const adoptedDefaultRef = useRef(false);
  const [outputs, setOutputs] = useState<OutputDraft[]>([{ id: null, channel: 'Telegram', templateId: null, pushSubscriptionId: null, chatId: null, target: null }]);
  const originalRuleModelRef = useRef<RuleFormModel>(structuredClone(ruleModel));
  const originalRuleIdRef = useRef<string | null>(null);
  const originalCooldownRef = useRef<Cooldown>(DEFAULT_COOLDOWN);
  const originalIgnoreExtendedRef = useRef(true);
  const originalOutputs = useRef<OutputDraft[]>([{ id: null, channel: 'Telegram', templateId: null, pushSubscriptionId: null, chatId: null, target: null }]);

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  // The edit target never loaded (failed query or a ruleless subscription): the only
  // error state that disables the submit button. `error` is display-only: it also holds
  // retryable save failures and fixable validations, so gating on it would lock the
  // button (handleSave clears it on entry, but the disabled button never gets an entry)
  const [loadFailed, setLoadFailed] = useState(false);

  const triggerSectionRef = useRef<HTMLElement>(null);
  const notificationSectionRef = useRef<HTMLElement>(null);
  const formErrorRef = useRef<HTMLDivElement>(null);

  // Bring the failed validation (or the error alert) into view after the state update commits
  const scrollToRef = useCallback((ref: RefObject<HTMLElement | null>) => {
    requestAnimationFrame(() => ref.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }));
  }, []);

  useEffect(() => {
    if (isEdit || adoptedDefaultRef.current || config?.defaultCooldownSeconds == null) return;
    adoptedDefaultRef.current = true;
    const minutes = Math.max(1, Math.floor(defaultCooldownSecondsOf(config) / 60));
    setCooldown((prev) => (deepEqual(prev, DEFAULT_COOLDOWN) ? { ...prev, cooldownMinutes: minutes } : prev));
  }, [isEdit, config]);

  // Computed inline (not memoized) so it always reflects the baseline refs after a save resets them.
  // THEN-actions are not user-editable (always SendNotification on trigger), so only the
  // trigger (name + conditions), cooldown and notifications are tracked.
  const changed = {
    trigger: !deepEqual(ruleModel.group, originalRuleModelRef.current.group)
      || ruleModel.name !== originalRuleModelRef.current.name,
    cooldown: !deepEqual(cooldown, originalCooldownRef.current),
    ignoreExtended: ignoreExtendedSessions !== originalIgnoreExtendedRef.current,
    notification: !deepEqual(outputs, originalOutputs.current),
  };

  const hasChanges = changed.trigger || changed.cooldown || changed.ignoreExtended || changed.notification;

  // Register a navigation guard while there are unsaved changes, so menu navigation (and tab
  // close/refresh) asks for confirmation instead of silently dropping the edits.
  const { setGuard, confirmNavigation } = useNavigationGuard();
  useEffect(() => {
    if (!hasChanges) return;
    setGuard({ message: t.common.unsavedChangesMessage });
    return () => setGuard(null);
  }, [hasChanges, setGuard, t.common.unsavedChangesMessage]);

  // Accumulating instrument cache (ticker|exchange → item): never purged by another row's
  // search, so a row's selected instrument always resolves for the trigger label.
  // Replaces the old shared options array, whose per-search
  // replacement blanked the other rows' selections.
  const instrumentCacheRef = useRef(new Map<string, InstrumentSelectItem>());

  const mergeInstruments = useCallback(
    (items: { ticker: string; exchangeName: string | null; name: string | null }[]): InstrumentSelectItem[] => {
      const mapped = items.map(d => ({ ticker: d.ticker, exchangeName: d.exchangeName, name: d.name }));
      for (const m of mapped) instrumentCacheRef.current.set(instrumentKey(m.ticker, m.exchangeName), m);
      return mapped;
    },
    [],
  );

  const resolveInstrument = useCallback(
    (ticker: string, exchange: string | null) => instrumentCacheRef.current.get(instrumentKey(ticker, exchange)) ?? null,
    [],
  );

  // Load indicators (limits come from /api/Config); instruments are
  // fetched lazily by the comboboxes (default list + debounced search)
  const indicatorsQuery = useQuery({
    queryKey: qk.indicators,
    queryFn: () => api.getIndicators(),
    enabled: loaded && isAuthenticated,
  });
  // the list stays editable offline through the static fallback
  const indicators = useMemo(
    () => indicatorsQuery.data ?? (indicatorsQuery.error ? ['SMA', 'EMA', 'StochRSI', 'RoundPrice', 'Gap', 'MACD', 'RSI', 'BollingerBands', 'SuperTrend', 'VWAP', 'ADX', 'Stochastic', 'ATR', 'Ichimoku', 'OBV', 'Volume', 'PriceChange', 'CCI', 'MFI', 'WilliamsR', 'CMO', 'TSI', 'Keltner', 'Donchian', 'WMA', 'HMA', 'VWMA', 'KAMA', 'PSAR', 'Chandelier', 'Aroon', 'Vortex', 'OpenChange', 'Week52HighLow', 'TrailingStop'] : []),
    [indicatorsQuery.data, indicatorsQuery.error],
  );

  // Built-in one-tap presets from the backend seed catalog (create mode only; a
  // an applicable preset pre-fills the whole tree)
  const presetsQuery = useQuery({
    queryKey: qk.rulePresets(locale),
    queryFn: () => api.getRulePresets(locale),
    enabled: !isEdit && loaded && isAuthenticated,
  });
  const presets = presetsQuery.data ?? [];
  const [activePresetKey, setActivePresetKey] = useState<string | null>(null);

  // Load the existing subscription through the shared cache (the detail page
  // usually has it fresh already) and seed the form + the "changed" baselines
  const subscriptionQuery = useQuery({
    queryKey: qk.subscription(subId ?? ''),
    queryFn: () => api.getSubscription(subId!),
    enabled: isEdit && !!subId && loaded && isAuthenticated,
  });
  const loadedSubscription = subscriptionQuery.data;
  const loading = isEdit ? subscriptionQuery.isPending : false;

  // Seed the form exactly once per visit: a background refetch of the shared
  // entry (e.g. the invalidation issued after a save) must not clobber edits
  const seededRef = useRef(false);
  useEffect(() => {
    if (!isEdit || !loadedSubscription || seededRef.current) return;
    seededRef.current = true;
    // A subscription is a pure container of its rule — without a resolvable rule there
    // is nothing to edit (legacy flat subscriptions were removed with the refactor)
    if (!loadedSubscription.rule) {
      setError(t.errors.failedToLoad);
      setLoadFailed(true);
      return;
    }

    const model = toRuleFormModel(loadedSubscription.rule);

    const s: Cooldown = loadedSubscription.cooldownSeconds
      ? { ...DEFAULT_COOLDOWN, cooldownMinutes: Math.floor(loadedSubscription.cooldownSeconds! / 60) }
      : DEFAULT_COOLDOWN;

    const loadedOutputs: OutputDraft[] = (loadedSubscription.outputs ?? []).map((o) => ({
      id: o.id,
      channel: (['Telegram', 'WebPush', 'Webhook', 'Discord', 'WhatsApp'].includes(o.channel) ? o.channel : 'Telegram') as OutputChannel,
      templateId: o.notificationTemplateId,
      pushSubscriptionId: o.pushSubscriptionId,
      chatId: o.chatId ?? null,
      target: o.target ?? null,
    }));

    setRuleModel(model);
    setCooldown(s);
    setIgnoreExtendedSessions(loadedSubscription.ignoreExtendedSessions ?? true);
    setOutputs(loadedOutputs);
    originalRuleModelRef.current = structuredClone(model);
    originalRuleIdRef.current = loadedSubscription.rule.id;
    originalOutputs.current = loadedOutputs.map((o) => ({ ...o }));
    originalCooldownRef.current = { ...s };
    originalIgnoreExtendedRef.current = loadedSubscription.ignoreExtendedSessions ?? true;
    setError(null);
    setLoadFailed(false);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isEdit, loadedSubscription]);

  // a failed load reports; the global healing refetches it once the API is back.
  // A 404 means the alert was deleted in another window/device - there is nothing
  // left to edit, so explain in a toast and land on the list instead of keeping
  // the user on a dead form. The ref makes it fire once: removeQueries on this
  // still-mounted query would refetch it, the new 404 error would re-enter the
  // effect and the pair would spin into a GET storm, so the cache entry simply
  // ages out through gc after the navigation unmounts the form
  const loadError = subscriptionQuery.error;
  const goneHandledRef = useRef(false);
  useEffect(() => {
    if (!isEdit || !loadError || goneHandledRef.current) return;
    if (loadError instanceof ApiError && loadError.statusCode === 404 && subId) {
      goneHandledRef.current = true;
      queryClient.invalidateQueries({ queryKey: qk.subscriptions });
      toast({ title: t.errors.subscriptionGone, variant: 'destructive' });
      navigate(`${prefix}/subscriptions`);
      return;
    }
    setError(t.errors.failedToLoad);
    setLoadFailed(true);
  // the error object is stable per failed attempt, so this fires once per attempt
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isEdit, loadError]);

  // Server-side instrument search for the comboboxes; debouncing and stale-response
  // guarding live in the component (per dropdown, not shared across rows). Results go
  // through the shared cache, so reopening a dropdown (or the instruments page)
  // with the same query costs no request.
  const fetchInstrumentsForCombobox = useCallback(async (query: string) => {
    const r = await queryClient.fetchQuery({
      queryKey: qk.instruments(1, 50, query, ''),
      queryFn: () => api.getAllInstruments({ page: 1, pageSize: 50, search: query || undefined }),
    });
    return mergeInstruments(r.items);
  }, [queryClient, mergeInstruments]);

  const searchInstruments = useCallback((query: string) => fetchInstrumentsForCombobox(query.trim()), [fetchInstrumentsForCombobox]);

  // The empty-query default list is cached under the same key and reused by every combobox
  const loadDefaultInstruments = useCallback(() => fetchInstrumentsForCombobox(''), [fetchInstrumentsForCombobox]);

  // Wire shape of the destination: phone normalized to E.164; chat/push channels carry none
  const outputTarget = (o: OutputDraft): string | null =>
    isWhatsApp(o.channel) ? normalizeWhatsAppPhone(o.target) : isUrlChannel(o.channel) ? o.target?.trim() ?? null : null;

  const handleSave = useCallback(async () => {
    setError(null);
    setFieldErrors({});

    const leaves = collectLeafConditions(ruleModel.group);
    if (leaves.length === 0) {
      setFieldErrors({ rule: [t.rules.atLeastOneCondition] });
      scrollToRef(triggerSectionRef);
      return;
    }
    if (leaves.length > maxConditionsPerRuleOf(config)) {
      setFieldErrors({ rule: [t.rules.conditionsLimitReached] });
      scrollToRef(triggerSectionRef);
      return;
    }
    const errs: Record<string, string[]> = {};
    if (leaves.some((l) => !l.condition.indicator)) errs['rule.indicator'] = [t.validation.indicatorRequired];
    if (leaves.some((l) => !l.condition.ticker)) errs['rule.ticker'] = [t.validation.instrumentRequired];
    if (Object.keys(errs).length > 0) {
      setFieldErrors(errs);
      scrollToRef(triggerSectionRef);
      return;
    }

    if (outputs.length === 0) {
      setFieldErrors({ notification: [t.notifications.atLeastOneChannel] });
      scrollToRef(notificationSectionRef);
      return;
    }

    if (outputs.some((o) => o.channel === 'WebPush' && !o.pushSubscriptionId)) {
      // A field error, not the form-level error: the row is fixable (enable push - the
      // section backfills the id - or drop the row), so the submit must stay enabled
      setFieldErrors({ notification: [t.notifications.notEnabled] });
      scrollToRef(notificationSectionRef);
      return;
    }

    // URL/phone destinations are validated before save so the row turns red here
    // instead of bouncing off the server's validation
    const invalidTarget = outputs.find(
      (o) => (isUrlChannel(o.channel) && !isValidTargetUrl(o.target)) || (isWhatsApp(o.channel) && normalizeWhatsAppPhone(o.target) === null),
    );
    if (invalidTarget) {
      setFieldErrors({ notification: [isWhatsApp(invalidTarget.channel) ? t.validation.whatsappPhoneInvalid : t.validation.webhookUrlInvalid] });
      scrollToRef(notificationSectionRef);
      return;
    }

    setSaving(true);
    try {
      const first = conditionToApi(leaves[0].condition);
      const ruleRequest = toCreateRuleRequest(ruleModel);

      if (isEdit && subId) {
        // The rule carries every indicator/instrument/parameter change (updateRule runs
        // first); the subscription patch only touches the subscription's own fields —
        // the check cooldown and the alert outputs
        const origTicker = collectLeafConditions(originalRuleModelRef.current.group)[0]?.condition.ticker ?? '';

        if (!originalRuleIdRef.current) {
          // Every subscription is a container of a rule; without a resolvable rule the
          // edit cannot be persisted
          throw new Error(t.errors.failedToSave);
        }

        await api.updateRule(originalRuleIdRef.current, ruleRequest);

        const ops = [
          { op: 'replace', path: '/CooldownSeconds', value: cooldown.cooldownMinutes * 60 },
          { op: 'replace', path: '/IgnoreExtendedSessions', value: ignoreExtendedSessions },
          {
            op: 'replace',
            path: '/Outputs',
            value: outputs.map((o) => ({
              id: o.id ?? uuidv7(),
              channel: o.channel,
              notificationTemplateId: o.templateId,
              pushSubscriptionId: o.channel === 'WebPush' ? o.pushSubscriptionId : null,
              chatId: o.channel === 'Telegram' ? o.chatId : null,
              target: outputTarget(o),
              isEnabled: true,
            })),
          },
        ];

        await api.patchSubscription(subId, ops);

        originalRuleModelRef.current = structuredClone(ruleModel);
        originalCooldownRef.current = { ...cooldown };
        originalIgnoreExtendedRef.current = ignoreExtendedSessions;
        originalOutputs.current = outputs.map((o) => ({ ...o }));
        // the server copy changed: the cached subscription/detail views must not
        // keep showing the pre-save state within their freshness window
        queryClient.invalidateQueries({ queryKey: qk.subscription(subId) });
        queryClient.invalidateQueries({ queryKey: qk.subscriptions });
        toast({ title: t.common.saved, variant: 'success' });

        // The URL embeds the old ticker; after an instrument change it is stale, so land
        // on the subscription detail under the new one
        if (origTicker !== first.ticker) {
          navigate(`${prefix}/subscriptions/${encodeURIComponent(first.ticker)}/${subId}`);
        }
      } else {
        await api.createSubscription(first.ticker, first.exchangeName ?? undefined, {
          cooldownSeconds: cooldown.cooldownMinutes * 60,
          ignoreExtendedSessions,
          outputs: outputs.map((o) => ({
            channel: o.channel,
            notificationTemplateId: o.templateId,
            pushSubscriptionId: o.channel === 'WebPush' ? o.pushSubscriptionId : null,
            chatId: o.channel === 'Telegram' ? o.chatId : null,
            target: outputTarget(o),
          })),
          rule: ruleRequest,
        });
        queryClient.invalidateQueries({ queryKey: qk.subscriptions });
        toast({ title: t.common.created, variant: 'success' });
        navigate(`${prefix}/subscriptions`);
      }
    } catch (e: unknown) {
      // The alert was deleted in another window/device while this form was open: the
      // save's first call (the rule update) 404s on the cascade-deleted rule. There is
      // nothing left to save into - report it and leave the form for the list (no
      // unsaved-changes prompt: the target is gone; no removeQueries either - the load
      // effect would refetch the removed active query, see the load-effect comment)
      if (e instanceof ApiError && e.statusCode === 404 && isEdit && subId) {
        goneHandledRef.current = true;
        queryClient.invalidateQueries({ queryKey: qk.subscriptions });
        toast({ title: t.errors.subscriptionGone, variant: 'destructive' });
        navigate(`${prefix}/subscriptions`);
        return;
      }

      const errType = classifyApiError(e);
      let message: string;
      if (errType === 'network') message = t.errors.noInternet;
      else if (errType === 'api') message = e instanceof ApiError && e.message ? e.message : t.errors.apiUnavailable;
      else message = e instanceof Error ? e.message : t.errors.failedToSave;

      // Structured validation errors (backend "errorDetails") — localized from the user's
      // language and shown in the trigger section. As a field error it also keeps the submit
      // button enabled for a retry after fixing.
      if (e instanceof ApiError && e.statusCode === 400 && e.details && e.details.length > 0) {
        const localized = localizeValidationErrors(e.details, t);
        if (localized.length > 0) {
          setFieldErrors({ 'rule.parameters': [localized.join('; ')] });
          scrollToRef(triggerSectionRef);
          return;
        }
      }

      // A parameter validation error belongs to the trigger section (the fields live there);
      // as a field error it also keeps the submit button enabled for a retry after fixing
      if (e instanceof ApiError && e.statusCode === 400 && PARAM_VALIDATION_ERROR.test(message)) {
        setFieldErrors({ 'rule.parameters': [message] });
        scrollToRef(triggerSectionRef);
        return;
      }

      setError(message);
      scrollToRef(formErrorRef);
    } finally {
      setSaving(false);
    }
  }, [ruleModel, cooldown, ignoreExtendedSessions, outputs, isEdit, subId, navigate, prefix, toast, t, config, scrollToRef, queryClient]);

  // The arrow behaves like the browser back (it usually came from the page
  // that opened the form); cancelUrl stays as the deep-link fallback
  const handleBack = () => {
    confirmNavigation(() => goBackOr(cancelUrl));
  };

  // Revert all edits (conditions, cooldown, notifications) back to the loaded baseline.
  const handleReset = () => {
    setRuleModel(structuredClone(originalRuleModelRef.current));
    setCooldown({ ...originalCooldownRef.current });
    setIgnoreExtendedSessions(originalIgnoreExtendedRef.current);
    setOutputs(originalOutputs.current.map((o) => ({ ...o })));
    setError(null);
    setFieldErrors({});
  };

  const getError = (field: string) => fieldErrors[field]?.[0];

  // Preset one-tap: pre-fill the constructor with the preset's whole condition tree
  // (a combo replaces the group), keep the already-selected instrument and move the
  // dirty-tracking baseline so a freshly applied preset is not shown as an edit
  const applyPreset = useCallback((preset: RulePresetDto) => {
    const leaf = collectLeafConditions(ruleModel.group)[0];
    const ticker = leaf?.condition.ticker.trim() || preselectedTicker || '';
    const exchange = leaf?.condition.exchange ?? preselectedExchange;
    const model: RuleFormModel = {
      group: presetGroupToModel(preset.when, ticker, exchange),
      thenTrue: [{ id: uuidv7(), type: 'SendNotification' }],
      thenFalse: [],
      name: preset.name.slice(0, MAX_RULE_NAME_LENGTH),
    };
    setRuleModel(model);
    originalRuleModelRef.current = structuredClone(model);
    setActivePresetKey(preset.key);
    setFieldErrors({});
  }, [ruleModel, preselectedTicker, preselectedExchange]);

  const handlePresetPick = useCallback((preset: RulePresetDto) => {
    applyPreset(preset);
  }, [applyPreset]);

  // Deep link (?preset=): applied once as soon as the catalog resolves
  const presetAppliedRef = useRef(false);
  useEffect(() => {
    if (!presetParam || presetAppliedRef.current || !presetsQuery.data) return;
    const preset = presetsQuery.data.find((p) => p.key === presetParam);
    if (!preset) return;
    presetAppliedRef.current = true;
    applyPreset(preset);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [presetParam, presetsQuery.data]);

  const handleCooldownChange = (minutes: number) => {
    setCooldown({ ...cooldown, cooldownMinutes: minutes });
  };

  const handleInstrumentSelect = useCallback((entryId: string, item: InstrumentSelectItem) => {
    // Recents come from localStorage and may never have been fetched in this session;
    // keep the cache complete so the trigger label resolves the instrument name
    instrumentCacheRef.current.set(instrumentKey(item.ticker, item.exchangeName), item);
    setRuleModel((prev) => ({
      ...prev,
      group: mapLeafConditions(prev.group, (e: RuleConditionEntry) =>
        e.id === entryId
          ? { ...e, condition: { ...e.condition, ticker: item.ticker, exchange: item.exchangeName ?? undefined } }
          : e),
    }));
  }, []);

  // New conditions default to the first condition's instrument; the first condition also
  // owns the instrument picker while the plan keeps a single instrument per rule
  // Crypto instruments trade 24/7 and their venues never have extended sessions, so the
  // opt-out checkbox is hidden when every instrument of the rule is crypto. The details
  // come from the shared instrument cache (the same qk.instrument the detail page warms).
  const ruleInstruments = useMemo(() => {
    const seen = new Map<string, { ticker: string; exchangeName: string | null }>();
    for (const leaf of collectLeafConditions(ruleModel.group)) {
      const ticker = leaf.condition.ticker.trim();
      if (!ticker) continue;
      const exchangeName = leaf.condition.exchange ?? null;
      const key = instrumentKey(ticker, exchangeName);
      if (!seen.has(key)) seen.set(key, { ticker, exchangeName });
    }
    return [...seen.values()];
  }, [ruleModel]);

  const instrumentDetails = useQueries({
    queries: ruleInstruments.map((instrument) => ({
      queryKey: qk.instrument(instrument.ticker, instrument.exchangeName),
      queryFn: () => api.getInstrument(instrument.ticker, instrument.exchangeName),
      enabled: loaded && isAuthenticated,
    })),
  });

  const allInstrumentsCrypto = ruleInstruments.length > 0
    && instrumentDetails.length === ruleInstruments.length
    && instrumentDetails.every((query) => query.data?.ticker && /crypto/i.test(query.data.type ?? ''));

  const firstLeaf = collectLeafConditions(ruleModel.group)[0];
  const firstLeafTicker = firstLeaf?.condition.ticker ?? '';
  const firstLeafExchange = firstLeaf?.condition.exchange ?? null;
  const redundantCount = findRedundantConditions(ruleModel.group);

  // Blank while auth is unresolved or anonymous (MainLayout redirects to login);
  // the fetches above run in parallel, so authenticated loads lose no time
  if (!loaded || !isAuthenticated) return null;

  if (loading) {
    return (
      <div className="flex flex-col h-[var(--app-page-h-bar)] md:h-[var(--app-page-h)]" role="status">
        <span className="sr-only">{t.common.loading}</span>
        <div className="flex items-center gap-3">
          <Skeleton className="h-10 w-10 rounded-md flex-shrink-0" />
          <Skeleton className="h-7 w-40" />
        </div>
      <div ref={scrollAreaRef} className="relative mt-6 flex-1 min-h-0 -mx-1.5 px-1.5 overflow-y-auto pb-2 space-y-6">
          <div className="rounded-lg border p-4">
            <Skeleton className="h-3 w-24" />
            <Skeleton className="mt-1.5 h-3 w-48" />
            <div className="mt-4 space-y-1.5">
              <Skeleton className="h-3 w-20" />
              <Skeleton className="h-10 w-full" />
            </div>
            <Skeleton className="mt-4 h-24 w-full rounded-lg" />
          </div>
          <div className="rounded-lg border p-4">
            <Skeleton className="h-3 w-28" />
            <Skeleton className="mt-1.5 h-3 w-40" />
            <div className="mt-4 space-y-2">
              <Skeleton className="h-11 w-full" />
              <Skeleton className="h-11 w-full" />
            </div>
          </div>
        </div>
      </div>
    );
  }

  // "Изменено" markers are only meaningful when editing an existing subscription
  const showChanged = isEdit;

  return (
    <div className="flex flex-col h-[var(--app-page-h-bar)] md:h-[var(--app-page-h)]">
      {/* Fixed viewport-fit height, the same pattern as the instruments/subscriptions list views:
          the page root is h-[var(--app-page-h-bar)] so it ends exactly above the fixed bottom
          action bar (--bar-h) that sits on the bottom nav (--nav-h) — see index.css tokens;
          the page must never scroll the browser window */}
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" onClick={handleBack}>
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <h1 className="text-xl md:text-2xl font-bold">{isEdit ? t.form.editTitle : t.form.createTitle}</h1>
      </div>

      <div className="relative mt-6 flex-1 min-h-0 -mx-1.5 px-1.5 overflow-y-auto pb-2 space-y-6">
        {/* TRIGGER (WHEN) */}
        <section ref={triggerSectionRef} className={`rounded-lg border p-4 transition-colors ${showChanged && (changed.trigger || changed.cooldown || changed.ignoreExtended) ? 'border-primary/50 bg-primary/[0.04]' : ''}`}>
          <div className="mb-3">
            <div className="flex items-center gap-2">
              <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">{t.rules.title}</p>
              {showChanged && (changed.trigger || changed.cooldown || changed.ignoreExtended) && <span className="text-[10px] font-medium text-primary bg-primary/10 px-1.5 py-0.5 rounded">{t.common.updated}</span>}
            </div>
            <p className="text-xs text-muted-foreground mt-0.5">{t.rules.hint}</p>
          </div>
          <div className="space-y-1.5 mb-4">
            <div className="flex items-center justify-between">
              <Label htmlFor="rule-name" className="text-xs font-medium text-muted-foreground">{t.rules.name}</Label>
              <span className="text-[11px] text-muted-foreground tabular-nums">{ruleModel.name.length}/{MAX_RULE_NAME_LENGTH}</span>
            </div>
            <Input
              id="rule-name"
              value={ruleModel.name}
              onChange={(e) => setRuleModel({ ...ruleModel, name: e.target.value })}
              placeholder={buildRuleName(ruleModel.group)}
              maxLength={MAX_RULE_NAME_LENGTH}
            />
            <p className="text-xs text-muted-foreground">{t.rules.nameHint}</p>
          </div>
          {!isEdit && (
            <div className="mb-4" data-tour="presets">
              <PresetPicker
                presets={presets}
                activeKey={activePresetKey}
                onPick={handlePresetPick}
                t={t}
              />
            </div>
          )}
          {/* the condition builder is one tour anchor ("conditions"), a plain box changes no layout */}
          <div data-tour="conditions">
            <RuleGroupEditor
              group={ruleModel.group}
              onChange={(group) => {
                setRuleModel({ ...ruleModel, group });
                // a manual edit of the tree detaches it from the applied preset
                if (activePresetKey) setActivePresetKey(null);
              }}
              indicators={indicators}
              locale={locale}
              t={t}
              defaultTicker={preselectedTicker ?? firstLeafTicker}
              defaultExchange={preselectedExchange ?? firstLeafExchange}
              disableTicker={lockInstrument}
              originalGroup={originalRuleModelRef.current.group}
              searchInstruments={searchInstruments}
              loadDefaultInstruments={loadDefaultInstruments}
              resolveInstrument={resolveInstrument}
              onInstrumentSelect={handleInstrumentSelect}
              maxConditions={config?.maxConditionsPerRule}
              maxGroupDepth={config?.maxGroupDepth}
            />
          </div>
          {getError('rule') && <p className="text-xs text-destructive mt-1">{getError('rule')}</p>}
          {getError('rule.indicator') && <p className="text-xs text-destructive mt-1">{getError('rule.indicator')}</p>}
          {getError('rule.ticker') && <p className="text-xs text-destructive mt-1">{getError('rule.ticker')}</p>}
          {getError('rule.parameters') && <p className="text-xs text-destructive mt-1">{getError('rule.parameters')}</p>}
          {redundantCount > 0 && <p className="text-xs text-amber-600 dark:text-amber-400 mt-1">{t.rules.redundant}</p>}

          {/* Log-scale slider + manual minute input (the deviation field pattern) */}
          <div className="mt-5 space-y-1.5" data-tour="cooldown">
            <Label htmlFor="cooldown" className="text-xs font-medium text-muted-foreground">{t.form.cooldownLabel}</Label>
            <CooldownField
              id="cooldown"
              value={cooldown.cooldownMinutes}
              onChange={handleCooldownChange}
              t={t}
            />
          </div>

          {/* Hidden on crypto-only rules: 24/7 venues have no extended sessions to ignore */}
          {!allInstrumentsCrypto && (
            <div className="mt-5">
              <label className="flex items-start gap-2.5 text-sm text-muted-foreground cursor-pointer select-none">
                <input
                  type="checkbox"
                  className="mt-0.5 h-4 w-4 shrink-0 accent-primary"
                  checked={ignoreExtendedSessions}
                  onChange={(e) => setIgnoreExtendedSessions(e.target.checked)}
                />
                <span>
                  {t.form.ignoreExtendedLabel}
                  <span className="block text-xs mt-0.5">{t.form.ignoreExtendedHint}</span>
                </span>
              </label>
            </div>
          )}
        </section>

        {/* NOTIFICATION */}
        <section data-tour="notifications" ref={notificationSectionRef} className={`rounded-lg border p-4 transition-colors ${showChanged && changed.notification ? 'border-primary/50 bg-primary/[0.04]' : ''}`}>
          <div className="mb-3">
            <div className="flex items-center gap-2">
              <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">{t.notifications.title}</p>
              {showChanged && changed.notification && <span className="text-[10px] font-medium text-primary bg-primary/10 px-1.5 py-0.5 rounded">{t.common.updated}</span>}
            </div>
            <p className="text-xs text-muted-foreground mt-0.5">{t.notifications.hint}</p>
          </div>
          <NotificationsSection
            outputs={outputs}
            onOutputsChange={setOutputs}
            t={t}
            whatsappEnabled={config?.whatsappEnabled ?? false}
            maxOutputs={maxOutputs}
          />
          {getError('notification') && <p className="text-xs text-destructive mt-2">{getError('notification')}</p>}
        </section>

        <div ref={formErrorRef}>
          {error && <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert>}
        </div>

        {/* Desktop actions */}
        <div className="hidden md:flex justify-end gap-2 pt-4 border-t">
          {isEdit && hasChanges && (
            <Button variant="outline" onClick={handleReset} disabled={saving}>
              {t.common.reset}
            </Button>
          )}
          <Button data-tour="create" onClick={handleSave} disabled={saving || loadFailed || (isEdit && !hasChanges)}>
            {saving && <Loader2 className="h-4 w-4 animate-spin" />}
            {isEdit ? t.common.save : t.common.create}
          </Button>
        </div>

        {/* the tour blocker swallows touches, so the user cannot scroll; the notifications
            section is the last content block and the scroll container cannot lift its head
            into the spotlight once maxScroll is reached (on tall phones the whole content
            is barely scrollable) - a tail strip gives the tour's last steps room to move */}
        {showCreateTour && <div aria-hidden className="h-[40dvh]" />}
      </div>

      <OnboardingTour open={showCreateTour} steps={createTourSteps} onDone={completeSubscriptionTour} />

      {/* Mobile sticky bottom actions; hidden while the soft keyboard is up (it would
          sit behind the keys; --bar-h collapses in index.css so the form reclaims it) */}
      {!keyboardOpen && (
        <div className="md:hidden fixed bottom-[var(--nav-h)] left-0 right-0 z-30 flex items-center gap-2 p-3 bg-background/95 backdrop-blur border-t">
          {isEdit && hasChanges && (
            <Button variant="outline" className="h-11" onClick={handleReset} disabled={saving}>
              {t.common.reset}
            </Button>
          )}
          <Button data-tour="create" className="h-11 flex-1" onClick={handleSave} disabled={saving || loadFailed || (isEdit && !hasChanges)}>
            {saving && <Loader2 className="h-4 w-4 animate-spin" />}
            {isEdit ? t.common.save : t.common.create}
          </Button>
        </div>
      )}
    </div>
  );
}
