# AGENTS.md — Stock Indicators

Self-hosted, single-user market-indicator app: one operator, their own provider keys (BYOK),
their own Postgres. AGPL-3.0, no billing, no multi-user auth, no telemetry. The frontend ships
inside the backend image; the public landing and the SEO'd indicator catalog are part of the
product. There is no hosted instance - everything in this repository must work from a fresh
`docker compose up` on the operator's machine.

## Layout

```
src/    .NET 10 solution (stock-indicators.sln)
  StockIndicators.API                composition root, controllers, SignalR hub, internal middleware
                                     (Program.cs decides which providers exist - see Providers below)
  StockIndicators.Contracts          DTOs shared with the frontend (the frontend mirrors them by hand)
  StockIndicators.Abstract           cross-cutting abstractions
  StockIndicators.Components.Utils   small shared helpers
  Application                        use cases (queries/commands + behaviors), provider-agnostic
  Domain                             entities, repository interfaces, domain events
  Infrastructure                     EF Core context + migrations, Postgres, Redis, SMTP, gateways
  StockIndicators.Providers.*        market-data adapters (Tinkoff, Alpaca, Binance, BusinessQuant)
  StockIndicators.Providers.Base     shared polling/caching plumbing for the adapters
  StockIndicators.Enrichers.Yahoo    instrument metadata enrichment
  Tests/StockIndicators.Indicators.Tests, StockIndicators.Providers.Tests   xUnit
  tools/tinkoff-tls-bridge           Go dev tool (see Running locally)
web/    React 19 + Vite + TypeScript + Tailwind v4 + shadcn/ui (PWA); no test runner -
        the checks are lint + typecheck + check:i18n + check:seo + build
packaging/  docker-compose, .env.example, image wiring
docs/   DATA_SOURCES.md, screenshots (README)
```

Requests go Controller → `IQuery`/`ICommand` → pipeline behavior (validation, logging) →
handler. Handlers stay thin; domain rules belong in Domain, I/O in Infrastructure. Responses
leave through the `Envelope` filter (`EnvelopeActionFilter`/`EnvelopeExceptionFilter`): the
frontend `request()` unwraps `{ statusCode, result }`. Client-visible failures throw typed
exceptions, not status codes.

## Commands

```bash
cd src && dotnet build && dotnet test
cd web && npm run lint && npm run typecheck && npm run check:i18n && npm run check:seo && npm run build
```

**Never push without the owner's explicit word.** Commit as the author this repository is
configured for (`git config user.name` / `user.email`) - never set an identity ad hoc per commit.

Never leave a dev server or `docker compose` stack running after a check: kill the process /
`docker compose down`. When killing test instances, kill by PID - the owner's own dev instance
usually runs `dotnet run` on 5051, and the dev Telegram bot loses updates to a second poller.

## Running locally

- Backend: `cd src && dotnet run --project StockIndicators.API` → `https://localhost:5051`.
  `src/StockIndicators.API/appsettings.Local.json` is the topmost configuration layer
  (`AddJsonFile`, gitignored): the whole dev config lives there - Postgres, Redis, `LocalAuth`,
  provider keys, dev bot. Never commit it, never quote its values.
- Frontend dev: `cd web && npm run dev` (vite proxies `/api` to the backend).
- Migrations: EF applies them at startup. Create from the API project:
  `dotnet ef migrations add <Name> --project ../Infrastructure/StockIndicators.Infrastructure`.
  The released schema is the rebaselined `InitialCreate` plus additive migrations; there are no
  third-party installs to stay compatible with yet, so prefer clean schema changes over
  compatibility shims.
- Tinkoff/T-Bank on macOS: the provider dials `invest-public-api.tbank.ru:443`, whose chain the
  OS TLS stack cannot verify - run `go run ./tools/tinkoff-tls-bridge` (port 18443) and keep
  `TinkoffProviderOptions:TlsBridgeAddress` in the Local file. The connection tester assembles
  the SDK's channel shape on purpose (authority with `:443`, `SslCredentials` + plugin
  interceptor): a plain metadata call gets `40003 Unauthenticated` with a perfectly good token.

## Providers (BYOK)

`Application/Providers/ProviderRegistry.cs` is the single source of truth the settings page
renders: code, kind, env section, docs URL, markets, fields. Semantics:

- The database (`ProviderSettings`) wins over the environment (`<Section>:<Field>`); env stays
  the fallback. Secrets round-trip to the UI in full - the page is behind the password and the
  UI masks them behind a reveal toggle.
- Wiring happens once, at composition: enablement and required keys decide whether a provider
  exists in the process at all. Saves update the live `ProviderSettingsSnapshot` in place, but
  take effect only after restart - `StartupRows` (frozen at startup) vs the stored row is what
  marks a provider `restartPending`, and the amber icon on the settings page shows that.
- `EnvEnabledDefault`: channels that are free when configured come up enabled; WhatsApp is
  opt-in (`false`) - registry default, sender check and the `whatsappEnabled` config flag must
  stay in agreement.
- Connection tests probe exactly what the provider itself calls (Shares for Tinkoff, Trading-API
  assets for Alpaca) - never an endpoint that tests an entitlement the key may legitimately
  lack (GetAccounts scopes, the paid overnight feed). The Telegram test fails when link buttons
  are on but the instance address is one Telegram refuses.
- `ProviderFieldKind.Url` marks an address something outside the process opens (the bot's
  `InstanceUrl`, `BotLink`). `Notifications/Telegram/PublicWebAddress.cs` and
  `web/src/lib/publicUrl.ts` mirror each other and reject loopback, LAN names and LAN addresses:
  Telegram validates every button URL on its own servers, so a local address fails the whole
  message with `Wrong HTTP URL`. The providers page flags it while the value is being typed.
- `TelegramBotOptions.IncludeWebLinks` (off by default) is what makes the instance address used
  at all: `CanSendWebLinks` gates every url button, and bot forms and alerts go out without a
  keyboard while it is off or the address is unusable - never with a link Telegram rejects.
- Display copy: the registry stays English; the frontend localizes it by code through
  `settings.providers.items` in each locale (backend labels are the fallback). Connection-test
  result messages are backend English diagnostics.

## Frontend

- **No browser scrollbar.** A page must never overflow the viewport; all scrolling happens
  inside the content container. Page root is `flex flex-col` with `h-[var(--app-page-h)]` (or
  `h-[var(--app-page-h-bar)]` when there is a fixed mobile action bar); the scroller is
  `relative flex-1 min-h-0 overflow-y-auto`. `relative` is not cosmetic: an `sr-only` span inside
  a tall skeleton with no positioned ancestor escapes the scroller and opens a phantom browser
  scrollbar. Chrome geometry lives in the tokens in `index.css` — never hardcode
  `calc(100dvh - 177px)` or `bottom-14`, and never add a global `overflow: hidden`.
- **Loading states.** Content still loading → `Skeleton` laid out like the real content, wrapped
  in `role="status"` with the visible label moved to an `sr-only` span. Lazy route chunk →
  `RouteFallback`. A pending user action → `Loader2` inside the button. A waiting page with
  nothing to replace (link/consent screens) → `Loader2`.
- **Hints on touch.** Radix tooltips never open on touch, so a trigger without its own action
  uses `Hint`/`HintTrigger`/`HintContent` (tooltip on hover devices, popover on touch, detected
  via `hooks/useHasHover.ts`). A trigger with an action keeps the tooltip for hover and shows the
  explanation another way (toast on tap, or `aria-disabled` + `Hint` wrapper when the guard blocks
  the button). Native `title` is never enough — it does not show in mobile WebView at all.
- **Touch targets** are at least 44×44 (`size-11`, or `min-h-11` plus `flex-1`); insets under the
  status bar / home indicator use `--safe-t` / `--safe-b` only.
- **Routing.** Everything inside the app lives under `/{locale}/dashboard/...`; build paths with
  `dashboardPrefix(locale)` from `lib/paths.ts`, never by hand. Legacy un-prefixed paths are
  permanent redirects — backend links stay locale-neutral (`/dashboard/...`) and get localized on
  arrival, so old notification links keep working.
- **Instance mode.** `lib/siteMode.ts` reads `/api/site/config` once; the API also stamps the answer
  (`window.__SI_SITE_LANDING_ONLY__`) into every HTML shell it serves (`Internal/SiteModeScript.cs`,
  marker comment in `web/index.html`), so a backend-served instance knows its shape at first paint
  and the landing never flashes a sign-in affordance it would take back - the probe is the fallback
  for environments without the stamp (vite dev). On a public-site instance
  (`PublicSite:LandingOnly` on the backend) the landing's entry points and calls to action go to
  the install section and the repository instead of a login, and `/login` plus every dashboard
  route lead back to the localized landing (`AppArea` in `App.tsx`). An unreadable answer means
  the whole product: a self-hosted install is the shape this software exists for, so a failed
  probe may cost a button, never a working dashboard.
- **Charts** all render through `components/InteractiveChart.tsx` over `lib/chartCore`; a new
  chart surface wraps it instead of drawing its own chart code.
- **i18n.** Interface texts live in `src/i18n/<locale>.json`; `en.json` is the type source
  (`Translations = typeof en`), so all nine locales (en, ru, be, uk, de, es, fr, it, pt-BR) must
  be edited in the same commit and `npm run check:i18n` must pass. A script touching a locale
  file rewrites the **whole document** back (`json.dump` of the parsed tree) - partial splicing
  has destroyed locale files before. `fr`/`de` address the user as `tu`/`du`.
- **Single dashes in UI text.** User-facing copy never uses a double dash: write ` - ` (a plain
  hyphen, spaces around it where a dash reads) - never `—`, `–` or `--` - in locale files, in
  strings rendered by components, and in backend-owned user text (`LocalizationCatalog`,
  indicator template descriptions, Telegram messages, alert images). Code comments, log lines
  and verbatim license texts (`lib/openSourceLicenses.ts`) are exempt.
- **Unsaved changes.** A form with unsaved edits registers the shared
  `NavigationGuardProvider` guard (see `SubscriptionFormPage`) so navigation, logout, tab close
  and reload confirm first; dirty sections highlight (`border-primary/50 bg-primary/[0.04]`) and
  carry the `common.updated` chip. The providers panel follows the same rules.
- **Contracts.** `web/src/types/contracts.ts` is a hand-maintained mirror of the C# DTOs -
  change both sides in the same commit; there is no generator.
- **JSX gotchas.** `//` outside `{}` in JSX renders as visible text; `///` doc comments inside
  a record's parameter list are CS1587 - use `//`.

## Backend

- The backend does not localize: user-facing copy is frontend i18n. Strings the backend sends
  to the browser (connection-test results, delivery-error codes mapped in
  `subscriptions.triggerHistory.errors.*`) follow the existing conventions - prefer stable codes
  and single dashes (the frontend "Single dashes in UI text" rule applies to them too).
- UI-owned state that must persist (the settings guide's dismissal) goes to the `AppSettings`
  key/value table (jsonb, `IAppSettingsStore`); provider credentials have their own table.
- Redis is a best-effort second-level cache only. Nothing may require it: the app must start and
  work with an empty `RedisConfiguration.Hosts`.
- No secret, provider key or user data ever goes into the repository or into a log line; all
  configuration arrives through `appsettings.json` / `Section__Key` environment variables
  (plus the gitignored `appsettings.Local.json` for development).
- Every behavior change gets a test in `Tests/StockIndicators.Indicators.Tests` — the suite is
  the contract for the rules/indicators pipeline.
- Alert images render through headless Chromium against the frontend's `renderer` page
  (`ChartImage` section; `ChartImage:PageHtmlPath` is null in `appsettings.json`, but the
  Dockerfile turns the renderer on with an `ENV` and ships the browser - the published landing
  gets its real link-preview pictures from it, so keep the Dockerfile's chromium/font layers in
  sync with the frontend's rendering needs). Without a working renderer (path unset or unreadable,
  or Chromium that will not start) the share endpoints redirect to the static brand card, so a
  link is never left without a picture.
- The project's own landing is published by `packaging/deploy-landing.sh` (host details in the
  gitignored `packaging/deploy-landing.env`). A publish from a mac must carry
  `-p:PlaywrightPlatform=linux-x64` - MSBuild otherwise copies only the darwin node driver and
  the server's renderer starts without Chromium, degrading every link preview to the brand card -
  and the service's env file must keep `ChartImage__PageHtmlPath` (`appsettings.json` ships it
  null, only the Dockerfile's ENV sets it); the script enforces both and verifies the picture
  after the restart.
- **Public-site mode** (`PublicSite:LandingOnly`, how the project's own landing is published):
  `Program.cs` registers `AddPublicSite` instead of the application, so no session, database,
  provider, hub or scheduler exists to be reached, and `PublicSiteGate` answers every other
  `/api`, `/hubs`, `/swagger`, `/metrics` path with the 404 envelope. A controller is public only
  when it carries `[PublicSite]` - the gate reads its own route templates, so forgetting the marker
  hides an endpoint rather than exposing one, and a marked controller must stay anonymous. The
  public handlers are listed in `AddPublicSite`: MediatR scans both assemblies and the rest is
  filtered back out, because development container validation would build those handlers against
  repositories this graph does not have.
- Rule presets are product content owned by the code (`Application/Rules/Data/rule-presets.json`,
  read by `RulePresetCatalog`), not a seeded table: the public catalog serves them without a
  database, and changing a preset is a release change guarded by `RulePresetCatalogTests` instead
  of a migration.

## Data sources

The application redistributes no market data: each user connects their own provider account with
their own key. Do not add a shared/demo key, a proxy through our own account, or a seeded
"try it out" feed — that both breaks the providers' terms and defeats the point of self-hosting.
