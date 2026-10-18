using StockIndicators.Application.Charts;
using Xunit;

namespace StockIndicators.Indicators.Tests.Charts;

/// <summary>
/// The link-preview card's shape: the size messengers ask for and the word-boundary cut of
/// the description drawn over it. The picture itself is drawn by the renderer page (and read
/// back by check:seo), this guards the request the renderer is given.
/// </summary>
public class ChartRenderOptionsTests
{
  [Fact]
  public void Card_UsesOpenGraphSizeAtOneScale()
  {
    var card = ChartRenderOptions.Card("RSI", "some description");

    Assert.Equal(ChartRenderOptions.CardMode, card!.Mode);
    Assert.Equal(1200, card.Width);
    Assert.Equal(630, card.Height);
    Assert.Equal(1f, card.DeviceScaleFactor);
  }

  [Fact]
  public void Alert_KeepsTheHistoricalSizeAndScale()
  {
    var alert = ChartRenderOptions.Alert();

    Assert.Equal(ChartRenderOptions.AlertMode, alert.Mode);
    Assert.Equal(880, alert.Width);
    Assert.Equal(440, alert.Height);
    Assert.Equal(2f, alert.DeviceScaleFactor);
    Assert.Null(alert.Title);
  }

  [Fact]
  public void Truncate_CutsOnAWordBoundaryAndMarksIt()
  {
    var longText = string.Join(' ', Enumerable.Repeat("word", 100));

    var cut = ChartRenderOptions.Truncate(longText);

    Assert.NotNull(cut);
    Assert.True(cut!.Length <= ChartRenderOptions.MaxSubtitleLength + 1, $"cut to {cut.Length}");
    Assert.EndsWith("…", cut);
    Assert.DoesNotContain("  ", cut.TrimEnd('…'));
  }

  [Fact]
  public void Truncate_LeavesShortAndBlankTextAlone()
  {
    Assert.Equal("Short text", ChartRenderOptions.Truncate("Short text"));
    Assert.Null(ChartRenderOptions.Truncate(null));
    Assert.Null(ChartRenderOptions.Truncate("   "));
  }

  [Fact]
  public void Truncate_HardCutsASingleLongWord()
  {
    var oneWord = new string('a', ChartRenderOptions.MaxSubtitleLength * 2);

    var cut = ChartRenderOptions.Truncate(oneWord);

    Assert.NotNull(cut);
    Assert.EndsWith("…", cut!);
    Assert.Equal(ChartRenderOptions.MaxSubtitleLength + 1, cut.Length);
  }
}
