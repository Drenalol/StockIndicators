import { useCallback, useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, ExternalLink, Eye, EyeOff, Loader2, PlugZap, Trash2, TriangleAlert, Undo2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Hint, HintContent, HintTrigger } from '@/components/ui/hint';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { useNavigationGuard } from '@/components/layout/NavigationGuardProvider';
import { useToast } from '@/hooks/use-toast';
import { useTranslation } from '@/i18n';
import type { Translations } from '@/i18n';
import { api, ApiError } from '@/services/api';
import { classifyPublicUrl } from '@/lib/publicUrl';
import { qk } from '@/lib/query';
import type { ProviderDto, ProviderFieldDto, ProviderTestResultDto } from '@/types/contracts';

/**
 * The BYOK panel: every provider the app knows, its enable toggle and its
 * credential fields. Values live in the database; the environment (compose
 * variables) stays the fallback whenever a field is not stored. Enabling and
 * keys take effect at the next start - which providers exist is decided while
 * the backend composes itself - so the panel says so on every save.
 */

interface Draft {
  enabled: boolean;
  values: Record<string, string>;
  clear: string[];
}

function draftOf(provider: ProviderDto): Draft {
  return {
    enabled: provider.enabled,
    values: Object.fromEntries(provider.fields.map((f) => [f.name, f.value ?? ''])),
    clear: [],
  };
}

// same dirty rule the save button greys on - reused for the card highlight and the
// unsaved-changes navigation guard, so the three never disagree
function isDirty(provider: ProviderDto, draft: Draft): boolean {
  return draft.enabled !== provider.enabled
    || draft.clear.length > 0
    || provider.fields.some((f) => (draft.values[f.name] ?? '') !== (f.value ?? ''));
}

// display copy for the provider cards, keyed by provider code: the backend registry stays
// English (it is one locale-free service), the locales name providers, markets and fields.
// Missing entries fall back to the backend labels, so a newly registered provider still
// renders before the translations catch up.
interface ProviderCopy {
  name: string;
  markets: string[];
  fields: Record<string, string>;
}

function providerCopy(t: Translations): Record<string, ProviderCopy> {
  return t.settings.providers.items as Record<string, ProviderCopy>;
}

// Registry field names the page couples by hand: the bot's link toggle decides whether the
// instance address is used at all, so it also decides whether that address is worth checking.
const WEB_LINKS_FIELD = 'IncludeWebLinks';
const INSTANCE_URL_FIELD = 'InstanceUrl';

// amber marker: the stored config and the running one disagree until the next start
function RestartPendingBadge() {
  const { t } = useTranslation();

  return (
    <Hint>
      <HintTrigger asChild>
        <span tabIndex={0} className="inline-flex cursor-help text-amber-500" aria-label={t.settings.providers.restartPending}>
          <TriangleAlert className="h-4 w-4" />
        </span>
      </HintTrigger>
      <HintContent side="bottom" align="center" className="max-w-64">{t.settings.providers.restartPending}</HintContent>
    </Hint>
  );
}

export default function ProvidersSection() {
  const { t } = useTranslation();
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const providersQuery = useQuery({
    queryKey: qk.providers,
    queryFn: () => api.getProviders(),
  });
  const providers = useMemo(() => providersQuery.data ?? [], [providersQuery.data]);

  // Local drafts keyed by provider code; a refetch (save) reseeds them from the server
  const [drafts, setDrafts] = useState<Record<string, Draft>>({});
  // Which providers' secret inputs are shown in plain text (secrets stay dotted until revealed)
  const [revealedCodes, setRevealedCodes] = useState<Record<string, boolean>>({});
  const [results, setResults] = useState<Record<string, ProviderTestResultDto | 'testing'>>({});
  const draftFor = useCallback((provider: ProviderDto): Draft => drafts[provider.code] ?? draftOf(provider), [drafts]);

  // the subscription form's guard: while any card holds edits, navigation, tab close
  // and refresh all confirm before dropping them
  const { setGuard } = useNavigationGuard();
  const anyDirty = useMemo(
    () => providers.some((p) => drafts[p.code] !== undefined && isDirty(p, drafts[p.code])),
    [providers, drafts],
  );
  useEffect(() => {
    if (!anyDirty) return;
    setGuard({ message: t.common.unsavedChangesMessage });
    return () => setGuard(null);
  }, [anyDirty, setGuard, t.common.unsavedChangesMessage]);

  const patchDraft = (code: string, patch: Partial<Draft>, provider: ProviderDto) =>
    setDrafts((prev) => ({ ...prev, [code]: { ...draftOf(providers.find(p => p.code === code) ?? provider), ...prev[code], ...patch } }));

  // dropping the draft makes draftFor fall back to the stored (server) values again
  const revertDraft = (code: string) =>
    setDrafts((prev) => {
      const next = { ...prev };
      delete next[code];
      return next;
    });

  const saveMutation = useMutation({
    mutationFn: ({ provider, draft }: { provider: ProviderDto; draft: Draft }) => {
      const credentials: Record<string, string | null> = {};
      for (const field of provider.fields) {
        const value = draft.values[field.name];
        if (value !== (field.value ?? '')) credentials[field.name] = value;
      }
      return api.saveProvider(provider.code, {
        isEnabled: draft.enabled,
        credentials: Object.keys(credentials).length ? credentials : {},
        clearFields: draft.clear.length ? draft.clear : undefined,
      });
    },
    onSuccess: () => {
      setDrafts({});
      void queryClient.invalidateQueries({ queryKey: qk.providers });
      toast({ title: t.settings.providers.saved, variant: 'success' });
    },
    onError: (e) => {
      toast({ title: e instanceof Error && e.message ? e.message : t.settings.providers.saveFailed, variant: 'destructive' });
    },
  });

  const handleTest = useCallback(async (provider: ProviderDto, draft: Draft) => {
    setResults((prev) => ({ ...prev, [provider.code]: 'testing' }));
    try {
      // what is on screen, saved or not: a typo surfaces before it is stored
      const credentials: Record<string, string> = {};
      for (const field of provider.fields) {
        const value = draft.values[field.name]?.trim();
        if (value) credentials[field.name] = value;
      }
      const result = await api.testProvider(provider.code, Object.keys(credentials).length ? credentials : undefined);
      setResults((prev) => ({ ...prev, [provider.code]: result }));
    } catch (e) {
      setResults((prev) => ({
        ...prev,
        [provider.code]: { success: false, message: e instanceof ApiError && e.message ? e.message : t.settings.providers.testFailed, latencyMs: 0 },
      }));
    }
  }, [t]);

  const sections = useMemo(() => ([
    { kind: 'data', title: t.settings.providers.kindData },
    { kind: 'enricher', title: t.settings.providers.kindEnricher },
    { kind: 'channel', title: t.settings.providers.kindChannel },
    { kind: 'auxiliary', title: t.settings.providers.kindAuxiliary },
  ] as const)
    .map((section) => ({ ...section, items: providers.filter((p) => p.kind === section.kind) }))
    .filter((section) => section.items.length > 0), [providers, t]);

  const anyRestartPending = providers.some((p) => p.restartPending);

  if (providersQuery.isPending) {
    return (
      <div className="space-y-3" role="status">
        <span className="sr-only">{t.common.loading}</span>
        {[0, 1].map((i) => <div key={i} className="h-28 rounded-lg border bg-muted/30" />)}
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="flex items-center gap-2 text-lg font-semibold">
          {t.settings.providers.title}
          {anyRestartPending && <RestartPendingBadge />}
        </h2>
        <p className="text-sm text-muted-foreground">{t.settings.providers.description}</p>
      </div>

      {sections.map((section) => (
        <div key={section.kind} className="space-y-3">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{section.title}</h3>
          {section.items.map((provider) => {
            // the registry's copy is English; locales name providers by code and the
            // backend labels stay the fallback for codes the locales have not caught up with
            const copy = providerCopy(t)[provider.code];
            const draft = draftFor(provider);
            const dirty = isDirty(provider, draft);
            const result = results[provider.code];
            // while the bot carries no links, the instance address is dead config: nothing is
            // built from it, so nothing about it is worth asking the operator about
            const webLinksOn = draft.values[WEB_LINKS_FIELD] === 'true';

            return (
              <Card key={provider.code} className={`transition-colors${dirty ? ' border-primary/50 bg-primary/[0.04]' : ''}`}>
                <CardHeader>
                  <div className="flex items-center gap-3">
                    <CardTitle className="flex items-center gap-2 text-base">
                      {copy?.name ?? provider.displayName}
                      {dirty && <span className="text-[10px] font-medium text-primary bg-primary/10 px-1.5 py-0.5 rounded">{t.common.updated}</span>}
                      {provider.configured
                        ? <Badge variant="success">{t.settings.providers.configured}</Badge>
                        : <Badge variant="outline" className="text-amber-600 border-amber-500/60 dark:text-amber-400"><AlertTriangle className="h-3 w-3 mr-1" />{t.settings.providers.needsKey}</Badge>}
                      {provider.autoConfigured && <Badge variant="secondary">{t.settings.providers.auto}</Badge>}
                      {provider.restartPending && <RestartPendingBadge />}
                      {provider.fields.some((f) => f.kind === 'secret') && (
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          className="h-7 w-7 text-muted-foreground"
                          aria-label={revealedCodes[provider.code] ? t.settings.providers.hideSecrets : t.settings.providers.showSecrets}
                          title={revealedCodes[provider.code] ? t.settings.providers.hideSecrets : t.settings.providers.showSecrets}
                          onClick={() => setRevealedCodes((prev) => ({ ...prev, [provider.code]: !prev[provider.code] }))}
                        >
                          {revealedCodes[provider.code] ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                        </Button>
                      )}
                    </CardTitle>
                    <div className="ml-auto flex items-center gap-2">
                      <Label htmlFor={`enable-${provider.code}`} className="text-xs text-muted-foreground">{t.settings.providers.enabled}</Label>
                      <Switch
                        id={`enable-${provider.code}`}
                        checked={draft.enabled}
                        onCheckedChange={(checked) => patchDraft(provider.code, { enabled: checked }, provider)}
                      />
                    </div>
                  </div>
                  <CardDescription>
                    {(copy?.markets ?? provider.markets).join(' · ')}
                    {provider.docsUrl && provider.fields.some((f) => f.kind === 'secret') && (
                      <>
                        {' · '}
                        <a href={provider.docsUrl} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-0.5 underline underline-offset-2">
                          {t.settings.providers.docs}<ExternalLink className="h-3 w-3" />
                        </a>
                      </>
                    )}
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-3">
                  {provider.fields.length > 0 && (
                    <div className={`grid gap-3${draft.enabled ? '' : ' opacity-60'}`}>
                      {provider.fields.map((field) => (
                        <ProviderFieldRow
                          key={field.name}
                          provider={provider}
                          field={field}
                          label={copy?.fields[field.name] ?? field.label}
                          required={field.name === INSTANCE_URL_FIELD ? webLinksOn || field.required : field.required}
                          checkUrl={field.name !== INSTANCE_URL_FIELD || webLinksOn}
                          draft={draft}
                          revealed={!!revealedCodes[provider.code]}
                          disabled={!draft.enabled}
                          onPatch={(patch) => patchDraft(provider.code, patch, provider)}
                        />
                      ))}
                    </div>
                  )}
                  {result === 'testing' && (
                    <p className="flex items-center gap-2 text-sm text-muted-foreground"><Loader2 className="h-3.5 w-3.5 animate-spin" />{t.settings.providers.testing}</p>
                  )}
                  {result && result !== 'testing' && (
                    <p className={`text-sm ${result.success ? 'text-green-600 dark:text-green-400' : 'text-destructive'}`}>
                      {result.success
                        ? t.settings.providers.testSuccess.replace('{{ms}}', String(result.latencyMs)).replace('{{message}}', result.message)
                        : t.settings.providers.testFailure.replace('{{message}}', result.message)}
                    </p>
                  )}
                  <div className="flex items-center justify-end gap-2">
                    <Hint>
                      <HintTrigger asChild>
                        {/* aria-disabled instead of disabled: a natively disabled button swallows pointer
                            events, so the hint would never open - neither on hover nor on touch */}
                        <Button
                          type="button"
                          variant="outline"
                          size="icon"
                          className="size-11 aria-disabled:opacity-50 md:size-10"
                          aria-disabled={!draft.enabled || result === 'testing' || undefined}
                          aria-label={t.settings.providers.test}
                          onClick={() => {
                            if (!draft.enabled || result === 'testing') return;
                            void handleTest(provider, draft);
                          }}
                        >
                          {result === 'testing' ? <Loader2 className="h-4 w-4 animate-spin" /> : <PlugZap className="h-4 w-4" />}
                        </Button>
                      </HintTrigger>
                      <HintContent side="top">{t.settings.providers.test}</HintContent>
                    </Hint>
                    <Hint>
                      <HintTrigger asChild>
                        <Button
                          type="button"
                          variant="outline"
                          size="icon"
                          className="size-11 aria-disabled:opacity-50 md:size-10"
                          aria-disabled={!dirty || undefined}
                          aria-label={t.settings.providers.revert}
                          onClick={() => {
                            if (dirty) revertDraft(provider.code);
                          }}
                        >
                          <Undo2 className="h-4 w-4" />
                        </Button>
                      </HintTrigger>
                      <HintContent side="top">{t.settings.providers.revert}</HintContent>
                    </Hint>
                    <Button
                      className="h-11 md:h-10"
                      disabled={!dirty || saveMutation.isPending}
                      onClick={() => saveMutation.mutate({ provider, draft })}
                    >
                      {saveMutation.isPending && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
                      {t.settings.providers.save}
                    </Button>
                  </div>
                  <p className="text-right text-xs text-muted-foreground">{t.settings.providers.restartNote}</p>
                </CardContent>
              </Card>
            );
          })}
        </div>
      ))}
    </div>
  );
}

// the docs link is labelled "get a key", so it renders only where a secret is expected -
// keyless providers (Binance public candles, Yahoo enrichment) would just show noise
function ProviderFieldRow({ provider, field, label, required, checkUrl, draft, revealed, disabled, onPatch }: {
  provider: ProviderDto;
  field: ProviderFieldDto;
  label: string;
  /** the red asterisk: the registry's flag, or the instance address while links are on */
  required: boolean;
  /** whether this address is in use right now: an unused one is nobody's problem */
  checkUrl: boolean;
  draft: Draft;
  revealed: boolean;
  disabled: boolean;
  onPatch: (patch: Partial<Draft>) => void;
}) {
  const { t } = useTranslation();
  const value = draft.values[field.name] ?? '';
  const cleared = draft.clear.includes(field.name);
  const inputId = `${provider.code}-${field.name}`;

  // an address an outside service or a phone has to open: Telegram refuses a button whose
  // URL it cannot reach, so a loopback or LAN value is worth flagging while it is typed.
  // null = the value is not in use (or is not an address), so nothing is worth saying about it
  const urlState = field.kind === 'url' && checkUrl && !disabled ? classifyPublicUrl(value) : null;
  // an empty address only matters when something has to be built from it
  const urlIsError = urlState === 'invalid' || (urlState === 'empty' && required);
  const urlIsLocal = urlState === 'local';
  const urlHintId = `${inputId}-hint`;
  const urlHint = urlIsError
    ? (urlState === 'empty' ? t.settings.providers.urlRequired : t.settings.providers.urlInvalid)
    : urlIsLocal
      ? t.settings.providers.urlNeedsPublic
      : null;

  const setValue = (next: string) => {
    // emptying a stored field means removing it (the backend drops blanks): show the same
    // "will be removed on save" state the Clear button sets, and undo it once text is back
    const clear = next === '' && field.hasStored
      ? [...new Set([...draft.clear, field.name])]
      : draft.clear.filter((n) => n !== field.name);
    onPatch({ values: { ...draft.values, [field.name]: next }, clear });
  };

  return (
    <div className="grid gap-1.5">
      <div className="flex items-center justify-between gap-2">
        <Label htmlFor={inputId} className="text-sm">
          {label}
          {required && <span className="text-destructive"> *</span>}
          {field.hasEnv && <span className="ml-1 text-xs font-normal text-muted-foreground">({t.settings.providers.fromEnv})</span>}
        </Label>
        {field.kind === 'secret' && field.hasStored && !cleared && (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            disabled={disabled}
            className="h-6 px-2 text-xs text-muted-foreground"
            onClick={() => onPatch({ clear: [...draft.clear, field.name], values: { ...draft.values, [field.name]: '' } })}
          >
            <Trash2 className="h-3 w-3 mr-1" />
            {t.settings.providers.clear}
          </Button>
        )}
      </div>
      {field.kind === 'bool' ? (
        <div className="flex items-center gap-2 pt-1">
          <Switch
            id={inputId}
            disabled={disabled}
            checked={value === 'true'}
            onCheckedChange={(checked) => setValue(checked ? 'true' : 'false')}
          />
        </div>
      ) : field.kind === 'secret' ? (
        <Input
          id={inputId}
          type={revealed ? 'text' : 'password'}
          autoComplete="off"
          spellCheck={false}
          disabled={disabled}
          value={value}
          onChange={(e) => setValue(e.target.value)}
          placeholder={cleared ? t.settings.providers.cleared : t.settings.providers.enterValue}
        />
      ) : field.kind === 'choice' ? (
        // the draft starts at the server-computed value; an untouched dropdown saves nothing
        <Select value={value || field.value || field.options?.[0] || ''} onValueChange={setValue}>
          <SelectTrigger id={inputId} disabled={disabled} className="w-full">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {(field.options ?? []).map(option => (
              <SelectItem key={option} value={option}>{option}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      ) : (
        <Input
          id={inputId}
          type={field.kind === 'int' ? 'number' : 'text'}
          inputMode={field.kind === 'url' ? 'url' : undefined}
          spellCheck={field.kind === 'url' ? false : undefined}
          disabled={disabled}
          value={value}
          onChange={(e) => setValue(e.target.value)}
          aria-invalid={urlIsError || undefined}
          aria-describedby={urlHint ? urlHintId : undefined}
          placeholder={field.hasStored ? undefined : t.settings.providers.enterValue}
        />
      )}
      {urlHint && (
        <p id={urlHintId} className={`flex items-start gap-1.5 text-xs ${urlIsError ? 'text-destructive' : 'text-amber-600 dark:text-amber-400'}`}>
          <AlertTriangle className="mt-0.5 h-3 w-3 shrink-0" aria-hidden="true" />
          {urlHint}
        </p>
      )}
    </div>
  );
}
