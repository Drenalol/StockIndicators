namespace StockIndicators.Domain.Models.Providers;

/// <summary>
/// What the operator configured for one provider on the settings page: the enable
/// toggle and the credentials, keyed by the provider code ("tinkoff", "telegram", ...).
/// Values are plaintext - the database belongs to the same operator as the keys, and
/// encrypting them with a key stored next to the database would be theater.
/// </summary>
public class ProviderSetting : UpdatableEntity<string>
{
  public bool IsEnabled { get; set; }

  /// <summary>JSON object of field name to value (field names match the options properties).</summary>
  public string Credentials { get; set; } = "{}";
}
