using System.Net;
using Refit;
using Serilog;
using StockIndicators.Abstract;

namespace StockIndicators.Providers.Base;

/// <summary>
/// Retries a provider API call on 429 (rate limited) / 418 (IP banned):
/// honors the Retry-After header, otherwise exponential backoff 1s -&gt; 2s -&gt; 4s.
/// Every attempt, including retries, is guarded by the provider time limiter.
/// After <c>maxRetries</c> retries the last exception is rethrown so the
/// caller (polling loop) logs it and skips the cycle.
/// </summary>
public static class ThrottleRetryHelper
{
  private static readonly HttpStatusCode[] ThrottleStatusCodes = [HttpStatusCode.TooManyRequests, (HttpStatusCode)418];

  public static async Task<T> ExecuteAsync<T>(
    Func<CancellationToken, Task<T>> call,
    ITimeLimiter limiter,
    ILogger logger,
    CancellationToken cancellationToken = default,
    int maxRetries = 3)
  {
    for (var attempt = 0; ; attempt++)
    {
      await limiter.WaitAsync(cancellationToken);

      try
      {
        return await call(cancellationToken);
      }
      catch (ApiException exception) when (ThrottleStatusCodes.Contains(exception.StatusCode))
      {
        if (attempt >= maxRetries)
        {
          logger.Error(
            "Throttled by provider ({StatusCode}) after {MaxRetries} retries, skipping cycle",
            (int)exception.StatusCode, maxRetries);
          throw;
        }

        var delay = GetRetryAfterDelay(exception) ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
        logger.Warning(
          "Throttled by provider ({StatusCode}), retry {Attempt}/{MaxRetries} in {Delay:hh\\:mm\\:ss}",
          (int)exception.StatusCode, attempt + 1, maxRetries, delay);
        await Task.Delay(delay, cancellationToken);
      }
    }
  }

  private static TimeSpan? GetRetryAfterDelay(ApiException exception)
  {
    var retryAfter = exception.Headers.RetryAfter;
    if (retryAfter is null)
      return null;

    var delay = retryAfter.Delta ?? (retryAfter.Date is { } date ? date - DateTime.UtcNow : null);
    return delay is { TotalSeconds: > 0 } ? delay : null;
  }
}
