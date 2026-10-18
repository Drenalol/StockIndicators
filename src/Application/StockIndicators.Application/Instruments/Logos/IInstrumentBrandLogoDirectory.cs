namespace StockIndicators.Application.Instruments.Logos;

/// <summary>
/// Fast read-only access to the issuer brand logo file names carried by a provider's
/// instrument catalog (currently only Tinkoff: Brand.LogoName on its shares/ETFs).
/// Implemented by the provider client over its already-loaded instrument cache; a cold
/// catalog answers null without hitting the provider API.
/// </summary>
public interface IInstrumentBrandLogoDirectory
{
  Task<string?> TryGetBrandLogoNameAsync(string ticker, CancellationToken cancellationToken);
}
