using StockIndicators.Application.Configuration.Queries;

namespace StockIndicators.Application.Instruments.GetInstruments;

/// <summary>
/// Instrument catalog search over the persistent Instruments table. Search matches tickers,
/// (transliterated) names, crypto asset aliases and tolerates typos; filters narrow the result
/// to a type/exchange or to instruments the given user watches with a certain indicator
/// (via the RuleInstruments/RuleIndicators projections of their subscriptions).
/// </summary>
public class GetInstrumentsQuery : IQuery<PaginatedResult<InstrumentDto>>
{
  public string? Search { get; }
  public string? Type { get; }
  public string? Indicator { get; }
  public string? ExchangeName { get; }
  public Guid UserId { get; }
  public int Page { get; }
  public int PageSize { get; }

  public GetInstrumentsQuery(
    string? search = null,
    string? type = null,
    int page = 1,
    int pageSize = 50,
    string? indicator = null,
    string? exchangeName = null,
    Guid userId = default)
  {
    Search = search;
    Type = type;
    Page = page < 1 ? 1 : page;
    PageSize = pageSize < 1 ? 50 : (pageSize > 100 ? 100 : pageSize);
    Indicator = indicator;
    ExchangeName = exchangeName;
    UserId = userId;
  }
}
