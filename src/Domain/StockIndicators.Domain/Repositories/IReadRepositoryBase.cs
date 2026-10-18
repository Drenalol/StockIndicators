using System.Linq.Expressions;

namespace StockIndicators.Domain.Repositories;

public interface IReadRepositoryBase<TEntity, in TId> where TEntity : Entity<TId> where TId : notnull
{
  Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken);
  IAsyncEnumerable<TEntity> GetAsync(Expression<Func<TEntity, bool>> predicate);
}