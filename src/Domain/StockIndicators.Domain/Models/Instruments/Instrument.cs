namespace StockIndicators.Domain.Models.Instruments;

/// <summary>
/// Persisted instrument reference row: the provider instrument lists are upserted into this
/// table by the startup warm-up (InstrumentsWarmupService), which makes search/filter SQL
/// over names, types and exchanges possible (the in-memory <c>instruments:all</c> cache
/// cannot answer "find by company name with typos"). Normalized text columns
/// (<see cref="TickerNorm"/>, <see cref="NameNorm"/>, <see cref="NameTranslit"/>,
/// <see cref="Aliases"/>) feed the tsvector/trigram indexes created around this table;
/// they are written by the sync, never edited by hand.
/// </summary>
public class Instrument
{
  public string Ticker { get; set; } = null!;

  /// <summary>Empty string means "no exchange" (same convention as <c>RuleInstruments</c>).</summary>
  public string ExchangeName { get; set; } = string.Empty;

  public string? ClassCode { get; set; }

  public string? Currency { get; set; }

  public string? Name { get; set; }

  public string? Isin { get; set; }

  public string? Sector { get; set; }

  /// <summary>Instrument category: Stock, Bond, Etf, Option, Future, Crypto.</summary>
  public string? Type { get; set; }

  /// <summary>Provider that supplied the instrument: tinkoff, binance, alpaca, businessquant.</summary>
  public string Source { get; set; } = null!;

  /// <summary>Lowercase ticker without separators ("BTC/USDT" -> "btcusdt").</summary>
  public string TickerNorm { get; set; } = string.Empty;

  /// <summary>Lowercase name with punctuation turned into spaces.</summary>
  public string NameNorm { get; set; } = string.Empty;

  /// <summary>Romanized <see cref="NameNorm"/> (null when the name has no Cyrillic letters).</summary>
  public string? NameTranslit { get; set; }

  /// <summary>Lowercase human asset names for crypto (CoinGecko names of the base asset); empty otherwise.</summary>
  public string[] Aliases { get; set; } = [];

  /// <summary>Subset of <see cref="Aliases"/> for coins inside the top market-cap pages: "bitcoin" ranks a BTC pair above a symbol clone.</summary>
  public string[] AliasesTop { get; set; } = [];

  public DateTime UpdatedAt { get; set; }
}
