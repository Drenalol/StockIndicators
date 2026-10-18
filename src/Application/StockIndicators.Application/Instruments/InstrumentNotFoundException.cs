using StockIndicators.Application.Configuration.Validation;

namespace StockIndicators.Application.Instruments;

public class InstrumentNotFoundException : NotFoundException
{
  public InstrumentNotFoundException(string figi) : base($"No instrument found for '{figi}'")
  {
  }
}