namespace StockIndicators.Domain;

/// <summary>
/// Base class for entities.
/// </summary>
public abstract class Entity
{
  private List<IDomainEvent>? _domainEvents;

  /// <summary>
  /// Domain events occurred.
  /// </summary>
  public IReadOnlyCollection<IDomainEvent>? DomainEvents => _domainEvents;

  /// <summary>
  /// Add domain event.
  /// </summary>
  /// <param name="domainEvent"></param>
  protected void AddDomainEvent(IDomainEvent domainEvent)
  {
    _domainEvents ??= [];
    _domainEvents.Add(domainEvent);
  }

  /// <summary>
  /// Clear domain events.
  /// </summary>
  public void ClearDomainEvents()
  {
    _domainEvents?.Clear();
  }

  /*protected static void CheckRule(IBusinessRule rule)
  {
      if (rule.IsBroken())
      {
          throw new BusinessRuleValidationException(rule);
      }
  }*/
}

public abstract class Entity<TId> : Entity where TId : notnull
{
  public TId Id { get; init; } = default!;

  private bool Equals(Entity<TId> other) => Id.Equals(other.Id);

  public override bool Equals(object? obj)
  {
    if (ReferenceEquals(null, obj))
      return false;
    
    if (ReferenceEquals(this, obj))
      return true;
    
    if (obj.GetType() != GetType())
      return false;
    
    return Equals((Entity<TId>)obj);
  }

  public override int GetHashCode() => Id.GetHashCode();
}

public abstract class UpdatableEntity<TId> : Entity<TId> where TId : notnull
{
  public DateTime Created { get; set; }
  public DateTime? Updated { get; set; }
}