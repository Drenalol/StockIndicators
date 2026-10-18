import { useCallback, useEffect, useMemo, useRef, useState, type ReactElement, type ReactNode } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Trash2, Copy, Send, Plus, Loader2, Languages, ChevronsUpDown, AlertTriangle, Scale, RefreshCw, FileText, LogOut, CircleHelp, Settings2, Database, BellRing, Info } from 'lucide-react';
import { account, ApiError } from '@/services/api';
import { qk } from '@/lib/query';
import { useAuth } from '@/hooks/useAuth';
import { useToast } from '@/hooks/use-toast';
import { useTranslation, getTranslations, SUPPORTED_LOCALES, LOCALE_NAMES, type Locale } from '@/i18n';
import { getBrowserTimezone, getTimezoneGroups, formatTzLabel } from '@/lib/timezones';
import { dashboardPrefix } from '@/lib/paths';
import { openExternalUrl } from '@/lib/externalLink';
import ProvidersSection from '@/components/ProvidersSection';
import OnboardingCard from '@/components/OnboardingCard';
import { useOnboardingState } from '@/hooks/useOnboarding';
import { useNavigationGuard } from '@/components/layout/NavigationGuardProvider';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Skeleton } from '@/components/ui/skeleton';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { AlertDialog, AlertDialogContent, AlertDialogHeader, AlertDialogFooter, AlertDialogTitle, AlertDialogDescription, AlertDialogAction, AlertDialogCancel } from '@/components/ui/alert-dialog';
import {
  Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList, CommandShortcut,
} from '@/components/ui/command';
import type { TelegramChatDto, TelegramLinkDto } from '@/types/contracts';

/**
 * Tooltip whose bubble is readable on touch too: Radix's Tooltip intentionally
 * ignores touch pointers, so a touch press additionally opens the styled bubble
 * for a moment (the same touch-hint trick as the "mark all read" button on the
 * subscriptions page). A plain title= would render the native browser tooltip,
 * which touch devices never show at all.
 */
function TouchHintTooltip({
  label, children,
}: {
  label: string;
  children: ReactElement;
}) {
  const [hintOpen, setHintOpen] = useState(false);
  const hintTimer = useRef<number | null>(null);

  const showTouchHint = () => {
    if (hintTimer.current) window.clearTimeout(hintTimer.current);
    setHintOpen(true);
    hintTimer.current = window.setTimeout(() => {
      hintTimer.current = null;
      setHintOpen(false);
    }, 2000);
  };

  useEffect(() => () => {
    if (hintTimer.current) window.clearTimeout(hintTimer.current);
  }, []);

  return (
    <TooltipProvider delayDuration={100}>
      <Tooltip
        open={hintOpen}
        // while the touch hint runs Radix's own close (pointerup/leave) must not kill it
        onOpenChange={(next) => { if (!next && hintTimer.current) return; setHintOpen(next); }}
      >
        <TooltipTrigger asChild onPointerDown={(e) => { if (e.pointerType === 'touch') showTouchHint(); }}>
          {children}
        </TooltipTrigger>
        <TooltipContent side="top" className="max-w-56 text-center">{label}</TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}

/** Actionable icon button with a touch-readable tooltip (see TouchHintTooltip). */
function TouchHintIconButton({
  label, onClick, disabled, variant = 'ghost', className, children,
}: {
  label: string;
  onClick: () => void;
  disabled?: boolean;
  variant?: 'ghost' | 'outline';
  className?: string;
  children: ReactNode;
}) {
  return (
    <TouchHintTooltip label={label}>
      <Button
        type="button"
        variant={variant}
        size="icon"
        disabled={disabled}
        aria-label={label}
        onClick={onClick}
        className={className}
      >
        {children}
      </Button>
    </TouchHintTooltip>
  );
}

const SETTINGS_TABS = ['general', 'providers', 'notifications', 'about'] as const;
type SettingsTab = (typeof SETTINGS_TABS)[number];

/**
 * Settings: profile preferences (UI/notification language, time zone), the
 * provider registry with the operator's own API keys, the linked Telegram
 * chat (at most one; linking a new chat unlinks the previous one), the alert
 * templates and the static page links. Session (cookie) only.
 * The blocks are split over four tabs so the page is not one long scroll; the
 * selected tab lives in the `?tab=` search param, so deep links (the first-run
 * notice points straight at Providers) and reloads land on the right panel.
 */
export default function SettingsPage() {
  const { t, locale, setLocale } = useTranslation();
  const { profile, loaded, clear, refresh } = useAuth();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const navigate = useNavigate();

  // the tab is part of the URL (replace: switching tabs should not stack history);
  // an unknown value just renders the default panel
  const [searchParams, setSearchParams] = useSearchParams();
  const urlTab = searchParams.get('tab');
  const tab: SettingsTab = (SETTINGS_TABS as readonly string[]).includes(urlTab ?? '') ? urlTab as SettingsTab : 'general';
  const scrollRef = useRef<HTMLDivElement>(null);
  const handleTabChange = useCallback((next: string) => {
    const value = SETTINGS_TABS.includes(next as SettingsTab) ? next as SettingsTab : 'general';
    setSearchParams(value === 'general' ? {} : { tab: value }, { replace: true });
    // every tab starts at the top: the panels share the one scroll container
    scrollRef.current?.scrollTo(0, 0);
  }, [setSearchParams]);

  const isSession = profile !== null;

  // The shared RQ cache (30 s staleTime) replaces the old module-level cache:
  // a revisit within the window renders instantly without a request
  const chatsQuery = useQuery({
    queryKey: qk.telegramChats,
    queryFn: () => account.getTelegramChats(),
    enabled: loaded && isSession,
  });
  const chats = useMemo(() => chatsQuery.data ?? [], [chatsQuery.data]);
  const loading = isSession ? chatsQuery.isPending : false;

  const [busyChat, setBusyChat] = useState<string | null>(null);
  const [savingLang, setSavingLang] = useState(false);
  const [savingTz, setSavingTz] = useState(false);
  const [tzOpen, setTzOpen] = useState(false);
  const [unlinkTarget, setUnlinkTarget] = useState<TelegramChatDto | null>(null);
  const [linkBusy, setLinkBusy] = useState(false);
  const [link, setLink] = useState<TelegramLinkDto | null>(null);

  const timezoneGroups = useMemo(() => getTimezoneGroups(), []);
  const languageValue = profile?.language ?? locale;
  const timezoneValue = profile?.timezone ?? getBrowserTimezone() ?? undefined;

  const handleLanguageChange = async (value: string) => {
    setSavingLang(true);
    try {
      await account.updatePreferences({ language: value });
      await refresh();
      // The saved locale is the source of truth (App.tsx rewrites URLs whose
      // prefix disagrees with it), so apply it here first and then move the
      // URL to the new locale prefix to match.
      setLocale(value as Locale);
      navigate(`${dashboardPrefix(value)}/settings`, { replace: true });
      // The context `t` is still the OLD locale in this closure (no re-render
      // has happened yet), so confirm in the language the user just picked.
      toast({ title: getTranslations(value as Locale).settings.profile.saved, variant: 'success' });
    } catch (e) {
      toast({ title: e instanceof Error ? e.message : t.settings.profile.saveFailed, variant: 'destructive' });
    } finally {
      setSavingLang(false);
    }
  };

  const handleTimezoneSelect = (value: string) => {
    setTzOpen(false);
    if (value === timezoneValue || !value) return;
    setSavingTz(true);
    account.updatePreferences({ timezone: value })
      .then(() => refresh())
      .then(() => toast({ title: t.settings.profile.saved, variant: 'success' }))
      .catch((e) => toast({ title: e instanceof Error ? e.message : t.settings.profile.saveFailed, variant: 'destructive' }))
      .finally(() => setSavingTz(false));
  };

  /** Updates the shared chats cache so the change survives remounts of this page. */
  const applyChatsUpdate = useCallback((updater: (prev: TelegramChatDto[]) => TelegramChatDto[]) => {
    queryClient.setQueryData<TelegramChatDto[]>(qk.telegramChats, (prev) => (prev ? updater(prev) : prev));
  }, [queryClient]);

  // with nothing to show, a failed load still reports; a background failure
  // over cached data just keeps the stale data, 401 means no session at all
  const loadError = chatsQuery.error;
  useEffect(() => {
    if (loadError && !chatsQuery.data && !(loadError instanceof ApiError && loadError.statusCode === 401)) {
      toast({ title: loadError instanceof Error ? loadError.message : t.settings.loadFailed, variant: 'destructive' });
    }
  // the error object is stable per failed attempt, so this fires once per attempt
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [loadError]);

  const handleGenerateTelegramLink = useCallback(async () => {
    setLinkBusy(true);
    try {
      const next = await account.createTelegramLinkToken();
      if (!next.deepLink) {
        // The bot is not configured on this instance - there is nothing to open or copy
        toast({ title: t.settings.telegram.linkUnavailable, variant: 'destructive' });
        return;
      }
      setLink(next);
      openExternalUrl(next.deepLink);
    } catch (e) {
      toast({ title: e instanceof Error ? e.message : t.settings.telegram.linkFailed, variant: 'destructive' });
    } finally {
      setLinkBusy(false);
    }
  }, [t, toast]);

  const copyLink = useCallback((value: string) => {
    void navigator.clipboard.writeText(value).then(
      () => toast({ title: t.settings.telegram.copied }),
      () => toast({ title: t.settings.telegram.copyFailed, variant: 'destructive' }),
    );
  }, [t, toast]);

  const handleConfirmUnlinkChat = async () => {
    const chat = unlinkTarget;
    if (!chat) return;
    setUnlinkTarget(null);
    setBusyChat(chat.id);
    try {
      await account.deleteTelegramChat(chat.id);
      applyChatsUpdate((prev) => prev.filter((c) => c.id !== chat.id));
      void refresh();
    } catch (e) {
      toast({ title: e instanceof Error ? e.message : t.settings.telegram.unlinkFailed, variant: 'destructive' });
    } finally {
      setBusyChat(null);
    }
  };

  // the guide's dismissal flag (shared cache with the card itself): hidden -> header help button
  const onboarding = useOnboardingState();
  // logging out with dirty provider drafts drops them like any other navigation
  const { confirmNavigation } = useNavigationGuard();

  // Blank while auth is unresolved (anonymous is redirected by MainLayout) —
  // the login page must not be preceded by a spinner flash on a cold start
  if (!loaded) {
    return null;
  }

  if (!isSession) {
    return (
      <Card className="max-w-md mx-auto mt-10">
        <CardHeader>
          <CardTitle>{t.settings.title}</CardTitle>
          <CardDescription>
            {t.settings.notSession}
          </CardDescription>
        </CardHeader>
      </Card>
    );
  }

  return (
    <div className="flex flex-col h-[var(--app-page-h)]">
      {/* Fixed viewport-fit height — the page must never scroll the browser window (see AGENTS.md) */}
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-bold">{t.settings.title}</h1>
        <div className="flex items-center gap-2 flex-shrink-0">
          {onboarding.hidden && (
            <TouchHintIconButton
              label={t.settings.onboarding.show}
              variant="outline"
              className="h-11 w-11 md:h-10 md:w-10"
              onClick={() => {
                onboarding.setHidden(false);
                // the guide lives on the General tab - show it where it renders
                handleTabChange('general');
              }}
            >
              <CircleHelp className="h-6 w-6 md:h-5 md:w-5" />
            </TouchHintIconButton>
          )}
          <Button
            variant="outline"
            className="h-11 md:h-10"
            onClick={() => confirmNavigation(() => {
              void clear();
              navigate(`/${locale}/login`);
            })}
          >
            <LogOut className="h-4 w-4" />
            {t.auth.logout}
          </Button>
        </div>
      </div>

      <Tabs value={tab} onValueChange={handleTabChange} className="mt-4 flex flex-1 min-h-0 flex-col">
        <TabsList aria-label={t.settings.title} className="shrink-0">
          <TabsTrigger value="general">
            <Settings2 className="h-5 w-5 shrink-0 md:h-4 md:w-4" aria-hidden="true" />
            <span className="max-w-full truncate">{t.settings.tabs.general}</span>
          </TabsTrigger>
          <TabsTrigger value="providers">
            <Database className="h-5 w-5 shrink-0 md:h-4 md:w-4" aria-hidden="true" />
            <span className="max-w-full truncate">{t.settings.tabs.providers}</span>
          </TabsTrigger>
          <TabsTrigger value="notifications">
            <BellRing className="h-5 w-5 shrink-0 md:h-4 md:w-4" aria-hidden="true" />
            <span className="max-w-full truncate">{t.settings.tabs.notifications}</span>
          </TabsTrigger>
          <TabsTrigger value="about">
            <Info className="h-5 w-5 shrink-0 md:h-4 md:w-4" aria-hidden="true" />
            <span className="max-w-full truncate">{t.settings.tabs.about}</span>
          </TabsTrigger>
        </TabsList>

        <div ref={scrollRef} className="relative mt-6 -mx-1.5 flex-1 min-h-0 overflow-y-auto px-1.5 pb-2">
          {/* forceMount keeps every panel mounted: the providers drafts and the page
              state below must survive a tab switch, so the inactive panels hide */}
          <TabsContent value="general" forceMount className="space-y-6">
            <OnboardingCard />

            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-base">
                  <Languages className="h-4 w-4" />
                  {t.settings.profile.title}
                </CardTitle>
                <CardDescription>
                  {t.settings.profile.description}
                </CardDescription>
              </CardHeader>
              <CardContent className="grid gap-4 sm:grid-cols-2">
                <div className="grid gap-2">
                  <Label htmlFor="profile-language">{t.settings.profile.language}</Label>
                  <Select value={languageValue} onValueChange={handleLanguageChange} disabled={savingLang}>
                    <SelectTrigger id="profile-language" className="w-full">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {SUPPORTED_LOCALES.map((code) => (
                        <SelectItem key={code} value={code}>{LOCALE_NAMES[code]}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="profile-timezone">{t.settings.profile.timezone}</Label>
                  <Popover open={tzOpen} onOpenChange={setTzOpen}>
                    <PopoverTrigger asChild>
                      <Button
                        id="profile-timezone"
                        variant="outline"
                        role="combobox"
                        aria-expanded={tzOpen}
                        disabled={savingTz}
                        className="w-full justify-between font-normal"
                      >
                        {timezoneValue ? formatTzLabel(timezoneValue) : t.settings.profile.timezonePlaceholder}
                        <ChevronsUpDown className="h-4 w-4 shrink-0 opacity-50" />
                      </Button>
                    </PopoverTrigger>
                    <PopoverContent className="w-full p-0" align="start">
                      <Command value={timezoneValue}>
                        <CommandInput placeholder={t.settings.profile.timezoneSearch} />
                        <CommandList>
                          <CommandEmpty>{t.settings.profile.timezoneNotFound}</CommandEmpty>
                          {timezoneGroups.map((group) => (
                            <CommandGroup key={group.region} heading={group.region}>
                              {group.items.map((item) => (
                                <CommandItem key={item.zone} value={item.zone} onSelect={handleTimezoneSelect}>
                                  {item.zone}
                                  <CommandShortcut>{item.offset}</CommandShortcut>
                                </CommandItem>
                              ))}
                            </CommandGroup>
                          ))}
                        </CommandList>
                      </Command>
                    </PopoverContent>
                  </Popover>
                </div>
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="providers" forceMount className="space-y-6">
            <ProvidersSection />
          </TabsContent>

          <TabsContent value="notifications" forceMount className="space-y-6">
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-base">
                  <Send className="h-4 w-4" />
                  {t.settings.telegram.title}
                </CardTitle>
                <CardDescription>
                  {t.settings.telegram.description}
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                {loading ? (
                  <div className="space-y-3" role="status">
                    <span className="sr-only">{t.common.loading}</span>
                    <Skeleton className="h-11 w-11 rounded-md" />
                    <Skeleton className="h-4 w-3/4" />
                  </div>
                ) : (
                  <>
                    {link && (
                      <div className="rounded-md border bg-muted/30 p-3 text-xs">
                        <p className="mb-2 text-muted-foreground">{t.settings.telegram.linkExpires.replace('{{time}}', new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'short' }).format(new Date(link.expiresAt)))}</p>
                        {link.deepLink && (
                          <div className="flex min-w-0 items-center gap-2 py-1">
                            <code className="flex-1 truncate font-mono">{link.deepLink}</code>
                            <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => copyLink(link.deepLink!)} aria-label={t.settings.telegram.copyLinkAria}>
                              <Copy className="h-3.5 w-3.5" />
                            </Button>
                          </div>
                        )}
                      </div>
                    )}
                    {chats.length === 0 ? (
                      <p className="text-sm text-muted-foreground">{t.settings.telegram.empty}</p>
                    ) : (
                      <ul className="divide-y">
                        {chats.map((chat) => (
                          <li key={chat.id} className="flex items-center gap-2 py-2">
                            <span className="min-w-0 flex-1 text-sm">
                              {chat.username ? `@${chat.username}` : t.settings.telegram.chat}{' '}
                              <span className="text-xs text-muted-foreground">({chat.chatId})</span>
                            </span>
                            <TouchHintIconButton
                              label={t.settings.telegram.replace}
                              className="ml-1 size-11 flex-shrink-0"
                              disabled={linkBusy}
                              onClick={() => void handleGenerateTelegramLink()}
                            >
                              {linkBusy ? <Loader2 className="h-4 w-4 animate-spin" /> : <RefreshCw className="h-4 w-4" />}
                            </TouchHintIconButton>
                            <TouchHintIconButton
                              label={t.settings.telegram.unlinkAria}
                              className="size-11 flex-shrink-0"
                              disabled={busyChat === chat.id}
                              onClick={() => setUnlinkTarget(chat)}
                            >
                              <Trash2 className="h-4 w-4" />
                            </TouchHintIconButton>
                          </li>
                        ))}
                      </ul>
                    )}
                    {/* Linking the first chat is the add-action of this card: a plain plus
                        at the bottom-right (thumb reach on mobile). Once a chat is linked
                        the plus disappears - a second chat is not possible, replacing the
                        current one is the per-row refresh button */}
                    {chats.length === 0 && (
                      <div className="flex justify-end">
                        <TouchHintIconButton
                          label={t.settings.telegram.generate}
                          variant="outline"
                          className="size-11 md:size-10"
                          disabled={linkBusy}
                          onClick={() => void handleGenerateTelegramLink()}
                        >
                          {linkBusy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Plus className="h-4 w-4" />}
                        </TouchHintIconButton>
                      </div>
                    )}
                  </>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-base">
                  <FileText className="h-4 w-4" />
                  {t.settings.templates.title}
                </CardTitle>
                <CardDescription>{t.settings.templates.description}</CardDescription>
              </CardHeader>
              <CardContent>
                <div className="flex justify-end">
                  <Button asChild variant="outline" className="h-11 md:h-10">
                    <Link to={`${dashboardPrefix(locale)}/templates`}>
                      <FileText className="h-4 w-4 mr-2" />
                      {t.settings.templates.manage}
                    </Link>
                  </Button>
                </div>
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="about" forceMount className="space-y-6">
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-base">
                  <Scale className="h-4 w-4" />
                  {t.settings.legal.title}
                </CardTitle>
                <CardDescription>{t.settings.legal.description}</CardDescription>
              </CardHeader>
              <CardContent>
                <nav className="flex flex-col divide-y">
                  {[
                    { to: `/${locale}/legal/disclaimer`, label: t.footer.disclaimer },
                    { to: `/${locale}/legal/licenses`, label: t.footer.licenses },
                  ].map(({ to, label }) => (
                    <Link
                      key={to}
                      to={to}
                      className="py-2 text-sm font-medium text-primary first:pt-0 last:pb-0 hover:underline underline-offset-2"
                    >
                      {label}
                    </Link>
                  ))}
                </nav>
              </CardContent>
            </Card>
          </TabsContent>
        </div>
      </Tabs>

      <AlertDialog open={unlinkTarget != null} onOpenChange={(open) => { if (!open) setUnlinkTarget(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle className="flex items-center gap-2">
              <AlertTriangle className="h-5 w-5 text-destructive flex-shrink-0" />
              {t.settings.telegram.unlinkTitle}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {unlinkTarget
                && t.settings.telegram.unlinkConfirm
                  .replace('{{name}}', unlinkTarget.username ? `@${unlinkTarget.username}` : t.settings.telegram.chat)
                  .replace('{{id}}', String(unlinkTarget.chatId))}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t.common.cancel}</AlertDialogCancel>
            <AlertDialogAction className="bg-destructive text-destructive-foreground hover:bg-destructive/90" onClick={handleConfirmUnlinkChat}>
              {t.settings.telegram.unlink}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
