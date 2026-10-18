# Third-party notices

Stock Indicators is AGPL-3.0 (see [LICENSE](LICENSE)); it is built on open-source work whose
licenses are summarized here. The running instance also lists every bundled npm package with its
full license text under **Settings → Legal → Open Source Licenses** (generated from the lockfiles).

## Backend (.NET)

All runtime dependencies are MIT / Apache-2.0 / BSD-family licensed: ASP.NET Core and EF Core
(MIT, .NET runtime ships in the base image), `MediatR`, `FluentValidation`, `Autofac`, `Dapper`,
`Npgsql`, `Newtonsoft.Json`, `MailKit`/`MimeKit`, `NSwag`, `Microsoft.Playwright` (Apache-2.0),
`Telegram.Bot`, `FacioQuo.Stock.Indicators`, `Alpaca.Markets`, `Tinkoff.InvestApi`,
`OpenTelemetry`, `Serilog`, `LazyCache`, `Nito.AsyncEx`, health checks and the prometheus exporter.

The rendered alert images come from Chromium (BSD-3-Clause) driven by Playwright; the container
copies the Playwright browser build and the system fonts below.

## Frontend

React, Vite, `lightweight-charts` (Apache-2.0), TanStack Query, Radix UI, lucide icons, Tailwind,
`motion`, `@microsoft/signalr`, Zod and the rest of the tree — the complete generated list with
license texts lives in `web/src/lib/openSourceLicenses.ts` and is served in the app.

## Fonts and logos

- `fonts-dejavu-core` (free, Bitstream Vera derivative) and `fonts-liberation` (BSD-style) are
  installed into the Docker image for chart rendering.
- Instrument logos are served at runtime from the Tinkoff brand CDN, [Logo.dev](https://logo.dev)
  and [CoinGecko](https://www.coingecko.com) — each optional, each under its own terms; the UI
  carries the Logo.dev credit in the footer.

## Data providers

Tinkoff Invest API, Alpaca, Binance, BusinessQuant and Yahoo Finance are third-party services; the
client libraries they use (`Tinkoff.InvestApi`, `Alpaca.Markets`, plain `HttpClient`) are MIT. The
services themselves are not open source and their market data is not redistributed by this project
— see [docs/DATA_SOURCES.md](docs/DATA_SOURCES.md).
