namespace StockIndicators.Abstract;

public interface IInstrument
{
  public string Ticker { get; }
  public string? ExchangeName { get; }
  public string? ClassCode { get; }
  /// <summary>Provider-specific instrument identifier (e.g. FIGI for Tinkoff MOEX instruments).</summary>
  public string? Figi { get; }
  public string Currency { get; }
  public string? Name { get; }
  public string? Isin { get; }
  public string? Sector { get; }
  /// <summary>Instrument category: Stock, Bond, Etf, Option, Future. Null when the source does not provide it.</summary>
  public string? Type { get; }
  /// <summary>Provider that supplied the instrument: tinkoff, alpaca</summary>
  public string Source { get; }
}
