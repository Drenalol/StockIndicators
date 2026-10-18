using MediatR;
using LazyCache;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using StockIndicators.Application.Auth.SignIn;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Rules.GetRulePresets;
using StockIndicators.Application.Subscriptions.GetTriggerChart;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Subscriptions;
using StockIndicators.Infrastructure.Database;
using StockIndicators.PublicSite;
using Xunit;

namespace StockIndicators.Indicators.Tests.PublicSite;

/// <summary>
/// What a public-site instance is made of: the marked controllers are the published surface, and
/// the service graph behind them carries no database, no Redis, no providers and no background
/// work - the instance must start and serve with nothing but the frontend and its fixtures.
/// </summary>
public class PublicSiteSurfaceTests
{
  private static readonly string[] PublicControllers =
  [
    "IndicatorController",
    "LandingController",
    "RulePresetController",
    "ShareController",
    "SiteController",
  ];

  // What the host always carries but a bare ServiceCollection does not: configuration (the
  // options classes bind from it), logging (MediatR wants an ILoggerFactory) and the Serilog
  // logger the serializer writes its parse warnings with.
  private static readonly IServiceCollection Graph = new ServiceCollection()
    .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
    .AddLogging()
    .AddSingleton<Serilog.ILogger>(Serilog.Log.Logger)
    .AddPublicSite();

  [Fact]
  public void MarkedControllersAreExactlyThePublicSurface()
  {
    var marked = typeof(PublicSiteAttribute).Assembly.GetTypes()
      .Where(type => type.IsClass && typeof(ControllerBase).IsAssignableFrom(type))
      .Where(type => type.GetCustomAttributes(typeof(PublicSiteAttribute), inherit: false).Any())
      .Select(type => type.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(PublicControllers, marked);
  }

  [Fact]
  public void MarkedControllersAnswerAnonymously() =>
    // A marked controller is reachable from the open internet: a session requirement there would
    // not protect anything, it would only break the page.
    Assert.All(
      typeof(PublicSiteAttribute).Assembly.GetTypes()
        .Where(type => type.GetCustomAttributes(typeof(PublicSiteAttribute), inherit: false).Any()),
      controller => Assert.Empty(controller.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)));

  [Fact]
  public void ThePushImageEndpointIsNotPartOfThePublicSurface()
  {
    // Anonymous by its HMAC signature, and it reads a user's trigger - public by transport does
    // not mean public by content.
    var imageController = typeof(PublicSiteAttribute).Assembly.GetType("StockIndicators.Controllers.ImageController");

    Assert.NotNull(imageController);
    Assert.Empty(imageController!.GetCustomAttributes(typeof(PublicSiteAttribute), inherit: false));
  }

  [Theory]
  [InlineData(typeof(StockIndicatorsContext))]
  [InlineData(typeof(ICandleRepository))]
  [InlineData(typeof(ISubscriptionRepository))]
  [InlineData(typeof(IConnectionMultiplexer))]
  [InlineData(typeof(ITriggerChartService))]
  public void Nothing_that_needs_the_installation_is_registered(Type serviceType) =>
    Assert.DoesNotContain(Graph, descriptor => descriptor.ServiceType == serviceType);

  [Fact]
  public void No_background_work_of_our_own_starts() =>
    // the health-check publisher the framework registers is a no-op without publishers; what must
    // not be here is the instance's own work: warm-up, providers, the bot, the alert dispatcher
    Assert.DoesNotContain(Graph, descriptor =>
      descriptor.ServiceType == typeof(IHostedService)
      && descriptor.ImplementationType?.Assembly.GetName().Name?.StartsWith("StockIndicators", StringComparison.Ordinal) == true);

  [Fact]
  public void ThePublicHandlersAndWhatTheyReadAreRegistered()
  {
    var provider = Graph.BuildServiceProvider();

    Assert.NotNull(provider.GetService<IMediator>());
    Assert.NotNull(provider.GetService<IAppCache>());
    Assert.NotNull(provider.GetService<ISerializer>());
    Assert.NotNull(provider.GetService<IChartImageRenderer>());
    Assert.NotNull(provider.GetService<IRequestHandler<GetRulePresetsQuery, IReadOnlyList<RulePresetDto>>>());
  }

  [Fact]
  public void TheApplicationHandlersAreOutOfTheGraph() =>
    // signing in and drawing a trigger chart belong to an installation, and development-time
    // container validation would have to build their repositories otherwise
    Assert.All(new[] { typeof(SignInCommandHandler), typeof(GetTriggerChartImageQueryHandler) }, handler =>
      Assert.DoesNotContain(Graph, descriptor => descriptor.ImplementationType == handler));
}
