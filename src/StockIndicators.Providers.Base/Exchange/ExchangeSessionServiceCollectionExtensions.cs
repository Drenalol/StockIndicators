using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockIndicators.Application.Rules;

namespace StockIndicators.Providers.Base.Exchange;

public static class ExchangeSessions
{
  public const string Moex = "Moex";
  public const string Us = "Us";
}

public static class ExchangeSessionServiceCollectionExtensions
{
  public static IServiceCollection AddExchangeSession(this IServiceCollection services, IConfiguration configuration, string name)
  {
    if (!services.Any(service => service.ServiceType == typeof(TimeProvider)))
      services.AddSingleton(TimeProvider.System);

    services
      .AddOptions<ExchangeSessionOptions>(name)
      .Bind(configuration.GetSection($"Exchanges:{name}"))
      .ValidateDataAnnotations()
      .ValidateOnStart();

    services.AddKeyedSingleton<ExchangeSession>(
      name,
      (provider, _) => new ExchangeSession(
        provider.GetRequiredService<IOptionsMonitor<ExchangeSessionOptions>>().Get(name),
        provider.GetRequiredService<TimeProvider>()));

    return services;
  }

  /// <summary>
  /// Registers the alert-side extended-session gate. Registered unconditionally (after the
  /// provider extensions have run): a session the providers did not configure resolves to null
  /// and its exchange is simply never gated.
  /// </summary>
  public static IServiceCollection AddTradingSessionGate(this IServiceCollection services)
  {
    services.AddSingleton<ITradingSessionGate>(provider => new ExchangeSessionGate(
      provider.GetKeyedService<ExchangeSession>(ExchangeSessions.Us),
      provider.GetKeyedService<ExchangeSession>(ExchangeSessions.Moex)));

    return services;
  }
}
