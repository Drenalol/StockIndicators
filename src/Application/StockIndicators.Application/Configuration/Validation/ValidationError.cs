namespace StockIndicators.Application.Configuration.Validation;

/// <summary>
/// A machine-readable validation error: a stable <see cref="Code"/> (e.g. "template.paramMin"),
/// the English <see cref="Message"/> (kept for logs and for API consumers that still read the
/// plain error string) and the <see cref="Args"/> the client needs to render a localized message.
/// The client maps the code to its own i18n table and substitutes the arguments.
/// </summary>
public sealed record ValidationError(string Code, string? Message, IReadOnlyDictionary<string, object?> Args)
{
  public ValidationError(string code, string? message)
    : this(code, message, new Dictionary<string, object?>())
  {
  }

  /// <summary>Returns a copy carrying the given argument dictionary (used to fold in context such as indicator/ticker).</summary>
  public ValidationError WithArgs(IReadOnlyDictionary<string, object?> args) => this with { Args = args };
}
