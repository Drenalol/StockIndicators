using StockIndicators.Abstract;

namespace StockIndicators.Providers.Binance;

public record BinanceInstrument(string Ticker, string ExchangeName, string Currency, string? Name) : IInstrument
{
  public string? ClassCode => null;
  public string? Figi => null;
  public string? Isin => null;
  public string? Sector => null;
  public string Type => "Crypto";
  public string Source => "binance";
  public override int GetHashCode() => HashCode.Combine(Ticker, ExchangeName);
}
