using StockIndicators.Providers.Tinkoff.Mappers;
using Tinkoff.InvestApi.V1;
using Xunit;

namespace StockIndicators.Providers.Tests;

public class TinkoffInstrumentMapperTests
{
  [Fact]
  public void ToTinkoffInstrument_MapsShareFields()
  {
    var share = new Share
    {
      Ticker = "SBER",
      ClassCode = "T0001056902",
      Currency = "rur",
      Name = "ПАО Сбербанк",
      Isin = "RU0009029546",
      Sector = "Financial services"
    };

    var instrument = share.ToTinkoffInstrument();

    Assert.Equal("SBER", instrument.Ticker);
    Assert.Equal("MOEX", instrument.ExchangeName);
    Assert.Equal("T0001056902", instrument.ClassCode);
    Assert.Equal("RUR", instrument.Currency);
    Assert.Equal("ПАО Сбербанк", instrument.Name);
    Assert.Equal("RU0009029546", instrument.Isin);
    Assert.Equal("Financial services", instrument.Sector);
    Assert.Equal("Stock", instrument.Type);
    Assert.Equal("tinkoff", instrument.Source);
    Assert.Null(instrument.BrandLogoName);
  }

  [Fact]
  public void ToTinkoffInstrument_MapsShareBrandLogoName()
  {
    var share = new Share
    {
      Ticker = "SBER",
      ClassCode = "T0001056902",
      Currency = "rur",
      Name = "ПАО Сбербанк",
      Brand = new BrandData { LogoName = "sber3.png", LogoBaseColor = "#21A038", TextColor = "#FFFFFF" }
    };

    Assert.Equal("sber3.png", share.ToTinkoffInstrument().BrandLogoName);
  }

  [Fact]
  public void ToTinkoffInstrument_MapsEtfBrandLogoName()
  {
    var etf = new Etf
    {
      Ticker = "AKGD",
      ClassCode = "TQ0009A2DZB9",
      Currency = "rub",
      Name = "АК БАРС Золото",
      Brand = new BrandData { LogoName = "RU000A100Q4.png" }
    };

    Assert.Equal("RU000A100Q4.png", etf.ToTinkoffInstrument().BrandLogoName);
  }

  [Fact]
  public void ToTinkoffInstrument_MapsEtfFields()
  {
    var etf = new Etf
    {
      Ticker = "SBMX",
      ClassCode = "TQ0009A261C",
      Currency = "rur",
      Name = "Индекс МосБирни",
      Isin = "RU0009659426",
      Sector = "Equity"
    };

    var instrument = etf.ToTinkoffInstrument();

    Assert.Equal("SBMX", instrument.Ticker);
    Assert.Equal("MOEX", instrument.ExchangeName);
    Assert.Equal("TQ0009A261C", instrument.ClassCode);
    Assert.Equal("RUR", instrument.Currency);
    Assert.Equal("Индекс МосБирни", instrument.Name);
    Assert.Equal("RU0009659426", instrument.Isin);
    Assert.Equal("Etf", instrument.Type);
    Assert.Equal("tinkoff", instrument.Source);
  }
}
