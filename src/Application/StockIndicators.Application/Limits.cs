namespace StockIndicators.Application;

/// <summary>
/// Product limits of a single-user installation. These are not paid tiers - there is nothing to
/// buy - they keep a rule tree and a subscription list inside what the UI, the database schema and
/// one machine's provider rate limits can carry. The values are the generous ones the hosted
/// version gave to paying users.
/// </summary>
public static class Limits
{
  /// <summary>Shortest cooldown between two alerts of one subscription.</summary>
  public static readonly TimeSpan MinCooldown = TimeSpan.FromMinutes(1);

  /// <summary>Cooldown a new subscription starts with.</summary>
  public const int DefaultCooldownSeconds = 3600;

  /// <summary>Alert output channels per subscription: one per supported channel.</summary>
  public const int MaxOutputChannels = 5;

  public static int MinCooldownSeconds => (int)MinCooldown.TotalSeconds;

  public static int MaxConditionsPerRule => Rules.RuleValidation.MaxTotalConditions;

  public static int MaxGroupDepth => Rules.RuleValidation.MaxGroupDepth;
}
