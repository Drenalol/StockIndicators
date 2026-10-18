using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Application.Instruments.GetInstruments;
using StockIndicators.Application.Subscriptions.GetSubscription;

namespace StockIndicators.Application.Subscriptions.GetSubscriptions;

/// <summary>
/// Page of subscriptions (newest first) for paginated browsers like the Telegram bot.
/// The full list query loads every rule tree of the account, which is prohibitive for
/// large accounts; this one filters by ticker (via the RuleInstruments projection)
/// and loads rules only for the returned page.
/// </summary>
public class GetSubscriptionsPageQuery : IQuery<PaginatedResult<SubscriptionDto>>
{
  public Guid UserId { get; }

  public string? Search { get; }

  public int Page { get; }

  public int PageSize { get; }

  public GetSubscriptionsPageQuery(Guid userId, string? search = null, int page = 1, int pageSize = 20)
  {
    UserId = userId;
    Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
    Page = page < 1 ? 1 : page;
    PageSize = pageSize < 1 ? 20 : (pageSize > 100 ? 100 : pageSize);
  }
}
