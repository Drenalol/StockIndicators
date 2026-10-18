using Microsoft.EntityFrameworkCore;
using StockIndicators.Domain.Models.Users;
using StockIndicators.Infrastructure.Database;

namespace StockIndicators.Infrastructure.Domain.Users;

public class UserRepository : IUserRepository
{
  private readonly StockIndicatorsContext _stockIndicatorsContext;

  public UserRepository(StockIndicatorsContext stockIndicatorsContext) => _stockIndicatorsContext = stockIndicatorsContext;

  public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    => await _stockIndicatorsContext.Users.FindAsync([id], cancellationToken: cancellationToken);

  public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    => await _stockIndicatorsContext.Users.FirstOrDefaultAsync(user => EF.Functions.ILike(user.Email!, email), cancellationToken);

  public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    => await _stockIndicatorsContext.Users.AnyAsync(user => user.Id == id, cancellationToken);

  public async Task<User?> GetAnyAsync(CancellationToken cancellationToken = default)
    => await _stockIndicatorsContext.Users.OrderBy(user => user.Created).FirstOrDefaultAsync(cancellationToken);

  public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
  {
    var entry = await _stockIndicatorsContext.Users.AddAsync(user, cancellationToken);
    return entry.Entity;
  }

  public Task<int> CommitAsync(CancellationToken cancellationToken = default) => _stockIndicatorsContext.SaveChangesAsync(cancellationToken);
}
