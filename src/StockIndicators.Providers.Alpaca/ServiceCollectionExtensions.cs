using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refit;
using StockIndicators.Abstract;
using StockIndicators.Providers.Alpaca.Api;
using StockIndicators.Providers.Alpaca;
using StockIndicators.Providers.Base.Exchange;

namespace StockIndicators;

public static class AlpacaServiceCollectionExtensions
{
  public static IServiceCollection AddAlpacaProvider(this IServiceCollection services, IConfiguration configuration)
  {
    services
      .AddOptions<AlpacaProviderOptions>()
      .Bind(configuration.GetSection(nameof(AlpacaProviderOptions)))
      .ValidateDataAnnotations()
      .ValidateOnStart();

    services.AddExchangeSession(configuration, ExchangeSessions.Us);

    // Raw REST for the BOATS overnight feed (the SDK 7.x MarketDataFeed has no Boats; 8.x
    // is a breaking upgrade). Same data host for paper and live environments.
    services
      .AddRefitGeneratedClient<IAlpacaBoatsApi>()
      .ConfigureHttpClient(client => client.BaseAddress = new Uri("https://data.alpaca.markets"));

    services.AddSingleton<AlpacaProvider>();
    services.AddSingleton<AlpacaProviderClient>();
    services.AddSingleton<IInstrumentSource, AlpacaProviderClient>();
    services.AddHostedService<AlpacaProviderConsumerProducer>();
    return services;
  }
}
