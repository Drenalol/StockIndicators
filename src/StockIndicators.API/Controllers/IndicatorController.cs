using System.Text;
using LazyCache;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Indicators.GetIndicatorCatalog;
using StockIndicators.Application.Indicators.GetIndicatorSchema;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using StockIndicators.PublicSite;

namespace StockIndicators.Controllers;

// The whole controller is public (anonymous): it only exposes the indicator catalog,
// template schemas and example charts, all derived from template attributes and the
// embedded AAPL/TSLA fixtures - no user data. It feeds both the authenticated
// dashboard and the public landing indicators pages.
[Route("api/[controller]")]
[ApiController]
[AllowAnonymous]
[PublicSite]
public class IndicatorController : ControllerBase
{
  /// <summary>Example charts are deterministic (fixtures + sample defaults), so a long TTL is safe.</summary>
  private static readonly TimeSpan ExampleCacheTtl = TimeSpan.FromHours(24);

  private readonly IMediator _mediator;
  private readonly IAppCache _cache;

  public IndicatorController(IMediator mediator, IAppCache cache)
  {
    _mediator = mediator;
    _cache = cache;
  }

  /// <summary>
  /// The indicator catalog localized into <paramref name="lang"/>: display key, premium
  /// flag and the localized description. Powers the dashboard and the public indicators
  /// list; a new indicator (enum member + template) shows up here automatically.
  /// </summary>
  [HttpGet("catalog")]
  public async Task<ActionResult<IndicatorCatalogDto[]>> GetCatalog(
    [FromQuery] string? lang = null,
    CancellationToken cancellationToken = default) =>
    Ok(await _mediator.Send(
      new GetIndicatorCatalogQuery(UserLocale.NormalizeLanguage(lang)),
      cancellationToken));

  [HttpGet]
  public ActionResult<IEnumerable<string>> GetIndicators() =>
    Ok(Enum.GetValues<Indicator>().Select(indicator => indicator.ToString()));

  /// <summary>
  /// Template schema for the indicator: localized "what is this" description,
  /// property name → .NET type name, property name → default value,
  /// plus localized parameter hints (<paramref name="lang"/>: en (default), ru or es).
  /// </summary>
  [HttpGet("{indicator:required}")]
  public async Task<ActionResult<IndicatorSchemaDto>> GetIndicator(
    Indicator indicator,
    [FromQuery] string? lang = null,
    CancellationToken cancellationToken = default)
  {
    try
    {
      var language = UserLocale.NormalizeLanguage(lang);

      return Ok(await _mediator.Send(
        new GetIndicatorSchemaQuery(indicator, language),
        cancellationToken));
    }
    catch
    {
      return StatusCode(418, "Temporarily not available");
    }
  }

  /// <summary>
  /// Example chart of the indicator as raw series data for the client chart core: the
  /// overlay at sample parameters on real historical candles (embedded AAPL/TSLA fixtures),
  /// marked at the candle where the indicator triggers, for the indicator docs.
  /// </summary>
  [HttpGet("{indicator:required}/example")]
  public ActionResult<ChartData> GetExample(Indicator indicator, [FromQuery] string? lang = null)
  {
    Type templateType;

    try
    {
      templateType = indicator.GetTemplate();
    }
    catch
    {
      return StatusCode(418, "Temporarily not available");
    }

    var language = UserLocale.NormalizeLanguage(lang);

    var chart = _cache.GetOrAdd(
      $"indicator-example:{indicator}:{language}",
      () =>
      {
        var template = (IIndicatorTemplate)Activator.CreateInstance(templateType)!;
        ExampleIndicatorChart.ApplySampleDefaults(template);

        return ExampleIndicatorChart.RenderChartData(indicator, template, language);
      },
      DateTimeOffset.UtcNow.Add(ExampleCacheTtl));

    return chart;
  }
}
