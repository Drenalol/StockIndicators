using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace StockIndicators.Infrastructure.Processing;

public static class HttpContextExtensions
{
  /// <summary>
  /// Id of the authenticated user (issued by the auth handler for both API keys and web sessions).
  /// </summary>
  public static Guid GetUserId(this HttpContext httpContext)
  {
    var value = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
      ?? throw new InvalidOperationException("User id claim is missing");

    return Guid.Parse(value);
  }
}