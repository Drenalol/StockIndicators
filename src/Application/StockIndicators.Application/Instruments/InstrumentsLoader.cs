using Serilog;
using StockIndicators.Abstract;
using StockIndicators.Application.Instruments.GetInstrument;
using StockIndicators.Application.Instruments.GetInstruments;
using StockIndicators.Components.Utils;

namespace StockIndicators.Application.Instruments;

/// <summary>
/// Aggregated provider load: the merged instrument list plus the <see cref="IInstrument.Source"/>
/// names that answered successfully. The persistent-table sync uses the source set to prune
/// stale rows only where a provider actually refreshed (a failed provider must not wipe its
/// instruments from the table). <see cref="UnwiredSources"/> are the data provider codes that
/// were not wired at all (disabled or unconfigured): the table sync prunes their rows entirely,
/// the warm-up fills the set in from <c>IUnwiredDataProviders</c>.
/// </summary>
public record InstrumentsLoadResult(
  IReadOnlyList<InstrumentDto> Instruments,
  IReadOnlySet<string> LoadedSources,
  IReadOnlySet<string> UnwiredSources);

public static class InstrumentsLoader
{
  public static async Task<InstrumentsLoadResult> LoadAllAsync(
    List<IInstrumentSource> sources, CancellationToken cancellationToken)
  {
    var result = new HashSet<IInstrument>(InstrumentComparer.Instance);
    var loadedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var source in sources)
    {
      var sourceName = source.GetType().Name;

      try
      {
        var instruments = await source.GetInstrumentsAsync(cancellationToken);

        foreach (var inst in instruments)
        {
          result.Add(inst);
          loadedSources.Add(inst.Source);
        }

        Log.Information("Instruments loaded from {Source}: {Count}", sourceName, instruments.Count);
      }
      catch (NotImplementedException)
      {
        Log.Information("Source {Source}: GetInstrumentsAsync not implemented", sourceName);
      }
      catch (Exception ex)
      {
        Log.Error(ex, "Failed to load instruments from {Source}", sourceName);
      }
    }

    return new InstrumentsLoadResult(
      result.Select(i => new InstrumentDto
      {
        Ticker = i.Ticker,
        ExchangeName = i.ExchangeName,
        ClassCode = i.ClassCode,
        Currency = i.Currency,
        Name = i.Name,
        Isin = i.Isin,
        Sector = i.Sector,
        Type = i.Type,
        Source = i.Source,
      }).ToList(),
      loadedSources,
      UnwiredSources: new HashSet<string>(StringComparer.OrdinalIgnoreCase));
  }

  public static InstrumentDetailDto ToDetailDto(InstrumentDto instrument) => new()
  {
    Ticker = instrument.Ticker,
    ExchangeName = instrument.ExchangeName,
    ClassCode = instrument.ClassCode,
    Currency = instrument.Currency,
    Name = instrument.Name,
    Isin = instrument.Isin,
    Sector = instrument.Sector,
    Type = instrument.Type,
  };
}
