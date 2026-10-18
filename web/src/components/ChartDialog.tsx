import type { LandingExampleChartDto } from '@/types/contracts';
import { useTranslation } from '@/i18n';
import { getIndicatorFullNameWithAbbr } from '@/i18n/indicatorTranslations';
import { useAuth } from '@/hooks/useAuth';
import { ExchangeLogo } from '@/components/ExchangeLogo';
import { ChartSettingsButton } from '@/components/ChartSettingsButton';
import { InteractiveChart } from '@/components/InteractiveChart';
import { Badge } from '@/components/ui/badge';
import { Dialog, DialogContent } from '@/components/ui/dialog';

/**
 * Fullscreen view of one landing example chart. The preview cards are static (every drag
 * belongs to the page), so a reader who wants to inspect a chart gets it here: the chart
 * takes the whole viewport and pans in every direction like in the dashboard
 * (interactive), with the trigger marker and indicator overlays.
 */
export function ChartDialog({ example, open, onOpenChange }: {
  example: LandingExampleChartDto | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const { t } = useTranslation();
  const { isAuthenticated } = useAuth();

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent size="fullscreen">
        {example && (
          <div className="flex h-full min-h-0 flex-col gap-3">
            {/* 44px-tall toolbar line: the gear joins the title (only when signed in),
                the X aligns on its center; pr-11 sm:pr-9 = card padding + gear-to-X gap
                of exactly one gap-2 */}
            <div className="flex min-h-11 flex-wrap items-center gap-2 pr-11 sm:pr-9">
              <span className="font-mono text-lg font-semibold">{example.ticker}</span>
              {example.exchangeName && <ExchangeLogo name={example.exchangeName} />}
              <Badge variant="secondary">{getIndicatorFullNameWithAbbr(example.indicator, t)}</Badge>
              {isAuthenticated && <ChartSettingsButton className="ml-auto size-11" />}
            </div>
            <div className="min-h-0 flex-1">
              <InteractiveChart data={example.chart} fill interactive />
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
