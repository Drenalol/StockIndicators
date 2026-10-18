using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Serilog;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Users;

namespace StockIndicators.Application.Auth.SignIn;

public class SignInCommandHandler : ICommandHandler<SignInCommand, SignInResult>
{
  private readonly IOptions<LocalAuthOptions> _options;
  private readonly LocalUserProvisioner _users;
  private readonly ILogger _logger;

  public SignInCommandHandler(IOptions<LocalAuthOptions> options, LocalUserProvisioner users, ILogger logger)
  {
    _options = options;
    _users = users;
    _logger = logger.ForContext<SignInCommandHandler>();
  }

  public async Task<SignInResult> Handle(SignInCommand request, CancellationToken cancellationToken)
  {
    var options = _options.Value;

    // An instance nobody configured yet answers every attempt the same way; the operator
    // learns what to do from the setup instructions, an anonymous caller learns nothing.
    if (!options.IsConfigured)
    {
      _logger.Warning("Sign-in refused: this instance has no LocalAuth password configured");
      throw new InvalidCredentialsException();
    }

    if (!Matches(request.Password, options))
    {
      _logger.Information("Sign-in refused: wrong password");
      throw new InvalidCredentialsException();
    }

    var user = await _users.EnsureAsync(cancellationToken);

    _logger.Information("Signed in as {UserId}", user.Id);

    return new SignInResult(user.Id, user.DisplayName);
  }

  private static bool Matches(string password, LocalAuthOptions options)
  {
    if (string.IsNullOrEmpty(password))
      return false;

    if (!string.IsNullOrWhiteSpace(options.PasswordHash))
      return LocalPasswordHash.Verify(password, options.PasswordHash);

    return CryptographicOperations.FixedTimeEquals(
      Encoding.UTF8.GetBytes(password),
      Encoding.UTF8.GetBytes(options.Password));
  }
}
