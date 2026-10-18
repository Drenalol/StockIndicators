using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockIndicators.Application.Auth;
using StockIndicators.Application.Auth.GetMe;
using StockIndicators.Application.Auth.SignIn;
using StockIndicators.Contracts.Auth;
using StockIndicators.Infrastructure.Processing;

namespace StockIndicators.Controllers;

/// <summary>
/// Sign-in for a self-hosted instance: one account, one password, one cookie. The hosted
/// version's login surface (email codes, Google/VK/Yandex/Telegram OIDC, passkeys, captcha,
/// session list, refresh rotation) lived at this route and is gone - see the README for how
/// the password is configured.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
  private readonly IMediator _mediator;
  private readonly IOptions<LocalAuthOptions> _authOptions;

  public AuthController(IMediator mediator, IOptions<LocalAuthOptions> authOptions)
  {
    _mediator = mediator;
    _authOptions = authOptions;
  }

  /// <summary>
  /// What the login screen can say before anyone authenticates: whether this instance has a
  /// password at all. Without one every sign-in is refused, so the setup step has to be visible.
  /// </summary>
  [HttpGet("status")]
  [AllowAnonymous]
  public ActionResult<AuthStatusDto> Status() => Ok(new AuthStatusDto(_authOptions.Value.IsConfigured));

  [HttpPost("login")]
  [AllowAnonymous]
  public async Task<ActionResult<AuthProfileDto>> Login(
    [FromBody] LoginRequest request,
    CancellationToken cancellationToken)
  {
    var result = await _mediator.Send(new SignInCommand(request.Password), cancellationToken);

    var identity = new ClaimsIdentity(
      [
        new Claim(ClaimTypes.NameIdentifier, result.UserId.ToString()),
        new Claim(ClaimTypes.Name, result.DisplayName ?? "Local"),
      ],
      CookieAuthenticationDefaults.AuthenticationScheme);

    await HttpContext.SignInAsync(
      CookieAuthenticationDefaults.AuthenticationScheme,
      new ClaimsPrincipal(identity),
      new AuthenticationProperties
      {
        IsPersistent = true,
        ExpiresUtc = DateTimeOffset.UtcNow.Add(_authOptions.Value.SessionLifetime),
      });

    // Not Me(): SignInAsync sets the cookie on the response but leaves this request's
    // principal anonymous, so the user id has to come from the sign-in result
    return Ok(await _mediator.Send(new GetMeQuery(result.UserId), cancellationToken));
  }

  [HttpPost("logout")]
  [Authorize]
  public async Task<IActionResult> Logout()
  {
    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    return NoContent();
  }

  [HttpGet("me")]
  [Authorize]
  public async Task<ActionResult<AuthProfileDto>> Me(CancellationToken cancellationToken) =>
    Ok(await _mediator.Send(new GetMeQuery(HttpContext.GetUserId()), cancellationToken));
}

public record LoginRequest(string Password);

/// <summary>
/// Pre-authentication instance status. <c>PasswordConfigured</c> is false until the operator
/// sets <c>LocalAuth:Password</c> (or its hash), and until then nothing can be signed into.
/// </summary>
public record AuthStatusDto(bool PasswordConfigured);
