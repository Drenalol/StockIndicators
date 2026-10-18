using StockIndicators.Application.Configuration.Queries;

namespace StockIndicators.Application.Instruments.GetInstrument;

public class GetInstrumentQuery : IQuery<InstrumentDetailDto>
{
  public string Ticker { get; }
  public string? ExchangeName { get; }

  public GetInstrumentQuery(string ticker, string? exchangeName = null)
  {
    Ticker = ticker;
    ExchangeName = exchangeName;
  }
}

public record InstrumentDetailDto
{
  public string Ticker { get; init; } = null!;
  public string? ExchangeName { get; init; }
  public string? ClassCode { get; init; }
  public string Currency { get; init; } = null!;
  public string? Name { get; init; }
  public string? LongName { get; init; }
  public string? Isin { get; init; }
  public string? Sector { get; init; }
  public string? Industry { get; init; }
  public string? Country { get; init; }
  public string? Website { get; init; }
  public int? Employees { get; init; }
  public string? Summary { get; init; }
  /// <summary>Instrument category: Stock, Bond, Etf, Option, Future. Null when the source does not provide it.</summary>
  public string? Type { get; init; }
}
