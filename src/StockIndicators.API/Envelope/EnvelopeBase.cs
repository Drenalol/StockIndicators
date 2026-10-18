using Newtonsoft.Json;

namespace StockIndicators.Envelope;

public abstract class EnvelopeBase
{
  [JsonProperty(Order = -2)]
  public int? StatusCode { get; set; }
}