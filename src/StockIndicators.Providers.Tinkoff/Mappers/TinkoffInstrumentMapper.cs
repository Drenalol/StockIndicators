using Tinkoff.InvestApi.V1;

namespace StockIndicators.Providers.Tinkoff.Mappers;

public static class TinkoffInstrumentMapper
{
  public static TinkoffInstrument ToTinkoffInstrument(this Share share) =>
    new(share.Ticker, "MOEX", share.ClassCode, share.Currency.ToUpper(), share.Name, share.Isin, share.Sector, "Stock") { Figi = share.Figi, BrandLogoName = share.Brand?.LogoName };

  public static TinkoffInstrument ToTinkoffInstrument(this Etf etf) =>
    new(etf.Ticker, "MOEX", etf.ClassCode, etf.Currency.ToUpper(), etf.Name, etf.Isin, etf.Sector, "Etf") { Figi = etf.Figi, BrandLogoName = etf.Brand?.LogoName };
}
