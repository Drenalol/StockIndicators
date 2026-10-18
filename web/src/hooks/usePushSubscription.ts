import { useCallback, useEffect, useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/services/api';
import { qk } from '@/lib/query';
import type { PushSubscriptionDto } from '@/types/contracts';

export type PushPermission = NotificationPermission | 'unsupported';

function urlBase64ToArrayBuffer(base64: string): ArrayBuffer {
  const padding = '='.repeat((4 - (base64.length % 4)) % 4);
  const base64Url = (base64 + padding).replace(/-/g, '+').replace(/_/g, '/');
  const latin = atob(base64Url);
  const array = new Uint8Array(latin.length);
  for (let i = 0; i < latin.length; i += 1) array[i] = latin.charCodeAt(i);
  return array.buffer;
}

export interface UsePushSubscriptionResult {
  permission: PushPermission;
  subscriptions: PushSubscriptionDto[];
  activeEndpoint: string | null;
  loading: boolean;
  enable: () => Promise<void>;
  disable: () => Promise<void>;
  sendTest: () => Promise<number>;
}

export function usePushSubscription(): UsePushSubscriptionResult {
  const queryClient = useQueryClient();
  const [permission, setPermission] = useState<PushPermission>('unsupported');
  const [activeEndpoint, setActiveEndpoint] = useState<string | null>(null);

  const browserSupported = typeof window !== 'undefined' && 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;

  // The server-side list lives in the shared cache (the subscription detail page
  // runs the deliverability check on the very same data)
  const subsQuery = useQuery({
    queryKey: qk.pushSubscriptions,
    queryFn: () => api.getPushSubscriptions(),
    enabled: browserSupported,
  });
  const subscriptions = useMemo(() => subsQuery.data ?? [], [subsQuery.data]);
  const loading = browserSupported ? subsQuery.isPending : false;

  // Keep permission in sync when the user changes it in browser settings
  useEffect(() => {
    if (!browserSupported) return;
    const permissionEvents = Notification as typeof Notification & { onPermissionChange?: (() => void) | null };
    if (permissionEvents.onPermissionChange === undefined) return;
    const onChange = () => setPermission(Notification.permission);
    permissionEvents.onPermissionChange = onChange;
    return () => { permissionEvents.onPermissionChange = null; };
  }, [browserSupported]);

  // Mark the entry matching the endpoint this browser is registered with,
  // whenever the (cached) list arrives or changes
  useEffect(() => {
    if (!browserSupported) {
      setPermission('unsupported');
      return;
    }
    setPermission(Notification.permission);
    const list = subsQuery.data;
    if (!list) return;
    let cancelled = false;
    void (async () => {
      try {
        const registration = await navigator.serviceWorker.getRegistration();
        const browserSub = registration ? await registration.pushManager.getSubscription() : null;
        if (!cancelled && browserSub && list.some((s) => s.endpoint === browserSub.endpoint)) {
          setActiveEndpoint(browserSub.endpoint);
        }
      } catch {
        /* keep empty state */
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [browserSupported, subsQuery.data]);

  const enable = useCallback(async () => {
    if (!browserSupported) return;
    const perm = await Notification.requestPermission();
    setPermission(perm);
    if (perm !== 'granted') return;

    const { key } = await api.getVapidPublicKey();
    const registration = await navigator.serviceWorker.ready;
    const existing = await registration.pushManager.getSubscription();
    const subscription =
      existing ??
      (await registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToArrayBuffer(key),
      }));

    const json = subscription.toJSON();
    await api.savePushSubscription(
      {
        endpoint: json.endpoint ?? '',
        keys: { p256dh: json.keys?.p256dh ?? '', auth: json.keys?.auth ?? '' },
        deviceLabel: `${navigator.platform || 'web'}`,
      },
    );
    const list = await api.getPushSubscriptions();
    queryClient.setQueryData(qk.pushSubscriptions, list);
    setActiveEndpoint(subscription.endpoint);
  }, [browserSupported, queryClient]);

  const disable = useCallback(async () => {
    if (!browserSupported) return;
    const registration = await navigator.serviceWorker.getRegistration();
    const browserSub = registration ? await registration.pushManager.getSubscription() : null;
    if (browserSub) {
      await browserSub.unsubscribe();
      const match = subscriptions.find((s) => s.endpoint === browserSub.endpoint);
      if (match) await api.deletePushSubscription(match.id);
    }
    queryClient.setQueryData(qk.pushSubscriptions, await api.getPushSubscriptions());
    setActiveEndpoint(null);
  }, [subscriptions, browserSupported, queryClient]);

  const sendTest = useCallback(async () => {
    const result = await api.sendPushTest();
    return result.sent;
  }, []);

  return { permission, subscriptions, activeEndpoint, loading, enable, disable, sendTest };
}
