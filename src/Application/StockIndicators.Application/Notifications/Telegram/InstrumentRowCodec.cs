using StockIndicators.Application.Instruments.GetInstruments;

namespace StockIndicators.Application.Notifications.Telegram;

/// <summary>
/// Encodes an instrument (Ticker + ExchangeName) into a readable reply-keyboard
/// button label and decodes it back, so browsing rows stay human-friendly while
/// carrying the full instrument identity ("TICKER · EXCHANGE", or just TICKER).
/// </summary>
public static class InstrumentRowCodec
{
  private const string Separator = " · ";

  public static string Encode(InstrumentDto instrument)
  {
    var exchange = instrument.ExchangeName;

    return string.IsNullOrWhiteSpace(exchange) ? instrument.Ticker : $"{instrument.Ticker}{Separator}{exchange}";
  }

  public static (string Ticker, string? Exchange) Decode(string text)
  {
    var index = text.IndexOf(Separator, StringComparison.Ordinal);

    if (index <= 0)
      return (text, null);

    return (text[..index], text[(index + Separator.Length)..]);
  }
}
