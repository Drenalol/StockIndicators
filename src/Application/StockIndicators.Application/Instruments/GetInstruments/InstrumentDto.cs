using Newtonsoft.Json;

namespace StockIndicators.Application.Instruments.GetInstruments;

public record InstrumentDto
{
  public string Ticker { get; init; } = null!;
  public string? ExchangeName { get; init; }
  public string? ClassCode { get; init; }
  public string Currency { get; init; } = null!;
  public string? Name { get; init; }
  public string? Isin { get; init; }
  public string? Sector { get; init; }
  /// <summary>Instrument category: Stock, Bond, Etf, Option, Future. Null when the source does not provide it.</summary>
  public string? Type { get; init; }
  /// <summary>
  /// Source provider: tinkoff, alpaca. Internal only: identifies where the instrument was
  /// loaded from (candles routing, identity checks), never exposed to API clients.
  /// </summary>
  [JsonIgnore]
  public string Source { get; init; } = null!;
}

public record PaginatedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
