using System.Text;
using Microsoft.AspNetCore.Http;
using StockIndicators.Internal;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class SiteModeScriptTests
{
  [Theory]
  [InlineData(true, "window.__SI_SITE_LANDING_ONLY__=true;")]
  [InlineData(false, "window.__SI_SITE_LANDING_ONLY__=false;")]
  public void Inject_ReplacesTheMarkerWithTheAssignment(bool landingOnly, string expected)
  {
    var stamped = SiteModeScript.Inject($"<head>{SiteModeScript.Marker}</head>", landingOnly);

    Assert.Contains($"<script>{expected}</script>", stamped);
    Assert.DoesNotContain(SiteModeScript.Marker, stamped);
  }

  [Fact]
  public void Inject_LeavesAShellWithoutTheMarkerUntouched()
  {
    const string html = "<html><head><title>old dist</title></head></html>";

    Assert.Equal(html, SiteModeScript.Inject(html, true));
  }

  [Theory]
  [InlineData("/", true)]
  [InlineData("/ru", true)]
  [InlineData("/ru/dashboard/settings", true)]
  [InlineData("/ru/index.html", true)]
  [InlineData("/en/indicators/SMA/index.html", true)]
  [InlineData("/assets/index-a1b2c3.js", false)]
  [InlineData("/favicon.png", false)]
  [InlineData("/manifest.webmanifest", false)]
  [InlineData("/api/site/config", false)]
  [InlineData("/hubs/chart", false)]
  [InlineData("/swagger/index.html", false)]
  public void IsHtmlShellPath_PicksThePathsAnHtmlShellCouldComeOutOf(string path, bool expected)
  {
    Assert.Equal(expected, SiteModeScriptMiddleware.IsHtmlShellPath(path));
  }

  [Theory]
  [InlineData(true, "true")]
  [InlineData(false, "false")]
  public async Task Middleware_StampsEveryServedShell(bool landingOnly, string expectedFlag)
  {
    var (body, contentLength) = await Run(landingOnly, "/ru/index.html",
      $"<html><head>{SiteModeScript.Marker}</head><body></body></html>", "text/html; charset=utf-8");

    Assert.Contains($"window.__SI_SITE_LANDING_ONLY__={expectedFlag};", body);
    Assert.Equal(Encoding.UTF8.GetByteCount(body), contentLength);
  }

  [Fact]
  public async Task Middleware_ServesBytesOfAMarkerlessShellUnchanged()
  {
    const string html = "<html><body>static hosting, no stamp</body></html>";

    var (body, contentLength) = await Run(true, "/ru/index.html", html, "text/html; charset=utf-8");

    Assert.Equal(html, body);
    Assert.Equal(Encoding.UTF8.GetByteCount(html), contentLength);
  }

  [Fact]
  public async Task Middleware_ServesNonHtmlResponsesUnchanged()
  {
    const string json = """{"statusCode":200,"result":{"landingOnly":true}}""";

    var (body, _) = await Run(true, "/api/site/config", json, "application/json; charset=utf-8");

    Assert.Equal(json, body);
  }

  /// <summary>
  /// One request through the middleware with a terminal that writes the given document,
  /// answering what the client would receive and what the response claims its length is.
  /// </summary>
  private static async Task<(string Body, long? ContentLength)> Run(
    bool landingOnly, string path, string terminalBody, string contentType)
  {
    var context = new DefaultHttpContext
    {
      Request = { Path = path, Method = HttpMethods.Get },
      Response = { Body = new MemoryStream() }
    };

    var middleware = new SiteModeScriptMiddleware(async _ =>
    {
      context.Response.ContentType = contentType;
      var bytes = Encoding.UTF8.GetBytes(terminalBody);
      context.Response.ContentLength = bytes.Length;
      await context.Response.Body.WriteAsync(bytes);
    }, landingOnly);

    await middleware.InvokeAsync(context);

    var output = (MemoryStream)context.Response.Body;
    return (Encoding.UTF8.GetString(output.ToArray()), context.Response.ContentLength);
  }
}
