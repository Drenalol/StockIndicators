namespace StockIndicators.Application.Instruments.Logos;

/// <summary>
/// Instrument logo sources. Every key is optional: an unconfigured source is skipped
/// (empty key - no HTTP calls), so the cascade degrades to whatever is set up.
/// The free Logo.dev plan serves CDN images from a publishable key only (the secret
/// key and the JSON APIs are not used: logos are downloaded once and cached in Redis).
/// </summary>
public class LogoOptions
{
  /// <summary>Logo.dev publishable key ("pk_..."); empty disables the source.</summary>
  public string LogoDevPublishableKey { get; set; } = "";

  public string LogoDevBaseUrl { get; set; } = "https://img.logo.dev";

  /// <summary>Issuer brand CDN behind the Invest API brand data (file names from Brand.LogoName).</summary>
  public string TinkoffBrandBaseUrl { get; set; } = "https://invest-brands.cdn-tinkoff.ru";

  public string CoinGeckoBaseUrl { get; set; } = "https://api.coingecko.com/api/v3";

  /// <summary>Optional CoinGecko demo key (raises the public rate limit); empty works too.</summary>
  public string CoinGeckoApiKey { get; set; } = "";

  /// <summary>Cached logo age after which a background re-resolution from the sources is kicked off.</summary>
  public int RevalidateAfterDays { get; set; } = 30;
}
