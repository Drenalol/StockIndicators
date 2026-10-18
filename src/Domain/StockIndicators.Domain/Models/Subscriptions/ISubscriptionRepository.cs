using StockIndicators.Domain.Models.Instruments;
using StockIndicators.Domain.Repositories;

namespace StockIndicators.Domain.Models.Subscriptions;

public interface ISubscriptionRepository : ICreateRepositoryBase<Subscription>
{
  Task<Subscription> GetByIdAsync(Guid id, Guid? userId, CancellationToken cancellationToken);

  /// <summary>
  /// Active rule subscriptions whose rule watches at least one of the given instruments.
  /// A subscription appears once even when several instruments of the batch match its rule.
  /// </summary>
  Task<Subscription[]> GetRuleSubscriptionsByInstrumentsAsync(IReadOnlyCollection<InstrumentKey> instruments, CancellationToken cancellationToken);
  Task<List<SubscriptionOutput>> GetOutputsAsync(Guid subscriptionId, CancellationToken cancellationToken);
  Task ReconcileOutputsAsync(Guid subscriptionId, IReadOnlyCollection<SubscriptionOutput> outputs, CancellationToken cancellationToken);
  /// <summary>
  /// The user's own switch: moves the subscription to <see cref="SubscriptionStatus.Active"/>
  /// or <see cref="SubscriptionStatus.Inactive"/>.
  /// </summary>
  Task SetActiveAsync(Subscription subscription, bool isActive, CancellationToken cancellationToken);

  /// <summary>
  /// Explicit status transition. Keeps the provider watch bookkeeping consistent for every
  /// transition across the Active boundary, which is why even <see cref="SetActiveAsync"/>
  /// goes through it.
  /// </summary>
  Task SetStatusAsync(Subscription subscription, SubscriptionStatus status, CancellationToken cancellationToken);

  /// <summary>
  /// Clears the "new alert" flag: stamps <c>LastReadAt</c> with the current time. The
  /// cooldown cache is untouched — the read state does not affect rule evaluation.
  /// </summary>
  Task MarkReadAsync(Subscription subscription, CancellationToken cancellationToken);

  /// <summary>
  /// Clears the "new alert" flag of every unread subscription of the user in one bulk UPDATE.
  /// </summary>
  Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken);
  Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken);

  /// <summary>
  /// Rule ids referenced by the user's subscriptions whose rule watches the instrument
  /// (same filter as <see cref="DeleteByInstrumentAsync"/>).
  /// </summary>
  Task<Guid[]> GetRuleGroupIdsByInstrumentAsync(string ticker, string? exchangeName, Guid userId, CancellationToken cancellationToken);
  Task DeleteByInstrumentAsync(string ticker, string? exchangeName, Guid userId, CancellationToken cancellationToken);

  /// <summary>
  /// Reconciles provider tracking and InstrumentChangedEvent publications after a rule's
  /// instrument set changed (the rule was updated): instruments nobody's active subscriptions
  /// watch anymore are untracked, newly watched ones are tracked.
  /// </summary>
  Task SyncInstrumentsAfterRuleChangeAsync(IReadOnlyCollection<InstrumentKey> previousInstruments, CancellationToken cancellationToken = default);

  /// <summary>
  /// Number of active subscriptions owned by the user. Used for the free-plan limit.
  /// </summary>
  Task<int> CountActiveByUserAsync(Guid userId, CancellationToken cancellationToken);
  Task<bool> IsTriggeredAsync(Subscription subscription, CancellationToken cancellationToken);
  Task SetTriggeredAsync(Subscription subscription, CancellationToken cancellationToken = default);

  /// <summary>
  /// Starts the alert cooldown without stamping <c>LastTriggeredAt</c>: used when a repeat on
  /// an already-notified candle was suppressed, so the subscription keeps quiet without the
  /// unread badge lighting up again for an alert the user already got.
  /// </summary>
  Task SetCooldownAsync(Subscription subscription, CancellationToken cancellationToken = default);
  Task InvalidateAsync(Subscription subscription, CancellationToken cancellationToken = default);
}
