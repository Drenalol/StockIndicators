using System.Net;
using Refit;
using Serilog;
using StockIndicators.Providers.Binance.Api;

namespace StockIndicators.Providers.Binance.Internal;

/// <summary>
/// Picks the Binance API host. On the first geo-block (451/403) from the primary
/// host it switches to the mirror for the lifetime of the process.
/// </summary>
public sealed class BinanceApiSelector
{
  private readonly BinanceApiClients _clients;
  private readonly ILogger _logger;
  private volatile bool _useMirror;

  public BinanceApiSelector(BinanceApiClients clients, ILogger logger)
  {
    _clients = clients;
    _logger = logger;
  }

  public bool UsesMirror => _useMirror;

  public async Task<T> ExecuteAsync<T>(Func<IBinanceApi, CancellationToken, Task<T>> call, CancellationToken cancellationToken)
  {
    try
    {
      return await call(Pick(), cancellationToken);
    }
    catch (ApiException exception) when (!UsesMirror && IsGeoBlocked(exception))
    {
      _useMirror = true;
      _logger.Warning("Binance primary host geo-blocked ({StatusCode}), switching to mirror", (int)exception.StatusCode);
      return await call(Pick(), cancellationToken);
    }
  }

  private IBinanceApi Pick() => UsesMirror ? _clients.Mirror : _clients.Primary;

  private static bool IsGeoBlocked(ApiException exception) =>
    exception.StatusCode is HttpStatusCode.UnavailableForLegalReasons or HttpStatusCode.Forbidden;
}
