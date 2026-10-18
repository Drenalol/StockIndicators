namespace StockIndicators.Domain.Models.Indicators;

public static class AlertDirectionExtensions
{
  /// <summary>
  /// Whether an event of the given side passes the template's direction filter.
  /// <see cref="AlertDirection.Both"/> accepts every event, sideless ones included,
  /// so rules written before the Direction parameter existed keep their exact behavior.
  /// </summary>
  public static bool Matches(this AlertDirection filter, AlertDirection? eventDirection) =>
    filter == AlertDirection.Both || eventDirection == filter;
}
