namespace StockIndicators.Application.Configuration.Validation;

public class ValidationException : Exception
{
  public object? Details { get; }

  /// <summary>
  /// Optional machine-readable error codes for clients that localize the message themselves.
  /// The human-readable <see cref="Exception.Message"/> stays unchanged for existing API consumers;
  /// when present these codes are surfaced alongside it in the response envelope.
  /// </summary>
  public IReadOnlyList<ValidationError>? ErrorCodes { get; }

  public ValidationException(string message, object? details, IReadOnlyList<ValidationError>? errorCodes = null) : base(message)
  {
    Details = details;
    ErrorCodes = errorCodes;
  }
}
