using StockIndicators.Application.Subscriptions.GetSubscription;
using StockIndicators.Contracts.Subscriptions;

namespace StockIndicators.Application.Notifications.Telegram;

/// <summary>
/// Encodes a subscription into a readable reply-keyboard row label ("TICKER · INDICATOR",
/// with "+N" when the rule watches more indicators and a pause prefix when inactive),
/// so the list shows what a subscription is about instead of its raw GUID. Labels are
/// not unique by nature (two identical rules read the same), so <see cref="MakeUnique"/>
/// appends a counter to duplicates of a page — the form maps labels back to ids by the
/// same page-scoped values.
/// </summary>
public static class SubscriptionRowCodec
{
  private const string InactivePrefix = "⏸ ";

  public static string Encode(SubscriptionDto subscription)
  {
    var conditions = SubscriptionsText.Conditions(subscription);

    string label;

    if (conditions.Count == 0)
      // A rule-less container has nothing readable to show — the rule name (or a dash) keeps the row usable
      label = subscription.Rule?.Name is { Length: > 0 } name ? name : "-";
    else
    {
      var indicators = conditions
        .Select(condition => condition.Indicator)
        .Distinct()
        .ToList();

      var extra = indicators.Count > 1 ? $" +{indicators.Count - 1}" : string.Empty;

      label = $"{conditions[0].Ticker} · {indicators[0]}{extra}";
    }

    // Both non-running states read as paused in a row label; the detail line names the reason
    return subscription.Status == SubscriptionStatusDto.Active ? label : $"{InactivePrefix}{label}";
  }

  public static string MakeUnique(string label, HashSet<string> used)
  {
    if (used.Add(label))
      return label;

    var counter = 2;
    string candidate;

    do
      candidate = $"{label} #{counter++}";
    while (!used.Add(candidate));

    return candidate;
  }
}
