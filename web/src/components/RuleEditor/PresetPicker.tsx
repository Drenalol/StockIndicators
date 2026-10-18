import { useEffect, useId, useState } from 'react';
import { ChevronDown } from 'lucide-react';
import { SegmentedPresetChip } from './SegmentedPresetChip';
import type { Translations } from '@/i18n';
import type { RulePresetDto } from '@/types/contracts';

interface Props {
  presets: RulePresetDto[];
  /** The preset the form was last filled from (the chip stays selected) */
  activeKey: string | null;
  onPick: (preset: RulePresetDto) => void;
  t: Translations;
}

/**
 * One-tap preset chips from the backend seed catalog, kept under a spoiler
 * (most users pick the indicator and tune parameters themselves). Picking a
 * preset pre-fills the rule constructor (the whole condition tree for combos).
 * Catalog members sharing a family render as one segmented chip.
 * A deep-linked preset (?preset=) opens the spoiler when it gets applied.
 */
export function PresetPicker({ presets, activeKey, onPick, t }: Props) {
  const contentId = useId();
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (activeKey) setOpen(true);
  }, [activeKey]);

  if (presets.length === 0) return null;

  const active = open ? presets.find((preset) => preset.key === activeKey) : undefined;

  return (
    <div className="space-y-1.5">
      <button
        type="button"
        aria-expanded={open}
        aria-controls={contentId}
        onClick={() => setOpen((prev) => !prev)}
        className="-mx-1 flex w-full items-center justify-between gap-2 rounded-md px-1 py-1.5 text-left text-xs font-medium text-muted-foreground transition-colors hover:text-foreground"
      >
        {t.rules.presetsTitle}
        <ChevronDown className={`h-4 w-4 shrink-0 transition-transform ${open ? 'rotate-180' : ''}`} />
      </button>
      {open && (
        <div id={contentId} className="flex flex-wrap gap-2">
          {groupPresets(presets).map((group) => {
            if (group.family && group.members.length > 1) {
              return (
                <SegmentedPresetChip
                  key={`${group.family}:${group.members[0].key}`}
                  family={group.family}
                  label={group.members.find((member) => member.familyLabel)?.familyLabel ?? group.family}
                  members={group.members}
                  activeKey={activeKey}
                  onPick={onPick}
                />
              );
            }
            return group.members.map((preset) => {
              const selected = preset.key === activeKey;
              return (
                <button
                  key={preset.key}
                  type="button"
                  aria-pressed={selected}
                  onClick={() => onPick(preset)}
                  className={`inline-flex min-h-11 items-center gap-1.5 rounded-full border px-4 text-sm transition-colors ${selected ? 'border-primary bg-primary/10 font-medium text-primary' : 'bg-background hover:bg-accent hover:text-accent-foreground'}`}
                >
                  {preset.name}
                </button>
              );
            });
          })}
        </div>
      )}
      <p className="text-xs text-muted-foreground">{active?.description ?? t.rules.presetsHint}</p>
    </div>
  );
}

interface PresetGroup {
  family: string | null;
  members: RulePresetDto[];
}

/** Consecutive catalog members sharing a family collapse into one chip group. */
function groupPresets(presets: RulePresetDto[]): PresetGroup[] {
  const groups: PresetGroup[] = [];
  for (const preset of presets) {
    const last = groups[groups.length - 1];
    if (preset.family && last?.family === preset.family) last.members.push(preset);
    else groups.push({ family: preset.family, members: [preset] });
  }
  return groups;
}
