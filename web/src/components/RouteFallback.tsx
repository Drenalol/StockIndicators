import { useTranslation } from '@/i18n';
import { Skeleton } from '@/components/ui/skeleton';

/**
 * Generic page skeleton shown while a lazy route chunk loads (fullPage: outside
 * the app chrome). A spinner here would flash before the page's own loading
 * skeleton, so both states shimmer the same way (see AGENTS.md "Loading states").
 */
export function RouteFallback({ fullPage }: { fullPage?: boolean }) {
  const { t } = useTranslation();

  const page = (
    <div className="flex flex-col" role="status">
      <span className="sr-only">{t.common.loading}</span>
      <Skeleton className="h-7 w-40" />
      <Skeleton className="mt-6 h-10 w-full" />
      <div className="mt-6 space-y-3">
        {Array.from({ length: 4 }).map((_, i) => (
          <Skeleton key={i} className="h-16 w-full rounded-lg" />
        ))}
      </div>
    </div>
  );

  if (fullPage) {
    return (
      <div className="min-h-[100dvh] bg-background">
        <div className="mx-auto w-full max-w-3xl px-3 md:px-4 pt-10">{page}</div>
      </div>
    );
  }
  return page;
}
