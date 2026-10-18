namespace StockIndicators.Application.Instruments.CryptoNames;

/// <summary>
/// CoinGecko display names of one crypto base asset (lowercase): <see cref="All"/> covers
/// every coin sharing the symbol (clones included), <see cref="Top"/> only the names of
/// coins inside the top market-cap pages. The split ranks a search for "bitcoin": the
/// canonical BTC pairs (top alias) come before instruments whose symbol merely collides
/// with the clone names.
/// </summary>
public sealed record AssetNames(IReadOnlyList<string> All, IReadOnlyList<string> Top);

/// <summary>
/// Human asset names of crypto bases (symbol -> CoinGecko coin names): providers name
/// crypto pairs after tickers only ("BTC/USDT"), so a search for "bitcoin" needs an
/// external name catalog keyed by the base asset symbol.
/// </summary>
public interface ICryptoAssetNames
{
  /// <summary>
  /// Lowercase base-asset symbol to its display names ("btc" -> all: ["bitcoin", "buy the cat"...],
  /// top: ["bitcoin"]). The mapping is cached for days; a provider failure yields an empty
  /// map (never throws).
  /// </summary>
  Task<IReadOnlyDictionary<string, AssetNames>> GetNamesBySymbolAsync(CancellationToken cancellationToken);
}
