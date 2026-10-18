import { useCallback, useEffect, useRef, useState } from 'react';
import { Navigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '@/hooks/useAuth';
import { useTranslation } from '@/i18n';
import { auth, ApiError } from '@/services/api';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Logo } from '@/components/Logo';
import { Loader2, Lock } from 'lucide-react';
import { dashboardPrefix } from '@/lib/paths';
import { sanitizeLoginFrom } from '@/lib/loginFrom';

const MAX_ATTEMPTS = 5;
const LOCKOUT_MS = 15_000;

export default function LoginPage() {
  const { loginWithPassword, isAuthenticated, loading } = useAuth();
  const { t, locale } = useTranslation();
  const [params] = useSearchParams();
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [lockoutEnd, setLockoutEnd] = useState<number | null>(null);
  // Whether the instance has a password at all: without one every sign-in is refused,
  // and the operator has to be told where to put it instead of fighting a dead form
  const [passwordConfigured, setPasswordConfigured] = useState(true);
  const failedCountRef = useRef(0);
  const inputRef = useRef<HTMLInputElement>(null);

  const loginFrom = sanitizeLoginFrom(params.get('from'));

  useEffect(() => {
    let cancelled = false;
    void auth.status().then(status => {
      if (!cancelled) setPasswordConfigured(status.passwordConfigured);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    inputRef.current?.focus();
  }, []);

  // Lockout countdown: the tick effect re-renders while the lock is active
  const [lockoutRemaining, setLockoutRemaining] = useState(0);
  useEffect(() => {
    if (lockoutEnd === null) {
      setLockoutRemaining(0);
      return;
    }
    const tick = () => {
      const left = lockoutEnd - Date.now();
      if (left <= 0) {
        setLockoutEnd(null);
        failedCountRef.current = 0;
        return;
      }
      setLockoutRemaining(Math.ceil(left / 1000));
    };
    tick();
    const timer = setInterval(tick, 250);
    return () => clearInterval(timer);
  }, [lockoutEnd]);

  const isLocked = lockoutRemaining > 0;

  const handleSubmit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (isLocked || busy) return;

    if (!password) {
      setError(t.auth.passwordRequired);
      return;
    }

    setError(null);
    setBusy(true);
    try {
      await loginWithPassword(password);
      // success: the profile lands in the context and the render below redirects
    } catch (err) {
      if (err instanceof ApiError && err.statusCode === 401) {
        failedCountRef.current += 1;
        if (failedCountRef.current >= MAX_ATTEMPTS) {
          setLockoutEnd(Date.now() + LOCKOUT_MS);
          setError(t.auth.lockoutError.replace('{{seconds}}', String(Math.ceil(LOCKOUT_MS / 1000))));
        } else {
          setError(t.auth.invalidPasswordError);
        }
      } else if (err instanceof ApiError && err.statusCode === 400) {
        // the server refuses every attempt while no password is configured
        setError(t.auth.noPasswordConfigured);
        setPasswordConfigured(false);
      } else {
        setError(t.auth.genericError);
      }
    } finally {
      setBusy(false);
      // The password field never keeps a rejected secret in the DOM
      setPassword('');
      inputRef.current?.focus();
    }
  }, [password, isLocked, busy, loginWithPassword, t]);

  // A visitor who is already signed in must not get the login card painted even for a
  // frame: render the redirect directly instead of a navigate() in an effect. replace
  // keeps the login entry out of history, so browser back from the landing goes to the
  // landing, not back here.
  if (isAuthenticated) {
    return <Navigate to={loginFrom ? `/${locale}/${loginFrom}` : `${dashboardPrefix(locale)}/subscriptions`} replace />;
  }

  return (
    <div className="flex min-h-svh items-center justify-center bg-muted/40 p-4">
      <Card className="w-full max-w-sm">
        <CardHeader className="text-center">
          <div className="flex justify-center mb-3">
            <Logo className="h-14 md:h-16" />
          </div>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-4">
            {!passwordConfigured && (
              <p className="rounded-md border border-amber-500/60 bg-amber-500/10 p-3 text-sm text-amber-700 dark:text-amber-400">
                {t.auth.noPasswordConfigured}
              </p>
            )}
            <div className="grid gap-2">
              <Label htmlFor="password">{t.auth.passwordLabel}</Label>
              <div className="relative">
                <Lock className="absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  ref={inputRef}
                  id="password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  autoComplete="current-password"
                  aria-describedby={error ? 'auth-error' : undefined}
                  className="pl-9"
                />
              </div>
            </div>
            {error && (
              <p id="auth-error" className="text-sm text-destructive" role="alert">{error}</p>
            )}
            <Button type="submit" className="w-full" disabled={busy || loading || isLocked}>
              {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
              {isLocked
                ? t.auth.lockedOut.replace('{{seconds}}', String(lockoutRemaining))
                : t.auth.signIn}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
