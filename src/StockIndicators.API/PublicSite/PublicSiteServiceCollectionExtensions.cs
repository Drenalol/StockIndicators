using FluentValidation;
using LazyCache;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Validation;
using StockIndicators.Application.Indicators.GetIndicatorCatalog;
using StockIndicators.Application.Indicators.GetIndicatorSchema;
using StockIndicators.Application.Indicators.GetIndicatorShareImage;
using StockIndicators.Application.Indicators.GetLandingExamples;
using StockIndicators.Application.Indicators.GetLandingShareImage;
using StockIndicators.Application.Notifications.Channels;
using StockIndicators.Application.PublicSite;
using StockIndicators.Application.Rules.GetRulePresets;
using StockIndicators.Filters;
using StockIndicators.Infrastructure.Charts;
using StockIndicators.Infrastructure.Data;

namespace StockIndicators.PublicSite;

public static class PublicSiteServiceCollectionExtensions
{
  /// <summary>
  /// The whole DI graph of a public-site instance: the marketing endpoints over the static
  /// frontend, and nothing else. Deliberately absent - the database, Redis, market-data
  /// providers, notification channels, the bot and the alert scheduler: a public box has no
  /// credentials to run them with and no account to run them for. What is registered here is
  /// what the public handlers need, and every one of them answers from embedded fixtures or
  /// reflection-derived metadata, so the instance starts and serves without a database.
  /// </summary>
  public static IServiceCollection AddPublicSite(this IServiceCollection services)
  {
    services.AddResponseCompression();
    services.AddHttpContextAccessor();

    services.RegisterOptions<PublicSiteOptions>(PublicSiteOptions.SectionName);

    // Controllers with the same envelope filters the application uses: the frontend's request()
    // unwraps one contract on either side of the flag.
    services.AddControllers().AddNewtonsoftJson();

    services
      .AddOptions<MvcOptions>()
      .Configure(
        options =>
        {
          options.Filters.Add<EnvelopeExceptionFilter>();
          options.Filters.Add<EnvelopeActionFilter>();
        }
      );

    // Handlers: MediatR scans the same assemblies as on a self-hosted instance, and everything
    // that cannot be answered here is taken back out. Two reasons: development-time container
    // validation builds every registration, and the application's other handlers depend on
    // repositories this graph does not carry; and a registered-but-gated handler would still be
    // addressable through a request the mediator resolves. What stays is the list below - the
    // whole public surface, and every one of them answers from fixtures or reflection.
    var scanStart = services.Count;

    services
      .AddMediatR(
        config => config
          .RegisterServicesFromAssemblies(typeof(StockIndicators.Application.AssemblyMarker).Assembly, typeof(StockIndicators.Infrastructure.AssemblyMarker).Assembly)
          .AddOpenBehavior(typeof(ValidationBehavior<,>))
      );

    services.AddValidatorsFromAssembly(typeof(StockIndicators.Application.AssemblyMarker).Assembly);

    foreach (var descriptor in services.Skip(scanStart).Where(OutsidePublicSurface).ToArray())
      services.Remove(descriptor);

    // The in-process cache the catalog and share-image handlers memoize their answers with
    // (LazyCache: memory only, Redis is not part of this graph)
    services.AddSingleton<IAppCache, CachingService>();
    services.AddSingleton<ISerializer>(provider => ActivatorUtilities.CreateInstance<DefaultSerializer>(provider, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error }));

    // The link-preview pictures: only the renderer, not the trigger-chart service (that one
    // reads candles and subscriptions). The renderer stays off unless ChartImage:PageHtmlPath
    // points at the frontend's renderer page, and without it the share endpoints redirect to the
    // static brand card.
    services.AddSingleton<IChartImageRenderer, PlaywrightChartImageRenderer>();
    services.RegisterOptions<ChartImageOptions>("ChartImage");
    services.RegisterOptions<ShareImageOptions>("ShareImage");

    // of AlertOptions the public surface reads one field: PublicBaseUrl, the redirect target of
    // the brand card. It is registered by AddNotifications on a self-hosted instance.
    services.RegisterOptions<AlertOptions>();

    // The frontend overlay polls health; with no dependencies to probe, the report is simply Healthy.
    services.AddHealthChecks();

    return services;
  }

  /// <summary>
  /// The handlers a public-site instance answers with. A new public endpoint means adding its
  /// handler here - forgetting it leaves the endpoint failing loudly rather than serving the
  /// whole application by accident.
  /// </summary>
  private static readonly Type[] PublicHandlers =
  [
    typeof(GetLandingExamplesQueryHandler),
    typeof(GetIndicatorCatalogQueryHandler),
    typeof(GetIndicatorSchemaQueryHandler),
    typeof(GetRulePresetsQueryHandler),
    typeof(GetIndicatorShareImageQueryHandler),
    typeof(GetLandingShareImageQueryHandler),
  ];

  /// <summary>
  /// Whether a registration the two scans added belongs to the application rather than to the
  /// public surface. The mediator's own machinery is registered through factories (no
  /// implementation type) and stays, and so does the open pipeline behavior; every handler and
  /// validator that is not one of the public ones goes.
  /// </summary>
  private static bool OutsidePublicSurface(ServiceDescriptor descriptor) =>
    descriptor.ImplementationType is { } implementation
    && !PublicHandlers.Contains(implementation)
    // the mediator itself is registered as a concrete implementation and serves every request
    && implementation != typeof(Mediator)
    && descriptor.ServiceType != typeof(IPipelineBehavior<,>);
}
