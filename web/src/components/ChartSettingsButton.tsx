import type { CSSProperties } from 'react';
import { Settings2 } from 'lucide-react';
import { useTranslation } from '@/i18n';
import { useChartPreferences } from '@/hooks/useChartPreferences';
import { Button } from '@/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Switch } from '@/components/ui/switch';

/**
 * The chart display options gear (per-account preferences: OHLC tooltip, crosshair
 * magnet), persisted server-side like the subscriptions sort. Placement belongs to
 * the caller (className): floating over a dashboard card's corner, or inline in a
 * fullscreen dialog's title row where it does not cover the chart's OHLC legend.
 */
export function ChartSettingsButton({ className, style }: { className?: string; style?: CSSProperties }) {
  const { t } = useTranslation();
  const settings = t.subscriptions.triggerHistory.chart.settings;
  const { tooltipEnabled, magnetEnabled, toggle } = useChartPreferences();

  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button variant="ghost" size="icon" className={className} style={style} aria-label={settings.title}>
          <Settings2 className="h-5 w-5" />
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="flex w-64 flex-col gap-4">
        <p className="text-sm font-medium">{settings.title}</p>
        <label className="flex items-center justify-between gap-4 text-sm">
          {settings.tooltip}
          <Switch checked={tooltipEnabled} onCheckedChange={(on) => toggle({ ohlcTooltip: on })} />
        </label>
        <label className="flex items-center justify-between gap-4 text-sm">
          {settings.magnet}
          <Switch checked={magnetEnabled} onCheckedChange={(on) => toggle({ magnet: on })} />
        </label>
      </PopoverContent>
    </Popover>
  );
}
