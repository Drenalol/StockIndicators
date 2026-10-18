import { useTranslation } from '@/i18n';
import { Badge } from '@/components/ui/badge';
import { Clock } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { ExtendedQuoteData } from '@/types/contracts';

function defaultFormatPrice(value: number, locale: string): string {
  return value.toLocaleString(locale, {
    minimumFractionDigits: 2,
    maximumFractionDigits: Math.abs(value) < 10 ? 4 : 2,
  });
}

/**
 * The extended-hours quote line (like the public quote services show it): while the
 * regular session has not opened (or has already closed), the chart candles legitimately
 * stop at the last regular bar, so the newest pre/post-market trade travels as its own
 * line, priced against the matching regular close. Renders nothing outside extended hours.
 */
export function ChartExtendedLine({
  extended,
  formatPrice,
  className,
}: {
  extended: ExtendedQuoteData | null | undefined;
  formatPrice?: (value: number) => string;
  className?: string;
}) {
  const { t, locale } = useTranslation();

  if (!extended) return null;

  const format = formatPrice ?? ((v: number) => defaultFormatPrice(v, locale));
  const label =
    extended.session === 'pre'
      ? t.common.chartPreMarket
      : extended.session === 'overnight'
        ? t.common.chartOvernight
        : t.common.chartAfterHours;

  const pct = extended.referenceClose && extended.referenceClose !== 0
    ? (extended.price / extended.referenceClose - 1) * 100
    : null;
  const up = pct !== null && pct > 0;
  const down = pct !== null && pct < 0;
  const pctText = pct === null
    ? null
    : `${pct > 0 ? '+' : pct < 0 ? '-' : ''}${new Intl.NumberFormat(locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(Math.abs(pct))}%`;

  return (
    <Badge variant="secondary" className={cn('py-1.5 gap-1.5 font-normal', className)}>
      <Clock className="h-3.5 w-3.5 text-muted-foreground" />
      <span className="text-muted-foreground">{label}:</span>
      <span className="tabular-nums">{format(extended.price)}</span>
      {pctText !== null && (
        <span className={cn('tabular-nums', up ? 'text-success' : down ? 'text-destructive' : 'text-muted-foreground')}>
          {pctText}
        </span>
      )}
    </Badge>
  );
}
