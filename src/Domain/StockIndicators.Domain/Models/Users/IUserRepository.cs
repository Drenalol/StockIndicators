namespace StockIndicators.Domain.Models.Users;

public interface IUserRepository
{
  Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

  /// <summary>
  /// Case-insensitive lookup by email.
  /// </summary>
  Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

  /// <summary>
  /// Existence-only probe (session authentication): no entity materialization.
  /// </summary>
  Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

  /// <summary>
  /// The account of a single-user installation, whichever it is: a self-hosted instance has
  /// exactly one row here, so "the" user does not need an id to be found.
  /// </summary>
  Task<User?> GetAnyAsync(CancellationToken cancellationToken = default);

  Task<User> AddAsync(User user, CancellationToken cancellationToken = default);

  Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
