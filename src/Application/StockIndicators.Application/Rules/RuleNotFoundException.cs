using StockIndicators.Application.Configuration.Validation;

namespace StockIndicators.Application.Rules;

public class RuleNotFoundException : NotFoundException
{
  public RuleNotFoundException() : base("Rule not found")
  {
  }
}
