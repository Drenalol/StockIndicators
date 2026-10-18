using StockIndicators.Abstract;

namespace StockIndicators.Providers.BusinessQuant;

public record BusinessQuantInstrument(string Ticker, string? ExchangeName, string Currency, string? Name, string? Sector, string? Type) : IInstrument
{
  public string? ClassCode => null;
  public string? Figi => null;
  public string? Isin => null;
  public string Source => "businessquant";
  public override int GetHashCode() => HashCode.Combine(Ticker, ExchangeName);
}
