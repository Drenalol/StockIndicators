namespace StockIndicators.Domain.Repositories;

public interface IRepositoryBase<TEntity, in TId> :
  ICreateRepositoryBase<TEntity>,
  IReadRepositoryBase<TEntity, TId>,
  IUpdateRepositoryBase<TEntity>,
  IDeleteRepositoryBase<TEntity, TId> where TEntity : Entity<TId> where TId : notnull;