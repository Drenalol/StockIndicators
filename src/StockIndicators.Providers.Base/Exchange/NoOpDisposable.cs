namespace StockIndicators.Providers.Base.Exchange;

internal sealed class NoOpDisposable : IDisposable
{
  public static NoOpDisposable Instance { get; } = new();

  public void Dispose()
  {
  }
}
