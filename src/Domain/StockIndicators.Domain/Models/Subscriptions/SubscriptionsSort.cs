namespace StockIndicators.Domain.Models.Subscriptions;

/// <summary>
/// Sorts of the subscriptions list, stored as a user preference (User.SubscriptionsSort).
/// A null preference means the default order — <see cref="AlertDate"/>.
/// </summary>
public static class SubscriptionsSort
{
  /// <summary>Groups by the freshest last alert first, never-fired ones at the bottom.</summary>
  public const string AlertDate = "alertDate";

  /// <summary>Groups with more alerts first, equal groups — by the freshest alert.</summary>
  public const string AlertCount = "alertCount";

  /// <summary>Ticker A to Z, then exchange.</summary>
  public const string Ticker = "ticker";

  /// <summary>Groups by their newest subscription first.</summary>
  public const string Created = "created";

  public static readonly string[] All = [AlertDate, AlertCount, Ticker, Created];

  public static bool IsValid(string? value) => value is null || Array.IndexOf(All, value) >= 0;
}
