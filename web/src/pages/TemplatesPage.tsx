import { useMemo, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, ArrowLeft, FileText, Loader2, Lock, Pencil, Plus, Trash2 } from 'lucide-react';
import { useAuth } from '@/hooks/useAuth';
import { useTranslation } from '@/i18n';
import { useGoBackOr } from '@/hooks/useGoBackOr';
import { useToast, type ToastInput } from '@/hooks/use-toast';
import { api } from '@/services/api';
import { qk, apiErrorText } from '@/lib/query';
import { dashboardPrefix } from '@/lib/paths';
import type { NotificationTemplateDto } from '@/types/contracts';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';

type EditorTarget = { mode: 'create' } | { mode: 'edit'; template: NotificationTemplateDto };

export default function TemplatesPage() {
  const { loaded, isAuthenticated } = useAuth();
  const { t, locale } = useTranslation();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const goBackOr = useGoBackOr();

  const [editor, setEditor] = useState<EditorTarget | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<NotificationTemplateDto | null>(null);
  const [busy, setBusy] = useState(false);

  const authed = loaded && isAuthenticated;

  // Templates are channel-agnostic: one list for every delivery channel
  const templatesQuery = useQuery({
    queryKey: qk.notificationTemplates,
    queryFn: () => api.getNotificationTemplates(),
    enabled: authed,
  });
  const templates = useMemo(() => templatesQuery.data ?? [], [templatesQuery.data]);
  const loading = authed ? templatesQuery.isPending : false;

  const applyTemplatesUpdate = (updater: (prev: NotificationTemplateDto[]) => NotificationTemplateDto[]) => {
    queryClient.setQueryData<NotificationTemplateDto[]>(qk.notificationTemplates, (prev) => (prev ? updater(prev) : prev));
  };

  const handleSaved = (saved: NotificationTemplateDto) => {
    applyTemplatesUpdate((prev) => {
      const next = prev.some((tp) => tp.id === saved.id) ? prev.map((tp) => (tp.id === saved.id ? saved : tp)) : [...prev, saved];
      return next.sort((a, b) => a.name.localeCompare(b.name));
    });
  };

  const handleDelete = async () => {
    const template = deleteTarget;
    if (!template) return;
    setDeleteTarget(null);
    setBusy(true);
    try {
      await api.deleteNotificationTemplate(template.id);
      applyTemplatesUpdate((prev) => prev.filter((tp) => tp.id !== template.id));
      toast({ title: t.templates.deleted, variant: 'success' });
    } catch (e) {
      toast({ title: e instanceof Error ? e.message : t.templates.deleteFailed, variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="flex flex-col h-[var(--app-page-h)]">
      {/* Fixed viewport-fit height — the page must never scroll the browser window (see AGENTS.md) */}
      <div className="flex items-center gap-2">
        <Button
          variant="ghost"
          size="icon"
          className="-ml-2 flex-shrink-0"
          onClick={() => goBackOr(`${dashboardPrefix(locale)}/settings`)}
          aria-label={t.common.back}
        >
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <div className="min-w-0 flex-1">
          <h1 className="text-2xl font-bold">{t.templates.title}</h1>
          <p className="text-sm text-muted-foreground">{t.templates.hint}</p>
        </div>
        {/* same height as the other create buttons: size sm on desktop (as SubscriptionsPage),
            a 44px thumb target on mobile where there is no bottom action bar here */}
        <Button size="sm" className="h-11 md:h-9" onClick={() => setEditor({ mode: 'create' })}>
          <Plus className="h-4 w-4" />
          <span className="hidden sm:inline">{t.templates.new}</span>
        </Button>
      </div>

      <div className="relative mt-4 -mx-1.5 flex-1 min-h-0 overflow-y-auto px-1.5 pb-2">
        {loading ? (
          <ul className="divide-y" role="status">
            <span className="sr-only">{t.common.loading}</span>
            {Array.from({ length: 4 }).map((_, i) => (
              <li key={i} className="flex items-start gap-3 py-3">
                <div className="min-w-0 flex-1 space-y-1.5">
                  <Skeleton className="h-4 w-32" />
                  <Skeleton className="h-3 w-3/4" />
                </div>
                <div className="flex flex-shrink-0 items-center gap-1">
                  <Skeleton className="size-11 rounded-md" />
                  <Skeleton className="size-11 rounded-md" />
                </div>
              </li>
            ))}
          </ul>
        ) : templatesQuery.error ? (
          <div className="py-10 text-center space-y-3">
            <p className="text-sm text-muted-foreground">{apiErrorText(templatesQuery.error, t)}</p>
            <Button variant="outline" className="h-11" onClick={() => void templatesQuery.refetch()}>{t.templates.retry}</Button>
          </div>
        ) : templates.length === 0 ? (
          <p className="py-10 text-center text-sm text-muted-foreground">{t.templates.empty}</p>
        ) : (
          <ul className="divide-y">
            {templates.map((template) => {
              const editable = template.canEdit;
              return (
                <li key={template.id} className="flex items-start gap-3 py-3">
                  <div className="min-w-0 flex-1 space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="break-words text-sm font-medium">{template.name}</span>
                      {!editable && (
                        <Badge variant="outline" className="flex-shrink-0 text-muted-foreground">
                          <span className="flex items-center gap-1">
                            <Lock className="h-3 w-3" />
                            {t.templates.readOnlyBundled}
                          </span>
                        </Badge>
                      )}
                    </div>
                    <p className="break-all font-mono text-xs text-muted-foreground">{template.template}</p>
                  </div>
                  {editable && (
                    <div className="flex flex-shrink-0 items-center gap-1">
                      <Button variant="ghost" size="icon" className="size-11" onClick={() => setEditor({ mode: 'edit', template })} aria-label={t.templates.editAria}>
                        <Pencil className="h-4 w-4" />
                      </Button>
                      <Button variant="ghost" size="icon" className="size-11" onClick={() => setDeleteTarget(template)} aria-label={t.templates.deleteAria}>
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </div>

      <TemplateEditorDialog editor={editor} onClose={() => setEditor(null)} onSaved={handleSaved} toast={toast} />

      <AlertDialog open={deleteTarget != null} onOpenChange={(open) => { if (!open) setDeleteTarget(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle className="flex items-center gap-2">
              <AlertTriangle className="h-5 w-5 text-destructive flex-shrink-0" />
              {t.templates.deleteTitle}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {deleteTarget && t.templates.deleteConfirm.replace('{{name}}', deleteTarget.name)}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busy}>{t.common.cancel}</AlertDialogCancel>
            <AlertDialogAction className="bg-destructive text-destructive-foreground hover:bg-destructive/90" onClick={(e) => { e.preventDefault(); void handleDelete(); }}>
              {busy && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
              {t.common.delete}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

interface EditorProps {
  editor: EditorTarget | null;
  onClose: () => void;
  onSaved: (template: NotificationTemplateDto) => void;
  toast: (opts: ToastInput) => void;
}

// The renderer substitutes exactly these tokens (AlertTemplateRenderer); invariant across locales
const TEMPLATE_TOKENS = ['{{ticker}}', '{{exchange}}', '{{indicator}}', '{{alertText}}', '{{time}}', '{{subscriptionId}}'] as const;

function TemplateEditorDialog({ editor, onClose, onSaved, toast }: EditorProps) {
  const { t } = useTranslation();
  const [name, setName] = useState('');
  const [body, setBody] = useState('');
  const [saving, setSaving] = useState(false);
  const bodyRef = useRef<HTMLInputElement>(null);

  // Insert a token into the body at the caret, replacing the current selection
  // (the token chips keep focus on the input via preventDefault on mousedown)
  const insertToken = (tokenText: string) => {
    const el = bodyRef.current;
    const start = el?.selectionStart ?? body.length;
    const end = el?.selectionEnd ?? body.length;
    setBody(body.slice(0, start) + tokenText + body.slice(end));

    const caret = start + tokenText.length;
    requestAnimationFrame(() => {
      el?.focus();
      el?.setSelectionRange(caret, caret);
    });
  };

  // Reseed the fields on every open (create starts empty, edit carries the row)
  const openKey = editor ? (editor.mode === 'edit' ? `edit-${editor.template.id}` : 'create') : null;
  const [lastKey, setLastKey] = useState<string | null>(null);
  if (openKey !== lastKey) {
    setLastKey(openKey);
    if (editor?.mode === 'edit') {
      setName(editor.template.name);
      setBody(editor.template.template);
    } else if (editor?.mode === 'create') {
      setName('');
      setBody('');
    }
  }

  const valid = name.trim() !== '' && body.trim() !== '';

  const handleSave = async () => {
    if (!editor || !valid) return;
    setSaving(true);
    try {
      if (editor.mode === 'create') {
        const id = await api.createNotificationTemplate({ name: name.trim(), template: body.trim() });
        onSaved({ id, name: name.trim(), isBundled: false, template: body.trim(), canEdit: true });
        toast({ title: t.templates.created, variant: 'success' });
      } else {
        await api.updateNotificationTemplate(editor.template.id, { name: name.trim(), template: body.trim() });
        onSaved({ ...editor.template, name: name.trim(), template: body.trim() });
        toast({ title: t.templates.saved, variant: 'success' });
      }
      onClose();
    } catch (e) {
      toast({ title: e instanceof Error ? e.message : t.templates.saveFailed, variant: 'destructive' });
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={editor != null} onOpenChange={(open) => { if (!open) onClose(); }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <FileText className="h-5 w-5" />
            {editor?.mode === 'edit' ? t.templates.editTitle : t.templates.new}
          </DialogTitle>
        </DialogHeader>
        <div className="mt-2 space-y-4">
          <div className="space-y-1.5">
            <Label>{t.notifications.templateName}</Label>
            <Input value={name} onChange={(e) => setName(e.target.value)} maxLength={50} />
          </div>
          <div className="space-y-1.5">
            <Label>{t.notifications.templateBody}</Label>
            <Input
              ref={bodyRef}
              value={body}
              onChange={(e) => setBody(e.target.value)}
              placeholder="{{ticker}} - {{indicator}}: {{alertText}}"
            />
          </div>
          <div className="space-y-2">
            <div className="flex flex-wrap gap-1.5">
              {TEMPLATE_TOKENS.map((tokenText) => (
                <button
                  key={tokenText}
                  type="button"
                  // keep focus (and the caret) on the body input so the token lands at the cursor
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => insertToken(tokenText)}
                  className="rounded-full transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                  title={t.templates.tokensInsertHint}
                >
                  <Badge variant="outline" className="pointer-events-none cursor-pointer bg-muted/50 px-2.5 py-1 font-mono text-xs font-normal text-foreground hover:bg-muted">
                    {tokenText}
                  </Badge>
                </button>
              ))}
            </div>
            <p className="text-xs text-muted-foreground">{t.templates.tokensInsertHint}</p>
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>{t.common.cancel}</Button>
          <Button onClick={() => void handleSave()} disabled={saving || !valid}>
            {saving && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
            {editor?.mode === 'edit' ? t.common.save : t.common.create}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
