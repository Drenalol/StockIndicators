using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Application.Subscriptions.GetSubscription;

namespace StockIndicators.Application.Subscriptions.GetSubscriptions;

/// <summary>
/// Optional list filters of the subscriptions page (chips above the list, state lives in the
/// URL): free text over the instruments of the rule (ticker/name/transliteration/typos),
/// subscription statuses, instrument type and exchange (resolved through the persistent
/// Instruments table), indicator (RuleIndicators projection), rule shape (flat vs
/// multi-condition) and "triggered within the last week".
/// </summary>
public record GetSubscriptionsFilters
{
  public const int TriggeredWindowDays = 7;

  public string? Search { get; init; }

  /// <summary>Subscription status names (<see cref="Contracts.Subscriptions.SubscriptionStatusDto"/>); null/empty = any.</summary>
  public IReadOnlyList<string>? Statuses { get; init; }

  public string? Type { get; init; }

  public string? Indicator { get; init; }

  public string? ExchangeName { get; init; }

  /// <summary>"flat" (one condition) or "multi" (more than one); null = any.</summary>
  public string? RuleType { get; init; }

  /// <summary>Only subscriptions with an alert inside <see cref="TriggeredWindowDays"/> days.</summary>
  public bool? TriggeredRecently { get; init; }

  public bool IsEmpty =>
    string.IsNullOrWhiteSpace(Search) && (Statuses is null || Statuses.Count == 0) &&
    string.IsNullOrWhiteSpace(Type) && string.IsNullOrWhiteSpace(Indicator) &&
    string.IsNullOrWhiteSpace(ExchangeName) && string.IsNullOrWhiteSpace(RuleType) &&
    TriggeredRecently is null;
}

public class GetSubscriptionsQuery : IQuery<List<SubscriptionDto>>
{
  public Guid UserId { get; }

  public GetSubscriptionsFilters Filters { get; }

  public GetSubscriptionsQuery(Guid userId, GetSubscriptionsFilters? filters = null)
  {
    UserId = userId;
    Filters = filters ?? new GetSubscriptionsFilters();
  }
}
