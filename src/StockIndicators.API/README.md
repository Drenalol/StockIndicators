# Stock Indicators API

The ASP.NET Core backend: candle ingestion from your own providers, rule evaluation, alert
delivery, and the frontend it serves from `wwwroot`. Single user, single origin — the
[repository README](../README.md) covers the product and installation.

## Running from source

```bash
# database (or any Postgres 14+ reachable by the connection string below)
docker run -d --name si-postgres -p 5432:5432 \
  -e POSTGRES_DB=stock_indicators -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres \
  postgres:17-alpine

cd src
export ConnectionStrings__CandleStorage="Host=127.0.0.1;Port=5432;Database=stock_indicators;Username=postgres;Password=postgres"
export LocalAuth__Password=dev-password            # or LocalAuth__PasswordHash, see below
dotnet run --project StockIndicators.API           # https://localhost:5051
```

Migrations run at startup; the one account is seeded automatically. Swagger UI: `/swagger`.
The frontend dev server (`web`, `npm run dev`) proxies `/api` here.

To generate the PBKDF2 password digest instead of the plain value:

```bash
dotnet run --project StockIndicators.API -- hash-password 'your password'
```

## Configuration

Sections bind from `appsettings.json` and are overridable as `Section__Key` environment variables.
Provider API keys normally live in the database (Settings → Providers, edited through
`/api/providers`); environment values are the fallback for headless setups. For per-machine values without
exporting variables there is `appsettings.Local.json` (git- and docker-ignored, topmost
layer):

| Key | Purpose |
|---|---|
| `ConnectionStrings__CandleStorage` | Postgres (required) |
| `LocalAuth__Password` / `LocalAuth__PasswordHash` | the sign-in password (plaintext or PBKDF2 digest); unset = nobody can sign in |
| `RedisConfiguration__Hosts__0__Host` | optional redis cache; the app runs without it |
| `TelegramBotOptions__Token` / `BotLink` / `IncludeWebLinks` / `InstanceUrl` | your bot, created with @BotFather (or stored via Settings → Providers); link buttons are off until `IncludeWebLinks` is on |
| `ChartImage__PageHtmlPath` | renderer page for alert chart images; unset = alerts without pictures |
| `SmtpOptions__*` | SMTP relay for the email channel (optional) |
| `Metrics__Enabled` | expose the Prometheus `/metrics` endpoint (off by default) |
| `AlertOptions__PublicBaseUrl` | base URL in links inside notifications |
| `Frontend__Origins` | extra origins for CORS/CSRF; not needed single-origin |

Which providers and channels exist is decided at startup (enabled + configured ⇒ registered), so
changes on the providers page apply after a restart.

## Layout

```
StockIndicators.API/          composition root, controllers, internal middleware (Program.cs wires everything)
Application/                  use cases (CQRS handlers), provider-agnostic domain services
Domain/                       entities, statuses, domain services
Infrastructure/               EF Core + Dapper persistence, notification channels, chart rendering
StockIndicators.Providers/    market-data adapters (Tinkoff, Alpaca, Binance, BusinessQuant, Yahoo enricher)
Tests/                        xUnit suites: unit tests and provider integration tests
```

Charts: the same `ChartData` JSON feeds the web chart core; alert images are rasterized by headless
Chromium (Microsoft.Playwright) over the frontend's `render/renderer.html` page. A publish copies
the Playwright node driver of the build platform only: publishing from a mac or a windows box for
a Linux server needs `-p:PlaywrightPlatform=linux-x64`, or the deployed renderer has no browser to
start (the Dockerfile builds inside the target image and needs nothing).

## Tests

```bash
cd src && dotnet test
```

Provider integration tests hit live sandbox/public endpoints; unit tests never do.
