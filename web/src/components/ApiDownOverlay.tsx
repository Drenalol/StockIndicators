import { Loader2, WifiOff } from 'lucide-react';
import { useTranslation } from '@/i18n';

interface ApiDownOverlayProps {
  offline: boolean;
}

/**
 * Full-screen overlay shown while the API is unreachable: blurs and blocks
 * the whole site (including /login) until the health endpoint answers again.
 */
export default function ApiDownOverlay({ offline }: ApiDownOverlayProps) {
  const { t } = useTranslation();

  return (
    <div
      role="status"
      aria-live="polite"
      className="fixed inset-0 z-50 flex items-center justify-center bg-background/70 backdrop-blur"
    >
      <div className="mx-4 flex flex-col items-center gap-3 text-center">
        {offline
          ? <WifiOff className="h-8 w-8 text-muted-foreground" />
          : <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />}
        <p className="text-sm font-medium md:text-base">
          {offline ? t.errors.noInternet : t.errors.apiUnavailable}
        </p>
        {offline && (
          <p className="text-xs text-muted-foreground">{t.errors.retryingOffline}</p>
        )}
      </div>
    </div>
  );
}
