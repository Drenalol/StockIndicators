namespace StockIndicators.Application.Configuration.Validation;

public abstract class NotFoundException : Exception
{
  protected NotFoundException(string message) : base(message)
  {
  }
}