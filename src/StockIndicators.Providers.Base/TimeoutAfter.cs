namespace StockIndicators.Providers.Base;

/// <summary>
/// Hard timeout for a single provider call on a user-facing (request) path.
/// Enforced with <c>Task.WhenAny</c> in addition to the linked token: a request
/// must never hang behind a provider that stops answering, so the waiter gives up after
/// <c>timeout</c> and throws <see cref="TimeoutException"/> even when the inner
/// task ignores cancellation. The orphaned task keeps running in the background and its
/// result is simply discarded.
/// </summary>
public static class TimeoutAfter
{
  public static async Task<T> Run<T>(Func<CancellationToken, Task<T>> call, TimeSpan timeout, CancellationToken cancellationToken = default)
  {
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    cts.CancelAfter(timeout);

    var callTask = call(cts.Token);
    var completed = await Task.WhenAny(callTask, Task.Delay(timeout, cts.Token));

    if (completed != callTask)
      throw new TimeoutException($"Provider call timed out after {timeout}.");

    return await callTask;
  }
}
