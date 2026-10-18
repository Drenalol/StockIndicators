using StockIndicators.Components.Utils;
using Xunit;

namespace StockIndicators.Indicators.Tests.Instruments;

public class TransliterationTests
{
  [Theory]
  [InlineData("газпром", "gazprom")]
  [InlineData("ПАО «ГАЗПРОМ» ао", "pao «gazprom» ao")]
  [InlineData("Щёлково", "shchyolkovo")]
  [InlineData("облигация", "obligatsiya")]
  [InlineData("sber", null)] // no Cyrillic - no variant to gain
  [InlineData("AAPL", null)]
  [InlineData(null, null)]
  [InlineData("", null)]
  public void RuToLatin_TransliteratesCyrillicOnly(string? input, string? expected) =>
    Assert.Equal(expected, Transliteration.RuToLatin(input));

  [Fact]
  public void RuToLatin_MixesScripts() =>
    Assert.Equal("vtb bank 1", Transliteration.RuToLatin("ВТБ банк 1"));
}

public class InstrumentTextTests
{
  [Theory]
  [InlineData("ПАО «ГАЗПРОМ» ао", "пао газпром ао")]
  [InlineData("BTC/USDT", "btc usdt")]
  [InlineData("  Apple,   Inc.  ", "apple inc")]
  [InlineData(null, "")]
  [InlineData("...", "")]
  public void NormalizeName_LowercasesAndCollapsesSeparators(string? input, string expected) =>
    Assert.Equal(expected, InstrumentText.NormalizeName(input));

  [Theory]
  [InlineData("BTC/USDT", "btcusdt")]
  [InlineData("AAPL", "aapl")]
  [InlineData("SR", "sr")]
  public void NormalizeTicker_StripsSeparators(string input, string expected) =>
    Assert.Equal(expected, InstrumentText.NormalizeTicker(input));

  [Fact]
  public void BuildTsQuery_AndsPrefixLexemes() =>
    Assert.Equal("vtb:* & bank:*", InstrumentText.BuildTsQuery("vtb bank"));

  [Fact]
  public void BuildTsQuery_OrsCyrillicWithTransliteration() =>
    Assert.Equal("(газпром:* | gazprom:*)", InstrumentText.BuildTsQuery("газпром"));

  [Fact]
  public void BuildTsQuery_OrsPhoneticAndDictionaryCryptoSpellings()
  {
    // "биткоин" romanizes to "bitkoin"; the canonical spelling needs c ("bitcoin"),
    // and the dictionary supplies it too
    Assert.Equal("(биткоин:* | bitkoin:* | bitcoin:*)", InstrumentText.BuildTsQuery("биткоин"));
    Assert.Equal("(эфир:* | efir:* | ethereum:*)", InstrumentText.BuildTsQuery("эфир"));

    // compound names translate token by token
    Assert.Equal(
      "(биткоин:* | bitkoin:* | bitcoin:*) & (кеш:* | kesh:* | cesh:* | cash:*)",
      InstrumentText.BuildTsQuery("биткоин кеш"));
  }

  [Theory]
  [InlineData("биткоин", "bitcoin")]
  [InlineData("биткоин кеш", "bitcoin cash")]
  [InlineData("эфир", "ethereum")]
  [InlineData("газпром", null)] // plain transliteration territory
  public void CryptoCyrillicNames_TranslatesKnownTokens(string query, string? expected) =>
    Assert.Equal(expected, CryptoCyrillicNames.TranslateQuery(query));

  [Fact]
  public void BuildTsQuery_StripsOperatorsAndPunctuation() =>
    Assert.Equal("btc:* & usdt:*", InstrumentText.BuildTsQuery("btc/usdt & | !"));

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("& | !")]
  public void BuildTsQuery_NullWhenNothingSearchable(string? input) =>
    Assert.Null(InstrumentText.BuildTsQuery(input));
}

public class CryptoBaseAssetTests
{
  [Theory]
  [InlineData("BTC/USDT", "BTCUSDT", "USDT", "btc")] // Binance: name carries base/quote
  [InlineData("BTC/USD", "BTC", "USD", "btc")] // Alpaca crypto symbol is the bare base
  [InlineData(null, "ETHUSDT", "USDT", "eth")] // no name: quote currency suffix
  [InlineData(null, "BTCUSD", "TUSD", "btc")] // TUSD suffix absent, the known "USD" quote wins
  [InlineData(null, "BNBUSDD", null, "bnb")] // known quote fallback
  [InlineData(null, "DOGE", "USDT", "doge")] // ticker shorter than a quote: the ticker itself
  [InlineData(null, "SBER", "RUB", "sber")]
  public void Extract_BaseAsset(string? name, string ticker, string? quote, string expected) =>
    Assert.Equal(expected, CryptoBaseAsset.Extract(name, ticker, quote));
}
