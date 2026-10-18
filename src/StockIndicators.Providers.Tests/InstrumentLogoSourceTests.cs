using Microsoft.Extensions.Options;
using StockIndicators.Application.Instruments.GetInstrumentLogo;
using StockIndicators.Application.Instruments.GetInstruments;
using StockIndicators.Application.Instruments.Logos;
using Xunit;

namespace StockIndicators.Providers.Tests;

public class InstrumentLogoSourceTests
{
  [Fact]
  public void BrandFileCandidates_tries_exact_then_sized_variants()
  {
    var candidates = TinkoffBrandLogoSource.BrandFileCandidates("sber3.png");

    Assert.Equal("sber3.png", candidates[0]);
    Assert.Contains("sber3x160.png", candidates);
    Assert.Contains("sber3x640.png", candidates);
  }

  [Fact]
  public void BrandFile_candidates_for_name_with_baked_size_keep_exact_first()
  {
    var candidates = TinkoffBrandLogoSource.BrandFileCandidates("vkcx640.png");

    Assert.Equal("vkcx640.png", candidates[0]);
    Assert.Equal("vkcx640160.png", candidates[^1]);
  }

  [Fact]
  public void LookupPaths_for_stock_uses_ticker_then_isin()
  {
    var request = new InstrumentLogoRequest("AAPL", null, new InstrumentDto { Ticker = "AAPL", Currency = "USD", Isin = "US0378331005", Source = "alpaca" });

    var paths = LogoDevLogoSource.LookupPaths(request);

    Assert.Equal("ticker/AAPL", paths[0]);
    Assert.Equal("isin/US0378331005", paths[1]);
  }

  [Fact]
  public void LookupPaths_for_crypto_uses_base_asset_symbol()
  {
    var request = new InstrumentLogoRequest("TONUSDT", "BINANCE", new InstrumentDto { Ticker = "TONUSDT", Currency = "USDT", Type = "Crypto", Source = "binance" });

    Assert.Equal(["crypto/TON"], LogoDevLogoSource.LookupPaths(request));
  }

  [Fact]
  public void BuildUrl_pins_png_size_and_404_fallback()
  {
    var options = new LogoOptions { LogoDevPublishableKey = "pk_test", LogoDevBaseUrl = "https://img.logo.dev" };

    var url = LogoDevLogoSource.BuildUrl(options, "ticker/AAPL").ToString();

    Assert.StartsWith("https://img.logo.dev/ticker/AAPL?", url);
    Assert.Contains("token=pk_test", url);
    Assert.Contains("format=png", url);
    Assert.Contains("size=64", url);
    // Without fallback=404 Logo.dev answers 200 with its own monogram image
    Assert.Contains("fallback=404", url);
  }

  [Fact]
  public void CoinGecko_rank_order_prefers_the_canonical_coin_over_lookalikes()
  {
    // "ton" (Toncoin, rebranded) is ranked, the fake clone is not; id order is irrelevant.
    var ordered = CoinGeckoLogoSource.OrderByRank([
      new CoinGeckoLogoSource.CoinCandidate("ton-fake", null, "https://x/fake"),
      new CoinGeckoLogoSource.CoinCandidate("the-open-network", 13, "https://x/ton"),
      new CoinGeckoLogoSource.CoinCandidate("ton-2", 9000, "https://x/ton2")
    ]);

    Assert.Equal(["the-open-network", "ton-2", "ton-fake"], ordered.Select(c => c.Id));
  }

  [Fact]
  public void ParseKey_recovers_ticker_and_exchange()
  {
    Assert.Equal(("SBER", "MOEX"), GetInstrumentLogoQueryHandler.ParseKey("instrument-logo:SBER:MOEX"));
    Assert.Equal(("AAPL", null), GetInstrumentLogoQueryHandler.ParseKey("instrument-logo:AAPL:"));
  }
}
