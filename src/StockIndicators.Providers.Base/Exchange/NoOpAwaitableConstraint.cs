using RateLimiter;

namespace StockIndicators.Providers.Base.Exchange;

public sealed class NoOpAwaitableConstraint : IAwaitableConstraint
{
  public static NoOpAwaitableConstraint Instance { get; } = new();

  public Task<IDisposable> WaitForReadiness(CancellationToken cancellationToken)
    => Task.FromResult<IDisposable>(NoOpDisposable.Instance);

  public IAwaitableConstraint Clone() => this;
}
