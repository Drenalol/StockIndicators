using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Users;
using StockIndicators.Contracts.Rules;

namespace StockIndicators.Application.Subscriptions.CreateSubscription;

public class CreateSubscriptionCommand : ICommand<Guid>, IHasUserId
{
  public Guid UserId { get; set; }

  /// <summary>
  /// Instrument identity from the route (the instrument the user subscribed on); it is the
  /// existence-check input, not a stored subscription field — instruments live on the rule.
  /// </summary>
  public string Ticker { get; }
  public string? ExchangeName { get; }
  public TimeSpan Cooldown { get; }
  public IReadOnlyCollection<SubscriptionOutputRequest>? Outputs { get; }

  /// <summary>
  /// Optional instrument identity resolved by the client (from the instrument search/list
  /// it already visited). When present the existence check skips the blocking provider
  /// iteration entirely; the server still validates it against the warm instruments cache.
  /// </summary>
  public InstrumentIdentity? Instrument { get; }

  /// <summary>
  /// The rule (WHEN-THEN with AND/OR) the subscription is a container of: it is created
  /// together with the subscription and is the single source of instruments/indicators.
  /// </summary>
  public CreateRuleRequest Rule { get; }

  /// <summary>
  /// When false the subscription also alerts on signals coming from extended-session
  /// candles (pre-market, after-hours, overnight). Null and true keep the default: extended
  /// candles never alert. A no-op on 24/7 crypto instruments, which have no extended windows.
  /// </summary>
  public bool IgnoreExtendedSessions { get; }

  public CreateSubscriptionCommand(
    Guid userId,
    string ticker,
    string? exchangeName,
    TimeSpan cooldown,
    CreateRuleRequest rule,
    IReadOnlyCollection<SubscriptionOutputRequest>? outputs = null,
    InstrumentIdentity? instrument = null,
    bool ignoreExtendedSessions = true
  )
  {
    UserId = userId;
    Ticker = ticker;
    ExchangeName = exchangeName;
    Cooldown = cooldown;
    Rule = rule;
    Outputs = outputs;
    Instrument = instrument;
    IgnoreExtendedSessions = ignoreExtendedSessions;
  }
}

/// <summary>
/// Client-resolved instrument identity sent with the create request so the server does not
/// have to iterate provider sources on the request path.
/// </summary>
/// <param name="Source">Provider that supplied the instrument (tinkoff, alpaca, binance, businessquant).</param>
/// <param name="ExchangeName">Exchange the instrument belongs to (e.g. BINANCE).</param>
public record InstrumentIdentity(string Source, string? ExchangeName);
