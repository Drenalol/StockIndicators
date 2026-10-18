import * as React from 'react';
import { Slider } from '@/components/ui/slider';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
import {
  DEVIATION_SLIDER_MAX_POS,
  formatPercent,
  percentToSliderPos,
  sliderPosToPercent,
} from '@/lib/deviation';

interface Props {
  id?: string;
  /** Stored deviation fraction (0.0005 = 0.05%); undefined means "not set" */
  value: number | undefined;
  /** Receives the fraction, or undefined when the field is cleared */
  onChange: (fraction: number | undefined) => void;
  className?: string;
}

/**
 * Percent-oriented editor for fraction-valued parameters: a log-scale slider
 * (0 at the left edge, 0.01%→100%) plus a manual input with a "%" suffix.
 * The slider cannot land on values like 0.05% exactly, so the input is the
 * precise path; both stay in sync with the stored fraction.
 */
export function PercentField({ id, value, onChange, className }: Props) {
  const [text, setText] = React.useState(() => (value === undefined ? '' : formatPercent(value * 100)));
  const editing = React.useRef(false);
  const onChangeRef = React.useRef(onChange);
  onChangeRef.current = onChange;

  React.useEffect(() => {
    if (!editing.current) setText(value === undefined ? '' : formatPercent(value * 100));
  }, [value]);

  function handleSliderChange([pos]: number[]) {
    // Snap to two significant digits so dragging lands on clean values
    // (0.05%, 5%, 10%) instead of log-scale artifacts like 0.0501%; the
    // input next to the slider remains the path for exact values
    const pct = parseFloat(sliderPosToPercent(pos).toPrecision(2));
    setText(formatPercent(pct));
    onChangeRef.current(Number((pct / 100).toPrecision(6)));
  }

  function handleInputChange(e: React.ChangeEvent<HTMLInputElement>) {
    const raw = e.target.value;
    setText(raw);
    const normalized = raw.replace(',', '.').trim();
    if (normalized === '') {
      onChangeRef.current(undefined);
      return;
    }
    const pct = Number(normalized);
    if (Number.isFinite(pct) && pct >= 0) {
      onChangeRef.current(Number((pct / 100).toPrecision(6)));
    }
  }

  function handleBlur() {
    editing.current = false;
    // Revert garbage or partial input ("1.", "-", "abc") to the last valid value
    setText(value === undefined ? '' : formatPercent(value * 100));
  }

  return (
    <div className={cn('flex items-center gap-2', className)}>
      <Slider
        min={0}
        max={DEVIATION_SLIDER_MAX_POS}
        step={1}
        value={[percentToSliderPos((value ?? 0) * 100)]}
        onValueChange={handleSliderChange}
        aria-labelledby={id}
        className="min-w-0 flex-1"
      />
      <div className="relative w-24 shrink-0">
        <Input
          id={id}
          type="text"
          inputMode="decimal"
          value={text}
          onChange={handleInputChange}
          onFocus={() => { editing.current = true; }}
          onBlur={handleBlur}
          className="pr-6 text-right tabular-nums"
        />
        <span className="pointer-events-none absolute inset-y-0 right-3 flex items-center text-sm text-muted-foreground">
          %
        </span>
      </div>
    </div>
  );
}
