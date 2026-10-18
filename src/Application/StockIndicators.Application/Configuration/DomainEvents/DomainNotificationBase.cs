using System.Text.Json.Serialization;
using StockIndicators.Domain;

namespace StockIndicators.Application.Configuration.DomainEvents;

public class DomainNotificationBase<T> : IDomainEventNotification<T> where T : IDomainEvent
{
  [JsonIgnore]
  public T? DomainEvent { get; }

  public Guid Id { get; }

  public DomainNotificationBase(T? domainEvent)
  {
    Id = Guid.CreateVersion7();
    DomainEvent = domainEvent;
  }
}