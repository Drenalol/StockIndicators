using StockIndicators.Application.Instruments.GetInstruments;
using StockIndicators.Application.Instruments.Logos;
using Xunit;

namespace StockIndicators.Providers.Tests;

public class InstrumentLogoRequestTests
{
  [Theory]
  // Binance pairs and Alpaca crypto are crypto even without a catalog entry
  [InlineData("TONUSDT", "BINANCE", null, true)]
  [InlineData("BTC/USD", "CRYPTO", null, true)]
  [InlineData("ANYCOIN", "SOME_EXCHANGE", "Crypto", true)]
  // stocks/ETFs are not
  [InlineData("SBER", "MOEX", "Stock", false)]
  [InlineData("AAPL", null, "Stock", false)]
  [InlineData("GOLD", "MOEX", "Etf", false)]
  [InlineData("TONUSDT", "BINANCE", "Crypto", true)]
  public void IsCrypto_detects_crypto_by_type_exchange_and_shape(string ticker, string? exchange, string? catalogType, bool expected)
  {
    var catalog = catalogType is null ? null : new InstrumentDto { Ticker = ticker, Currency = "USDT", Type = catalogType, Source = "x" };

    Assert.Equal(expected, new InstrumentLogoRequest(ticker, exchange, catalog).IsCrypto);
  }

  [Theory]
  // Binance concatenated pairs strip the catalog quote currency
  [InlineData("TONUSDT", "USDT", "TON")]
  [InlineData("BTCUSDT", "USDT", "BTC")]
  [InlineData("ETHRUB", "RUB", "ETH")]
  // slash symbols take the first part
  [InlineData("BTC/USD", "USD", "BTC")]
  // unknown quotes still strip well-known ones
  [InlineData("XRPFDUSD", "FDUSD", "XRP")]
  // non-crypto tickers pass through unchanged (a quote-suffix must not eat real letters)
  [InlineData("BTC", "USD", "BTC")]
  [InlineData("USDT", "USDT", "USDT")]
  [InlineData("SBER", "RUR", "SBER")]
  [InlineData("STOXSBER", "RUR", "STOXSBER")]
  public void BaseAsset_extracts_the_base_symbol(string ticker, string? quoteCurrency, string expected)
  {
    var catalog = quoteCurrency is null ? null : new InstrumentDto { Ticker = ticker, Currency = quoteCurrency, Type = "Crypto", Source = "binance" };

    Assert.Equal(expected, InstrumentLogoRequest.GetBaseAsset(catalog, ticker));
  }

  [Theory]
  [InlineData("sber", null, "instrument-logo:sber:")]
  [InlineData("SBER", "MOEX", "instrument-logo:SBER:MOEX")]
  [InlineData("TONUSDT", "BINANCE", "instrument-logo:TONUSDT:BINANCE")]
  public void Keys_roundtrip_ticker_and_exchange(string ticker, string? exchange, string expected) =>
    Assert.Equal(expected, InstrumentLogoKeys.For(ticker, exchange));
}
