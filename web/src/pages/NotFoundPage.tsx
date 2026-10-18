import { useNavigate } from 'react-router-dom';
import { useTranslation } from '@/i18n';
import { Button } from '@/components/ui/button';
import { Home } from 'lucide-react';

export default function NotFoundPage() {
  const { t, locale } = useTranslation();
  const navigate = useNavigate();

  return (
    <div className="flex flex-col items-center justify-center h-[var(--app-page-h)] text-center">
      <p className="text-7xl md:text-8xl font-bold text-primary/80 leading-none">404</p>
      <h1 className="mt-4 md:mt-6 text-xl md:text-2xl font-bold">{t.notFound.title}</h1>
      <p className="mt-2 text-sm md:text-base text-muted-foreground max-w-md">{t.notFound.description}</p>
      <Button size="sm" className="mt-6" onClick={() => navigate(`/${locale}`, { replace: true })}>
        <Home className="h-4 w-4 mr-2" />
        {t.notFound.backHome}
      </Button>
    </div>
  );
}
