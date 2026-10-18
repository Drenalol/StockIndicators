using Xunit;

namespace StockIndicators.Indicators.Tests.Charts;

public class ChartXTests
{
  [Fact]
  public void ChartX_UsesOadateConvention()
  {
    var date = new DateTime(2026, 8, 20, 16, 8, 59, DateTimeKind.Utc);

    Assert.Equal(date.ToOADate(), ChartX.ToX(date));
    Assert.Equal(date, ChartX.FromX(ChartX.ToX(date)));
  }
}
