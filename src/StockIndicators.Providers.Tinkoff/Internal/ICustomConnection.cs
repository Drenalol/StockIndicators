using Grpc.Core;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;

namespace StockIndicators.Providers.Tinkoff.Internal;

public interface ICustomConnection : IDisposable
{
  Guid Id { get; }
  AsyncDuplexStreamingCall<MarketDataRequest, MarketDataResponse> Stream { get; }
}