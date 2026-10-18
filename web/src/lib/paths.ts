/**
 * Root of the authenticated dashboard app. The marketing landing owns the
 * localized root ("/ru", "/en", "/es"), every managed page lives one level
 * below it ("/{locale}/dashboard/..."). Auth and legal pages stay outside the
 * dashboard namespace.
 */
export const dashboardPrefix = (locale: string): string => `/${locale}/dashboard`;
