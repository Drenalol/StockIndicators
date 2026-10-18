import * as Sentry from '@sentry/react';

// GlitchTip speaks the Sentry protocol; without VITE_GLITCHTIP_DSN the SDK stays a no-op
// (e.g. contributor clones), so init is unconditional.
Sentry.init({
  dsn: import.meta.env.VITE_GLITCHTIP_DSN,
  environment: import.meta.env.MODE,
  release: import.meta.env.VITE_APP_RELEASE,
  // Errors only for now; tracing adds volume GlitchTip does not need yet
  tracesSampleRate: 0,
  beforeBreadcrumb(breadcrumb) {
    const url = breadcrumb.type === 'fetch' ? String(breadcrumb.data?.url ?? '') : '';
    // The health probe polls every 5-30s and would drown out useful breadcrumbs
    if (url.includes('/api/health')) return null;
    return breadcrumb;
  },
  beforeSend(event) {
    const parts = [event.message ?? '', ...(event.exception?.values ?? []).map((value) => `${value.type}: ${value.value}`)];
    const text = parts.join(' ') + JSON.stringify(event.exception?.values?.[0]?.stacktrace?.frames ?? '');
    // Browser-extension and ResizeObserver noise is not actionable and would drown real issues
    if (text.includes('chrome-extension://') || text.includes('moz-extension://') || text.includes('safari-web-extension://')) return null;
    if (text.includes('ResizeObserver loop')) return null;
    return event;
  },
});

export { Sentry };
