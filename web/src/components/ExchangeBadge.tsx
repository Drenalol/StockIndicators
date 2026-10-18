import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

interface Props {
  name: string;
  className?: string;
}

/**
 * Exchange badge shown next to a ticker chip. Sized to match the ticker code
 * chip (text-sm px-2 py-1, see the subscription group card) so the two sit
 * on one visual line everywhere the exchange is rendered.
 */
export function ExchangeBadge({ name, className }: Props) {
  return (
    <Badge variant="secondary" className={cn('flex-shrink-0 px-2 py-1 text-sm font-mono uppercase', className)}>
      {name}
    </Badge>
  );
}
