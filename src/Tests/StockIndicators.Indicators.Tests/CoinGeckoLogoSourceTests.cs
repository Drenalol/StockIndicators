using StockIndicators.Application.Instruments.Logos;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class CoinGeckoLogoSourceTests
{
  [Fact]
  public void OrderByRank_PrefersRankedCoinOverMemeClone()
  {
    // /coins/list orders ids alphabetically: "batcat" (a meme coin with symbol btc) came
    // before "bitcoin", and the first accepted candidate took the BTCUSDT logo for months.
    var ordered = CoinGeckoLogoSource.OrderByRank([
      new CoinGeckoLogoSource.CoinCandidate("batcat", 8677, "https://x/batcat.jpg"),
      new CoinGeckoLogoSource.CoinCandidate("bitcoin", 1, "https://x/bitcoin.png"),
      new CoinGeckoLogoSource.CoinCandidate("bitcoin-ai-2", null, "https://x/ai.jpg")
    ]);

    Assert.Equal(["bitcoin", "batcat", "bitcoin-ai-2"], ordered.Select(c => c.Id));
  }

  [Fact]
  public void OrderByRank_IsStableByIdForUnrankedCoins()
  {
    var ordered = CoinGeckoLogoSource.OrderByRank([
      new CoinGeckoLogoSource.CoinCandidate("zeta", null, "https://x/z"),
      new CoinGeckoLogoSource.CoinCandidate("alpha", null, "https://x/a")
    ]);

    Assert.Equal(["alpha", "zeta"], ordered.Select(c => c.Id));
  }
}
