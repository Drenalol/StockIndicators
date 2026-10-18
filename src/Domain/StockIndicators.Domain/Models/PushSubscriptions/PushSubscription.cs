using StockIndicators.Domain.Models.Users;

namespace StockIndicators.Domain.Models.PushSubscriptions;

/// <summary>
/// Push delivery target. Web = Web Push (VAPID) endpoint, Fcm/Apns = future mobile push device tokens.
/// </summary>
public class PushSubscription : UpdatableEntity<Guid>
{
  public Guid UserId { get; set; }
  public PushPlatform Platform { get; set; }
  public string Endpoint { get; set; } = null!;
  public string? PublicKey { get; set; }
  public string? AuthKey { get; set; }
  public string? DeviceLabel { get; set; }

  public User? User { get; set; }
}
