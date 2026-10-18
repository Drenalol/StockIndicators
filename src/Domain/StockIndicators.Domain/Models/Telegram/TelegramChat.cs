using StockIndicators.Domain.Models.Users;

namespace StockIndicators.Domain.Models.Telegram;

/// <summary>
/// The Telegram chat linked to a user. An account has at most one chat: it is both the
/// bot management menu and the alert destination. Linking a new chat unlinks the previous one.
/// </summary>
public class TelegramChat : UpdatableEntity<Guid>
{
  public Guid UserId { get; set; }

  public long ChatId { get; set; }

  public string? Username { get; set; }

  public User? User { get; set; }
}
