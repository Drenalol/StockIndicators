using MediatR;

namespace StockIndicators.Domain;

public interface IDomainEvent : INotification
{
  DateTime OccurredOn { get; }
}