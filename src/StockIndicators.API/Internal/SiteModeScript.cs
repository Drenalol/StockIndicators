using System.Text;

namespace StockIndicators.Internal;

/// <summary>
/// Stamps the instance shape into every HTML shell the API serves, so the frontend knows on
/// its first paint whether this is a landing-only public deployment - without waiting for
/// <c>GET /api/site/config</c> and without flashing sign-in affordances that a public instance
/// then removes. The shells carry a <see cref="Marker"/> comment (the frontend's index.html,
/// copied into every pre-rendered SEO page); a shell without the marker (older dist, static
/// hosting without this backend) is served untouched and the frontend falls back to the probe.
/// Runs after response compression so it sees plain text and the injected script is compressed
/// like the rest of the page.
/// </summary>
public static class SiteModeScript
{
  /// <summary>The placeholder the frontend build leaves in the document head.</summary>
  public const string Marker = "<!--si:site-mode-->";

  /// <summary>The global the frontend reads (<c>lib/siteMode.ts</c>) before it would probe.</summary>
  public const string WindowFlag = "window.__SI_SITE_LANDING_ONLY__";

  public static IApplicationBuilder UseSiteModeScript(this IApplicationBuilder app, bool landingOnly)
  {
    return app.UseMiddleware<SiteModeScriptMiddleware>(landingOnly);
  }

  /// <summary>
  /// The document with every marker replaced by the assignment. Idempotent: the replacement
  /// contains no marker, so an already stamped page passes through unchanged.
  /// </summary>
  public static string Inject(string html, bool landingOnly)
  {
    var value = landingOnly ? "true" : "false";
    return html.Replace(Marker, $"<script>{WindowFlag}={value};</script>");
  }
}

public class SiteModeScriptMiddleware
{
  /// <summary>HTML shells are small; anything bigger is served without rewriting.</summary>
  private const int MaxHtmlBytes = 1024 * 1024;

  /// <summary>The process's own surface: it never answers with an HTML shell.</summary>
  private static readonly string[] ServerSurface = ["/api", "/hubs", "/metrics", "/swagger", "/redoc"];

  private readonly RequestDelegate _next;
  private readonly bool _landingOnly;

  public SiteModeScriptMiddleware(RequestDelegate next, bool landingOnly)
  {
    _next = next;
    _landingOnly = landingOnly;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    if (context.WebSockets.IsWebSocketRequest || !IsHtmlShellPath(context.Request.Path))
    {
      await _next(context);
      return;
    }

    // Buffer the whole document: the marker could straddle any chunk boundary, and an HTML
    // shell is tens of kilobytes. The original bytes stay untouched unless the rewrite applies.
    var original = context.Response.Body;
    await using var buffer = new MemoryStream();
    context.Response.Body = buffer;
    try
    {
      await _next(context);
    }
    finally
    {
      context.Response.Body = original;
    }

    buffer.Position = 0;
    if (buffer.Length is 0 or > MaxHtmlBytes || !IsTextHtml(context.Response.ContentType))
    {
      await buffer.CopyToAsync(original);
      return;
    }

    var html = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();
    if (!html.Contains(SiteModeScript.Marker, StringComparison.Ordinal))
    {
      context.Response.ContentLength = buffer.Length;
      buffer.Position = 0;
      await buffer.CopyToAsync(original);
      return;
    }

    var bytes = Encoding.UTF8.GetBytes(SiteModeScript.Inject(html, _landingOnly));
    context.Response.ContentLength = bytes.Length;
    await original.WriteAsync(bytes, context.RequestAborted);
  }

  /// <summary>
  /// Paths an HTML shell could come out of: the default documents and the SEO pages (the path
  /// is already rewritten to <c>/&lt;locale&gt;/index.html</c> by <c>UseDefaultFiles</c>) and the
  /// extensionless deep links the SPA fallback answers. Files with an extension are assets; the
  /// server surface is JSON. A wrongly included path still costs nothing but a buffer - the
  /// content-type check below is what actually gates the rewrite.
  /// </summary>
  public static bool IsHtmlShellPath(PathString path)
  {
    var value = path.HasValue ? path.Value! : "/";
    if (ServerSurface.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
      return false;

    var lastSegment = value[(value.LastIndexOf('/') + 1)..];
    return lastSegment.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || !lastSegment.Contains('.');
  }

  private static bool IsTextHtml(string? contentType) =>
    contentType is not null &&
    contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase);
}
