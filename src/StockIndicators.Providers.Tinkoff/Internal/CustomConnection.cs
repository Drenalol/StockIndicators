using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;

namespace StockIndicators.Providers.Tinkoff.Internal;

public sealed class CustomConnection : ICustomConnection
{
  public Guid Id { get; }
  public AsyncDuplexStreamingCall<MarketDataRequest, MarketDataResponse> Stream { get; }
  private readonly IServiceScope _serviceScope;

  public CustomConnection(IServiceScope serviceScope)
  {
    Id = Guid.CreateVersion7();
    Stream = serviceScope.ServiceProvider.GetRequiredService<InvestApiClient>().MarketDataStream.MarketDataStream();
    _serviceScope = serviceScope;
  }

  public override bool Equals(object? obj) => obj is CustomConnection customConnection && customConnection.Id.Equals(Id);

  public override int GetHashCode() => Id.GetHashCode();

  public void Dispose()
  {
    Stream.Dispose();
    _serviceScope.Dispose();
  }
}