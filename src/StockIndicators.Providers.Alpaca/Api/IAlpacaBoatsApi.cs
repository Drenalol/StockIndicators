using Refit;
using StockIndicators.Providers.Alpaca.Schema;

namespace StockIndicators.Providers.Alpaca.Api;

/// <summary>
/// Raw market data REST client for the BOATS (Blue Ocean ATS) overnight feed. The Alpaca
/// .NET SDK 7.x predates the overnight feeds and its 8.x upgrade is a breaking change, so
/// the poller calls the documented bars endpoint directly: feed=boats returns the
/// 15-minute-delayed overnight session minute bars the SIP/IEX historical feeds never have.
/// </summary>
public interface IAlpacaBoatsApi
{
  [Get("/v2/stocks/bars")]
  Task<BoatsBarsResponse> GetBarsAsync(
    [Query, AliasAs("symbols")] string symbols,
    [Query, AliasAs("timeframe")] string timeframe,
    [Query, AliasAs("start")] string start,
    [Query, AliasAs("end")] string end,
    [Query, AliasAs("limit")] int limit,
    [Query, AliasAs("feed")] string feed = "boats",
    [Query, AliasAs("page_token")] string? pageToken = null,
    [Header("APCA-API-KEY-ID")] string keyId = "",
    [Header("APCA-API-SECRET-KEY")] string secret = "",
    CancellationToken cancellationToken = default);
}
