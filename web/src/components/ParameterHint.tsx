import { CircleHelp } from 'lucide-react';
import { Hint, HintContent, HintTrigger } from '@/components/ui/hint';
import type { Translations } from '@/i18n';

interface Props {
  /** Localized hint text for the parameter; renders nothing when empty */
  hint?: string | null;
  t: Translations;
}

export function ParameterHint({ hint, t }: Props) {
  if (!hint) return null;

  return (
    <Hint>
      <HintTrigger asChild>
        <button
          type="button"
          aria-label={t.common.hint}
          className="inline-flex h-4 w-4 shrink-0 items-center justify-center rounded-full text-muted-foreground/60 transition-colors hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <CircleHelp className="h-3.5 w-3.5" />
        </button>
      </HintTrigger>
      <HintContent side="top" className="max-w-[280px] whitespace-normal font-normal">
        {hint}
      </HintContent>
    </Hint>
  );
}
