import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useReducedMotion } from 'motion/react';
import { useTranslation } from '@/i18n';
import { useListPainted } from '@/hooks/useListPainted';
import { getIndicatorName, getIndicatorShortName, getIndicatorCategoryLabel } from '@/i18n/indicatorTranslations';
import type { IndicatorCatalogDto } from '@/types/contracts';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
import { Search, ArrowRight } from 'lucide-react';
import { cn } from '@/lib/utils';

interface Props {
  /** Catalog rows from GET /api/indicator/catalog */
  items: IndicatorCatalogDto[];
  pending: boolean;
  error: string | null;
  onOpen: (name: string) => void;
  /** When given, cards render as real links (Cmd/middle-click opens a tab); otherwise as buttons */
  hrefFor?: (name: string) => string;
  /**
   * Wrapper classes for the grid body: the dashboard passes its viewport-fit scroll
   * container (`flex-1 min-h-0 overflow-y-auto`), the public page leaves it empty so
   * the document itself scrolls.
   */
  bodyClassName?: string;
}

/**
 * Indicator card grid with client-side search, shared by the dashboard page and the
 * public landing indicators page. The data comes from the same public catalog
 * endpoint in both places, so a new indicator (backend enum member + template)
 * appears on both surfaces without touching this component.
 */
export function IndicatorsCatalog({ items, pending, error, onOpen, hrefFor, bodyClassName }: Props) {
  const { t } = useTranslation();
  const [search, setSearch] = useState('');

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return items;
    return items.filter((item) =>
      getIndicatorName(item.name, t).toLowerCase().includes(q)
      || getIndicatorShortName(item.name).toLowerCase().includes(q)
    );
  }, [items, search, t]);

  // Freshly committed cards paint progressively (see useListPainted); the gate keys
  // on the cache entry, so filtering stays instant.
  const reduceMotion = useReducedMotion();
  const { painted, busy } = useListPainted(items, visible.length);

  const skeletonGrid = (
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 overflow-hidden">
      {Array.from({ length: 6 }).map((_, i) => (
        <Card key={i} className="h-full">
          <CardContent className="h-full pt-4 px-4 pb-4 flex flex-col">
            <Skeleton className="h-5 w-2/3" />
            <Skeleton className="mt-1.5 h-3 w-10" />
            <Skeleton className="mt-3 h-3 w-full" />
            <Skeleton className="mt-1.5 h-3 w-4/5" />
            <div className="mt-auto flex items-center justify-between gap-2 pt-3">
              <Skeleton className="h-5 w-16 rounded-full" />
              <Skeleton className="h-4 w-4" />
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );

  if (pending) {
    return (
      <div role="status">
        <span className="sr-only">{t.common.loading}</span>
        <Skeleton className="h-10 w-full" />
        <div className="mt-6">{skeletonGrid}</div>
      </div>
    );
  }

  return (
    <>
      {/* Pinned outside the scroll container: an overflow ancestor clips the focus ring of an input flush with its edges */}
      {items.length > 0 && (
        <form onSubmit={(e) => e.preventDefault()} className="relative w-full">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t.indicators.searchPlaceholder}
            className="pl-9"
            aria-label={t.indicators.searchPlaceholder}
          />
        </form>
      )}

      <div className={cn('mt-6', bodyClassName)}>
        {error && <Alert variant="destructive" className="mb-6"><AlertDescription>{error}</AlertDescription></Alert>}

        {!busy && items.length === 0 && <p className="text-center py-8 text-muted-foreground">{t.indicators.noneAvailable}</p>}

        {!busy && items.length > 0 && visible.length === 0 && (
          <p className="text-center py-8 text-muted-foreground">{t.indicators.noResults}</p>
        )}

        {visible.length > 0 && (
          <div className="relative">
            <div className={cn('grid gap-3 sm:grid-cols-2 lg:grid-cols-3', !reduceMotion && 'transition-opacity duration-150', painted ? 'opacity-100' : 'opacity-0')} aria-hidden={!painted}>
              {visible.map((item) => {
                const body = (
                  <Card className="h-full transition-shadow group-hover:shadow-md group-hover:border-primary/40">
                    <CardContent className="h-full pt-4 px-4 pb-4 flex flex-col">
                      <div>
                        <p className="text-base md:text-lg font-semibold leading-snug">{getIndicatorName(item.name, t)}</p>
                        <p className="text-xs text-muted-foreground mt-0.5">{getIndicatorShortName(item.name)}</p>
                      </div>
                      {item.description && (
                        <p className="mt-2 line-clamp-3 text-sm leading-relaxed text-muted-foreground">{item.description}</p>
                      )}
                      <div className="mt-auto flex items-center justify-between gap-2 pt-3">
                        <span className="flex min-w-0 flex-wrap items-center gap-1.5">
                          <Badge variant="secondary" className="text-[11px]">{getIndicatorCategoryLabel(item.name, t)}</Badge>
                        </span>
                        <ArrowRight className="h-4 w-4 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5 group-hover:text-primary" />
                      </div>
                    </CardContent>
                  </Card>
                );
                const cardClass = 'group block h-full rounded-lg text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring';

                return hrefFor ? (
                  <Link key={item.name} to={hrefFor(item.name)} className={cardClass}>
                    {body}
                  </Link>
                ) : (
                  <button key={item.name} type="button" onClick={() => onOpen(item.name)} className={cardClass}>
                    {body}
                  </button>
                );
              })}
            </div>
            {busy && (
              <div className="absolute inset-0 z-10" role="status">
                <span className="sr-only">{t.common.loading}</span>
                {skeletonGrid}
              </div>
            )}
          </div>
        )}
      </div>
    </>
  );
}
