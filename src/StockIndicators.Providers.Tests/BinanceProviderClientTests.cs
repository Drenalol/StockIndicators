using System.Text.Json;
using LazyCache;
using Microsoft.Extensions.Options;
using Moq;
using Serilog;
using Serilog.Core;
using StockIndicators.Providers.Base;
using StockIndicators.Providers.Binance;
using StockIndicators.Providers.Binance.Api;
using StockIndicators.Providers.Binance.Internal;
using StockIndicators.Providers.Binance.Schema;
using StockIndicators.Providers.Tests.Mocks;
using Xunit;
using TimeFrame = StockIndicators.Domain.Models.Candles.TimeFrame;

namespace StockIndicators.Providers.Tests;

public class BinanceProviderClientTests
{
  private readonly Mock<IBinanceApi> _primary = new();
  private readonly FakeAppCache _cache = new();
  private readonly BinanceProviderClient _client;

  public BinanceProviderClientTests()
  {
    var provider = new BinanceProvider(Options.Create(new BinanceProviderOptions
    {
      Limits = [new LimitOption { Value = 1000, Interval = TimeSpan.FromSeconds(1) }]
    }));
    var selector = new BinanceApiSelector(new BinanceApiClients(_primary.Object, _primary.Object), Logger.None);
    _client = new BinanceProviderClient(provider, selector, _cache, Logger.None);
  }

  [Fact]
  public async Task GetInstruments_FiltersByStatus_QuoteWhitelist_SpotTrading()
  {
    _primary
      .Setup(a => a.GetExchangeInfoAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync(
        new BinanceExchangeInfo
        {
          Symbols =
          [
            Symbol("BTCUSDT", "TRADING", "BTC", "USDT", true),
            Symbol("ETHBTC", "TRADING", "ETH", "BTC", true),
            Symbol("XRPUSDC", "TRADING", "XRP", "USDC", true),
            Symbol("DOGEUSD", "TRADING", "DOGE", "USD", true),
            Symbol("LTCETH", "TRADING", "LTC", "ETH", true),
            Symbol("BNBBUSD", "TRADING", "BNB", "BUSD", true),
            Symbol("DELISTEDUSDT", "DELISTED", "DELISTED", "USDT", true),
            Symbol("MARGINUSDT", "TRADING", "MARGIN", "USDT", false)
          ]
        });

    var instruments = await _client.GetInstrumentsAsync();

    var tickers = instruments.Select(instrument => instrument.Ticker).ToHashSet();
    Assert.Equal(new[] { "BTCUSDT", "DOGEUSD", "ETHBTC", "LTCETH", "XRPUSDC" }, tickers.OrderBy(ticker => ticker, StringComparer.Ordinal).ToArray());

    var btcusdt = Assert.Single(instruments, instrument => instrument.Ticker == "BTCUSDT");
    Assert.Equal("BINANCE", btcusdt.ExchangeName);
    Assert.Equal("USDT", btcusdt.Currency);
    Assert.Equal("BTC/USDT", btcusdt.Name);
    Assert.Equal("Crypto", btcusdt.Type);
    Assert.Equal("binance", btcusdt.Source);
  }

  [Fact]
  public async Task GetInstruments_IsCached()
  {
    _primary
      .Setup(a => a.GetExchangeInfoAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync(new BinanceExchangeInfo { Symbols = [Symbol("BTCUSDT", "TRADING", "BTC", "USDT", true)] });

    var first = await _client.GetInstrumentsAsync();
    var second = await _client.GetInstrumentsAsync();

    Assert.Same(first, second);
    _primary.Verify(a => a.GetExchangeInfoAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task GetInstrument_ResolvesCaseInsensitive()
  {
    _primary
      .Setup(a => a.GetExchangeInfoAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync(new BinanceExchangeInfo { Symbols = [Symbol("BTCUSDT", "TRADING", "BTC", "USDT", true)] });

    var instrument = await _client.GetInstrumentAsync("btcusdt");

    Assert.Equal("BTCUSDT", instrument.Ticker);
  }

  [Fact]
  public async Task GetInstrument_UnknownSymbol_Throws()
  {
    _primary
      .Setup(a => a.GetExchangeInfoAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync(new BinanceExchangeInfo());

    await Assert.ThrowsAsync<InvalidOperationException>(() => _client.GetInstrumentAsync("NOTCOIN"));
  }

  [Fact]
  public async Task GetHistory_PaginatesKlines_UntilShortPage()
  {
    var pageOne = MakeKlines(1000);
    var pageTwo = MakeKlines(300, pageOne[^1][0]);
    var pages = new Queue<List<JsonElement[]>>([pageOne, pageTwo]);

    _primary
      .Setup(a => a.GetKlinesAsync("BTCUSDT", "1m", 1000, It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
      .Returns((string symbol, string interval, int limit, long? startTime, long? endTime, CancellationToken ct) =>
        Task.FromResult(pages.Dequeue()));

    var from = new DateTime(2026, 8, 1);
    var to = from.AddDays(1);
    var candles = await _client.GetHistoryAsync("BTCUSDT", from, to, TimeFrame.Minute).ToListAsync();

    Assert.Equal(1300, candles.Count);
    Assert.All(candles, candle =>
    {
      Assert.Equal("BTCUSDT", candle.Ticker);
      Assert.Equal("BINANCE", candle.ExchangeName);
      Assert.Equal(TimeFrame.Minute, candle.TimeFrame);
      Assert.True(candle.IsCompleted);
    });

    var dates = candles.Select(candle => candle.Timestamp).ToList();
    Assert.Equal(dates.OrderBy(date => date), dates);
    _primary.Verify(a => a.GetKlinesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
  }

  [Fact]
  public async Task GetHistory_RespectsMaxPeriods()
  {
    // Month: 250 candles fit one page, so the loop stops after a single full page
    var page = MakeKlines(250);

    _primary
      .Setup(a => a.GetKlinesAsync("BTCUSDT", "1M", 250, It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(page);

    var from = new DateTime(2020, 1, 1);
    var candles = await _client.GetHistoryAsync("BTCUSDT", from, from.AddYears(5), TimeFrame.Month).ToListAsync();

    Assert.Equal(250, candles.Count);
    _primary.Verify(a => a.GetKlinesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Once);
  }

  private static BinanceSymbol Symbol(string symbol, string status, string baseAsset, string quoteAsset, bool spotAllowed) => new()
  {
    Symbol = symbol,
    Status = status,
    BaseAsset = baseAsset,
    QuoteAsset = quoteAsset,
    IsSpotTradingAllowed = spotAllowed
  };

  private static List<JsonElement[]> MakeKlines(int count, JsonElement? afterOpenTime = null)
  {
    var nextOpenTime = afterOpenTime is { } last
      ? last.GetInt64() + 60_000
      : new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    return Enumerable
      .Range(0, count)
      .Select(index => JsonSerializer.SerializeToElement(new object[]
      {
        nextOpenTime + index * 60_000,
        "1", "2", "0.5", "1.5", "100",
        nextOpenTime + index * 60_000 + 59_999,
        "150", 10, "0", "0", 0
      }).EnumerateArray().ToArray())
      .ToList();
  }
}
