using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace StockIndicators.Contracts.Subscriptions;

/// <summary>
/// Wire mirror of <c>StockIndicators.Domain.Models.Subscriptions.SubscriptionStatus</c>:
/// "Active" runs, "Inactive" is the user's own switch.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum SubscriptionStatusDto
{
  Inactive,
  Active,
}
