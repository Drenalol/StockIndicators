using StockIndicators.Domain.Models.Notifications;

namespace StockIndicators.Application.Notifications.Channels;

/// <summary>
/// Per-trigger result a channel sender must report for every item of the batch it was given:
/// the dispatcher persists one delivery row per outcome.
/// </summary>
public sealed record DeliveryOutcome(Guid TriggerId, DeliveryStatus Status, string? Error = null);
