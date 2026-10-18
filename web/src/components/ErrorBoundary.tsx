import { Component, type ErrorInfo, type ReactNode } from 'react';
import { Sentry } from '@/lib/monitoring';
import { LocaleProvider, useTranslation } from '@/i18n';
import { Button } from '@/components/ui/button';

function CrashScreen() {
  const { t } = useTranslation();
  return (
    <div className="flex flex-col items-center justify-center min-h-screen p-6 text-center">
      <h1 className="text-xl md:text-2xl font-bold">{t.errors.appCrashed}</h1>
      <p className="mt-2 text-sm md:text-base text-muted-foreground max-w-md">{t.errors.appCrashedHint}</p>
      <Button size="sm" className="mt-6" onClick={() => window.location.reload()}>
        {t.errors.reload}
      </Button>
    </div>
  );
}

interface ErrorBoundaryState {
  error?: Error;
}

// Last line of defense: without this, a render-time exception is a blank white screen
// with no report. The error is sent to GlitchTip (with the React component stack) and
// the user gets a localized reload screen instead.
export default class ErrorBoundary extends Component<{ children: ReactNode }, ErrorBoundaryState> {
  state: ErrorBoundaryState = {};

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    Sentry.captureReactException(error, errorInfo);
  }

  render() {
    if (this.state.error) {
      return (
        <LocaleProvider>
          <CrashScreen />
        </LocaleProvider>
      );
    }
    return this.props.children;
  }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { error };
  }
}
