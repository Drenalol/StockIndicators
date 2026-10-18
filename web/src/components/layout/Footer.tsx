import { Link, useParams } from 'react-router-dom';
import { useTranslation, SUPPORTED_LOCALES, type Locale } from '@/i18n';
import { BOOSTY_URL } from '@/lib/support';

export default function Footer() {
  const { t, locale } = useTranslation();
  const params = useParams();
  const pathLocale = params.locale as Locale | undefined;
  const routeLocale = pathLocale && SUPPORTED_LOCALES.includes(pathLocale) ? pathLocale : locale;

  const links = [
    { to: `/${routeLocale}/legal/disclaimer`, label: t.footer.disclaimer },
    { to: `/${routeLocale}/legal/licenses`, label: t.footer.licenses },
  ];

  return (
    <footer className="hidden md:block border-t bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
      <div className="mx-auto flex min-h-12 w-full flex-wrap items-center gap-x-4 gap-y-1 px-4 py-2 text-xs text-muted-foreground md:px-6">
        <nav className="flex flex-wrap items-center gap-x-4 gap-y-1">
          {links.map(({ to, label }) => (
            <Link key={to} to={to} className="whitespace-nowrap transition-colors hover:text-foreground">
              {label}
            </Link>
          ))}
          <a
            href={BOOSTY_URL}
            target="_blank"
            rel="noopener noreferrer"
            className="whitespace-nowrap transition-colors hover:text-foreground"
          >
            {t.footer.support}
          </a>
        </nav>
        <span className="ml-auto whitespace-nowrap">© {new Date().getFullYear()} {t.common.appName}</span>
      </div>
    </footer>
  );
}
