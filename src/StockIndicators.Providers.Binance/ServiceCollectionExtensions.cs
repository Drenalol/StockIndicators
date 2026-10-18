using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refit;
using StockIndicators.Abstract;
using StockIndicators.Providers.Binance;
using StockIndicators.Providers.Binance.Api;
using StockIndicators.Providers.Binance.Internal;

namespace StockIndicators;

public static class BinanceServiceCollectionExtensions
{
  private const string PrimaryClientName = "binance";
  private const string MirrorClientName = "binance-mirror";

  public static IServiceCollection AddBinanceProvider(this IServiceCollection services, IConfiguration configuration)
  {
    services
      .AddOptions<BinanceProviderOptions>()
      .Bind(configuration.GetSection(nameof(BinanceProviderOptions)))
      .ValidateDataAnnotations()
      .ValidateOnStart();

    services.AddSingleton<BinanceProvider>();

    services
      .AddHttpClient(PrimaryClientName)
      .ConfigureHttpClient((provider, client) => client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<BinanceProviderOptions>>().Value.BaseUrl));

    services
      .AddHttpClient(MirrorClientName)
      .ConfigureHttpClient((provider, client) =>
      {
        var options = provider.GetRequiredService<IOptions<BinanceProviderOptions>>().Value;
        client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(options.MirrorBaseUrl) ? options.BaseUrl : options.MirrorBaseUrl!);
      });

    services.AddSingleton(
      provider =>
      {
        var httpFactory = provider.GetRequiredService<IHttpClientFactory>();
        return new BinanceApiClients(
          RestService.ForGenerated<IBinanceApi>(httpFactory.CreateClient(PrimaryClientName)),
          RestService.ForGenerated<IBinanceApi>(httpFactory.CreateClient(MirrorClientName))
        );
      });

    services.AddSingleton<BinanceApiSelector>();
    services.AddSingleton<BinanceProviderClient>();
    services.AddSingleton<IInstrumentSource, BinanceProviderClient>();
    services.AddHostedService<BinanceProviderConsumerProducer>();
    return services;
  }
}
