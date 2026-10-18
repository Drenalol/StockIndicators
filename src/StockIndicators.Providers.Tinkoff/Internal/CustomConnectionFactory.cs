using Microsoft.Extensions.DependencyInjection;

namespace StockIndicators.Providers.Tinkoff.Internal;

public class CustomConnectionFactory
{
  private readonly IServiceScopeFactory _scopeFactory;

  public CustomConnectionFactory(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

  public CustomConnection Create() => new(_scopeFactory.CreateScope());
}