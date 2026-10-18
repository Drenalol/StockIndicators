namespace StockIndicators.Abstract;

public interface ITimeLimiter
{
  Task WaitAsync(CancellationToken cancellationToken);
}