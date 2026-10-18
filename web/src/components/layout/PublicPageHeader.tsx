import type { ReactNode } from 'react';
import { ArrowLeft } from 'lucide-react';
import { useTranslation } from '@/i18n';
import { useGoBackOr } from '@/hooks/useGoBackOr';
import { Logo } from '@/components/Logo';
import { Button } from '@/components/ui/button';

interface Props {
  /** Where the back arrow goes when there is no in-app history to pop (usually the landing) */
  backFallback: string;
  /** Optional right-aligned actions (language/theme controls) */
  actions?: ReactNode;
  /** Header inner width; legal pages read at max-w-3xl, the catalog grid uses max-w-5xl */
  wide?: boolean;
}

/**
 * Slim sticky header for the public pages outside MainLayout (legal documents,
 * the landing indicators catalog): a back arrow and the logo, no auth chrome.
 */
export function PublicPageHeader({ backFallback, actions, wide }: Props) {
  const { t } = useTranslation();
  const goBackOr = useGoBackOr();

  return (
    <header className="sticky top-0 z-40 border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 pt-[var(--safe-t)]">
      <div className={`mx-auto flex h-14 w-full items-center gap-2 px-3 ${wide ? 'max-w-5xl' : 'max-w-3xl'}`}>
        <Button
          type="button"
          variant="ghost"
          className="size-11 flex-shrink-0 text-muted-foreground hover:text-foreground"
          aria-label={t.common.back}
          onClick={() => goBackOr(backFallback)}
        >
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <Logo className="h-9 md:h-11" />
        {actions && <div className="ml-auto flex items-center gap-1.5">{actions}</div>}
      </div>
    </header>
  );
}
