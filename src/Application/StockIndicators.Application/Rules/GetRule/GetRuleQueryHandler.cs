using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Contracts.Rules;
using StockIndicators.Domain.Models.Rules;

namespace StockIndicators.Application.Rules.GetRule;

public class GetRuleQueryHandler : IQueryHandler<GetRuleQuery, RuleDto>
{
  private readonly IRuleRepository _ruleRepository;

  public GetRuleQueryHandler(IRuleRepository ruleRepository) => _ruleRepository = ruleRepository;

  public async Task<RuleDto> Handle(GetRuleQuery request, CancellationToken cancellationToken)
  {
    var rule = await _ruleRepository.GetByIdForUserAsync(request.Id, request.UserId, cancellationToken);

    if (rule == null)
      throw new RuleNotFoundException();

    return RuleMappers.ToDto(rule);
  }
}
