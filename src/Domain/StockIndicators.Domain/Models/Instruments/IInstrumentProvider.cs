using System.Reactive.Subjects;

namespace StockIndicators.Domain.Models.Instruments;

/// <summary>
/// Registry of instruments the candle providers must poll/stream. Membership is contributed
/// through <see cref="AddWatch"/>/<see cref="RemoveWatch"/> per <see cref="InstrumentWatchSource"/>;
/// <see cref="OnAdded"/>/<see cref="OnRemoved"/> are outgoing notifications for the providers
/// and must not be used to change membership directly.
/// </summary>
public interface IInstrumentProvider
{
  IReadOnlySet<string> Instruments { get; }
  /// <summary>Watched instruments with exchange binding (ticker + exchange).</summary>
  IReadOnlyCollection<InstrumentKey> Keys { get; }
  Subject<InstrumentKey> OnAdded { get; }
  Subject<InstrumentKey> OnRemoved { get; }
  /// <summary>Claims the instrument for a source; fires <see cref="OnAdded"/> on the first claim.</summary>
  void AddWatch(InstrumentKey key, InstrumentWatchSource source);
  /// <summary>Releases the instrument of a source; fires <see cref="OnRemoved"/> when the last source lets go.</summary>
  void RemoveWatch(InstrumentKey key, InstrumentWatchSource source);
}
