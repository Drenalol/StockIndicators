import { BellRing, Database, RotateCw, Send } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useOnboardingState } from '@/hooks/useOnboarding';
import { useTranslation } from '@/i18n';

/**
 * First-run guide: the settings page carries a lot of blocks, and the guide is the map -
 * which provider feeds which market, why the Telegram card sits twice (the bot is
 * configured on the Providers tab, the chat is linked on the Notifications tab), and what
 * waits for a restart. Dismissal persists to the database, so the guide never reappears
 * on another device.
 */
export default function OnboardingCard() {
  const { t } = useTranslation();
  const { hidden, isSettled, setHidden } = useOnboardingState();

  // nothing on first paint until the flag is known - a guide that flashes and vanishes
  // on a second visit is worse than one that appears a moment late
  if (!isSettled || hidden) return null;

  const items = [
    { icon: Database, text: t.settings.onboarding.data },
    { icon: Send, text: t.settings.onboarding.channels },
    { icon: BellRing, text: t.settings.onboarding.alerts },
    { icon: RotateCw, text: t.settings.onboarding.restart },
  ];

  return (
    <Card className="border-primary/30 bg-primary/[0.03]">
      <CardHeader>
        <CardTitle className="text-base">{t.settings.onboarding.title}</CardTitle>
        <CardDescription>{t.settings.onboarding.description}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <ul className="space-y-3">
          {items.map(({ icon: Icon, text }) => (
            <li key={text} className="flex gap-3 text-sm">
              <Icon className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
              <span className="text-balance">{text}</span>
            </li>
          ))}
        </ul>
        <div className="flex justify-end">
          <Button variant="outline" className="h-11 md:h-10" onClick={() => setHidden(true)}>
            {t.settings.onboarding.dismiss}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
