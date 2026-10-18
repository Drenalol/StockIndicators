using StockIndicators.Components.Utils;
using Xunit;

namespace StockIndicators.Providers.Tests;

public class InstrumentTypeInferenceTests
{
  [Theory]
  [InlineData("Columbia AAA CLO ETF")]
  [InlineData("SPDR S&P 500 ETF Trust")]
  [InlineData("iShares Gold ETP")]
  [InlineData("etf on moex index")]
  [InlineData(null, "ВТБ Бестуб. Фонд")]
  [InlineData(null, "БПИФ на индекс МосБиржи")]
  [InlineData("Фонд на индекс МосБиржи")]
  public void Infer_ReturnsEtf_ForEtfNames(string? name, string? longName = null) =>
    Assert.Equal("Etf", InstrumentTypeInference.Infer(name, longName));

  [Theory]
  [InlineData("Облигация ПАО Сбербанк")]
  [InlineData(null, "Облигации ВТБ 23024")]
  public void Infer_ReturnsBond_ForBondNames(string? name, string? longName = null) =>
    Assert.Equal("Bond", InstrumentTypeInference.Infer(name, longName));

  [Theory]
  [InlineData("Опцион на акцию Сбербанк")]
  [InlineData("Опционы фьючерс")]
  public void Infer_ReturnsOption_ForOptionNames(string? name, string? longName = null) =>
    Assert.Equal("Option", InstrumentTypeInference.Infer(name, longName));

  [Theory]
  [InlineData("Фьючерс на индекс МосБиржи")]
  [InlineData("Фьючерс Si-12.26")]
  public void Infer_ReturnsFuture_ForFutureNames(string? name, string? longName = null) =>
    Assert.Equal("Future", InstrumentTypeInference.Infer(name, longName));

  [Theory]
  // Funds take precedence over their underlying
  [InlineData("iShares Core US Aggregate Bond ETF")]
  [InlineData("Фонд облигаций федерального займа")]
  public void Infer_EtfPrecedence_OverBondMarkers(string name) =>
    Assert.Equal("Etf", InstrumentTypeInference.Infer(name));

  [Theory]
  [InlineData("Apple Inc.")]
  [InlineData("ПАО Сбербанк")]
  [InlineData("US Treasury 5.0% 05/15/2028")]
  [InlineData("Future Tech Holdings")]
  // "фондовый" is an adjective, not a fund
  [InlineData("Фондовый торговый индекс")]
  [InlineData(null, null)]
  [InlineData("", "")]
  public void Infer_ReturnsNull_WhenNoStrongMarker(string? name, string? longName = null) =>
    Assert.Null(InstrumentTypeInference.Infer(name, longName));

  [Fact]
  public void Infer_UsesLongName_WhenNameLacksMarker() =>
    Assert.Equal("Etf", InstrumentTypeInference.Infer("SPDR S&P 500", "SPDR S&P 500 ETF Trust"));
}
