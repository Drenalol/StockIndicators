using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using StockIndicators.Application.Account.CreateTelegramLinkToken;
using StockIndicators.Application.Account.DeleteTelegramChat;
using StockIndicators.Application.Account.GetTelegramChats;
using StockIndicators.Application.Account.UpdateUserPreferences;
using StockIndicators.Contracts.Auth;
using StockIndicators.Infrastructure.Processing;

namespace StockIndicators.Controllers;

/// <summary>
/// The operator's own settings: preferences and the telegram chat alerts go to. Account
/// lifecycle (email ownership, deletion schedule) is not here - one password protects one
/// account on one machine, and deleting the installation is a matter of stopping the container.
/// </summary>
[Route("api/account")]
[ApiController]
[Authorize]
public class AccountController : ControllerBase
{
  private readonly IMediator _mediator;

  public AccountController(IMediator mediator) => _mediator = mediator;

  /// <summary>
  /// Issue a Telegram chat-link deep link: <c>t.me/&lt;bot&gt;?start=link_&lt;one-time-token&gt;</c>.
  /// Opening it and tapping Start binds the private chat to this installation's account
  /// (groups and channels are rejected). The token is single-use and short-lived.
  /// </summary>
  [HttpPost("telegram-chats/link-token")]
  public async Task<ActionResult<TelegramLinkDto>> CreateTelegramLinkToken(CancellationToken cancellationToken)
    => Ok(await _mediator.Send(new CreateTelegramLinkTokenCommand(HttpContext.GetUserId()), cancellationToken));

  /// <summary>
  /// Preferred language ("ru"/"en"), IANA time zone, subscriptions-list sort and interactive-chart
  /// preferences. Partial update: only the provided fields are changed (chartPreferences merges
  /// flag-by-flag into the stored document). <c>completeOnboarding: true</c> marks the first-login
  /// tour as done (idempotent); <c>completeSubscriptionTour: true</c> does the same for the
  /// create-subscription page tour.
  /// </summary>
  [HttpPatch("preferences")]
  public async Task<ActionResult<AuthProfileDto>> UpdatePreferences(
    [FromBody] UpdatePreferencesRequest request,
    CancellationToken cancellationToken
  )
    => Ok(
      await _mediator.Send(
        new UpdateUserPreferencesCommand(HttpContext.GetUserId(), request.Language, request.Timezone, request.CompleteOnboarding, request.SubscriptionsSort, request.ChartPreferences, request.CompleteSubscriptionTour),
        cancellationToken));

  /// <summary>
  /// The linked Telegram chat (an account has at most one).
  /// </summary>
  [HttpGet("telegram-chats")]
  public async Task<ActionResult<IReadOnlyList<TelegramChatDto>>> GetTelegramChats(CancellationToken cancellationToken)
    => Ok(await _mediator.Send(new GetTelegramChatsQuery(HttpContext.GetUserId()), cancellationToken));

  /// <summary>
  /// Unlinks the chat.
  /// </summary>
  [HttpDelete("telegram-chats/{chatId:guid}")]
  public async Task<ActionResult> DeleteTelegramChat(Guid chatId, CancellationToken cancellationToken)
  {
    await _mediator.Send(new DeleteTelegramChatCommand(HttpContext.GetUserId(), chatId), cancellationToken);
    return NoContent();
  }
}

public class UpdatePreferencesRequest
{
  public string? Language { get; set; }

  public string? Timezone { get; set; }

  public bool? CompleteOnboarding { get; set; }

  public string? SubscriptionsSort { get; set; }

  public ChartPreferencesDto? ChartPreferences { get; set; }

  public bool? CompleteSubscriptionTour { get; set; }
}
