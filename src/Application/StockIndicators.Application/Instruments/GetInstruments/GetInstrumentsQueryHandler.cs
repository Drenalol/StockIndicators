using System.Data;
using Dapper;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Instruments.GetInstruments;

/// <summary>
/// Answers the catalog search from the persistent Instruments table (the startup warm-up
/// mirrors the provider lists there): ranking prefers exact ticker, then ticker prefix,
/// then an exact crypto asset-name alias ("bitcoin" over BTC pairs before any clone),
/// then name word prefixes, full-text prefix hits and trigram similarity (typos).
/// Cyrillic queries are transliterated, so "газпром" finds "ПАО ГАЗПРОМ ао".
/// </summary>
public class GetInstrumentsQueryHandler : IQueryHandler<GetInstrumentsQuery, PaginatedResult<InstrumentDto>>
{
  private readonly ISqlConnectionFactory _sqlConnectionFactory;

  public GetInstrumentsQueryHandler(ISqlConnectionFactory sqlConnectionFactory) => _sqlConnectionFactory = sqlConnectionFactory;

  public async Task<PaginatedResult<InstrumentDto>> Handle(GetInstrumentsQuery request, CancellationToken cancellationToken)
  {
    var search = InstrumentSearchParameters.Build(request.Search);

    var type = string.IsNullOrWhiteSpace(request.Type) ? null : request.Type.Trim();
    var exchange = string.IsNullOrWhiteSpace(request.ExchangeName) ? null : request.ExchangeName.Trim();
    var indicator = Enum.TryParse<Indicator>(request.Indicator, true, out _) ? request.Indicator!.Trim() : null;

    var conditions = new List<string>();

    if (search is not null)
      conditions.Add(search.Where("i"));

    if (type is not null)
      conditions.Add("""lower(i."Type") = lower(@Type)""");

    if (exchange is not null)
      conditions.Add("""lower(i."ExchangeName") = lower(@Exchange)""");

    // "instruments I watch with <indicator>": the (rule, ticker, indicator) projection joined
    // to the user's subscriptions; an empty condition exchange matches the ticker on any exchange
    if (indicator is not null)
      conditions.Add("""
        EXISTS (
          SELECT 1 FROM "RuleInstruments" ri
          JOIN "RuleIndicators" rind ON rind."RuleId" = ri."RuleId" AND lower(rind."Indicator") = lower(@Indicator)
          JOIN "Subscriptions" s ON s."RuleGroupId" = ri."RuleId" AND s."UserId" = @UserId
          WHERE lower(ri."Ticker") = lower(i."Ticker")
            AND (ri."ExchangeName" = '' OR lower(ri."ExchangeName") = lower(i."ExchangeName"))
        )
        """);

    var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;

    var parameters = new DynamicParameters();
    parameters.Add("PageSize", request.PageSize);
    parameters.Add("Offset", (request.Page - 1) * request.PageSize);

    if (type is not null)
      parameters.Add("Type", type, DbType.String);

    if (exchange is not null)
      parameters.Add("Exchange", exchange, DbType.String);

    if (indicator is not null)
    {
      parameters.Add("Indicator", indicator, DbType.String);
      parameters.Add("UserId", request.UserId);
    }

    search?.AddTo(parameters);

    await using var connection = await _sqlConnectionFactory.GetOpenConnectionAsync(cancellationToken);

    var totalCount = await connection.QuerySingleAsync<int>(
      $"""
       SELECT COUNT(*) FROM "Instruments" i
       {where}
       """,
      parameters);

    // The rank expression is only meaningful with a search term; without one the catalog sorts by ticker
    var orderBy = search is null
      ? "ORDER BY i.\"Ticker\""
      : $"ORDER BY ({search.Rank("i")}), i.\"Ticker\"";

    var items = (
      await connection.QueryAsync<InstrumentDto>(
        $"""
         SELECT i."Ticker", i."ExchangeName", i."ClassCode", i."Currency", i."Name",
                i."Isin", i."Sector", i."Type", i."Source"
         FROM "Instruments" i
         {where}
         {orderBy}
         LIMIT @PageSize OFFSET @Offset
         """,
        parameters
      )
    ).ToList();

    return new PaginatedResult<InstrumentDto>(items, totalCount, request.Page, request.PageSize);
  }
}
