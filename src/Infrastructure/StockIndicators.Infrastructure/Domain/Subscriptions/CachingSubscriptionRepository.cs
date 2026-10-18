using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StockIndicators.Application.EventBus;
using StockIndicators.Application.Subscriptions.GetSubscription;
using StockIndicators.Contracts.Events;
using StockIndicators.Domain.Models.Instruments;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;
using StockIndicators.Infrastructure.Database;

namespace StockIndicators.Infrastructure.Domain.Subscriptions;

public class CachingSubscriptionRepository : ISubscriptionRepository
{
  private readonly StockIndicatorsContext _stockIndicatorsContext;
  private readonly ISubscriptionCache _subscriptionCache;
  private readonly IInstrumentProvider _instrumentProvider;
  private readonly IEventBus _eventBus;
  private readonly ILogger _logger;

  public CachingSubscriptionRepository(
    StockIndicatorsContext stockIndicatorsContext,
    ISubscriptionCache subscriptionCache,
    IInstrumentProvider instrumentProvider,
    IEventBus eventBus,
    ILogger logger
  )
  {
    _stockIndicatorsContext = stockIndicatorsContext;
    _subscriptionCache = subscriptionCache;
    _instrumentProvider = instrumentProvider;
    _eventBus = eventBus;
    _logger = logger;
  }

  public async Task AddAsync(Subscription subscription, CancellationToken cancellationToken)
  {
    await _stockIndicatorsContext.Subscriptions.AddAsync(subscription, cancellationToken);

    // The subscription's rule (and its instrument projection) was committed before the
    // subscription exists, so every instrument the rule watches becomes live here
    foreach (var key in await GetRuleInstrumentsAsync(subscription.RuleGroupId, cancellationToken))
    {
      _instrumentProvider.AddWatch(key, InstrumentWatchSource.Subscription);

      await _eventBus.PublishAsync(new InstrumentChangedEvent(key.Ticker, key.ExchangeName, InstrumentAction.Subscribed), cancellationToken);
    }
  }

  public async Task<Subscription> GetByIdAsync(Guid id, Guid? userId, CancellationToken cancellationToken)
  {
    var subscription = await _stockIndicatorsContext.Subscriptions.FindAsync([id], cancellationToken);

    if (SubscriptionIsNull(subscription) || !OwnsSubscriptionAsync())
      throw new SubscriptionNotFoundException();

    return subscription;

    static bool SubscriptionIsNull([NotNullWhen(false)] Subscription? subscription) => subscription == null;

    bool OwnsSubscriptionAsync() => userId == null || subscription.UserId == userId;
  }

  public async Task<Subscription[]> GetRuleSubscriptionsByInstrumentsAsync(IReadOnlyCollection<InstrumentKey> instruments, CancellationToken cancellationToken)
  {
    if (instruments.Count == 0)
      return [];

    var wanted = instruments.Select(instrument => MatchKey(instrument.Ticker, instrument.ExchangeName)).ToHashSet();
    // List on purpose: an array would bind Contains to the span overload (C# 14) and EF could not translate it
    var tickers = wanted.Select(key => key.Ticker).ToList();

    // Match case-insensitively (candle tickers arrive upper-cased, stored conditions keep
    // the case the user created them with), then resolve the rule ids once
    var candidateRows = await _stockIndicatorsContext.RuleInstruments
      .AsNoTracking()
      .Where(instrument => tickers.Contains(instrument.Ticker.ToLower()))
      .Select(instrument => new { instrument.RuleId, instrument.Ticker, instrument.ExchangeName })
      .ToListAsync(cancellationToken);

    var ruleIds = candidateRows
      .Where(row => wanted.Contains(MatchKey(row.Ticker, row.ExchangeName)))
      .Select(row => row.RuleId)
      .Distinct()
      .ToList();

    if (ruleIds.Count == 0)
      return [];

    // Distinct by subscription: one candle batch carrying several instruments of the same
    // rule must not check (and alert) the same subscription twice
    return await _stockIndicatorsContext.Subscriptions
      .Include(subscription => subscription.Outputs)
      .Where(subscription => subscription.Status == SubscriptionStatus.Active && ruleIds.Contains(subscription.RuleGroupId))
      .ToArrayAsync(cancellationToken);
  }

  public Task SetActiveAsync(Subscription subscription, bool isActive, CancellationToken cancellationToken) =>
    SetStatusAsync(subscription, isActive ? SubscriptionStatus.Active : SubscriptionStatus.Inactive, cancellationToken);

  public async Task SetStatusAsync(Subscription subscription, SubscriptionStatus status, CancellationToken cancellationToken)
  {
    if (subscription.Status == status)
      return;

    var wasActive = subscription.Status == SubscriptionStatus.Active;

    subscription.Status = status;
    subscription.Updated = DateTime.UtcNow;

    var crossesActiveBoundary = wasActive != (status == SubscriptionStatus.Active);

    // The set runs before CommitAsync, so this subscription's row still carries the old
    // status; excluding it by id makes "watched by the others" correct in both directions
    var own = crossesActiveBoundary ? await GetRuleInstrumentsAsync(subscription.RuleGroupId, cancellationToken) : [];
    var watchedByOthers = crossesActiveBoundary ? await GetWatchedInstrumentsAsync([subscription.Id], cancellationToken) : [];

    await _stockIndicatorsContext.SaveChangesAsync(cancellationToken);
    await _subscriptionCache.InvalidateAsync(subscription, cancellationToken);

    // Activating (also resuming a plan pause): instruments nobody else watches start
    // streaming. Deactivating or pausing: the same difference are the instruments that
    // lose their last watcher
    foreach (var key in own.Where(key => !watchedByOthers.Contains(key)))
    {
      if (status == SubscriptionStatus.Active)
      {
        _instrumentProvider.AddWatch(key, InstrumentWatchSource.Subscription);

        await _eventBus.PublishAsync(new InstrumentChangedEvent(key.Ticker, key.ExchangeName, InstrumentAction.Subscribed), cancellationToken);
      }
      else
      {
        _instrumentProvider.RemoveWatch(key, InstrumentWatchSource.Subscription);
        await _eventBus.PublishAsync(new InstrumentChangedEvent(key.Ticker, key.ExchangeName, InstrumentAction.Unsubscribed), cancellationToken);
      }
    }
  }

  public async Task MarkReadAsync(Subscription subscription, CancellationToken cancellationToken)
  {
    // Only the read stamp changes; the cooldown cache and the rule instruments are unaffected
    subscription.LastReadAt = DateTime.UtcNow;

    await _stockIndicatorsContext.SaveChangesAsync(cancellationToken);
  }

  public async Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken)
  {
    // Bulk stamp of the user's unread subscriptions: one UPDATE, no tracked entities; the
    // read state plays no part in rule evaluation, so the cooldown cache stays untouched
    await _stockIndicatorsContext.Subscriptions
      .Where(subscription => subscription.UserId == userId
        && subscription.LastTriggeredAt != null
        && (subscription.LastReadAt == null || subscription.LastTriggeredAt > subscription.LastReadAt))
      .ExecuteUpdateAsync(
        setters => setters.SetProperty(subscription => subscription.LastReadAt!, DateTime.UtcNow),
        cancellationToken);
  }

  public async Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken)
  {
    var subscription = await GetByIdAsync(id, userId, cancellationToken);
    var own = await GetRuleInstrumentsAsync(subscription.RuleGroupId, cancellationToken);

    _stockIndicatorsContext.Subscriptions.Remove(subscription);
    await _subscriptionCache.InvalidateAsync(subscription, cancellationToken);

    // The row is not deleted yet, so it must be excluded explicitly to see who else watches
    var watchedByOthers = await GetWatchedInstrumentsAsync([subscription.Id], cancellationToken);

    foreach (var key in own.Where(key => !watchedByOthers.Contains(key)))
    {
      _instrumentProvider.RemoveWatch(key, InstrumentWatchSource.Subscription);
      await _eventBus.PublishAsync(new InstrumentChangedEvent(key.Ticker, key.ExchangeName, InstrumentAction.Unsubscribed), cancellationToken);
    }
  }

  public async Task<Guid[]> GetRuleGroupIdsByInstrumentAsync(string ticker, string? exchangeName, Guid userId, CancellationToken cancellationToken) =>
    await QuerySubscriptionsByInstrumentAsync(ticker, exchangeName, userId)
      .Select(subscription => subscription.RuleGroupId)
      .Distinct()
      .ToArrayAsync(cancellationToken);

  public async Task DeleteByInstrumentAsync(string ticker, string? exchangeName, Guid userId, CancellationToken cancellationToken)
  {
    var subscriptions = await QuerySubscriptionsByInstrumentAsync(ticker, exchangeName, userId)
      .ToListAsync(cancellationToken);

    if (subscriptions.Count == 0)
      return;

    var own = new HashSet<InstrumentKey>();

    foreach (var subscription in subscriptions)
    {
      _stockIndicatorsContext.Subscriptions.Remove(subscription);
      await _subscriptionCache.InvalidateAsync(subscription, cancellationToken);

      foreach (var key in await GetRuleInstrumentsAsync(subscription.RuleGroupId, cancellationToken))
        own.Add(key);
    }

    // The rows are not deleted yet, so the "watched by the others" set excludes the
    // subscriptions scheduled for removal
    var watchedByOthers = await GetWatchedInstrumentsAsync(subscriptions.Select(subscription => subscription.Id).ToArray(), cancellationToken);

    foreach (var key in own.Where(key => !watchedByOthers.Contains(key)))
    {
      _instrumentProvider.RemoveWatch(key, InstrumentWatchSource.Subscription);
      await _eventBus.PublishAsync(new InstrumentChangedEvent(key.Ticker, key.ExchangeName, InstrumentAction.Unsubscribed), cancellationToken);
    }
  }

  public async Task SyncInstrumentsAfterRuleChangeAsync(IReadOnlyCollection<InstrumentKey> previousInstruments, CancellationToken cancellationToken = default)
  {
    // Only active subscriptions make a rule's instruments "watched"; the rule may well be
    // updated while nothing (or only inactive subscriptions) references it
    var watched = await GetWatchedInstrumentsAsync(null, cancellationToken);
    var previous = previousInstruments.Select(instrument => MatchKey(instrument.Ticker, instrument.ExchangeName)).ToHashSet();

    foreach (var key in previous.Where(key => !watched.Contains(key)))
    {
      _instrumentProvider.RemoveWatch(key, InstrumentWatchSource.Subscription);
      await _eventBus.PublishAsync(new InstrumentChangedEvent(key.Ticker, key.ExchangeName, InstrumentAction.Unsubscribed), cancellationToken);
    }

    foreach (var key in watched.Where(key => !previous.Contains(key)))
    {
      _instrumentProvider.AddWatch(key, InstrumentWatchSource.Subscription);

      await _eventBus.PublishAsync(new InstrumentChangedEvent(key.Ticker, key.ExchangeName, InstrumentAction.Subscribed), cancellationToken);
    }
  }

  public async Task<List<SubscriptionOutput>> GetOutputsAsync(Guid subscriptionId, CancellationToken cancellationToken) =>
    await _stockIndicatorsContext.SubscriptionOutputs
      .AsNoTracking()
      .Where(output => output.SubscriptionId == subscriptionId)
      .OrderBy(output => output.Channel)
      .ToListAsync(cancellationToken);

  public async Task ReconcileOutputsAsync(Guid subscriptionId, IReadOnlyCollection<SubscriptionOutput> outputs, CancellationToken cancellationToken)
  {
    var existing = await _stockIndicatorsContext.SubscriptionOutputs
      .Where(output => output.SubscriptionId == subscriptionId)
      .ToListAsync(cancellationToken);

    var incoming = outputs.ToList();

    foreach (var stale in existing.Where(output => incoming.All(item => item.Id != output.Id)))
      _stockIndicatorsContext.SubscriptionOutputs.Remove(stale);

    foreach (var item in incoming)
    {
      var match = existing.FirstOrDefault(output => output.Id == item.Id);

      if (match == null)
      {
        item.SubscriptionId = subscriptionId;
        _stockIndicatorsContext.SubscriptionOutputs.Add(item);
      }
      else
      {
        match.Channel = item.Channel;
        match.NotificationTemplateId = item.NotificationTemplateId;
        match.PushSubscriptionId = item.PushSubscriptionId;
        match.ChatId = item.ChatId;
        match.Target = item.Target;
        match.IsEnabled = item.IsEnabled;
      }
    }

    await _stockIndicatorsContext.SaveChangesAsync(cancellationToken);
  }

  public Task<int> CountActiveByUserAsync(Guid userId, CancellationToken cancellationToken) =>
    _stockIndicatorsContext.Subscriptions.CountAsync(subscription => subscription.UserId == userId && subscription.Status == SubscriptionStatus.Active, cancellationToken);

  public Task<bool> IsTriggeredAsync(Subscription subscription, CancellationToken cancellationToken) => _subscriptionCache.IsTriggeredAsync(subscription, cancellationToken);

  public async Task SetTriggeredAsync(Subscription subscription, CancellationToken cancellationToken = default)
  {
    subscription.LastTriggeredAt = DateTime.UtcNow;
    await _subscriptionCache.SetTriggeredAsync(subscription, cancellationToken);
  }

  public Task SetCooldownAsync(Subscription subscription, CancellationToken cancellationToken = default) =>
    _subscriptionCache.SetTriggeredAsync(subscription, cancellationToken);

  public Task InvalidateAsync(Subscription subscription, CancellationToken cancellationToken = default) =>
    _subscriptionCache.InvalidateAsync(subscription, cancellationToken);

  public Task<int> CommitAsync(CancellationToken cancellationToken = default) => _stockIndicatorsContext.SaveChangesAsync(cancellationToken);

  /// <summary>
  /// Subscriptions of <paramref name="userId"/> whose rule watches the instrument
  /// (case-insensitive; an empty exchange matches instruments without one). Same filter for
  /// <see cref="GetRuleGroupIdsByInstrumentAsync"/> and <see cref="DeleteByInstrumentAsync"/>.
  /// </summary>
  private IQueryable<Subscription> QuerySubscriptionsByInstrumentAsync(string ticker, string? exchangeName, Guid userId)
  {
    var wantedTicker = ticker.ToLowerInvariant();
    var wantedExchange = (exchangeName ?? string.Empty).ToLowerInvariant();

    var ruleIds = _stockIndicatorsContext.RuleInstruments
      .AsNoTracking()
      .Where(instrument =>
        instrument.Ticker.ToLower() == wantedTicker
        && instrument.ExchangeName.ToLower() == wantedExchange)
      .Select(instrument => instrument.RuleId)
      .Distinct();

    return _stockIndicatorsContext.Subscriptions
      .Where(subscription => subscription.UserId == userId && ruleIds.Contains(subscription.RuleGroupId));
  }

  /// <summary>
  /// Distinct instruments watched by <em>active</em> subscriptions (subscriptions joined to
  /// their rule's instrument projection), optionally excluding subscriptions by id.
  /// </summary>
  private async Task<HashSet<InstrumentKey>> GetWatchedInstrumentsAsync(IReadOnlyCollection<Guid>? excludeSubscriptionIds, CancellationToken cancellationToken)
  {
    var rows = _stockIndicatorsContext.Subscriptions
      .AsNoTracking()
      .Join(
        _stockIndicatorsContext.RuleInstruments.AsNoTracking(),
        subscription => subscription.RuleGroupId,
        instrument => instrument.RuleId,
        (subscription, instrument) => new { subscription.Id, subscription.Status, instrument.Ticker, instrument.ExchangeName })
      .Where(row => row.Status == SubscriptionStatus.Active)
      .Select(row => new { row.Id, row.Ticker, row.ExchangeName })
      .Distinct();

    if (excludeSubscriptionIds is { Count: > 0 })
    {
      var excluded = excludeSubscriptionIds.ToList();
      rows = rows.Where(row => !excluded.Contains(row.Id));
    }

    var watched = await rows.ToListAsync(cancellationToken);

    return watched.Select(row => MatchKey(row.Ticker, row.ExchangeName)).ToHashSet();
  }

  /// <summary>
  /// The persisted instruments of a rule as instrument keys (empty exchange → null).
  /// </summary>
  private async Task<InstrumentKey[]> GetRuleInstrumentsAsync(Guid ruleId, CancellationToken cancellationToken)
  {
    var rows = await _stockIndicatorsContext.RuleInstruments
      .AsNoTracking()
      .Where(instrument => instrument.RuleId == ruleId)
      .Select(instrument => new { instrument.Ticker, instrument.ExchangeName })
      .ToArrayAsync(cancellationToken);

    return rows.Select(row => MatchKey(row.Ticker, row.ExchangeName)).ToArray();
  }

  /// <summary>
  /// Case-insensitive instrument identity used for every set/difference computation;
  /// an empty exchange normalizes to null.
  /// </summary>
  private static InstrumentKey MatchKey(string ticker, string? exchangeName)
  {
    var exchange = exchangeName?.ToLowerInvariant();

    return new InstrumentKey(ticker.ToLowerInvariant(), string.IsNullOrEmpty(exchange) ? null : exchange);
  }
}
