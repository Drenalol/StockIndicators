using StockIndicators.Application.Indicators.GetIndicatorSchema;
using StockIndicators.Domain.Models.Indicators;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class GetIndicatorSchemaQueryHandlerTests
{
  private static Task<IndicatorSchemaDto> Run(Indicator indicator) =>
    new GetIndicatorSchemaQueryHandler().Handle(new GetIndicatorSchemaQuery(indicator, "en"), default);

  // Custom template enums must ship their option names so the client can render a
  // select without hardcoding them; TimeFrame and CandlePart have built-in widgets.
  [Fact]
  public async Task PriceChange_ShipsDirectionOptions()
  {
    var schema = await Run(Indicator.PriceChange);

    Assert.Equal(["Both", "Up", "Down"], schema.EnumOptions["Direction"]);
    Assert.DoesNotContain("TimeFrame", schema.EnumOptions.Keys);
  }

  [Fact]
  public async Task MovingAverage_HasNoEnumOptions()
  {
    var schema = await Run(Indicator.SMA);

    Assert.Empty(schema.EnumOptions);
  }
}
