import { ArrowDown, ArrowUp } from 'lucide-react';
import type { RulePresetDto } from '@/types/contracts';

interface Props {
  family: string;
  /** Localized chip prefix shared by the members (from the catalog) */
  label: string;
  /** Family members in catalog order */
  members: RulePresetDto[];
  activeKey: string | null;
  onPick: (preset: RulePresetDto) => void;
}

/** Direction glyph per family, by member position in the catalog order. Unknown families get no glyph. */
const FAMILY_ICONS: Record<string, (typeof ArrowUp)[]> = {
  'rsi-zones': [ArrowUp, ArrowDown],
  'macd-cross': [ArrowUp, ArrowDown],
  'bb-breakout': [ArrowUp, ArrowDown],
  'gap-move': [ArrowUp, ArrowDown],
  'openchange-move': [ArrowDown, ArrowUp],
  'week52-extremes': [ArrowUp, ArrowDown],
};

/**
 * A preset family rendered as one chip: a shared label plus a one-tap segment
 * per member (e.g. "RSI | перекуплен ↑ / перепродан ↓"). Tapping a segment
 * applies that member's preset; the active segment stays filled while the rest
 * of the catalog chips keep their outline.
 */
export function SegmentedPresetChip({ family, label, members, activeKey, onPick }: Props) {
  const icons = FAMILY_ICONS[family];
  const selected = members.some((member) => member.key === activeKey);

  return (
    <div
      className={`inline-flex min-h-11 items-center gap-1 rounded-full border py-1 pl-4 pr-1 ${selected ? 'border-primary bg-primary/10' : 'bg-background'}`}
    >
      <span className="text-sm font-medium">{label}</span>
      {members.map((member, index) => {
        const active = member.key === activeKey;
        const Icon = icons?.[index];
        return (
          <button
            key={member.key}
            type="button"
            aria-pressed={active}
            title={member.name}
            onClick={() => onPick(member)}
            className={`inline-flex h-9 items-center gap-1 rounded-full px-3 text-sm transition-colors ${active ? 'bg-primary font-medium text-primary-foreground' : 'hover:bg-accent hover:text-accent-foreground'}`}
          >
            {member.segment}
            {Icon && <Icon className="h-3.5 w-3.5 shrink-0" />}
          </button>
        );
      })}
    </div>
  );
}
