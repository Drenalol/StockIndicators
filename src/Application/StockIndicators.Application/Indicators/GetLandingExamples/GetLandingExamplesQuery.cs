using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Queries;

namespace StockIndicators.Application.Indicators.GetLandingExamples;

/// <summary>
/// Public marketing gallery for the landing page: real trigger-shaped example charts of
/// several indicators (the same fixtures the indicator docs render, as interactive series
/// data). Anonymous by design - the landing is the entry page for unauthenticated users.
/// The payload is deterministic, so it is cached per language.
/// </summary>
public record GetLandingExamplesQuery(string Language) : IQuery<LandingExampleChartDto[]>;

/// <summary>
/// One gallery slide: the indicator enum name (the client localizes it), the instrument the
/// example candles belong to, and the chart payload in the interactive (v2) format.
/// </summary>
public sealed record LandingExampleChartDto(
  string Indicator,
  string Ticker,
  string? ExchangeName,
  ChartData Chart);
