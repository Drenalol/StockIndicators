using StockIndicators.Application.Configuration.Commands;

namespace StockIndicators.Application.Auth.SignIn;

/// <summary>
/// Verifies the operator's password and hands back the account to open a session for.
/// Issuing the cookie itself is the API layer's job - this answers the password question only.
/// </summary>
public record SignInCommand(string Password) : ICommand<SignInResult>;

public sealed record SignInResult(Guid UserId, string? DisplayName);
