namespace StockIndicators.Domain.Repositories;

public interface ICreateRepositoryBase<in TEntity> where TEntity : Entity
{
  Task AddAsync(TEntity entity, CancellationToken cancellationToken);
  Task<int> CommitAsync(CancellationToken cancellationToken = default);
}