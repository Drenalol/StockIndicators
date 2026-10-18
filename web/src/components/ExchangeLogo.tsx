import { ExchangeBadge } from '@/components/ExchangeBadge';
import { cn } from '@/lib/utils';
import moex from '@/assets/exchanges/moex.svg?raw';
import nasdaq from '@/assets/exchanges/nasdaq.svg?raw';
import nyse from '@/assets/exchanges/nyse.svg?raw';
import cboe from '@/assets/exchanges/cboe.svg?raw';
import iex from '@/assets/exchanges/iex.svg?raw';
import otc from '@/assets/exchanges/otc.svg?raw';
import binance from '@/assets/exchanges/binance.svg?raw';

interface Props {
  name: string;
  className?: string;
  /** Logo height; the literal classes keep Tailwind's JIT happy */
  size?: 'sm' | 'lg';
}

// The "[&>svg]" classes must sit on the span that directly wraps the injected svg,
// and they need "!important": cmdk forces sizes/colors onto every descendant svg
// of a CommandItem ("[&_svg]:size-4", "[&_[cmdk-item]_svg]:h-5", text-muted-foreground),
// which otherwise shrink the wordmarks and flatten their color inside the dropdown.
const SIZES = {
  sm: { wrap: 'h-5', svg: '[&>svg]:h-5! [&>svg]:w-auto! [&>svg]:text-foreground!' },
  lg: { wrap: 'h-6', svg: '[&>svg]:h-6! [&>svg]:w-auto! [&>svg]:text-foreground!' },
} as const;

interface ExchangeMark {
  svg: string;
  label?: string;
}

// Keys are the uppercase exchange names produced by the backend providers.
// Wordmarks use fill="currentColor" (tinted by text-foreground) so they stay
// visible on both themes; brand accents (colors) are kept where they read on
// both backgrounds. NYSE sub-brands render the NYSE wordmark + suffix label.
const LOGOS: Record<string, ExchangeMark> = {
  MOEX: { svg: moex },
  NASDAQ: { svg: nasdaq },
  NYSE: { svg: nyse },
  BINANCE: { svg: binance },
  BATS: { svg: cboe },
  CBOE: { svg: cboe },
  IEX: { svg: iex },
  OTC: { svg: otc },
  ARCA: { svg: nyse, label: 'Arca' },
  AMEX: { svg: nyse, label: 'American' },
  'NYSE MKT': { svg: nyse, label: 'American' },
};

/**
 * Exchange logo shown next to a ticker chip. Falls back to the text
 * ExchangeBadge for names without a bundled logo (e.g. CRYPTO or unknown
 * provider values) so the exchange never disappears entirely.
 */
export function ExchangeLogo({ name, className, size = 'sm' }: Props) {
  const mark = LOGOS[name.trim().toUpperCase()];
  if (!mark) return <ExchangeBadge name={name} className={className} />;

  return (
    <span
      role="img"
      aria-label={name}
      title={name}
      className={cn('inline-flex flex-shrink-0 items-center gap-1.5 text-foreground', SIZES[size].wrap, className)}
    >
      <span
        className={cn('contents', SIZES[size].svg)}
        dangerouslySetInnerHTML={{ __html: mark.svg }}
      />
      {mark.label && (
        <span className="text-xs font-mono font-semibold">{mark.label}</span>
      )}
    </span>
  );
}
