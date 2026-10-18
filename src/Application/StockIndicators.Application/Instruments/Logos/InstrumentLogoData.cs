namespace StockIndicators.Application.Instruments.Logos;

/// <summary>A downloaded logo image and where it came from ("tinkoff", "logo.dev", "coingecko").</summary>
public sealed record InstrumentLogoData(byte[] Data, string ContentType, string Source, DateTime FetchedAtUtc);
