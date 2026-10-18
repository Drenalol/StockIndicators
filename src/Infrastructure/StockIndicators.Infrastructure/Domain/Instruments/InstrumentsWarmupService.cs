using System.Diagnostics;
using LazyCache;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using StockIndicators.Abstract;
using StockIndicators.Application.Instruments;
using StockIndicators.Application.Providers;

namespace StockIndicators.Infrastructure.Domain.Instruments;

/// <summary>
/// Warms up the aggregated instruments list cache (<see cref="InstrumentsCache.AllKey"/>)
/// at startup: <see cref="StartAsync"/> blocks until the initial load completes or hits
/// <see cref="InstrumentsWarmupOptions.InitialWarmupTimeout"/> (a hard, <c>Task.WhenAny</c>-based
/// timeout), so the cache is warm before the other hosted services (and the web server)
/// start. Afterwards a background loop refreshes the cache every
/// <see cref="InstrumentsWarmupOptions.RefreshInterval"/> so the entry is rewritten before its
/// <see cref="InstrumentsCache.ListTtl"/> expires. After a failed attempt the retry delay drops to
/// <see cref="InstrumentsWarmupOptions.FailureRetryInterval"/> until the first success.
/// </summary>
public class InstrumentsWarmupService : IHostedService
{
  private readonly InstrumentsWarmupOptions _options;
  private readonly List<IInstrumentSource> _instrumentSources;
  private readonly IAppCache _cache;
  private readonly IInstrumentsTableSync _tableSync;
  private readonly IUnwiredDataProviders _unwiredDataProviders;
  private readonly ILogger _logger;

  private CancellationTokenSource? _stopCts;
  private Task? _refreshLoop;

  public InstrumentsWarmupService(
    IOptions<InstrumentsWarmupOptions> options,
    IEnumerable<IInstrumentSource> instrumentSources,
    IAppCache cache,
    IInstrumentsTableSync tableSync,
    IUnwiredDataProviders unwiredDataProviders,
    ILogger logger)
  {
    _options = options.Value;
    _instrumentSources = instrumentSources.ToList();
    _cache = cache;
    _tableSync = tableSync;
    _unwiredDataProviders = unwiredDataProviders;
    _logger = logger;
  }

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    var firstSucceeded = false;

    try
    {
      firstSucceeded = await TryWarmupAsync(cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      // The host is stopping: skip the refresh loop and let shutdown complete cleanly.
      _logger.Information("Instruments warmup cancelled while the host is stopping");
      return;
    }

    _stopCts = new CancellationTokenSource();
    _refreshLoop = Task.Run(() => RefreshLoopAsync(_stopCts.Token, firstSucceeded));
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    _stopCts?.Cancel();

    if (_refreshLoop is { } refreshLoop)
    {
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      cts.CancelAfter(TimeSpan.FromSeconds(5));

      try
      {
        await refreshLoop.WaitAsync(cts.Token);
      }
      catch (OperationCanceledException)
      {
        // The loop is tied to _stopCts and exits on its own; give up waiting.
      }
    }

    _stopCts?.Dispose();
    _stopCts = null;
  }

  /// <summary>
  /// Reloads the instruments list from all sources, overwrites the cache entry
  /// (resetting its TTL) and mirrors the load into the persistent Instruments table
  /// (search/filter SQL source). Per-source failures are absorbed by
  /// <see cref="InstrumentsLoader.LoadAllAsync"/> (partial result is still cached, and
  /// the table sync only prunes sources that answered or were not wired at all). When the
  /// token is cancelled mid-load the (partial) result is discarded, not cached.
  /// </summary>
  public async Task WarmupAsync(CancellationToken cancellationToken)
  {
    var stopwatch = Stopwatch.StartNew();

    var load = await InstrumentsLoader.LoadAllAsync(_instrumentSources, cancellationToken);
    cancellationToken.ThrowIfCancellationRequested();

    _cache.Add(InstrumentsCache.AllKey, load.Instruments, DateTimeOffset.UtcNow + InstrumentsCache.ListTtl);

    _logger.Information(
      "Instruments cache warmed: {Count} instruments in {ElapsedMilliseconds}ms",
      load.Instruments.Count,
      stopwatch.ElapsedMilliseconds);

    await _tableSync.SyncAsync(load with { UnwiredSources = _unwiredDataProviders.Codes }, cancellationToken);
  }

  private async Task RefreshLoopAsync(CancellationToken stoppingToken, bool firstSucceeded)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      var delay = firstSucceeded ? _options.RefreshInterval : _options.FailureRetryInterval;

      try
      {
        await Task.Delay(delay, stoppingToken);
      }
      catch (OperationCanceledException)
      {
        return;
      }

      firstSucceeded = await TryWarmupAsync(stoppingToken);
    }
  }

  /// <summary>
  /// Runs a warm-up attempt with a hard per-attempt timeout. The timeout is enforced with
  /// <c>Task.WhenAny</c> in addition to cancelling the token: the loader awaits
  /// provider cache tasks (LazyCache <c>GetOrAddAsync</c>) that may have been started by other
  /// callers with a different token, so a cancelled token alone cannot always break the wait.
  /// Returns false (and schedules a fast retry) on timeout or failure; rethrows only when
  /// the service is stopping.
  /// </summary>
  private async Task<bool> TryWarmupAsync(CancellationToken stoppingToken)
  {
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
    cts.CancelAfter(_options.InitialWarmupTimeout);

    try
    {
      var loadTask = WarmupAsync(cts.Token);
      var timeoutTask = Task.Delay(_options.InitialWarmupTimeout, cts.Token);
      var completed = await Task.WhenAny(loadTask, timeoutTask);

      if (completed != loadTask)
      {
        _logger.Warning(
          "Instruments cache refresh timed out after {Timeout}; will retry",
          _options.InitialWarmupTimeout);

        _ = loadTask.ContinueWith(
          task => _logger.Debug("Orphaned warm-up attempt finished (IsFaulted={Faulted})", task.IsFaulted),
          TaskContinuationOptions.ExecuteSynchronously);
        return false;
      }

      await loadTask;
      return true;
    }
    catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
    {
      _logger.Warning(
        "Instruments cache refresh timed out after {Timeout}; will retry",
        _options.InitialWarmupTimeout);
      return false;
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.Error(ex, "Instruments cache refresh failed; will retry");
      return false;
    }
  }
}
