import { useMemo } from 'react';
import { Button } from '@/components/ui/button';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useTranslation } from '@/i18n';

export function getPageSize(mobile = 10, desktop = 30): number {
  if (typeof window === 'undefined') return desktop;
  return window.innerWidth < 640 ? mobile : desktop;
}

export function PaginationControls({ page, totalPages, onPageChange }: {
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}) {
  const { t } = useTranslation();
  const getPageNumbers = useMemo(() => {
    const pages: (number | '...')[] = [];
    if (totalPages <= 7) {
      for (let i = 1; i <= totalPages; i++) pages.push(i);
    } else {
      pages.push(1);
      if (page > 3) pages.push('...');
      for (let i = Math.max(2, page - 1); i <= Math.min(totalPages - 1, page + 1); i++) {
        pages.push(i);
      }
      if (page < totalPages - 2) pages.push('...');
      pages.push(totalPages);
    }
    return pages;
  }, [page, totalPages]);

  return (
    <div className="flex items-center justify-center gap-1 pt-2 pb-2">
      <Button
        variant="outline"
        size="icon"
        className="h-8 w-8"
        disabled={page <= 1}
        aria-label={t.pagination.prev}
        onClick={() => onPageChange(Math.max(1, page - 1))}
      >
        <ChevronLeft className="h-4 w-4" />
      </Button>

      {getPageNumbers.map((p, i) =>
        p === '...' ? (
          <span key={`ellipsis-${i}`} className="px-1 text-muted-foreground">…</span>
        ) : (
          <Button
            key={p}
            variant={page === p ? 'default' : 'outline'}
            size="icon"
            className={cn('h-8 min-w-8 px-2', page === p && 'pointer-events-none')}
            onClick={() => onPageChange(p as number)}
          >
            {p}
          </Button>
        )
      )}

      <Button
        variant="outline"
        size="icon"
        className="h-8 w-8"
        disabled={page >= totalPages}
        aria-label={t.pagination.next}
        onClick={() => onPageChange(Math.min(totalPages, page + 1))}
      >
        <ChevronRight className="h-4 w-4" />
      </Button>
    </div>
  );
}
