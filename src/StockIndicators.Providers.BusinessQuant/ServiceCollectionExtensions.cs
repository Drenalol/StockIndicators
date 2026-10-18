using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refit;
using StockIndicators.Abstract;
using StockIndicators.Providers.Base.Exchange;
using StockIndicators.Providers.BusinessQuant;
using StockIndicators.Providers.BusinessQuant.Api;

namespace StockIndicators;

public static class BusinessQuantServiceCollectionExtensions
{
  public static IServiceCollection AddBusinessQuantProvider(this IServiceCollection services, IConfiguration configuration)
  {
    services
      .AddOptions<BusinessQuantProviderOptions>()
      .Bind(configuration.GetSection(nameof(BusinessQuantProviderOptions)))
      .ValidateDataAnnotations()
      .ValidateOnStart();

    services.AddExchangeSession(configuration, ExchangeSessions.Us);
    services.AddSingleton<BusinessQuantProvider>();

    services
      .AddRefitGeneratedClient<IBusinessQuantApi>()
      .ConfigureHttpClient((provider, client) => client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<BusinessQuantProviderOptions>>().Value.BaseUrl));

    services.AddSingleton<BusinessQuantProviderClient>();
    services.AddSingleton<IInstrumentSource, BusinessQuantProviderClient>();
    services.AddHostedService<BusinessQuantProviderConsumerProducer>();
    return services;
  }
}
