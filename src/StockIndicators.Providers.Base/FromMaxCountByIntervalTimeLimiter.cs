using RateLimiter;
using StockIndicators.Abstract;
using StockIndicators.Providers.Base.Exchange;

namespace StockIndicators.Providers.Base;

public class FromMaxCountByIntervalTimeLimiter : ITimeLimiter
{
  private readonly IAwaitableConstraint _constraint;

  public FromMaxCountByIntervalTimeLimiter(int maxCount, TimeSpan interval)
    => _constraint = new CountByIntervalAwaitableConstraint(maxCount, interval);

  public FromMaxCountByIntervalTimeLimiter(IEnumerable<LimitOption> limits)
    => _constraint = CreateConstraint(limits);

  public static IAwaitableConstraint CreateConstraint(IEnumerable<LimitOption> limits)
  {
    var constraints = limits
      .Select(limit => (IAwaitableConstraint)new CountByIntervalAwaitableConstraint(limit.Value, limit.Interval))
      .ToArray();

    return constraints.Length switch
    {
      0 => NoOpAwaitableConstraint.Instance,
      1 => constraints[0],
      _ => constraints.Aggregate((left, right) => left.Compose(right))
    };
  }

  public async Task WaitAsync(CancellationToken cancellationToken)
  {
    var disposable = await _constraint.WaitForReadiness(cancellationToken);
    disposable.Dispose();
  }
}
