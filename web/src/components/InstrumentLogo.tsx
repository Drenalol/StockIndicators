import { useState } from 'react';
import { apiUrl } from '@/services/api';
import { cn } from '@/lib/utils';

interface Props {
  ticker: string;
  exchangeName?: string | null;
  className?: string;
  /** Rendered box size; the literal classes keep Tailwind's JIT happy */
  size?: 'xs' | 'sm' | 'lg';
}

const SIZES = {
  xs: 'h-5 w-5 text-[10px]',
  sm: 'h-7 w-7 text-xs',
  lg: 'h-11 w-11 text-base',
} as const;

/**
 * Public backend endpoint: Redis-cached image resolved from the issuer brand data,
 * Logo.dev and CoinGecko (see "Instrument Logos" in the backend README).
 */
function logoUrl(ticker: string, exchangeName?: string | null): string {
  const params = new URLSearchParams({ ticker });
  if (exchangeName) params.set('exchange', exchangeName);
  // absolute: in a bundled shell a relative src would resolve against the WebView's own
  // capacitor://localhost origin, where no API lives, and every logo would fall to a monogram
  return apiUrl(`/instrument/logo?${params.toString()}`);
}

/**
 * Instrument logo with a letter-monogram fallback: the backend answers 404 when no
 * source has a logo for the instrument, and the component keeps rendering the same
 * monogram the UI used before logos existed (never a broken image).
 */
export function InstrumentLogo({ ticker, exchangeName, className, size = 'sm' }: Props) {
  const [failedFor, setFailedFor] = useState<string | null>(null);
  const src = logoUrl(ticker, exchangeName);

  if (failedFor !== src) {
    return (
      <img
        src={src}
        alt=""
        loading="lazy"
        onError={() => setFailedFor(src)}
        className={cn('rounded-lg object-contain flex-shrink-0', SIZES[size], className)}
      />
    );
  }

  const letter = (ticker[0] ?? '?').toUpperCase();

  return (
    <div className={cn('rounded-lg bg-primary/10 text-primary flex items-center justify-center font-semibold flex-shrink-0', SIZES[size], className)}>
      {letter}
    </div>
  );
}
