using StockIndicators.Abstract;

namespace StockIndicators.Providers.Alpaca;

public record AlpacaInstrument(string Ticker, string? ExchangeName, string Currency, string? Name, string? Type) : IInstrument
{
  public string? ClassCode => null;
  public string? Figi => null;
  public string? Isin => null;
  public string? Sector => null;
  public string Source => "alpaca";
  public override int GetHashCode() => HashCode.Combine(Ticker, ExchangeName);
}
