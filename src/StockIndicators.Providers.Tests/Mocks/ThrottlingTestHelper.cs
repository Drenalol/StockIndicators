using System.Net;
using System.Net.Http.Headers;
using Refit;

namespace StockIndicators.Providers.Tests.Mocks;

public static class ThrottlingTestHelper
{
  public static async Task<ApiException> ThrottledAsync(HttpStatusCode statusCode = HttpStatusCode.TooManyRequests, TimeSpan? retryAfter = null)
  {
    var response = new HttpResponseMessage(statusCode);
    if (retryAfter is { } delay)
      response.Headers.RetryAfter = new RetryConditionHeaderValue(delay);

    return await ApiException.Create(
      new HttpRequestMessage(HttpMethod.Get, "http://localhost/throttled"),
      HttpMethod.Get,
      response,
      new RefitSettings());
  }
}
