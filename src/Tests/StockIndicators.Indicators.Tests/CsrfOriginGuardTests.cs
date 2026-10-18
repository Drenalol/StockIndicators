using Microsoft.AspNetCore.Http;
using StockIndicators.Internal;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class CsrfOriginGuardTests
{
  private static readonly string[] Allowed =
  [
    "https://localhost",
    "https://dev.example.com",
    "https://example.com",
    "https://www.example.com"
  ];

  private const string CookieName = "si_session";

  private static HttpRequest Request(
    string method,
    string? cookie = "session",
    string? origin = null,
    string? referer = null,
    string scheme = "https",
    string host = "example.com"
  )
  {
    var context = new DefaultHttpContext
    {
      Request = { Scheme = scheme, Host = new HostString(host) }
    };

    if (cookie is not null)
      context.Request.Headers.Cookie = $"{CookieName}={cookie}";
    if (origin is not null)
      context.Request.Headers.Origin = origin;
    if (referer is not null)
      context.Request.Headers.Referer = referer;

    context.Request.Method = method;

    return context.Request;
  }

  [Theory]
  [InlineData("GET")]
  [InlineData("HEAD")]
  [InlineData("OPTIONS")]
  public void SafeMethods_AlwaysPass(string method)
  {
    Assert.True(CsrfOriginGuardMiddleware.IsAllowed(Request(method, origin: "https://evil.example"), Allowed, CookieName));
  }

  [Fact]
  public void CookiePost_ForeignOrigin_Blocked()
  {
    Assert.False(CsrfOriginGuardMiddleware.IsAllowed(Request("POST", origin: "https://evil.example"), Allowed, CookieName));
  }

  [Fact]
  public void CookiePost_AllowedOrigin_Passes()
  {
    Assert.True(CsrfOriginGuardMiddleware.IsAllowed(Request("DELETE", host: "api.internal", origin: "https://dev.example.com"), Allowed, CookieName));
    Assert.True(CsrfOriginGuardMiddleware.IsAllowed(Request("PATCH", host: "api.internal:9999", origin: "https://localhost"), Allowed, CookieName));
  }

  [Fact]
  public void CookiePost_SameOrigin_Passes()
  {
    // The single-origin deployment: the API answers on some host the operator chose, and the
    // browser declares exactly that address - it is not (and cannot be) in any static list.
    Assert.True(CsrfOriginGuardMiddleware.IsAllowed(Request("POST", scheme: "http", host: "nas.lan:8080", origin: "http://nas.lan:8080"), Allowed, CookieName));
    Assert.True(CsrfOriginGuardMiddleware.IsAllowed(Request("PATCH", scheme: "https", host: "indicators.example", origin: "https://indicators.example"), Allowed, CookieName));
  }

  [Fact]
  public void CookiePost_WithoutOrigin_FallsBackToReferer()
  {
    Assert.True(CsrfOriginGuardMiddleware.IsAllowed(Request("POST", host: "api.internal", referer: "https://dev.example.com/en/subscriptions"), Allowed, CookieName));
    Assert.False(CsrfOriginGuardMiddleware.IsAllowed(Request("POST", host: "api.internal", referer: "https://evil.example/form"), Allowed, CookieName));
  }

  [Fact]
  public void CookiePost_WithoutAnyOrigin_Blocked()
  {
    Assert.False(CsrfOriginGuardMiddleware.IsAllowed(Request("POST"), Allowed, CookieName));
  }

  [Fact]
  public void AuthorizationHeader_IsNotAnExemption()
  {
    // There are no Bearer callers anymore; an auth header must not open a way around the
    // origin check (it would be a free bypass - anyone can send one).
    var request = Request("POST", origin: "https://evil.example");
    request.Headers.Authorization = "Bearer anything";

    Assert.False(CsrfOriginGuardMiddleware.IsAllowed(request, Allowed, CookieName));
  }

  [Fact]
  public void AnonymousRequests_AreExempt()
  {
    // The login endpoint carries no session cookie to ride
    Assert.True(CsrfOriginGuardMiddleware.IsAllowed(Request("POST", cookie: null, origin: "https://evil.example"), Allowed, CookieName));
  }
}
