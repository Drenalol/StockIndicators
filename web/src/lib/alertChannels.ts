import type { Translations } from '@/i18n';

/**
 * Channel knowledge shared by the subscription form, the detail page and the
 * delivery badges (the backend SubscriptionOutputValidation mirrors these rules).
 */

/** Every channel is free to attach; WhatsApp additionally needs server-side Meta credentials. */
export const ALL_CHANNELS = ['Telegram', 'WebPush', 'Webhook', 'Discord', 'WhatsApp'] as const;

export function isUrlChannel(channel: string): boolean {
  return channel === 'Webhook' || channel === 'Discord';
}

export function isWhatsApp(channel: string): boolean {
  return channel === 'WhatsApp';
}

/** Localized channel name; unknown channels render their raw name. */
export function channelLabel(channel: string, t: Translations): string {
  switch (channel) {
    case 'Telegram':
      return t.notifications.channels.telegram;
    case 'WebPush':
      return t.notifications.channels.browser;
    case 'Webhook':
      return t.notifications.channels.webhook;
    case 'Discord':
      return t.notifications.channels.discord;
    case 'WhatsApp':
      return t.notifications.channels.whatsapp;
    default:
      return channel;
  }
}

/** Strict http(s) absolute URL check (mirrors the backend NormalizeUrl). */
export function isValidTargetUrl(raw: string | null | undefined): boolean {
  if (!raw || !raw.trim()) return false;

  try {
    const url = new URL(raw.trim());
    return (url.protocol === 'https:' || url.protocol === 'http:') && url.host.length > 0 && raw.trim().length <= 2048;
  } catch {
    return false;
  }
}

/**
 * Strips formatting characters and accepts an international number with or
 * without the plus; returns the E.164 form or null (mirrors the backend).
 */
export function normalizeWhatsAppPhone(raw: string | null | undefined): string | null {
  if (!raw) return null;

  let digits = raw.replaceAll(/[\s()-]/g, '').trim();

  if (!digits.startsWith('+')) digits = `+${digits}`;

  return /^\+[1-9]\d{5,14}$/.test(digits) ? digits : null;
}
