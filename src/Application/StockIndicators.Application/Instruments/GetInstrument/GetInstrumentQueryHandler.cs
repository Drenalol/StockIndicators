using LazyCache;
using Serilog;
using StockIndicators.Abstract;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Application.Instruments.Enrichers;
using StockIndicators.Application.Instruments.GetInstruments;
using StockIndicators.Components.Utils;

namespace StockIndicators.Application.Instruments.GetInstrument;

public class GetInstrumentQueryHandler : IQueryHandler<GetInstrumentQuery, InstrumentDetailDto>
{
  private readonly IEnumerable<IInstrumentSource> _instrumentSources;
  private readonly IEnumerable<IInstrumentEnricher> _instrumentEnrichers;
  private readonly IAppCache _cache;

  public GetInstrumentQueryHandler(
    IEnumerable<IInstrumentSource> instrumentSources,
    IEnumerable<IInstrumentEnricher> instrumentEnrichers,
    IAppCache cache)
  {
    _instrumentSources = instrumentSources;
    _instrumentEnrichers = instrumentEnrichers;
    _cache = cache;
  }

  public async Task<InstrumentDetailDto> Handle(GetInstrumentQuery request, CancellationToken cancellationToken)
  {
    var sources = _instrumentSources.ToList();

    var allInstruments = await _cache.GetOrAddAsync(
      InstrumentsCache.AllKey,
      async () => (await InstrumentsLoader.LoadAllAsync(sources, cancellationToken)).Instruments,
      DateTimeOffset.UtcNow + InstrumentsCache.ListTtl
    );

    Log.Information("GetInstrumentQuery: ticker='{Ticker}' exchange='{Exchange}', total={Count}",
      request.Ticker, request.ExchangeName, allInstruments.Count);

    var match = allInstruments.FirstOrDefault(i => Matches(i.Ticker, i.ExchangeName, request.Ticker, request.ExchangeName))
                ?? allInstruments.FirstOrDefault(i => i.Ticker == request.Ticker);

    if (match == null)
      throw new InstrumentNotFoundException(request.Ticker);

    var detail = InstrumentsLoader.ToDetailDto(match);

    string? enrichedType = null;

    // Secondary enrichment (best effort): fills fields primary sources do not carry
    foreach (var enricher in _instrumentEnrichers)
    {
      try
      {
        var enrichment = await enricher.GetEnrichmentAsync(match.Ticker, cancellationToken);

        if (enrichment == null)
          continue;

        enrichedType ??= enrichment.Type;

        detail = detail with
        {
          Name = detail.Name ?? enrichment.Name,
          LongName = enrichment.LongName,
          Sector = detail.Sector ?? enrichment.Sector,
          Industry = enrichment.Industry,
          Country = enrichment.Country,
          Website = enrichment.Website,
          Employees = enrichment.Employees,
          Summary = enrichment.Summary,
        };
      }
      catch (Exception e)
      {
        Log.Warning(e, "Instrument enrichment for {Ticker} failed via {Enricher}", match.Ticker, enricher.GetType().Name);
      }
    }

    // Type resolution: provider (known) -> enricher (authoritative) -> name heuristic (last resort)
    return detail with
    {
      Type = detail.Type ?? enrichedType ?? InstrumentTypeInference.Infer(detail.Name, detail.LongName)
    };

    static bool Matches(string ticker, string? exchange, string requestedTicker, string? requestedExchange) =>
      ticker == requestedTicker && (requestedExchange == null || (exchange ?? "") == requestedExchange);
  }
}
