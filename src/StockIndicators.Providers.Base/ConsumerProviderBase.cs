using Serilog;
using StockIndicators.Abstract.Models;

namespace StockIndicators.Providers.Base;

public abstract class ConsumerProviderBase
{
  protected ILogger Logger { get; }
  private readonly string _provider;

  protected ConsumerProviderBase(ProviderCode provider, ILogger logger)
  {
    _provider = provider.ToString();
    Logger = logger.ForContext("Provider", _provider);
  }

  /// <summary>The stable low-cardinality label for the provider's business metrics.</summary>
  protected string ProviderMetricLabel => _provider.ToLowerInvariant();

  protected Dictionary<string, object> CreateRetryContext(string requestName) => new() { ["Provider"] = _provider, ["Request"] = requestName };
}