using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Contracts.Rules;

namespace StockIndicators.Application.Rules.GetRules;

public class GetRulesQuery : IQuery<List<RuleDto>>
{
  public Guid UserId { get; }

  public GetRulesQuery(Guid userId) => UserId = userId;
}
