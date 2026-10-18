using MediatR;
using StockIndicators.Application.Configuration.Commands;

namespace StockIndicators.Application.Subscriptions.DeleteSubscriptionsByInstrument;

public class DeleteSubscriptionsByInstrumentCommand : ICommand<Unit>
{
  public Guid UserId { get; set; }
  public string Ticker { get; set; }
  public string? ExchangeName { get; set; }

  public DeleteSubscriptionsByInstrumentCommand(Guid userId, string ticker, string? exchangeName)
  {
    UserId = userId;
    Ticker = ticker;
    ExchangeName = exchangeName;
  }
}
