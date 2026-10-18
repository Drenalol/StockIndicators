using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Domain.Models.Rules;

namespace StockIndicators.Application.Rules.CreateRule;

public class CreateRuleCommandHandler : ICommandHandler<CreateRuleCommand, Guid>
{
  private readonly IRuleRepository _ruleRepository;
  private readonly ISerializer _serializer;

  public CreateRuleCommandHandler(IRuleRepository ruleRepository, ISerializer serializer)
  {
    _ruleRepository = ruleRepository;
    _serializer = serializer;
  }

  public async Task<Guid> Handle(CreateRuleCommand request, CancellationToken cancellationToken)
  {
    RuleValidation.Validate(request.When);
    RuleValidation.ValidateName(request.Name);
    RuleValidation.ValidateActions(request.ThenTrue, request.ThenFalse);
    RuleValidation.ValidateConditions(_serializer, request.When);
    RuleValidation.NormalizeConditionTimeFrames(_serializer, request.When);

    var id = Guid.CreateVersion7();
    var rule = RuleMappers.ToDomain(id, request.Name, request.When, request.ThenTrue, request.ThenFalse);
    rule.UserId = request.UserId;
    rule.RuleData = _serializer.Serialize(RuleMappers.ToPayload(rule));
    rule.Created = DateTime.UtcNow;

    await _ruleRepository.AddAsync(rule, cancellationToken);
    await _ruleRepository.CommitAsync(cancellationToken);

    return rule.Id;
  }
}
