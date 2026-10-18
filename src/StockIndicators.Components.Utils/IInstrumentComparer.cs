using StockIndicators.Abstract;

namespace StockIndicators.Components.Utils;

public class InstrumentComparer : IEqualityComparer<IInstrument>
{
  public static readonly InstrumentComparer Instance = new();

  public bool Equals(IInstrument? x, IInstrument? y) =>
    x?.Ticker == y?.Ticker && x?.ExchangeName == y?.ExchangeName;

  public int GetHashCode(IInstrument obj) =>
    HashCode.Combine(obj.Ticker, obj.ExchangeName);
}
