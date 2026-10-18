import logoSvg from '@/assets/logo.svg?raw';
import { cn } from '@/lib/utils';

interface Props {
  className?: string;
}

/**
 * App logo (candle-chart mark, no wordmark - the brand name lives in
 * surrounding HTML text). The navy parts use fill="currentColor" (tinted by
 * text-foreground) so they stay readable on both themes; the green candles
 * keep their fixed brand colors.
 * Sizing goes on the wrapper as height classes.
 */
export function Logo({ className }: Props) {
  return (
    <span
      role="img"
      aria-label="Stock-Indicators"
      className={cn('inline-flex shrink-0 items-center text-foreground [&>svg]:h-full [&>svg]:w-auto', className)}
      dangerouslySetInnerHTML={{ __html: logoSvg }}
    />
  );
}
