namespace StockIndicators.Application.Users;

/// <summary>
/// Requests that carry the id of the user they act for. The API layer fills it from the
/// authenticated principal. A self-hosted installation has exactly one user, but the id stays part
/// of the row identity - subscriptions, rules and telegram chats are keyed by it - instead of
/// turning into a magic constant spread through every query.
/// </summary>
public interface IHasUserId
{
  Guid UserId { get; }
}
