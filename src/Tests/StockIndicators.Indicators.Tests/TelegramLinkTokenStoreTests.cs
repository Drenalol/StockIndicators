using Microsoft.Extensions.Options;
using StackExchange.Redis;
using StockIndicators.Application.Notifications.Telegram;
using StockIndicators.Infrastructure.Processing;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class TelegramLinkTokenStoreTests : IDisposable
{
  private readonly Guid _userId = Guid.NewGuid();
  private readonly ConnectionMultiplexer _disconnected = Disconnected();
  private readonly TelegramLinkTokenStore _store;

  public TelegramLinkTokenStoreTests()
  {
    _store = new TelegramLinkTokenStore(
      _disconnected,
      Options.Create(new TelegramLinkTokenOptions { TtlMinutes = 15 }),
      Serilog.Log.Logger);
  }

  private static ConnectionMultiplexer Disconnected()
  {
    var options = new ConfigurationOptions
    {
      AbortOnConnectFail = false,
      ConnectTimeout = 200,
      SyncTimeout = 200
    };
    options.EndPoints.Add("127.0.0.1:1");
    return ConnectionMultiplexer.Connect(options);
  }

  [Fact]
  public async Task Issue_Consume_ReturnsUser_Once()
  {
    var issued = await _store.IssueAsync(_userId);

    Assert.False(string.IsNullOrEmpty(issued.Token));
    Assert.Equal(_userId, await _store.ConsumeAsync(issued.Token));
    Assert.Null(await _store.ConsumeAsync(issued.Token));
  }

  [Fact]
  public async Task Issue_ReusesActiveTokenForSameUser()
  {
    var first = await _store.IssueAsync(_userId);
    var second = await _store.IssueAsync(_userId);

    Assert.Equal(first.Token, second.Token);
  }

  [Fact]
  public async Task Issue_ReusedTokenCarriesSlidExpiry_AndStaysConsumable()
  {
    var first = await _store.IssueAsync(_userId);
    await Task.Delay(50);
    var second = await _store.IssueAsync(_userId);

    // The expiry is what the UI shows as the link's deadline: a reused token must carry the
    // refreshed one, otherwise a "still valid" link comes back invalid on consume.
    Assert.Equal(first.Token, second.Token);
    Assert.True(second.ExpiresAt > first.ExpiresAt);

    Assert.Equal(_userId, await _store.ConsumeAsync(second.Token));
    Assert.Null(await _store.ConsumeAsync(second.Token));
  }

  [Fact]
  public async Task Consume_UnknownToken_ReturnsNull()
  {
    Assert.Null(await _store.ConsumeAsync("never-issued"));
  }

  public void Dispose() => _disconnected.Dispose();
}
