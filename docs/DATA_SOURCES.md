# Data sources

Stock Indicators ships **no market data**. There is no demo mode, no shared account and no proxy:
every candle comes from a provider **you** connect with **your own** API key, and lands in **your
own** database.

## Providers

| Provider | Coverage | What you need |
|---|---|---|
| Tinkoff Invest API | Moscow Exchange: stocks, ETFs, bonds, futures, currencies | an invest-api token from your Tinkoff account |
| [Alpaca](https://docs.alpaca.markets/us/docs/trading-api) | US equities | an Alpaca API key/secret — the Trading API scope is all the app uses (paper keys work) |
| Binance | crypto pairs | none for public candles, an API key for higher rate limits |
| BusinessQuant | EOD + intraday for US/global equities | an API key |
| Yahoo Finance (enricher) | instrument names, sectors, descriptions | none (best effort, unofficial) |

Enable and configure providers under **Settings → Providers** in the app; the values are stored in
the database, environment variables stay a fallback. Which providers exist is decided at startup,
so a newly enabled provider needs an instance restart.

## Your obligations

Each provider's own terms decide what you may do with the data pulled with your key. In general:

- personal, non-commercial use on your own machine is the safe default;
- redistributing quotes (an instance reachable by other people with your keys) usually needs a
  commercial agreement with the provider — read their terms before exposing an instance;
- some providers (Tinkoff, BusinessQuant) restrict where their data may be processed and stored.

The synthetic candles used on the landing page and in the indicator examples are generated locally
(`ExampleCandles` in the backend) — they are not anyone's market data.

## Instrument logos

Logos cascade through three sources, each skipped when unconfigured: the Tinkoff brand CDN (no key),
[Logo.dev](https://logo.dev) (free publishable key) and [CoinGecko](https://www.coingecko.com)
(demo key works at low volume). The UI credits Logo.dev in the footer; keep that credit if you keep
the feature, per Logo.dev's free-tier terms.
