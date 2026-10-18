// Post-login redirect target ("?from=" on /login). Only in-app, language-neutral
// dashboard paths are accepted ("dashboard/subscriptions/new?indicator=RSI"), so a
// crafted from= can never bounce the user to an external origin (open redirect).
export function sanitizeLoginFrom(value: string | null | undefined): string | null {
  if (!value) return null;
  return /^dashboard\/[A-Za-z0-9/_?=&%#.-]*$/.test(value) ? value : null;
}
