using StockIndicators.Abstract;

namespace StockIndicators.Providers.Tinkoff;

public record TinkoffInstrument(string Ticker, string? ExchangeName, string? ClassCode, string Currency, string? Name, string? Isin, string? Sector, string? Type) : IInstrument
{
  public string? Figi { get; init; }
  /// <summary>Issuer brand logo file name from the provider's brand data (e.g. "sber3.png"), null when absent.</summary>
  public string? BrandLogoName { get; init; }
  public string Source => "tinkoff";
  public override int GetHashCode() => HashCode.Combine(Ticker, ExchangeName);
}

public record CachedTinkoffInstrument(string Ticker, string? ExchangeName, string? ClassCode, string Currency, string? Name, string? Isin, string? Sector, string? Type) : TinkoffInstrument(Ticker, ExchangeName, ClassCode, Currency, Name, Isin, Sector, Type)
{
  public Guid? ConnectionId { get; set; }
}
