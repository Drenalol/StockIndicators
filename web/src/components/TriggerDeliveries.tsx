import { CheckCircle2, XCircle, MinusCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Hint, HintContent, HintTrigger } from '@/components/ui/hint';
import { useHasHover } from '@/hooks/useHasHover';
import { channelLabel } from '@/lib/alertChannels';
import { formatTriggerDate } from '@/lib/datetime';
import type { TriggerDeliveryDto } from '@/types/contracts';
import type { Translations } from '@/i18n';
import type { Locale } from '@/i18n/types';

interface Props {
  deliveries: TriggerDeliveryDto[] | undefined;
  t: Translations;
  locale: Locale;
}

/** Known delivery error codes persisted by the backend (DeliveryErrors.*), shown localized. */
const ERROR_CODE_RE = /^delivery_error:([a-z0-9_]+)$/;

type DeliveryErrorCatalog = Translations['subscriptions']['triggerHistory']['errors'];

function deliveryErrorText(error: string, errors: DeliveryErrorCatalog): string {
  const match = ERROR_CODE_RE.exec(error);
  if (!match) return error;
  const key = match[1].replace(/_([a-z0-9])/g, (_, c: string) => c.toUpperCase()) as keyof DeliveryErrorCatalog;
  return errors[key] ?? error;
}

/**
 * Per-channel delivery status of one trigger: a compact badge per channel
 * (delivered / failed / skipped) with the delivery time and the error text
 * behind a hover tooltip (touch devices get a tap-opened popover). Legacy
 * triggers without delivery rows render a dash. Known error codes are
 * localized; provider/legacy free text is shown as-is.
 */
export function TriggerDeliveries({ deliveries, t, locale }: Props) {
  const d = t.subscriptions.triggerHistory.delivery;
  const hasHover = useHasHover();

  if (!deliveries || deliveries.length === 0) {
    return <span className="text-xs text-muted-foreground">-</span>;
  }

  return (
    <div className="flex flex-wrap gap-1.5">
      {deliveries.map((delivery) => {
        const failed = delivery.status === 'Failed';
        const skipped = delivery.status === 'Skipped';

        return (
          <Hint key={`${delivery.channel}-${delivery.sentAt}`} delayDuration={150}>
            <HintTrigger asChild>
              <Badge
                variant={failed ? 'destructive' : skipped ? 'secondary' : 'success'}
                className="text-[10px] py-0.5 gap-1 cursor-default"
                // On touch the tap opens the popover, so it must not also trigger
                // the row navigation the badge may sit in. On hover devices the
                // click is deliberately left to bubble to the row.
                onClick={hasHover ? undefined : (e) => e.stopPropagation()}
              >
                {failed ? <XCircle className="h-3 w-3" /> : skipped ? <MinusCircle className="h-3 w-3" /> : <CheckCircle2 className="h-3 w-3" />}
                {channelLabel(delivery.channel, t)}
              </Badge>
            </HintTrigger>
            {/* 340px keeps the longest localized error texts on one line. The arrow wrapper
                (Radix span with an inline trigger-following `top`) is pinned to the edge middle,
                otherwise the tip rides the short badge while the hint is several lines tall. */}
            <HintContent side="left" className="max-w-[340px] whitespace-normal [&>span]:top-[calc(50%_-_5px)]!">
              <div className="text-xs">
                <div>
                  {failed ? d.failed : skipped ? d.skipped : d.sent} · {formatTriggerDate(delivery.sentAt, locale)}
                </div>
                {delivery.error && <div className="mt-1 opacity-80 break-words">{deliveryErrorText(delivery.error, t.subscriptions.triggerHistory.errors)}</div>}
              </div>
            </HintContent>
          </Hint>
        );
      })}
    </div>
  );
}
