namespace StockIndicators.Envelope;

public class ResultEnvelope : EnvelopeBase
{
  public object? Result { get; set; }

  public static ResultEnvelope Create(int? statusCode, object? result) =>
    new()
    {
      StatusCode = statusCode,
      Result = result
    };
}