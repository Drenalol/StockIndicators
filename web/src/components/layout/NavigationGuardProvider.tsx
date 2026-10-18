import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { useTranslation } from '@/i18n';

// A guard is registered while a page has unsaved changes; navigation that would
// drop them is confirmed through the dialog below instead of a browser confirm().
interface UnsavedGuard {
  message: string;
}

interface NavigationGuardContextValue {
  setGuard: (guard: UnsavedGuard | null) => void;
  confirmNavigation: (run: () => void) => void;
}

const NavigationGuardContext = createContext<NavigationGuardContextValue>({
  setGuard: () => {},
  confirmNavigation: (run) => run(),
});

// Pages with unsaved changes register a guard; in-app navigation that would
// drop them must go through confirmNavigation(), which holds the navigation
// back until the user confirms in the dialog. Also warns on tab close/refresh.
export function NavigationGuardProvider({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const [guard, setGuard] = useState<UnsavedGuard | null>(null);
  const [pending, setPending] = useState<{ run: () => void } | null>(null);
  // Set when the confirmed action IS the reload: the beforeunload listener below
  // must not prompt again on top of our own dialog
  const allowNextUnload = useRef(false);

  const confirmNavigation = useCallback((run: () => void) => {
    if (!guard) {
      run();
    } else {
      setPending({ run });
    }
  }, [guard]);

  useEffect(() => {
    if (!guard) return;
    const onBeforeUnload = (e: BeforeUnloadEvent) => {
      if (allowNextUnload.current) return;
      e.preventDefault();
      e.returnValue = '';
    };
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [guard]);

  // F5 / Ctrl+R / Cmd+R would drop the unsaved changes without ever reaching the
  // confirm dialog (a reload is not a router navigation), so the shortcuts are
  // intercepted and routed through the same dialog; the actual reload is then a
  // programmatic one with the native beforeunload prompt suppressed. Unpreventable
  // paths (closing the tab, the browser menu) keep the native prompt.
  useEffect(() => {
    if (!guard) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.isComposing) return;
      const isReload = e.key === 'F5' || (e.key.toLowerCase() === 'r' && (e.metaKey || e.ctrlKey));
      if (!isReload) return;
      e.preventDefault();
      setPending({
        run: () => {
          allowNextUnload.current = true;
          window.location.reload();
        },
      });
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [guard]);

  const handleLeave = () => {
    if (!pending) return;
    const { run } = pending;
    setPending(null);
    run();
  };

  return (
    <NavigationGuardContext.Provider value={{ setGuard, confirmNavigation }}>
      {children}
      <AlertDialog open={pending !== null} onOpenChange={(open) => { if (!open) setPending(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t.common.unsavedChangesTitle}</AlertDialogTitle>
            <AlertDialogDescription>{guard?.message}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t.common.cancel}</AlertDialogCancel>
            <AlertDialogAction className="bg-destructive text-destructive-foreground hover:bg-destructive/90" onClick={handleLeave}>
              {t.common.leave}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </NavigationGuardContext.Provider>
  );
}

export function useNavigationGuard() {
  return useContext(NavigationGuardContext);
}
