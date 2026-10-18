using System.ComponentModel.DataAnnotations;
using StockIndicators.Application.Instruments;

namespace StockIndicators.Infrastructure.Domain.Instruments;

/// <summary>
/// Bound from the `InstrumentsWarmupOptions` appsettings section.
/// Controls the instruments list cache warm-up (see <see cref="InstrumentsWarmupService"/>).
/// </summary>
public class InstrumentsWarmupOptions
{
  /// <summary>
  /// Interval between background cache refreshes. Keep it shorter than
  /// <see cref="InstrumentsCache.ListTtl"/> so the entry is rewritten before it expires
  /// (default: 23h50m against the 1-day TTL — a 10-minute safety margin).
  /// </summary>
  [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
  public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(50));

  /// <summary>
  /// Upper bound for the blocking initial warm-up at startup. When exceeded the host keeps
  /// starting and the warm-up is retried in the background.
  /// </summary>
  [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
  public TimeSpan InitialWarmupTimeout { get; set; } = TimeSpan.FromMinutes(1);

  /// <summary>
  /// Retry delay after a failed warm-up attempt, used until the first success.
  /// </summary>
  [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
  public TimeSpan FailureRetryInterval { get; set; } = TimeSpan.FromMinutes(5);
}
