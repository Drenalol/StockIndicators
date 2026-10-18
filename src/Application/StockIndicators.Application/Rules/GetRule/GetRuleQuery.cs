using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Contracts.Rules;

namespace StockIndicators.Application.Rules.GetRule;

public class GetRuleQuery : IQuery<RuleDto>
{
  public Guid Id { get; }

  public Guid UserId { get; }

  public GetRuleQuery(Guid id, Guid userId)
  {
    Id = id;
    UserId = userId;
  }
}
