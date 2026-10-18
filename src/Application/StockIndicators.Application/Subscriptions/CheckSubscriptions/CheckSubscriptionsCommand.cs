using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Domain.Models.Instruments;

namespace StockIndicators.Application.Subscriptions.CheckSubscriptions;

public class CheckSubscriptionsCommand : IBackgroundCommand
{
  public IReadOnlySet<InstrumentKey> Instruments { get; }

  public CheckSubscriptionsCommand(IEnumerable<InstrumentKey> instruments) => Instruments = instruments.ToHashSet();
}
