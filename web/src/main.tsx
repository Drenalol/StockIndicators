import '@/lib/monitoring';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { ToastProvider, ToastViewport, Toast, ToastTitle, ToastDescription } from '@/components/ui/toast';
import { useToast } from '@/hooks/use-toast';
import { LocaleProvider } from '@/i18n';
import { ThemeProvider } from '@/hooks/useTheme';
import { AuthProvider } from '@/hooks/useAuth';
import { ApiHealthProvider } from '@/hooks/ApiHealthProvider';
import { AppQueryProvider } from '@/lib/QueryProvider';
import ErrorBoundary from '@/components/ErrorBoundary';
import { cn } from '@/lib/utils';
import { Sentry } from '@/lib/monitoring';
import App from './App';
import './index.css';

function ToastContainer() {
  const { toasts } = useToast();
  return (
    <>
      {toasts.map(({ id, title, description, variant, action, duration }) => {
        // The variant is an inline-flex centered chip: perfect for a bare title,
        // but a toast with a body and an action squeezed its three text blocks into
        // three narrow columns side by side. Those get a full-width row instead:
        // a left-aligned text column and the action on the right
        const rich = !!description || !!action;
        return (
          <Toast
            key={id}
            variant={variant}
            duration={duration}
            className={cn(rich && 'flex w-full gap-3 text-left')}
          >
            <div className={cn(rich ? 'grid min-w-0 flex-1 gap-1' : 'contents')}>
              {title && <ToastTitle>{title}</ToastTitle>}
              {description && <ToastDescription>{description}</ToastDescription>}
            </div>
            {action}
          </Toast>
        );
      })}
      <ToastViewport />
    </>
  );
}

function Root() {
  return (
    <ErrorBoundary>
    <StrictMode>
      {/* Above everything: AuthProvider needs useApiHealth() to retry the session
          probe after an outage, and App renders the down overlay from the same context */}
      <ApiHealthProvider>
        <AppQueryProvider>
          <ThemeProvider>
            <LocaleProvider>
              <AuthProvider>
                <ToastProvider>
                  <App />
                  <ToastContainer />
                </ToastProvider>
              </AuthProvider>
            </LocaleProvider>
          </ThemeProvider>
        </AppQueryProvider>
      </ApiHealthProvider>
    </StrictMode>
    </ErrorBoundary>
  );
}

// Dev builds must be distinguishable in the browser tab: wrap the title setter so
// every assignment (including useSeo in LandingPage) keeps the DEV suffix
if (import.meta.env.DEV) {
  const titleDescriptor = Object.getOwnPropertyDescriptor(Document.prototype, 'title')!;
  Object.defineProperty(document, 'title', {
    configurable: true,
    get: () => titleDescriptor.get!.call(document),
    set: (value: string) => {
      titleDescriptor.set!.call(document, value.endsWith(' DEV') ? value : `${value} DEV`);
    },
  });
  document.title = 'Stock Indicators';
}

// The service worker only delivers Web Push; registering it never breaks anything else
if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => {
      /* SW is optional: alerts keep working over other channels */
    });
  });
}

// React 19 error hooks: report to GlitchTip everything the boundaries above do not
// handle; onUncaughtError also keeps the default console output.
createRoot(document.getElementById('root')!, {
  onUncaughtError: Sentry.reactErrorHandler((error, errorInfo) => {
    console.warn('Uncaught error', error, errorInfo.componentStack);
  }),
  onCaughtError: Sentry.reactErrorHandler(),
  onRecoverableError: Sentry.reactErrorHandler(),
}).render(<Root />);
