namespace StockIndicators.Domain.Repositories;

public interface IDeleteRepositoryBase<TEntity, in TId> where TId : notnull
{
  Task DeleteAsync(TId id, CancellationToken cancellationToken);
}