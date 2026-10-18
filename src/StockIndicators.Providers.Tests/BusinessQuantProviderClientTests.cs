using System.Net;
using LazyCache;
using Microsoft.Extensions.Options;
using Moq;
using Serilog;
using Serilog.Core;
using StockIndicators.Providers.Base;
using StockIndicators.Providers.Base.Exchange;
using StockIndicators.Providers.BusinessQuant;
using StockIndicators.Providers.BusinessQuant.Api;
using StockIndicators.Providers.BusinessQuant.Schema;
using StockIndicators.Providers.Tests.Mocks;
using Xunit;
using TimeFrame = StockIndicators.Domain.Models.Candles.TimeFrame;

namespace StockIndicators.Providers.Tests;

public class BusinessQuantProviderClientTests
{
  private const string ApiKey = "test-key";

  private readonly Mock<IBusinessQuantApi> _api = new();
  private readonly FakeAppCache _cache = new();
  private readonly BusinessQuantProviderClient _client;

  public BusinessQuantProviderClientTests()
  {
    var provider = new BusinessQuantProvider(
      Options.Create(CreateOptions()),
      new ExchangeSession(new ExchangeSessionOptions
      {
        TimeZoneId = "America/New_York",
        Sessions = [new ExchangeSessionWindow { Open = new TimeSpan(9, 30, 0), Close = new TimeSpan(16, 0, 0) }]
      }));
    _client = new BusinessQuantProviderClient(provider, _api.Object, _cache, Logger.None);
  }

  [Fact]
  public async Task GetInstruments_LoadsUniverse_NormalizesExchange_AndCaches()
  {
    SetupUniverse(
      Record("AAPL", "NASDAQ", "Equity"),
      Record("SPY", "NYSE", "ETF")
    );

    var instruments = await _client.GetInstrumentsAsync();
    var again = await _client.GetInstrumentsAsync();

    Assert.Same(instruments, again);
    Assert.Equal(2, instruments.Count);

    var aapl = Assert.Single(instruments, instrument => instrument.Ticker == "AAPL");
    Assert.Equal("NASDAQ", aapl.ExchangeName);
    Assert.Equal("USD", aapl.Currency);
    Assert.Equal("businessquant", aapl.Source);
    Assert.Equal("Stock", aapl.Type);

    _api.Verify(a => a.GetUniverseAsync(ApiKey, "Equity,ETF", It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task GetInstrument_ResolvesFromUniverse_CaseInsensitive_AndCachesPerTicker()
  {
    SetupUniverse(Record("AAPL", "NASDAQ", "Equity"));

    var instrument = await _client.GetInstrumentAsync("aapl");
    var again = await _client.GetInstrumentAsync("AAPL");

    Assert.Equal("AAPL", instrument.Ticker);
    Assert.Equal("NASDAQ", instrument.ExchangeName);
    Assert.Same(instrument, again);
    _api.Verify(a => a.GetUniverseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task GetInstrument_UnknownTicker_Throws()
  {
    SetupUniverse();

    await Assert.ThrowsAsync<InvalidOperationException>(() => _client.GetInstrumentAsync("NOPE"));
  }

  [Fact]
  public async Task GetHistory_UnsupportedTimeFrame_ReturnsEmpty_WithoutApiCall()
  {
    SetupUniverse(Record("AAPL", "NASDAQ", "Equity"));

    var candles = await _client.GetHistoryAsync(
      "AAPL",
      new DateTime(2026, 8, 1),
      new DateTime(2026, 8, 2),
      TimeFrame.Hour).ToListAsync();

    Assert.Empty(candles);
    _api.Verify(a => a.GetQuotesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task GetHistory_NoData404_ReturnsEmpty_WithoutThrowing()
  {
    SetupUniverse(Record("AAPL", "NASDAQ", "Equity"));
    _api
      .Setup(a => a.GetQuotesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
      .Returns(async () => throw await ThrottlingTestHelper.ThrottledAsync(HttpStatusCode.NotFound));

    var candles = await _client.GetHistoryAsync(
      "AAPL",
      new DateTime(2026, 8, 1),
      new DateTime(2026, 8, 2),
      TimeFrame.Day).ToListAsync();

    Assert.Empty(candles);
  }

  [Fact]
  public async Task GetHistory_ReturnsBars_Ascending_WithExchange()
  {
    SetupUniverse(Record("AAPL", "NASDAQ", "Equity"));
    _api
      .Setup(a => a.GetQuotesAsync(ApiKey, "AAPL", "eod", null, "2026-08-01", "2026-08-03", 2000, 1, It.IsAny<CancellationToken>()))
      .ReturnsAsync(
        new BusinessQuantQuotesResponse
        {
          Metadata = new BusinessQuantQuotesMetadata
          {
            Pagination = new BusinessQuantPagination { CurrentPage = 1, TotalPages = 1 }
          },
          Data =
          [
            new BusinessQuantQuoteBar { Date = "2026-08-03 16:00:00", Open = 3m, High = 3.2m, Low = 2.9m, Close = 3.1m, Volume = 100 },
            new BusinessQuantQuoteBar { Date = "2026-08-01 16:00:00", Open = 1m, High = 1.2m, Low = 0.9m, Close = 1.1m, Volume = 50 }
          ]
        });

    var candles = await _client.GetHistoryAsync(
      "AAPL",
      new DateTime(2026, 8, 1),
      new DateTime(2026, 8, 3),
      TimeFrame.Day).ToListAsync();

    Assert.Equal(2, candles.Count);
    Assert.Equal("AAPL", candles[0].Ticker);
    Assert.Equal("NASDAQ", candles[0].ExchangeName);
    Assert.Equal(TimeFrame.Day, candles[0].TimeFrame);
    Assert.Equal(new DateTime(2026, 8, 1, 20, 0, 0), candles[0].Timestamp);
    Assert.True(candles[0].Timestamp < candles[1].Timestamp);
    Assert.All(candles, candle => Assert.True(candle.IsCompleted));
  }

  [Fact]
  public async Task GetInstruments_RetriesOn429_HonoringRetryAfter()
  {
    var calls = 0;
    _api
      .Setup(a => a.GetUniverseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(async () =>
      {
        calls++;
        if (calls < 3)
          throw await ThrottlingTestHelper.ThrottledAsync(HttpStatusCode.TooManyRequests, TimeSpan.FromMilliseconds(20));

        return UniverseResponse(Record("AAPL", "NASDAQ", "Equity"));
      });

    var instruments = await _client.GetInstrumentsAsync();

    Assert.Single(instruments);
    Assert.Equal(3, calls);
  }

  private static BusinessQuantProviderOptions CreateOptions() => new()
  {
    ApiKey = ApiKey,
    Limits = [new LimitOption { Value = 1000, Interval = TimeSpan.FromSeconds(1) }]
  };

  private void SetupUniverse(params BusinessQuantUniverseRecord[] records)
  {
    _api
      .Setup(a => a.GetUniverseAsync(ApiKey, "Equity,ETF", It.IsAny<CancellationToken>()))
      .ReturnsAsync(UniverseResponse(records));
  }

  private static BusinessQuantUniverseResponse UniverseResponse(params BusinessQuantUniverseRecord[] records) => new()
  {
    Metadata = new BusinessQuantUniverseMetadata { TotalRecords = records.Length },
    Data = records.ToList()
  };

  private static BusinessQuantUniverseRecord Record(string ticker, string exchange, string securityType) => new()
  {
    SecurityType = securityType,
    Ticker = ticker,
    Name = $"{ticker} Inc.",
    NameShort = ticker,
    Sector = "Technology",
    Exchange = exchange
  };
}
