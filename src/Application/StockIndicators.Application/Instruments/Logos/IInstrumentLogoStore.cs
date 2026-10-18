namespace StockIndicators.Application.Instruments.Logos;

/// <summary>Redis-cached logo (or the fact that no source had one) for an instrument.</summary>
public interface IInstrumentLogoStore
{
  Task<InstrumentLogoCacheEntry?> GetAsync(string key, CancellationToken cancellationToken);

  Task SaveAsync(string key, InstrumentLogoData logo, CancellationToken cancellationToken);

  /// <summary>Caches the negative result; it expires faster than a stored logo (see the implementation).</summary>
  Task SaveNegativeAsync(string key, CancellationToken cancellationToken);
}

public sealed record InstrumentLogoCacheEntry(InstrumentLogoData? Logo, bool Negative)
{
  public static InstrumentLogoCacheEntry From(InstrumentLogoData logo) => new(logo, false);
}

/// <summary>
/// Cache coordinates for instrument logos (Redis key and process-local L1 key). The
/// original casing is kept so a revalidation can look the ticker up in the instruments
/// catalog again; the frontend only ever requests tickers as the catalog spells them.
/// </summary>
public static class InstrumentLogoKeys
{
  public static string For(string ticker, string? exchangeName) =>
    $"instrument-logo:{ticker}:{exchangeName ?? ""}";
}
