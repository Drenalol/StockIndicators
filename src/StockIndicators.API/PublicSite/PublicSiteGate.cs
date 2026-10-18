using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StockIndicators.Envelope;

namespace StockIndicators.PublicSite;

/// <summary>
/// What a public-site instance may answer: the anonymous marketing endpoints, read-only. The
/// rules are pure so the wiring test can ask them without a host.
/// </summary>
public static class PublicSiteGate
{
  /// <summary>
  /// Path prefixes of the process's own surface. Everything under them that is not public is
  /// answered with a 404 rather than falling through to the SPA shell: a public box must not
  /// hand an HTML page to whoever probed <c>/swagger</c>, and must not tell them which private
  /// routes exist either.
  /// </summary>
  private static readonly string[] ServerSurface = ["/api", "/hubs", "/metrics", "/swagger", "/redoc"];

  /// <summary>Not a controller, and the frontend's health overlay asks for it on every instance.</summary>
  public const string HealthPath = "/api/health";

  /// <summary>
  /// The prefixes the public site answers, read from the <see cref="PublicSiteAttribute"/>
  /// controllers' own route templates: the marker is the only list to keep up to date.
  /// </summary>
  public static IReadOnlyList<string> PublicPrefixes(Assembly apiAssembly) =>
    apiAssembly.GetTypes()
      .Where(type => type.IsClass && !type.IsAbstract && type.GetCustomAttribute<PublicSiteAttribute>() is not null)
      .Select(RoutePrefix)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .OrderBy(prefix => prefix, StringComparer.Ordinal)
      .ToArray();

  /// <summary>
  /// True when a public-site instance answers this request: a read of a public endpoint. Routes
  /// are matched case-insensitively, the way ASP.NET matches them.
  /// </summary>
  public static bool IsAllowed(PathString path, string method, IReadOnlyCollection<string> prefixes)
  {
    if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
      return false;

    if (path.Equals(HealthPath, StringComparison.OrdinalIgnoreCase))
      return true;

    var text = path.ToString();

    return prefixes.Any(prefix =>
      text.Equals(prefix, StringComparison.OrdinalIgnoreCase)
      || text.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase));
  }

  /// <summary>Whether the path belongs to the process's own surface rather than to the frontend.</summary>
  public static bool IsServerSurface(PathString path)
  {
    var text = path.ToString();

    return ServerSurface.Any(prefix =>
      text.Equals(prefix, StringComparison.OrdinalIgnoreCase)
      || text.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase));
  }

  private static string RoutePrefix(Type controller)
  {
    var template = controller.GetCustomAttribute<RouteAttribute>()?.Template
      ?? throw new InvalidOperationException($"{controller.Name} is marked public but carries no [Route] template");

    var name = controller.Name.EndsWith("Controller", StringComparison.Ordinal)
      ? controller.Name[..^"Controller".Length]
      : controller.Name;

    return ("/" + template
      .Replace("[controller]", name, StringComparison.Ordinal)
      .Trim('/')).ToLowerInvariant();
  }
}

/// <summary>
/// The request gate of a public-site instance: it runs before anything else touches the
/// pipeline, so a private endpoint is not merely unauthorized here, it is not there at all.
/// </summary>
public sealed class PublicSiteGateMiddleware
{
  private readonly RequestDelegate _next;
  private readonly IReadOnlyList<string> _prefixes;

  public PublicSiteGateMiddleware(RequestDelegate next)
  {
    _next = next;
    _prefixes = PublicSiteGate.PublicPrefixes(typeof(PublicSiteAttribute).Assembly);
  }

  public Task InvokeAsync(HttpContext context)
  {
    if (PublicSiteGate.IsAllowed(context.Request.Path, context.Request.Method, _prefixes)
        || !PublicSiteGate.IsServerSurface(context.Request.Path))
      return _next(context);

    // the same envelope the MVC exception filter produces, so the frontend's request() reads it
    context.Response.StatusCode = StatusCodes.Status404NotFound;

    return context.Response.WriteAsJsonAsync(
      ErrorEnvelope.Create(StatusCodes.Status404NotFound, "This endpoint is not available on a public-site instance"));
  }
}

public static class PublicSiteGateApplicationBuilderExtensions
{
  public static IApplicationBuilder UsePublicSiteGate(this IApplicationBuilder app) =>
    app.UseMiddleware<PublicSiteGateMiddleware>();
}
