using MediatR;
using StockIndicators.Application.Configuration.Commands;

namespace StockIndicators.Application.Rules.DeleteRule;

public class DeleteRuleCommand : ICommand<Unit>
{
  public Guid Id { get; }

  public Guid UserId { get; }

  public DeleteRuleCommand(Guid userId, Guid id)
  {
    UserId = userId;
    Id = id;
  }
}
