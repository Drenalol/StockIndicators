using StockIndicators.Application.Instruments.GetInstruments;

namespace StockIndicators.Application.Instruments.Logos;

/// <summary>
/// A logo resolution request: the instrument identity plus whatever the warm instruments
/// catalog knows about it (null catalog when the cache is cold - sources then fall back to
/// ticker-shape heuristics).
/// </summary>
public sealed record InstrumentLogoRequest(string Ticker, string? ExchangeName, InstrumentDto? Catalog)
{
  public bool IsCrypto => IsCryptoInstrument(Catalog, Ticker, ExchangeName);

  /// <summary>Base asset symbol of a crypto pair ("TONUSDT" + quote "USDT" - "TON"); the ticker itself for non-crypto.</summary>
  public string BaseAsset => GetBaseAsset(Catalog, Ticker);

  private static readonly string[] KnownQuoteAssets =
  [
    "USDT", "USDC", "BUSD", "FDUSD", "TUSD", "DAI",
    "USD", "RUB", "EUR", "GBP", "TRY", "BRL", "UAH", "JPY", "CHF", "PLN", "ARS", "COP", "MXN", "KZT",
    "BTC", "ETH", "BNB", "XRP", "SOL"
  ];

  public static bool IsCryptoInstrument(InstrumentDto? catalog, string ticker, string? exchangeName) =>
    string.Equals(catalog?.Type, "Crypto", StringComparison.OrdinalIgnoreCase)
    || string.Equals(exchangeName, "BINANCE", StringComparison.OrdinalIgnoreCase)
    || string.Equals(exchangeName, "CRYPTO", StringComparison.OrdinalIgnoreCase)
    || ticker.Contains('/');

  /// <summary>
  /// Strips the quote asset: slash-separated symbols take the first part ("BTC/USD" - "BTC");
  /// concatenated Binance pairs strip the catalog currency or a known quote suffix ("TONUSDT" - "TON").
  /// Returns the ticker unchanged when nothing matches (also correct for non-crypto tickers).
  /// </summary>
  public static string GetBaseAsset(InstrumentDto? catalog, string ticker)
  {
    var slash = ticker.IndexOf('/');
    if (slash > 0)
      return ticker[..slash];

    var quote = catalog?.Currency;
    if (!string.IsNullOrEmpty(quote) && ticker.Length > quote.Length && ticker.EndsWith(quote, StringComparison.OrdinalIgnoreCase))
      return ticker[..^quote.Length];

    var known = KnownQuoteAssets.FirstOrDefault(q => ticker.Length > q.Length && ticker.EndsWith(q, StringComparison.OrdinalIgnoreCase));
    return known is null ? ticker : ticker[..^known.Length];
  }
}
