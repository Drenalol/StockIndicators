using StockIndicators.Abstract.Models;

namespace StockIndicators.Abstract;

public interface IProvider
{
  ProviderCode ProviderCode { get; }
}