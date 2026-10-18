namespace StockIndicators.Domain.Models.Instruments;

/// <summary>
/// Independent contributor of a watched instrument. A key is polled by the providers while
/// at least one source claims it: an open chart page must never tear down the stream of an
/// active subscription, and deactivating a subscription must keep the chart of a viewer.
/// </summary>
public enum InstrumentWatchSource
{
  /// <summary>An active subscription's rule watches the instrument (the classic source).</summary>
  Subscription,

  /// <summary>An open chart page asked the chart hub for live updates of the instrument.</summary>
  ChartView,
}
