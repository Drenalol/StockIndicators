using Newtonsoft.Json;
using StockIndicators.Application.Configuration.Validation;

namespace StockIndicators.Envelope;

public class ErrorEnvelope : EnvelopeBase
{
  public object? Error { get; set; }

  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public string? Exception { get; set; }

  /// <summary>
  /// Machine-readable validation errors (stable codes + arguments) for clients that render the
  /// message in the user's language. Omitted when null, so the plain <see cref="Error"/> string —
  /// and every existing API consumer — is unaffected.
  /// </summary>
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public IReadOnlyList<ValidationError>? ErrorDetails { get; set; }

  public static ErrorEnvelope Create(int? errorCode, object? error, string? exception = null) =>
    new()
    {
      StatusCode = errorCode,
      Error = error,
      Exception = exception
    };
}
