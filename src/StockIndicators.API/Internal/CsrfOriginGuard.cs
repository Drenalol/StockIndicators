namespace StockIndicators.Internal;

/// <summary>
/// CSRF defense-in-depth for cookie-authenticated requests. The session cookie is already
/// SameSite=Lax (cross-site form posts never carry it) and CORS does not allow credentials;
/// on top of that, unsafe (state-changing) requests carrying the session cookie must declare
/// an <c>Origin</c> (or <c>Referer</c>) that is either the address this API is served on
/// (a single-origin deployment - the operator's host and port are not known at compile time)
/// or one of the configured frontend origins. Runs after authentication, before the endpoints.
/// </summary>
public static class CsrfOriginGuard
{
  public static IApplicationBuilder UseCsrfOriginGuard(this IApplicationBuilder app, IReadOnlyList<string> allowedOrigins, string sessionCookieName)
  {
    return app.UseMiddleware<CsrfOriginGuardMiddleware>(allowedOrigins, sessionCookieName);
  }
}

public class CsrfOriginGuardMiddleware
{
  private readonly RequestDelegate _next;
  private readonly IReadOnlyList<string> _allowedOrigins;
  private readonly string _sessionCookieName;

  public CsrfOriginGuardMiddleware(RequestDelegate next, IReadOnlyList<string> allowedOrigins, string sessionCookieName)
  {
    _next = next;
    _allowedOrigins = allowedOrigins;
    _sessionCookieName = sessionCookieName;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    if (!IsAllowed(context.Request, _allowedOrigins, _sessionCookieName))
    {
      context.Response.StatusCode = StatusCodes.Status400BadRequest;

      await context.Response.WriteAsJsonAsync(new
      {
        statusCode = StatusCodes.Status400BadRequest,
        error = "Cross-site request blocked"
      });

      return;
    }

    await _next(context);
  }

  /// <summary>
  /// True when the request may pass the guard: safe methods, anonymous requests, and cookie-
  /// carrying requests whose declared origin is this very address or a configured frontend origin.
  /// </summary>
  public static bool IsAllowed(HttpRequest request, IReadOnlyList<string> allowedOrigins, string sessionCookieName)
  {
    if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) ||
        HttpMethods.IsOptions(request.Method) || HttpMethods.IsTrace(request.Method))
      return true;

    if (request.Cookies[sessionCookieName] is not { Length: > 0 })
      return true;

    var origin = DeclaredOrigin(request);

    return origin is not null &&
           (IsSameOrigin(request, origin) || allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase));
  }

  /// <summary>
  /// The browser declares the address it loaded the page from; when that is the address the
  /// API itself answers on, the request is same-origin and the CSRF attack this guard models
  /// (a foreign page riding the cookie) is not happening.
  /// </summary>
  private static bool IsSameOrigin(HttpRequest request, string origin) =>
    string.Equals(origin, $"{request.Scheme}://{request.Host.Value}", StringComparison.OrdinalIgnoreCase);

  private static string? DeclaredOrigin(HttpRequest request)
  {
    var origin = request.Headers.Origin.ToString();

    if (!string.IsNullOrEmpty(origin))
      return origin;

    return Uri.TryCreate(request.Headers.Referer.ToString(), UriKind.Absolute, out var referer) &&
           !string.IsNullOrEmpty(referer.Scheme) && !string.IsNullOrEmpty(referer.Authority)
      ? $"{referer.Scheme}://{referer.Authority}"
      : null;
  }
}
