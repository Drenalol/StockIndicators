using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Contracts.Rules;
using StockIndicators.Domain.Models.Rules;

namespace StockIndicators.Application.Rules.GetRules;

public class GetRulesQueryHandler : IQueryHandler<GetRulesQuery, List<RuleDto>>
{
  private readonly IRuleRepository _ruleRepository;

  public GetRulesQueryHandler(IRuleRepository ruleRepository) => _ruleRepository = ruleRepository;

  public async Task<List<RuleDto>> Handle(GetRulesQuery request, CancellationToken cancellationToken)
  {
    var rules = new List<Rule>();

    await foreach (var rule in _ruleRepository.GetAsync(stored => stored.UserId == request.UserId))
      rules.Add(rule);

    return rules.OrderBy(rule => rule.Created).Select(RuleMappers.ToDto).ToList();
  }
}
