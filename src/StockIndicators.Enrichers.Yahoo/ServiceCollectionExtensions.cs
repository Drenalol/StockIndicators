using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refit;
using StockIndicators.Application.Instruments.Enrichers;
using StockIndicators.Enrichers.Yahoo;
using StockIndicators.Enrichers.Yahoo.Internal;
using StockIndicators.Enrichers.Yahoo.Api;

namespace StockIndicators;

public static class YahooEnricherServiceCollectionExtensions
{
  public static IServiceCollection AddYahooEnricher(this IServiceCollection services, IConfiguration configuration)
  {
    services
      .AddOptions<YahooEnricherOptions>()
      .Bind(configuration.GetSection(nameof(YahooEnricherOptions)))
      .ValidateDataAnnotations()
      .ValidateOnStart();

    // Shared cookie container so the crumb session and the cookie bootstrap (fc.yahoo.com)
    // operate on the same A3 cookie
    var cookieContainer = new CookieContainer();
    services.AddSingleton(cookieContainer);

    services
      .AddHttpClient("yahoo")
      .ConfigureHttpClient((provider, client) => client.BaseAddress = new Uri(GetBaseUrl(provider)))
      .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
      {
        UseCookies = true,
        CookieContainer = cookieContainer
      });

    services
      .AddRefitGeneratedClient<IYahooEnrichmentApi>()
      .ConfigureHttpClient((provider, client) => client.BaseAddress = new Uri(GetBaseUrl(provider)))
      .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
      {
        UseCookies = true,
        CookieContainer = cookieContainer
      });

    services.AddSingleton<YahooCrumbProvider>();
    services.AddSingleton<IInstrumentEnricher, YahooInstrumentEnricher>();
    return services;
  }

  private static string GetBaseUrl(IServiceProvider provider) => provider.GetRequiredService<IOptions<YahooEnricherOptions>>().Value.BaseUrl!;
}
