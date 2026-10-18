using RateLimiter;
using StockIndicators.Abstract;

namespace StockIndicators.Providers.Base;

/// <summary>
/// Waits for all constraints before returning. Constraints are awaited sequentially and
/// each returned disposable is disposed on completion (in reverse order), including when
/// a later constraint faults. The RateLimiter package's <c>Compose()</c> is not used on
/// purpose: it composes constraints into a single lock and drops already-completed
/// sub-disposables when the wait faults, leaking the inner semaphore so that every
/// subsequent <see cref="WaitAsync"/> call stalls forever.
/// </summary>
public class ComposedTimeLimiter : ITimeLimiter
{
  private readonly IAwaitableConstraint[] _constraints;

  public ComposedTimeLimiter(params IAwaitableConstraint[] constraints)
  {
    if (constraints.Length == 0)
      throw new ArgumentException("At least one constraint is required.", nameof(constraints));

    _constraints = constraints;
  }

  public async Task WaitAsync(CancellationToken cancellationToken)
  {
    var acquired = new List<IDisposable>(_constraints.Length);

    try
    {
      foreach (var constraint in _constraints)
        acquired.Add(await constraint.WaitForReadiness(cancellationToken));
    }
    catch
    {
      for (var i = acquired.Count - 1; i >= 0; i--)
        acquired[i].Dispose();
      throw;
    }

    for (var i = acquired.Count - 1; i >= 0; i--)
      acquired[i].Dispose();
  }
}
