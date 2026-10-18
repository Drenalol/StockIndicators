using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Subscriptions.GetSubscription;
using StockIndicators.Contracts.Rules;
using StockIndicators.Contracts.Subscriptions;

namespace StockIndicators.Application.Notifications.Telegram;

/// <summary>
/// Human-readable summary of a subscription for the bot detail screen
/// (previously the raw DTO JSON was dumped into the chat). Instruments, indicators and
/// parameters are read from the subscription's rule — the subscription itself stores none.
/// </summary>
public static class SubscriptionsText
{
  /// <summary>
  /// All leaf conditions of the subscription's rule, in tree order.
  /// </summary>
  public static IReadOnlyList<RuleConditionDto> Conditions(SubscriptionDto subscription)
  {
    var conditions = new List<RuleConditionDto>();

    void Walk(RuleGroupDto group)
    {
      conditions.AddRange(group.Conditions);

      foreach (var nested in group.Groups)
        Walk(nested);
    }

    if (subscription.Rule is not null)
      Walk(subscription.Rule.When);

    return conditions;
  }

  /// <summary>
  /// Instrument the bot's edit link points at: the first condition's ticker (the
  /// subscription has no instrument of its own). Null when unresolvable.
  /// </summary>
  public static string? PrimaryTicker(SubscriptionDto subscription) =>
    Conditions(subscription) is { Count: > 0 } conditions ? conditions[0].Ticker : null;

  public static string Detail(SubscriptionDto subscription, string language)
  {
    var lines = new List<string>();

    var conditions = Conditions(subscription);

    var instruments = conditions
      .Select(condition => string.IsNullOrWhiteSpace(condition.ExchangeName)
        ? condition.Ticker
        : $"{condition.Ticker} ({condition.ExchangeName})")
      .Distinct()
      .ToList();

    lines.Add($"<b>{(instruments.Count > 0 ? string.Join(", ", instruments) : "-")}</b>");

    var indicators = conditions.Select(condition => condition.Indicator).Distinct().ToList();

    lines.Add($"{BotTexts.SubscriptionFieldIndicator(language)}: {(indicators.Count > 0 ? string.Join(", ", indicators) : "-")}");
    lines.Add($"{BotTexts.SubscriptionFieldCooldown(language)}: {FormatCooldown(subscription.Cooldown, language)}");
    lines.Add($"{BotTexts.SubscriptionFieldStatus(language)}: {StatusLabel(subscription.Status, language)}");

    // Parameters only read as a single blob for a single-condition rule; multi-condition
    // rules list every condition's parameters on its own line
    if (conditions.Count == 1)
    {
      var parameters = conditions[0].TemplateData?.ToString();

      if (!string.IsNullOrWhiteSpace(parameters) && parameters != "{}")
        lines.Add($"{BotTexts.SubscriptionFieldParameters(language)}: {parameters}");
    }
    else if (conditions.Count > 1)
    {
      foreach (var condition in conditions)
      {
        var parameters = condition.TemplateData?.ToString();

        if (!string.IsNullOrWhiteSpace(parameters) && parameters != "{}")
          lines.Add($"{condition.Ticker} {condition.Indicator}: {parameters}");
      }
    }

    if (subscription.Outputs.Count > 0)
    {
      var channels = subscription.Outputs
        .Where(output => output.IsEnabled)
        .Select(output => output.Channel);

      lines.Add($"{BotTexts.SubscriptionFieldChannels(language)}: {string.Join(", ", channels)}");
    }

    return string.Join("\n", lines);
  }

  private static string StatusLabel(SubscriptionStatusDto status, string language) => status switch
  {
    SubscriptionStatusDto.Active => BotTexts.SubscriptionActive(language),
    _ => BotTexts.SubscriptionInactive(language),
  };

  private static string FormatCooldown(TimeSpan? cooldown, string language)
  {
    if (cooldown is null)
      return "-";

    var total = cooldown.Value.TotalSeconds;

    if (total >= 86400 && total % 86400 == 0)
      return $"{total / 86400:0} {LocalizationCatalog.Get(LocalizationKey.IntervalUnitDay, language)}";

    if (total >= 3600 && total % 3600 == 0)
      return $"{total / 3600:0} {LocalizationCatalog.Get(LocalizationKey.IntervalUnitHour, language)}";

    if (total >= 60 && total % 60 == 0)
      return $"{total / 60:0} {LocalizationCatalog.Get(LocalizationKey.IntervalUnitMinute, language)}";

    return $"{total:0} {LocalizationCatalog.Get(LocalizationKey.IntervalUnitSecond, language)}";
  }
}
