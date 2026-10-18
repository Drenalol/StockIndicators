using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Domain.Models.Instruments;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Infrastructure.Database;

namespace StockIndicators.Infrastructure.Domain.Rules;

public class RuleRepository : IRuleRepository
{
  private readonly StockIndicatorsContext _context;
  private readonly ISerializer _serializer;
  private readonly ILogger _logger;

  public RuleRepository(StockIndicatorsContext context, ISerializer serializer, ILogger logger)
  {
    _context = context;
    _serializer = serializer;
    _logger = logger;
  }

  public async Task AddAsync(Rule rule, CancellationToken cancellationToken)
  {
    rule.ConditionCount = rule.When.CountConditions();
    await _context.Rules.AddAsync(rule, cancellationToken);
    await SyncInstrumentsAsync(rule, cancellationToken);
    await SyncRuleIndicatorsAsync(rule, cancellationToken);
  }

  public Task<int> CommitAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);

  public async Task<InstrumentKey[]> GetInstrumentsAsync(Guid ruleId, CancellationToken cancellationToken)
  {
    var rows = await _context.RuleInstruments
      .AsNoTracking()
      .Where(instrument => instrument.RuleId == ruleId)
      .Select(instrument => new { instrument.Ticker, instrument.ExchangeName })
      .ToArrayAsync(cancellationToken);

    return rows
      .Select(row => new InstrumentKey(row.Ticker, row.ExchangeName.Length > 0 ? row.ExchangeName : null))
      .ToArray();
  }

  public async Task<Rule?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
  {
    var rule = await _context.Rules.FindAsync([id], cancellationToken);
    return rule == null ? null : EnrichRule(rule);
  }

  public IAsyncEnumerable<Rule> GetAsync(Expression<Func<Rule, bool>> predicate)
  {
    var rules = _context.Rules.AsNoTracking().Where(predicate);

    return EnrichAsync(rules);

    async IAsyncEnumerable<Rule> EnrichAsync(IQueryable<Rule> rules)
    {
      var list = await rules.ToListAsync();

      foreach (var rule in list)
        yield return EnrichRule(rule);
    }
  }

  public async Task<Rule[]> GetAllAsync(CancellationToken cancellationToken)
  {
    var rules = await _context.Rules.AsNoTracking().OrderBy(rule => rule.Created).ToListAsync(cancellationToken);
    return rules.Select(EnrichRule).ToArray();
  }

  public async Task<Rule?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken)
  {
    var rule = await _context.Rules.AsNoTracking()
      .FirstOrDefaultAsync(stored => stored.Id == id && stored.UserId == userId, cancellationToken);

    return rule == null ? null : EnrichRule(rule);
  }

  public async Task UpdateAsync(Rule rule, CancellationToken cancellationToken)
  {
    // GetByIdForUserAsync reads AsNoTracking: without this the update handler's changes
    // never reach SaveChanges. The EF model ignores When/ThenTrue/ThenFalse, so Update
    // writes only the scalar columns (Name/RuleData/ConditionCount/Updated) - the tree lives in RuleData.
    rule.ConditionCount = rule.When.CountConditions();
    _context.Rules.Update(rule);
    await SyncInstrumentsAsync(rule, cancellationToken);
    await SyncRuleIndicatorsAsync(rule, cancellationToken);
  }

  /// <summary>
  /// Replaces the <see cref="RuleInstrument"/> projection with the instruments of the rule's
  /// current WHEN tree (tracked deletes/inserts, so it commits atomically with the rule),
  /// keeping rows that did not change.
  /// </summary>
  private async Task SyncInstrumentsAsync(Rule rule, CancellationToken cancellationToken)
  {
    var stale = await _context.RuleInstruments
      .Where(instrument => instrument.RuleId == rule.Id)
      .ToListAsync(cancellationToken);

    var fresh = rule.When.CollectInstruments(rule.Id);

    bool Same(RuleInstrument a, RuleInstrument b) => a.Ticker == b.Ticker && a.ExchangeName == b.ExchangeName;

    _context.RuleInstruments.RemoveRange(stale.Where(old => !fresh.Any(f => Same(old, f))));
    _context.RuleInstruments.AddRange(fresh.Where(next => !stale.Any(old => Same(old, next))));
  }

  /// <summary>
  /// Replaces the <see cref="RuleIndicator"/> projection with the (ticker, indicator) pairs of
  /// the rule's current WHEN tree, same tracked-diff pattern as <see cref="SyncInstrumentsAsync"/>.
  /// </summary>
  private async Task SyncRuleIndicatorsAsync(Rule rule, CancellationToken cancellationToken)
  {
    var stale = await _context.RuleIndicators
      .Where(indicator => indicator.RuleId == rule.Id)
      .ToListAsync(cancellationToken);

    var fresh = rule.When.CollectIndicators(rule.Id);

    bool Same(RuleIndicator a, RuleIndicator b) =>
      a.Ticker == b.Ticker && a.ExchangeName == b.ExchangeName && a.Indicator == b.Indicator;

    _context.RuleIndicators.RemoveRange(stale.Where(old => !fresh.Any(f => Same(old, f))));
    _context.RuleIndicators.AddRange(fresh.Where(next => !stale.Any(old => Same(old, next))));
  }

  public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
  {
    var rule = await _context.Rules.FindAsync([id], cancellationToken);

    if (rule != null)
      _context.Rules.Remove(rule);
  }

  // The subscriptions-to-rule FK is Restrict: even inactive subscriptions block the delete
  public Task<bool> IsReferencedBySubscriptionAsync(Guid ruleId, CancellationToken cancellationToken) =>
    _context.Subscriptions.AnyAsync(subscription => subscription.RuleGroupId == ruleId, cancellationToken);

  private Rule EnrichRule(Rule rule)
  {
    if (_serializer.DeserializeOrNull(rule.RuleData, typeof(RuleDataPayload)) is not RuleDataPayload payload)
    {
      var exception = new InvalidOperationException($"Deserialize '{rule.RuleData}' failed for rule '{rule.Id}'");
      _logger.Warning(exception, "Enrich rule {Id} failed", rule.Id);

      throw exception;
    }

    rule.When = payload.When;
    rule.ThenTrue = payload.ThenTrue;
    rule.ThenFalse = payload.ThenFalse;
    return rule;
  }
}
