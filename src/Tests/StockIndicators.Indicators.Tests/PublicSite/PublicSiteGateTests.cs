using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using StockIndicators.PublicSite;
using Xunit;

namespace StockIndicators.Indicators.Tests.PublicSite;

/// <summary>
/// The request gate of a public-site instance: what a public box answers, and what it behaves as
/// if it never had. The public list is asserted as data on purpose - publishing a new controller
/// is a decision about what the project shows the world, not a side effect of it being anonymous.
/// </summary>
public class PublicSiteGateTests
{
  private static readonly string[] Prefixes =
  [
    "/api/indicator",
    "/api/landing",
    "/api/rule-preset",
    "/api/share",
    "/api/site",
  ];

  [Fact]
  public void PrefixesComeFromTheMarkedControllers() =>
    Assert.Equal(Prefixes, PublicSiteGate.PublicPrefixes(typeof(PublicSiteAttribute).Assembly));

  [Theory]
  [InlineData("/api/indicator/catalog")]
  [InlineData("/api/indicator/RSI/example")]
  [InlineData("/api/landing/examples")]
  [InlineData("/api/rule-preset")]
  [InlineData("/api/share/landing/image.png")]
  [InlineData("/api/site/config")]
  [InlineData("/api/health")]
  public void Public_reads_are_allowed(string path) =>
    Assert.True(PublicSiteGate.IsAllowed(new PathString(path), HttpMethods.Get, Prefixes));

  [Theory]
  [InlineData("/api/auth/login")]
  [InlineData("/api/auth/me")]
  [InlineData("/api/subscription")]
  [InlineData("/api/config")]
  [InlineData("/api/providers")]
  [InlineData("/api/images/triggers/11111111-1111-1111-1111-111111111111")]
  [InlineData("/hubs/chart")]
  [InlineData("/metrics")]
  [InlineData("/swagger/index.html")]
  [InlineData("/swagger/v1/swagger.json")]
  [InlineData("/redoc")]
  public void Everything_else_the_process_serves_is_not_allowed(string path) =>
    Assert.False(PublicSiteGate.IsAllowed(new PathString(path), HttpMethods.Get, Prefixes));

  [Theory]
  [InlineData("POST")]
  [InlineData("PUT")]
  [InlineData("PATCH")]
  [InlineData("DELETE")]
  public void The_public_surface_is_read_only(string method) =>
    Assert.False(PublicSiteGate.IsAllowed(new PathString("/api/indicator/catalog"), method, Prefixes));

  [Fact]
  public void A_prefix_that_only_shares_the_name_is_not_public() =>
    Assert.False(PublicSiteGate.IsAllowed(new PathString("/api/indicatorfoo"), HttpMethods.Get, Prefixes));

  [Theory]
  [InlineData("/api", true)]
  [InlineData("/api/anything", true)]
  [InlineData("/hubs/chart", true)]
  [InlineData("/swagger/v1/swagger.json", true)]
  [InlineData("/redoc", true)]
  [InlineData("/metrics", true)]
  [InlineData("/", false)]
  [InlineData("/ru/indicators/RSI", false)]
  [InlineData("/ru/dashboard/settings", false)]
  [InlineData("/assets/index-ab12cd34.js", false)]
  [InlineData("/og/brand.png", false)]
  [InlineData("/sitemap.xml", false)]
  public void Only_the_process_surface_is_gated(string path, bool isServerSurface) =>
    Assert.Equal(isServerSurface, PublicSiteGate.IsServerSurface(new PathString(path)));

  [Fact]
  public async Task A_gated_endpoint_answers_with_the_404_envelope()
  {
    var reached = false;
    var middleware = new PublicSiteGateMiddleware(_ =>
    {
      reached = true;
      return Task.CompletedTask;
    });

    var context = new DefaultHttpContext
    {
      Request = { Method = HttpMethods.Get, Path = new PathString("/api/subscription") }
    };

    var body = new MemoryStream();
    context.Response.Body = body;

    await middleware.InvokeAsync(context);

    Assert.False(reached);
    Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

    // the same envelope shape the MVC exception filter writes: the frontend's request() reads it
    using var document = JsonDocument.Parse(Encoding.UTF8.GetString(body.ToArray()));
    Assert.Equal(StatusCodes.Status404NotFound, document.RootElement.GetProperty("statusCode").GetInt32());
  }

  [Theory]
  [InlineData("/")]
  [InlineData("/ru")]
  [InlineData("/ru/dashboard/settings")]
  [InlineData("/manifest.webmanifest")]
  public async Task The_frontend_is_not_the_gate_business(string path)
  {
    var reached = false;
    var middleware = new PublicSiteGateMiddleware(_ =>
    {
      reached = true;
      return Task.CompletedTask;
    });

    var context = new DefaultHttpContext
    {
      Request = { Method = HttpMethods.Get, Path = new PathString(path) }
    };

    await middleware.InvokeAsync(context);

    Assert.True(reached);
  }
}
