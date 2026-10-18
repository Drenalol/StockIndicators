namespace StockIndicators.Application.Instruments.Logos;

/// <summary>
/// One logo source of the cascade (Tinkoff issuer brand CDN - Logo.dev - CoinGecko).
/// Sources are registered as singletons in cascade order and must return null (not throw)
/// when they have nothing for the instrument; exceptions are logged and skipped by the handler.
/// </summary>
public interface IInstrumentLogoSource
{
  Task<InstrumentLogoData?> TryResolveAsync(InstrumentLogoRequest request, CancellationToken cancellationToken);
}
