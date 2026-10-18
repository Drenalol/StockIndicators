namespace StockIndicators.Application.Instruments.Enrichers;

/// <summary>
/// Secondary source of instrument metadata (company details, sector, website)
/// that primary instrument sources do not provide.
/// </summary>
public interface IInstrumentEnricher
{
  Task<InstrumentEnrichment?> GetEnrichmentAsync(string ticker, CancellationToken cancellationToken = default);
}

public record InstrumentEnrichment
{
  public string? Name { get; init; }
  public string? LongName { get; init; }
  /// <summary>Instrument category (Stock, Etf, Bond, Option, Future, Crypto). Null when the enricher cannot determine it.</summary>
  public string? Type { get; init; }
  public string? Sector { get; init; }
  public string? Industry { get; init; }
  public string? Country { get; init; }
  public string? Website { get; init; }
  public int? Employees { get; init; }
  public string? Summary { get; init; }
}
