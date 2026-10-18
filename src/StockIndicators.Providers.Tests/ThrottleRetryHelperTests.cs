using System.Diagnostics;
using System.Net;
using Refit;
using Serilog;
using Serilog.Core;
using StockIndicators.Abstract;
using StockIndicators.Providers.Base;
using StockIndicators.Providers.Tests.Mocks;
using Xunit;

namespace StockIndicators.Providers.Tests;

public class ThrottleRetryHelperTests
{
  [Fact]
  public async Task ThrottledCall_IsRetried_AndSucceeds()
  {
    var calls = 0;
    var limiter = new CountingLimiter();

    var result = await ThrottleRetryHelper.ExecuteAsync(
      async _ =>
      {
        calls++;
        if (calls < 3)
          throw await ThrottlingTestHelper.ThrottledAsync(retryAfter: TimeSpan.FromMilliseconds(50));
        return "ok";
      },
      limiter, Logger.None);

    Assert.Equal("ok", result);
    Assert.Equal(3, calls);
  }

  [Fact]
  public async Task ThrottledCall_ExhaustsRetries_AndRethrows()
  {
    var calls = 0;

    await Assert.ThrowsAsync<ApiException>(
      () => ThrottleRetryHelper.ExecuteAsync<string>(
        async _ =>
        {
          calls++;
          throw await ThrottlingTestHelper.ThrottledAsync(retryAfter: TimeSpan.FromMilliseconds(10));
        },
        new CountingLimiter(), Logger.None));

    Assert.Equal(4, calls);
  }

  [Fact]
  public async Task ThrottledCall_WithoutRetryAfter_UsesExponentialBackoff()
  {
    var calls = 0;
    var stopwatch = Stopwatch.StartNew();

    var result = await ThrottleRetryHelper.ExecuteAsync(
      async _ =>
      {
        calls++;
        if (calls < 2)
          throw await ThrottlingTestHelper.ThrottledAsync();
        return "ok";
      },
      new CountingLimiter(), Logger.None);

    stopwatch.Stop();
    Assert.Equal("ok", result);
    Assert.True(stopwatch.Elapsed >= TimeSpan.FromSeconds(1), $"expected backoff >= 1s, got {stopwatch.Elapsed}");
  }

  [Fact]
  public async Task IpBanned_IsRetriedLikeRateLimit()
  {
    var calls = 0;

    await Assert.ThrowsAsync<ApiException>(
      () => ThrottleRetryHelper.ExecuteAsync<string>(
        async _ =>
        {
          calls++;
          throw await ThrottlingTestHelper.ThrottledAsync((HttpStatusCode)418, TimeSpan.FromMilliseconds(10));
        },
        new CountingLimiter(), Logger.None));

    Assert.Equal(4, calls);
  }

  [Fact]
  public async Task NonThrottledError_IsNotRetried()
  {
    var calls = 0;

    await Assert.ThrowsAsync<ApiException>(
      () => ThrottleRetryHelper.ExecuteAsync<string>(
        async _ =>
        {
          calls++;
          throw await ThrottlingTestHelper.ThrottledAsync(HttpStatusCode.NotFound);
        },
        new CountingLimiter(), Logger.None));

    Assert.Equal(1, calls);
  }

  [Fact]
  public async Task Limiter_IsAwaited_OnEveryAttempt()
  {
    var calls = 0;
    var limiter = new CountingLimiter();

    await ThrottleRetryHelper.ExecuteAsync(
      async _ =>
      {
        calls++;
        if (calls < 3)
          throw await ThrottlingTestHelper.ThrottledAsync(retryAfter: TimeSpan.FromMilliseconds(10));
        return "ok";
      },
      limiter, Logger.None);

    Assert.Equal(3, limiter.Waits);
  }

  private sealed class CountingLimiter : ITimeLimiter
  {
    public int Waits { get; private set; }

    public Task WaitAsync(CancellationToken cancellationToken)
    {
      Waits++;
      return Task.CompletedTask;
    }
  }
}
