using RateLimiter;

namespace StockIndicators.Providers.Base.Exchange;

public sealed class ExchangeSessionAwaitableConstraint : IAwaitableConstraint
{
  private static readonly TimeSpan DefaultReadinessGrace = TimeSpan.FromSeconds(10);

  private readonly ExchangeSession _session;
  private readonly TimeSpan _readinessGrace;
  private readonly bool _includeExtendedSessions;

  public ExchangeSessionAwaitableConstraint(ExchangeSession session, TimeSpan? readinessGrace = null, bool includeExtendedSessions = false)
  {
    _session = session ?? throw new ArgumentNullException(nameof(session));
    _readinessGrace = readinessGrace ?? DefaultReadinessGrace;
    _includeExtendedSessions = includeExtendedSessions;
  }

  public async Task<IDisposable> WaitForReadiness(CancellationToken cancellationToken)
  {
    var now = _session.UtcNow;

    if (_session.IsOpenAt(now) || _includeExtendedSessions && _session.ExtendedKindAt(now) is not null)
      return NoOpDisposable.Instance;

    var delay = (_includeExtendedSessions ? _session.GetNextExtendedOpen(now) : _session.GetNextOpen(now)) - now;

    if (delay <= _readinessGrace)
      return NoOpDisposable.Instance;

    await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds, int.MaxValue - 1)), cancellationToken);
    return NoOpDisposable.Instance;
  }

  public IAwaitableConstraint Clone() => new ExchangeSessionAwaitableConstraint(_session, _readinessGrace, _includeExtendedSessions);
}
