import { useQuery } from '@tanstack/react-query';
import { api, type AppConfig } from '@/services/api';
import { qk } from '@/lib/query';
import type { SubscriptionStatus } from '@/types/contracts';
import type { Translations } from '@/i18n';

/**
 * Server-driven limits from /api/Config (the same values the backend validates
 * against). The config is cached once per session — pages mounting one after
 * another do not re-download it.
 */
export function useLimits(): AppConfig | undefined {
  const { data: config } = useQuery({ queryKey: qk.config, queryFn: () => api.getConfig() });
  return config;
}

/** Leaf conditions per rule — fallback until /api/Config resolves. */
export const DEFAULT_MAX_CONDITIONS = 20;

/** Shortest check cooldown, seconds; the server value rules once resolved. */
export function minCooldownSecondsOf(config: AppConfig | undefined): number {
  return config?.minCooldownSeconds ?? 60;
}

/** Cooldown a new subscription starts with, seconds. */
export function defaultCooldownSecondsOf(config: AppConfig | undefined): number {
  return config?.defaultCooldownSeconds ?? 3600;
}

export function maxConditionsPerRuleOf(config: AppConfig | undefined): number {
  return config?.maxConditionsPerRule ?? DEFAULT_MAX_CONDITIONS;
}

/** Alert output channels per subscription. */
export function maxOutputsOf(config: AppConfig | undefined): number {
  return config?.maxOutputs ?? 5;
}

/**
 * Status badge view shared by the list and the detail.
 */
export function statusView(status: SubscriptionStatus, t: Translations): { label: string; variant: 'success' | 'destructive' | 'outline'; className?: string } {
  if (status === 'Active') return { label: t.subscriptions.status.active, variant: 'success' };
  return { label: t.subscriptions.status.inactive, variant: 'destructive' };
}
