using System.Diagnostics.Metrics;

namespace StockIndicators.Application.Configuration.Metrics;

/// <summary>
/// The single meter carrying every business metric of the pipeline (candles, rule engine,
/// alerts, billing) under the <c>si_</c> instrument prefix. The OpenTelemetry SDK is wired
/// in the API composition root: it collects this meter together with the runtime/ASP.NET/
/// EF/Npgsql meters and serves them as Prometheus text on the loopback-only /metrics
/// endpoint. Instruments are static and long-lived by design (the metrics API says creating
/// them per call is the anti-pattern); the metric classes never throw by themselves.
/// </summary>
public static class StockIndicatorsMetrics
{
  public const string MeterName = "stock-indicators";

  public static readonly Meter Meter = new(MeterName, "1.0.0");

  /// <summary>Lower-cased enum name: the stable wire spelling of every label value.</summary>
  internal static string Label<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();

  internal static KeyValuePair<string, object?> Tag(string key, object? value) => new(key, value);
}
