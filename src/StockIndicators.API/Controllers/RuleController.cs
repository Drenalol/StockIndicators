using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockIndicators.Application.Rules.CreateRule;
using StockIndicators.Application.Rules.DeleteRule;
using StockIndicators.Application.Rules.GetRule;
using StockIndicators.Application.Rules.GetRules;
using StockIndicators.Application.Rules.UpdateRule;
using StockIndicators.Contracts.Rules;
using StockIndicators.Infrastructure.Processing;

namespace StockIndicators.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class RuleController : ControllerBase
{
  private readonly IMediator _mediator;

  public RuleController(IMediator mediator) => _mediator = mediator;

  [HttpGet]
  public async Task<ActionResult<IEnumerable<RuleDto>>> GetAllAsync(CancellationToken cancellationToken) => await _mediator.Send(new GetRulesQuery(HttpContext.GetUserId()), cancellationToken);

  [HttpGet("{ruleId:guid}")]
  public async Task<ActionResult<RuleDto>> GetByIdAsync([Required] Guid ruleId, CancellationToken cancellationToken) => await _mediator.Send(new GetRuleQuery(ruleId, HttpContext.GetUserId()), cancellationToken);

  [HttpPost]
  public async Task<ActionResult<Guid>> CreateAsync([FromBody] CreateRuleRequest request, CancellationToken cancellationToken)
  {
    var userId = HttpContext.GetUserId();
    var ruleId = await _mediator.Send(new CreateRuleCommand(userId, request.Name, request.When, request.ThenTrue, request.ThenFalse), cancellationToken);
    return Created(string.Empty, ruleId);
  }

  [HttpPatch("{ruleId:guid}")]
  public async Task<ActionResult> UpdateAsync([Required] Guid ruleId, [FromBody] PatchRuleRequest request, CancellationToken cancellationToken)
  {
    var userId = HttpContext.GetUserId();
    await _mediator.Send(new UpdateRuleCommand(userId, ruleId, request.Name, request.When, request.ThenTrue, request.ThenFalse), cancellationToken);
    return NoContent();
  }

  [HttpDelete("{ruleId:guid}")]
  public async Task<ActionResult> DeleteAsync([Required] Guid ruleId, CancellationToken cancellationToken)
  {
    await _mediator.Send(new DeleteRuleCommand(HttpContext.GetUserId(), ruleId), cancellationToken);
    return NoContent();
  }
}
