using StockIndicators.Domain.Models.Users;

namespace StockIndicators.Application.Users;

/// <summary>
/// Makes sure the instance has its one account. The hosted version created an account as a
/// side effect of an external sign-in; here the account exists before anyone logs in, so the
/// first thing an operator does (linking a telegram chat, opening a subscription) already has
/// an owner to point at.
/// </summary>
public class LocalUserProvisioner
{
  /// <summary>Marker email of the local account - nobody ever receives mail at it.</summary>
  public const string LocalEmail = "local@localhost";

  private readonly IUserRepository _userRepository;

  public LocalUserProvisioner(IUserRepository userRepository) => _userRepository = userRepository;

  public async Task<User> EnsureAsync(CancellationToken cancellationToken = default)
  {
    var existing = await _userRepository.GetAnyAsync(cancellationToken);

    if (existing is not null)
      return existing;

    var user = new User
    {
      Id = Guid.CreateVersion7(),
      Email = LocalEmail,
      DisplayName = "Local",
      Created = DateTime.UtcNow,
      Updated = DateTime.UtcNow,
    };

    await _userRepository.AddAsync(user, cancellationToken);
    await _userRepository.CommitAsync(cancellationToken);

    return user;
  }
}
