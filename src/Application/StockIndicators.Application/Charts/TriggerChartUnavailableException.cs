using StockIndicators.Application.Configuration.Validation;

namespace StockIndicators.Application.Charts;

/// <summary>
/// A trigger's chart has nothing to draw (404). The message is the stable code from
/// <see cref="TriggerChartResolution"/> - the frontend localizes it, so the backend stays
/// untranslated, and the code stays greppable in a log line.
/// </summary>
public class TriggerChartUnavailableException : NotFoundException
{
  public TriggerChartUnavailableException(string code) : base(code)
  {
  }
}
