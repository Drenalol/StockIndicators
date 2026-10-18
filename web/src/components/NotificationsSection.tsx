import { useEffect, useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Trash2, Zap } from 'lucide-react';
import { api, account } from '@/services/api';
import { qk } from '@/lib/query';
import { usePushSubscription } from '@/hooks/usePushSubscription';
import { useToast } from '@/hooks/use-toast';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { channelLabel, isUrlChannel, isWhatsApp } from '@/lib/alertChannels';
import type { NotificationTemplateDto } from '@/types/contracts';
import type { Translations } from '@/i18n';

export type OutputChannel = 'Telegram' | 'WebPush' | 'Webhook' | 'Discord' | 'WhatsApp';

const ALL_OUTPUT_CHANNELS: OutputChannel[] = ['Telegram', 'WebPush', 'Webhook', 'Discord', 'WhatsApp'];

export interface OutputDraft {
  id: string | null;
  channel: OutputChannel;
  templateId: string | null;
  pushSubscriptionId: string | null;
  /** Destination Telegram chat (channel = Telegram). null = account default */
  chatId: number | null;
  /** Destination URL (Webhook/Discord) or phone (WhatsApp) */
  target: string | null;
}

interface Props {
  outputs: OutputDraft[];
  onOutputsChange: (next: OutputDraft[]) => void;
  t: Translations;
  /** Server-side WhatsApp (Meta) credentials configured - otherwise the channel is hidden. */
  whatsappEnabled: boolean;
  /** Output count limit (from /api/Config). */
  maxOutputs: number;
}

export default function NotificationsSection({ outputs, onOutputsChange, t, whatsappEnabled, maxOutputs }: Props) {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const push = usePushSubscription();
  const [dialogRow, setDialogRow] = useState<number | null>(null);
  const [newName, setNewName] = useState('');
  const [newBody, setNewBody] = useState('');
  const [creating, setCreating] = useState(false);
  const [testing, setTesting] = useState(false);

  // Both lists live in the shared cache (the settings page and the subscription
  // detail page read the same chats data)
  const templatesQuery = useQuery({
    queryKey: qk.notificationTemplates,
    queryFn: () => api.getNotificationTemplates(),
  });
  const chatsQuery = useQuery({
    queryKey: qk.telegramChats,
    queryFn: () => account.getTelegramChats(),
  });
  // Templates are channel-agnostic: every template is offered on every output
  const templates = useMemo(() => templatesQuery.data ?? [], [templatesQuery.data]);
  const telegramChats = useMemo(() => chatsQuery.data ?? [], [chatsQuery.data]);

  // WhatsApp is offered only once the server carries the Meta credentials
  const channelOptions = whatsappEnabled ? ALL_OUTPUT_CHANNELS : ALL_OUTPUT_CHANNELS.filter((c) => c !== 'WhatsApp');

  const pickChannel = (index: number, channel: OutputChannel) => {
    if (isChannelTaken(channel, index)) return;

    const prev = outputs[index];
    updateRow(index, {
      channel,
      pushSubscriptionId: channel === 'WebPush' ? activePushSubscription?.id ?? null : null,
      // destinations are per channel; a channel switch never carries the old one over
      target: prev && channel === prev.channel ? prev.target : null,
    });

    // the channel pick is itself the user gesture that opens the permission dialog,
    // so a separate "enable" button is not needed (and once denied browsers ignore it anyway)
    if (channel === 'WebPush' && !activePushSubscription) void handleEnableBrowser();
  };

  const activePushSubscription = push.subscriptions.find((s) => s.endpoint === push.activeEndpoint) ?? null;

  // Backfill rows created before browser push was enabled
  useEffect(() => {
    if (!activePushSubscription || !outputs.some((o) => o.channel === 'WebPush' && !o.pushSubscriptionId)) return;
    onOutputsChange(outputs.map((o) => (o.channel === 'WebPush' && !o.pushSubscriptionId ? { ...o, pushSubscriptionId: activePushSubscription.id } : o)));
  }, [activePushSubscription, outputs, onOutputsChange]);

  // Drop draft references to push subscriptions that disappeared in the meantime
  // (disabled in another tab/device, or removed by the server when the push endpoint
  // expired): a dangling id would fail the save with a foreign key violation, so the
  // row falls back to the "not enabled" state and the save-time guard asks to enable
  useEffect(() => {
    if (push.permission === 'unsupported' || push.loading) return;
    const isStale = (o: OutputDraft) => o.pushSubscriptionId !== null && !push.subscriptions.some((s) => s.id === o.pushSubscriptionId);
    if (!outputs.some(isStale)) return;
    onOutputsChange(outputs.map((o) => (isStale(o) ? { ...o, pushSubscriptionId: null } : o)));
  }, [push.permission, push.loading, push.subscriptions, outputs, onOutputsChange]);

  const updateRow = (index: number, patch: Partial<OutputDraft>) => {
    if (index >= outputs.length) {
      const base: OutputDraft = { id: null, channel: 'WebPush', templateId: null, pushSubscriptionId: null, chatId: null, target: null };
      onOutputsChange([...outputs, { ...base, ...patch }]);
      return;
    }
    onOutputsChange(outputs.map((o, i) => (i === index ? { ...o, ...patch } : o)));
  };

  const isChannelTaken = (channel: OutputChannel, exceptIndex: number) =>
    outputs.some((o, i) => i !== exceptIndex && o.channel === channel);

  const removeRow = (index: number) => {
    if (outputs.length <= 1) return;
    onOutputsChange(outputs.filter((_, i) => i !== index));
  };

  const addRow = () => {
    if (outputs.length >= maxOutputs) return;
    const freeChannel = channelOptions.find((c) => !outputs.some((o) => o.channel === c));
    if (!freeChannel) return;
    pickChannel(outputs.length, freeChannel);
  };

  const handleEnableBrowser = async () => {
    try {
      await push.enable();
    } catch {
      toast({
        title: push.permission === 'denied' ? t.notifications.permissionDenied : t.notifications.enableFailed,
        variant: 'destructive',
      });
    }
  };

  const handleTest = async () => {
    setTesting(true);
    try {
      const sent = await push.sendTest();
      toast({ title: t.notifications.testSent, variant: sent > 0 ? 'success' : 'default' });
    } catch {
      toast({ title: t.notifications.testFailed, variant: 'destructive' });
    } finally {
      setTesting(false);
    }
  };

  const handleCreateTemplate = async () => {
    if (!newName.trim() || !newBody.trim() || dialogRow === null) return;
    setCreating(true);
    try {
      const id = await api.createNotificationTemplate({ name: newName.trim(), template: newBody.trim() });
      const created: NotificationTemplateDto = { id, name: newName.trim(), isBundled: false, template: newBody.trim(), canEdit: true };
      queryClient.setQueryData<NotificationTemplateDto[]>(qk.notificationTemplates,
        (prev) => (prev ? [...prev, created] : [created]));
      updateRow(dialogRow, { templateId: id });
      setDialogRow(null);
      setNewName('');
      setNewBody('');
    } catch (e) {
      toast({ title: e instanceof Error ? e.message : t.notifications.testFailed, variant: 'destructive' });
    } finally {
      setCreating(false);
    }
  };

  return (
    <div className="space-y-4">
      {outputs.map((output, index) => (
        <div key={output.id ?? `new-${index}`} className="rounded-md border p-3 space-y-3">
          <div className="flex items-center gap-2">
            <Select value={output.channel} onValueChange={(v) => pickChannel(index, v as OutputChannel)}>
              <SelectTrigger className="w-36">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {/* a stored channel stays listed even when its option is hidden (e.g. WhatsApp credentials were removed) */}
                {[...new Set([...channelOptions, output.channel])].map((channel) => (
                  <SelectItem key={channel} value={channel} disabled={isChannelTaken(channel, index)}>
                    {channelLabel(channel, t)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <div className="flex-1" />
            {output.channel === 'WebPush' && activePushSubscription && (
              <Button variant="ghost" size="sm" onClick={handleTest} disabled={testing}>
                <Zap className="h-3.5 w-3.5 mr-1" />
                {t.notifications.testNotification}
              </Button>
            )}
            {outputs.length <= 1 ? (
              <TooltipProvider delayDuration={100}>
                <Tooltip>
                  <TooltipTrigger asChild>
                    {/* aria-disabled instead of disabled: a natively disabled button swallows pointer events,
                        so neither the hover tooltip nor a tap would ever reach the user on touch devices */}
                    <span className="inline-flex cursor-not-allowed opacity-50">
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-8 w-8 aria-disabled:hover:bg-transparent"
                        aria-disabled
                        aria-label={t.notifications.removeOutput}
                        onClick={(e) => {
                          // On touch there is no hover tooltip, so explain via toast; on desktop the tooltip already covers it
                          if ((e.nativeEvent as PointerEvent).pointerType !== 'mouse') toast({ title: t.notifications.removeOutputDisabled, variant: 'default' });
                        }}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </span>
                  </TooltipTrigger>
                  <TooltipContent side="top" className="max-w-[240px] whitespace-normal">
                    {t.notifications.removeOutputDisabled}
                  </TooltipContent>
                </Tooltip>
              </TooltipProvider>
            ) : (
              <Button variant="ghost" size="icon" className="h-8 w-8" onClick={() => removeRow(index)} aria-label={t.notifications.removeOutput}>
                <Trash2 className="h-4 w-4" />
              </Button>
            )}
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label className="text-xs text-muted-foreground">{t.notifications.templateLabel}</Label>
              <div className="flex gap-2">
                <Select
                  value={output.templateId ?? 'default'}
                  onValueChange={(v) => updateRow(index, { templateId: v === 'default' ? null : v })}
                >
                  <SelectTrigger className="flex-1">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="default">{t.notifications.defaultTemplate}</SelectItem>
                    {templates.map((tp) => (
                      <SelectItem key={tp.id} value={tp.id}>{tp.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Button variant="outline" size="icon" className="h-10 w-10" onClick={() => setDialogRow(index)}>
                  <Plus className="h-4 w-4" />
                </Button>
              </div>
              {templates.length === 0 && (
                <p className="text-[11px] text-muted-foreground">{t.notifications.noTemplates}</p>
              )}
            </div>

            {output.channel === 'Telegram' && telegramChats.length > 0 && (
              <div className="space-y-1.5">
                <Label className="text-xs text-muted-foreground">{t.notifications.chatLabel}</Label>
                <p className="text-sm pt-1.5">
                  {(() => {
                    const chat = telegramChats.find((c) => c.chatId === output.chatId) ?? telegramChats[0];
                    return chat.username ? `@${chat.username}` : `Chat ${chat.chatId}`;
                  })()}
                </p>
              </div>
            )}

            {output.channel === 'WebPush' && (
              <div className="space-y-1.5">
                <Label className="text-xs text-muted-foreground">{t.notifications.channels.browser}</Label>
                {push.permission === 'unsupported' ? (
                  <p className="text-[11px] text-muted-foreground pt-1.5">{t.notifications.notSupported}</p>
                ) : activePushSubscription ? (
                  <p className="text-xs pt-1.5 text-green-600 dark:text-green-400">
                    {activePushSubscription.deviceLabel || activePushSubscription.endpoint.slice(-12)} · {t.notifications.browserEnabled}
                  </p>
                ) : push.permission === 'denied' ? (
                  <p className="text-[11px] text-muted-foreground pt-1.5">{t.notifications.denied}</p>
                ) : (
                  <p className="text-[11px] text-muted-foreground pt-1.5">{t.notifications.enableHint}</p>
                )}
              </div>
            )}

            {isUrlChannel(output.channel) && (
              <div className="space-y-1.5">
                <Label className="text-xs text-muted-foreground">{t.notifications.target.urlLabel}</Label>
                <Input
                  type="url"
                  value={output.target ?? ''}
                  onChange={(e) => updateRow(index, { target: e.target.value })}
                  placeholder={t.notifications.target.urlPlaceholder}
                />
                <p className="text-[11px] text-muted-foreground">
                  {output.channel === 'Discord' ? t.notifications.target.discordHint : t.notifications.target.webhookHint}
                </p>
              </div>
            )}

            {isWhatsApp(output.channel) && (
              <div className="space-y-1.5">
                <Label className="text-xs text-muted-foreground">{t.notifications.target.phoneLabel}</Label>
                <Input
                  type="tel"
                  value={output.target ?? ''}
                  onChange={(e) => updateRow(index, { target: e.target.value })}
                  placeholder={t.notifications.target.phonePlaceholder}
                />
                <p className="text-[11px] text-muted-foreground">{t.notifications.target.whatsappHint}</p>
              </div>
            )}
          </div>
        </div>
      ))}

      <Button variant="outline" size="sm" onClick={addRow} disabled={outputs.length >= maxOutputs}>
        <Plus className="h-3.5 w-3.5 mr-1" />
        {t.notifications.addOutput}
      </Button>

      <Dialog open={dialogRow !== null} onOpenChange={(open) => { if (!open) setDialogRow(null); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t.notifications.createTemplate}</DialogTitle>
            <DialogDescription>{t.notifications.templateTokensHint}</DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1.5">
              <Label>{t.notifications.templateName}</Label>
              <Input value={newName} onChange={(e) => setNewName(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label>{t.notifications.templateBody}</Label>
              <Input value={newBody} onChange={(e) => setNewBody(e.target.value)} placeholder="{{ticker}} - {{indicator}}: {{alertText}}" />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogRow(null)}>{t.common.cancel}</Button>
            <Button onClick={handleCreateTemplate} disabled={creating || !newName.trim() || !newBody.trim()}>{t.common.create}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
