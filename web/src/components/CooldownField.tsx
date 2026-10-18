import * as React from 'react';
import { Minus, Plus } from 'lucide-react';
import { Slider } from '@/components/ui/slider';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
import type { Translations } from '@/i18n';
import {
  COOLDOWN_SLIDER_MAX_POS,
  MAX_COOLDOWN_MINUTES,
  MIN_COOLDOWN_MINUTES,
  formatCooldown,
  minutesToSliderPos,
  parseCooldownText,
  sliderPosToMinutes,
} from '@/lib/cooldown';

interface Props {
  id?: string;
  /** Stored check cooldown in whole minutes */
  value: number;
  /** Receives whole minutes */
  onChange: (minutes: number) => void;
  t: Translations;
  className?: string;
}

/**
 * Cooldown-oriented editor for whole-minute cooldown: a log-scale slider
 * (slow minute-by-minute dragging up to ~1 h, several-minute steps towards
 * the 24 h edge) plus a manual input showing compact localized text
 * ("15 min", "1 h 30 min"), the same editing flow as PercentField.
 */
export function CooldownField({ id, value, onChange, t, className }: Props) {
  const [text, setText] = React.useState(() => formatCooldown(value, t));
  const editing = React.useRef(false);
  const onChangeRef = React.useRef(onChange);
  onChangeRef.current = onChange;

  React.useEffect(() => {
    if (!editing.current) setText(formatCooldown(value, t));
  }, [value, t]);

  function handleSliderChange([pos]: number[]) {
    const minutes = sliderPosToMinutes(pos);
    setText(formatCooldown(minutes, t));
    onChangeRef.current(minutes);
  }

  function handleInputChange(e: React.ChangeEvent<HTMLInputElement>) {
    const raw = e.target.value;
    setText(raw);
    // Plain minutes and the display forms parse live ("22" → 22, "22 ч" → 1320,
    // "1 ч 30 мин" → 90); partial or junk text ("2 ч ", "abc") does not
    // propagate, blur reverts the field to the last valid value
    const parsed = parseCooldownText(raw);
    if (parsed !== null && parsed >= MIN_COOLDOWN_MINUTES) {
      onChangeRef.current(Math.min(parsed, MAX_COOLDOWN_MINUTES));
    }
  }

  function handleBlur() {
    editing.current = false;
    // Revert garbage or partial input ("", "1.", "abc") to the last valid value
    setText(formatCooldown(value, t));
  }

  // +/- buttons step in natural minutes (the log slider has no linear step):
  // fine near a minute, coarse near a day
  const stepMinutes = value < 10 ? 1 : value < 60 ? 5 : value < 180 ? 15 : value < 600 ? 30 : 60;
  function handleStep(dir: 1 | -1) {
    const next = Math.min(MAX_COOLDOWN_MINUTES, Math.max(MIN_COOLDOWN_MINUTES, value + dir * stepMinutes));
    if (next === value) return;
    setText(formatCooldown(next, t));
    onChangeRef.current(next);
  }

  return (
    <div className={cn('flex items-center gap-2', className)}>
      <Slider
        min={0}
        max={COOLDOWN_SLIDER_MAX_POS}
        step={1}
        value={[minutesToSliderPos(value)]}
        onValueChange={handleSliderChange}
        aria-labelledby={id}
        className="min-w-0 flex-1"
      />
      {/* [-] [value] [+]: the formatted text carries its own units, so no absolute
          suffix here; w-24 fits the longest value ("23 ч 59 мин") without clipping,
          the +/- buttons keep 44px touch targets on mobile */}
      <Button
        type="button"
        variant="outline"
        size="icon"
        aria-label={t.cooldown.decrease}
        title={t.cooldown.decrease}
        disabled={value <= MIN_COOLDOWN_MINUTES}
        onClick={() => handleStep(-1)}
        className="size-11 shrink-0 md:size-10"
      >
        <Minus className="h-4 w-4" />
      </Button>
      <Input
        id={id}
        type="text"
        inputMode="text"
        value={text}
        onChange={handleInputChange}
        onFocus={(e) => { editing.current = true; e.currentTarget.select(); }}
        onBlur={handleBlur}
        className="w-24 shrink-0 text-center tabular-nums md:w-28"
      />
      <Button
        type="button"
        variant="outline"
        size="icon"
        aria-label={t.cooldown.increase}
        title={t.cooldown.increase}
        disabled={value >= MAX_COOLDOWN_MINUTES}
        onClick={() => handleStep(1)}
        className="size-11 shrink-0 md:size-10"
      >
        <Plus className="h-4 w-4" />
      </Button>
    </div>
  );
}
