namespace StockIndicators.Domain.Models.Instruments;

/// <summary>
/// Instrument identity scoped to an exchange: the same ticker may exist on
/// multiple exchanges (e.g. AAPL on MOEX and on NASDAQ) and must not mix.
/// </summary>
public record InstrumentKey(string Ticker, string? ExchangeName);
