using LazyCache;
using Serilog;

namespace StockIndicators.Providers.Base;

public class ProviderClientBase
{
  protected IAppCache Cache { get; }
  protected ILogger Logger { get; }

  public ProviderClientBase(IAppCache cache, ILogger logger)
  {
    Cache = cache;
    Logger = logger;
  }
}