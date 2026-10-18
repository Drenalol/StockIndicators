using StockIndicators.Application.Configuration.Validation;

namespace StockIndicators.Application.Account;

public class TelegramChatNotFoundException : NotFoundException
{
  public TelegramChatNotFoundException() : base("Telegram chat not found for this account")
  {
  }
}
