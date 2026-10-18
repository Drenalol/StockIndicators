namespace StockIndicators.Domain.Models.Users;

/// <summary>
/// External identity providers a user can authenticate with.
/// </summary>
public enum AuthProvider
{
  Email = 0,
  Google = 1,
  Telegram = 2,
  Vk = 3,
  Yandex = 4
}
