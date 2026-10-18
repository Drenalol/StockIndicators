import type { SubscriptionDto } from '@/types/contracts';

/**
 * A subscription's alert is "new" while a notification arrived after the user last
 * opened it (LastTriggeredAt > LastReadAt on the backend); opening the alert stamps
 * LastReadAt through POST /subscription/{id}/read.
 */
export function isSubNew(sub: Pick<SubscriptionDto, 'lastTriggeredAt' | 'lastReadAt'>): boolean {
  return !!sub.lastTriggeredAt && (!sub.lastReadAt || sub.lastTriggeredAt > sub.lastReadAt);
}
